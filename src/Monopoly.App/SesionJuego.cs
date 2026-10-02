using System;
using System.Windows.Forms;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Hardware;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Sesión de un jugador en la aplicación: su <see cref="Cliente"/> TCP y, si es el organizador, el
/// <see cref="Servidor"/> que aloja al banco. Reenvía los eventos del cliente (que llegan en el hilo
/// lector de la red) al hilo de la interfaz mediante <see cref="Control.BeginInvoke(Delegate)"/>, de modo
/// que los formularios pueden tocar sus controles directamente en los manejadores.
/// </summary>
/// <remarks>
/// El organizador también juega a través del protocolo: su interfaz nunca accede a <c>Juego</c>.
/// </remarks>
internal sealed class SesionJuego : IDisposable
{
    private readonly Control _hiloInterfaz;
    private readonly ListaSimple<string> _eventos = new ListaSimple<string>();
    private IDispositivoCajero _cajero = new CajeroSimulado();
    private bool _cerrada;

    /// <summary>
    /// Crea la sesión. Debe llamarse desde el hilo de la interfaz.
    /// </summary>
    /// <param name="cliente">Cliente ya creado (aún sin conectar).</param>
    /// <param name="servidor">Servidor alojado por el organizador, o <c>null</c> si solo se une.</param>
    /// <param name="host">Dirección del servidor.</param>
    /// <param name="puerto">Puerto del servidor.</param>
    public SesionJuego(Cliente cliente, Servidor? servidor, string host, int puerto)
    {
        Cliente = cliente;
        Servidor = servidor;
        Host = host;
        Puerto = puerto;

        // Control invisible cuyo identificador pertenece al hilo de la interfaz: sirve para BeginInvoke.
        _hiloInterfaz = new Control();
        _ = _hiloInterfaz.Handle;

        cliente.BienvenidaRecibida += (id, nombre) => EnHiloInterfaz(() =>
        {
            IdJugador = id;
            Nombre = nombre;
            BienvenidaRecibida?.Invoke(id, nombre);
        });
        cliente.EstadoActualizado += estado => EnHiloInterfaz(() =>
        {
            UltimoEstado = estado;
            EstadoActualizado?.Invoke(estado);
        });
        cliente.EventoRecibido += texto => EnHiloInterfaz(() =>
        {
            _eventos.AgregarAlFinal(texto);
            EventoRecibido?.Invoke(texto);
        });
        cliente.DadosRecibidos += (id, d1, d2) => EnHiloInterfaz(() => DadosRecibidos?.Invoke(id, d1, d2));
        cliente.ErrorRecibido += texto => EnHiloInterfaz(() => ErrorRecibido?.Invoke(texto));
        cliente.TransaccionesRecibidas += (filtro, lista) => EnHiloInterfaz(() => TransaccionesRecibidas?.Invoke(filtro, lista));
        cliente.FinRecibido += (ganador, resumen) => EnHiloInterfaz(() =>
        {
            Fin = (ganador, resumen);
            FinRecibido?.Invoke(ganador, resumen);
        });
        cliente.Desconectado += motivo => EnHiloInterfaz(() =>
        {
            if (!_cerrada)
            {
                Desconectado?.Invoke(motivo);
            }
        });
    }

    /// <summary>El servidor aceptó al jugador (id, nombre).</summary>
    public event Action<int, string>? BienvenidaRecibida;

    /// <summary>Llegó un nuevo estado completo.</summary>
    public event Action<EstadoRed>? EstadoActualizado;

    /// <summary>Llegó una línea del registro de la partida.</summary>
    public event Action<string>? EventoRecibido;

    /// <summary>Un jugador lanzó los dados (id, dado 1, dado 2).</summary>
    public event Action<int, int, int>? DadosRecibidos;

    /// <summary>El servidor rechazó una solicitud.</summary>
    public event Action<string>? ErrorRecibido;

    /// <summary>Respuesta a una consulta de transacciones.</summary>
    public event Action<string, ListaSimple<Transaccion>>? TransaccionesRecibidas;

    /// <summary>La partida terminó (ganador, resumen).</summary>
    public event Action<string, string>? FinRecibido;

    /// <summary>Se perdió la conexión con el servidor.</summary>
    public event Action<string>? Desconectado;

    /// <summary>Cliente TCP del jugador.</summary>
    public Cliente Cliente { get; }

    /// <summary>Servidor alojado en esta aplicación (solo el organizador), o <c>null</c>.</summary>
    public Servidor? Servidor { get; }

    /// <summary>Indica si este jugador aloja al banco.</summary>
    public bool EsOrganizador => Servidor != null;

    /// <summary>Dirección del servidor.</summary>
    public string Host { get; }

    /// <summary>Puerto del servidor.</summary>
    public int Puerto { get; }

    /// <summary>Id asignado por el servidor, o <c>null</c> antes de la bienvenida.</summary>
    public int? IdJugador { get; private set; }

    /// <summary>Nombre confirmado por el servidor.</summary>
    public string? Nombre { get; private set; }

    /// <summary>Último estado recibido, o <c>null</c>.</summary>
    public EstadoRed? UltimoEstado { get; private set; }

    /// <summary>Datos del mensaje FIN, si ya llegó.</summary>
    public (string Ganador, string Resumen)? Fin { get; private set; }

    /// <summary>
    /// Cambió el cajero o su estado (se dispara en el hilo de la interfaz). Solo en el organizador.
    /// </summary>
    public event Action? CajeroCambiado;

    /// <summary>
    /// Cajero conectado al servidor de esta aplicación (el simulado si no hay Pico W).
    /// </summary>
    public IDispositivoCajero Cajero => _cajero;

    /// <summary>
    /// Conecta un nuevo cajero al servidor alojado y libera el anterior. Solo el organizador aloja al
    /// servidor y, por lo tanto, al cajero.
    /// </summary>
    /// <param name="nuevo">Cajero a usar (la Pico ya conectada, o uno simulado).</param>
    public void CambiarCajero(IDispositivoCajero nuevo)
    {
        ArgumentNullException.ThrowIfNull(nuevo);
        if (Servidor == null)
        {
            throw new InvalidOperationException("Solo el organizador puede conectar el cajero.");
        }

        IDispositivoCajero anterior = _cajero;
        anterior.EstadoCambiado -= AlCambiarEstadoCajero;
        _cajero = nuevo;
        nuevo.EstadoCambiado += AlCambiarEstadoCajero;
        Servidor.UsarCajero(nuevo);
        if (!ReferenceEquals(anterior, nuevo))
        {
            anterior.Dispose();
        }

        CajeroCambiado?.Invoke();
    }

    /// <summary>
    /// Indica si el servidor alojado está en modo sin hardware (pruebas).
    /// </summary>
    public bool ModoSinHardware => Servidor?.ModoSinHardware ?? false;

    /// <summary>
    /// Activa o desactiva el modo sin hardware en el servidor alojado. Al activarlo se libera la Pico (si
    /// estaba conectada) y el servidor usa su propio cajero simulado.
    /// </summary>
    /// <param name="activo">Si se activa el modo sin hardware.</param>
    public void EstablecerModoSinHardware(bool activo)
    {
        if (Servidor == null)
        {
            throw new InvalidOperationException("Solo el organizador puede cambiar el modo.");
        }

        Servidor.EstablecerModoSinHardware(activo);
        if (activo && !ReferenceEquals(_cajero, Servidor.Cajero))
        {
            IDispositivoCajero anterior = _cajero;
            anterior.EstadoCambiado -= AlCambiarEstadoCajero;
            _cajero = Servidor.Cajero;
            anterior.Dispose();
        }

        CajeroCambiado?.Invoke();
    }

    /// <summary>
    /// Modo sin hardware: simula el botón del dado (mismo flujo que la Pico).
    /// </summary>
    public void SimularBoton()
    {
        MostrarRechazo(Servidor?.SimularBoton());
    }

    /// <summary>
    /// Modo sin hardware: simula la tarjeta del jugador en turno (mismo flujo que la Pico).
    /// </summary>
    public void SimularTarjetaDelJugadorEnTurno()
    {
        MostrarRechazo(Servidor?.SimularTarjetaDelJugadorEnTurno());
    }

    private void MostrarRechazo(Core.Logica.ResultadoAccion? resultado)
    {
        if (resultado != null && !resultado.Exito)
        {
            ErrorRecibido?.Invoke(resultado.Mensaje);
        }
    }

    /// <summary>
    /// Recorre los eventos recibidos desde el inicio de la sesión.
    /// </summary>
    /// <param name="accion">Acción a ejecutar con cada texto.</param>
    public void RecorrerEventos(Action<string> accion)
    {
        _eventos.Recorrer(accion);
    }

    /// <summary>
    /// Ejecuta una solicitud al servidor; si la conexión ya no existe, lo informa como error.
    /// </summary>
    /// <param name="solicitud">Acción que envía el mensaje con el cliente.</param>
    public void Solicitar(Action<Cliente> solicitud)
    {
        try
        {
            solicitud(Cliente);
        }
        catch (InvalidOperationException ex)
        {
            ErrorRecibido?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// Desconecta al cliente y, si es el organizador, detiene el servidor.
    /// </summary>
    public void Dispose()
    {
        if (_cerrada)
        {
            return;
        }

        _cerrada = true;
        Cliente.Dispose();
        Servidor?.Dispose();
        _cajero.EstadoCambiado -= AlCambiarEstadoCajero;
        _cajero.Limpiar();
        _cajero.Dispose();
        _hiloInterfaz.Dispose();
    }

    private void AlCambiarEstadoCajero(EstadoCajero estado)
    {
        // El cajero avisa desde su propio hilo (por ejemplo, al desconectar el cable).
        EnHiloInterfaz(() => CajeroCambiado?.Invoke());
    }

    private void EnHiloInterfaz(Action accion)
    {
        if (_hiloInterfaz.IsDisposed)
        {
            return;
        }

        try
        {
            _hiloInterfaz.BeginInvoke(accion);
        }
        catch (InvalidOperationException)
        {
            // La interfaz ya se está cerrando.
        }
    }
}
