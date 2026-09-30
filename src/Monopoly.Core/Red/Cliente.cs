using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Red;

/// <summary>
/// Cliente TCP del juego. Solo envía solicitudes (nunca saldos, posiciones, dados, turnos,
/// propiedades ni transacciones). Un hilo lector recibe los mensajes del servidor y los notifica
/// mediante eventos.
/// </summary>
/// <remarks>
/// Los eventos se disparan desde el hilo lector: una interfaz gráfica debe pasar al hilo de la
/// interfaz (por ejemplo, con <c>Control.BeginInvoke</c>) antes de tocar sus controles.
/// </remarks>
public sealed class Cliente : IDisposable
{
    private readonly object _candadoEscritura = new object();
    private TcpClient? _tcp;
    private StreamReader? _lector;
    private StreamWriter? _escritor;
    private Thread? _hiloLector;
    private int _desconexionNotificada;
    private volatile string? _motivoCierre;

    /// <summary>
    /// Cualquier mensaje recibido (se dispara antes que el evento específico).
    /// </summary>
    public event Action<Mensaje>? MensajeRecibido;

    /// <summary>
    /// <c>BIENVENIDA</c>: el servidor aceptó al jugador (id y nombre).
    /// </summary>
    public event Action<int, string>? BienvenidaRecibida;

    /// <summary>
    /// <c>ERROR</c>: el servidor rechazó una solicitud.
    /// </summary>
    public event Action<string>? ErrorRecibido;

    /// <summary>
    /// <c>ESTADO</c>: nuevo estado completo de la partida.
    /// </summary>
    public event Action<EstadoRed>? EstadoActualizado;

    /// <summary>
    /// <c>DADOS</c>: un jugador lanzó los dados (id del jugador, dado 1, dado 2).
    /// </summary>
    public event Action<int, int, int>? DadosRecibidos;

    /// <summary>
    /// <c>EVENTO</c>: texto del registro de la partida.
    /// </summary>
    public event Action<string>? EventoRecibido;

    /// <summary>
    /// <c>TRANSACCIONES</c>: respuesta a una consulta (filtro aplicado y transacciones en orden).
    /// </summary>
    public event Action<string, ListaSimple<Transaccion>>? TransaccionesRecibidas;

    /// <summary>
    /// <c>FIN</c>: la partida terminó (nombre del ganador y resumen).
    /// </summary>
    public event Action<string, string>? FinRecibido;

    /// <summary>
    /// Se perdió la conexión con el servidor (o se cerró desde este lado).
    /// </summary>
    public event Action<string>? Desconectado;

    /// <summary>
    /// Indica si hay una conexión abierta.
    /// </summary>
    public bool Conectado { get; private set; }

    /// <summary>
    /// Id asignado por el servidor tras la bienvenida, o <c>null</c>.
    /// </summary>
    public int? IdJugador { get; private set; }

    /// <summary>
    /// Indica si la conexión terminó porque el servidor avisó que cerraba (<c>SERVIDOR_CERRADO</c>),
    /// a diferencia de una caída de la red.
    /// </summary>
    public bool CerradoPorElServidor => _motivoCierre != null;

    /// <summary>
    /// Nombre confirmado por el servidor tras la bienvenida, o <c>null</c>.
    /// </summary>
    public string? Nombre { get; private set; }

    /// <summary>
    /// Tiempo máximo predeterminado para establecer la conexión.
    /// </summary>
    public const int TiempoEsperaConexionMs = 5000;

    /// <summary>
    /// Abre la conexión TCP y arranca el hilo lector. Después hay que enviar <see cref="Unirse"/>.
    /// </summary>
    /// <param name="host">IP o nombre del servidor.</param>
    /// <param name="puerto">Puerto del servidor.</param>
    /// <param name="tiempoEsperaMs">Tiempo máximo para conectar (una IP inexistente no responde nunca).</param>
    /// <exception cref="SocketException">Si la conexión es rechazada o la dirección no existe.</exception>
    /// <exception cref="TimeoutException">Si el servidor no responde a tiempo.</exception>
    /// <remarks>Use <see cref="DescribirErrorConexion"/> para mostrar un mensaje claro al usuario.</remarks>
    public void Conectar(string host, int puerto, int tiempoEsperaMs = TiempoEsperaConexionMs)
    {
        if (Conectado)
        {
            throw new InvalidOperationException("El cliente ya está conectado.");
        }

        TcpClient tcp = new TcpClient();
        try
        {
            Task intento = tcp.ConnectAsync(host, puerto);
            bool termino;
            try
            {
                termino = intento.Wait(tiempoEsperaMs);
            }
            catch (AggregateException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }

            if (!termino)
            {
                throw new TimeoutException($"{host}:{puerto} no respondió en {tiempoEsperaMs / 1000} segundos.");
            }
        }
        catch
        {
            tcp.Dispose();
            throw;
        }

        ConfiguracionSocket.Aplicar(tcp);
        _tcp = tcp;
        _motivoCierre = null;
        NetworkStream flujo = _tcp.GetStream();
        _lector = new StreamReader(flujo, Protocolo.Codificacion);
        _escritor = new StreamWriter(flujo, Protocolo.Codificacion) { NewLine = "\n", AutoFlush = true };
        _desconexionNotificada = 0;
        Conectado = true;
        _hiloLector = new Thread(LeerMensajes) { IsBackground = true, Name = "Cliente-Lector" };
        _hiloLector.Start();
    }

    /// <summary>
    /// Traduce un error al conectar en un mensaje claro para el usuario (IP incorrecta, servidor que no
    /// responde, puerto cerrado...).
    /// </summary>
    /// <param name="error">Excepción lanzada por <see cref="Conectar"/>.</param>
    /// <param name="host">IP o nombre al que se intentó conectar.</param>
    /// <param name="puerto">Puerto usado.</param>
    /// <returns>El mensaje.</returns>
    public static string DescribirErrorConexion(Exception error, string host, int puerto)
    {
        ArgumentNullException.ThrowIfNull(error);
        string sinRespuesta = $"No hubo respuesta de {host}:{puerto}. Revise que la IP sea la de la computadora del organizador, " +
                              $"que ambas estén conectadas a la misma red y que el Firewall de Windows del organizador permita el puerto {puerto}.";
        if (error is TimeoutException)
        {
            return sinRespuesta;
        }

        if (error is ArgumentException)
        {
            return $"La dirección '{host}' no es válida. Escriba una IP como 192.168.1.20.";
        }

        if (error is SocketException socket)
        {
            switch (socket.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    return $"No hay ninguna partida abierta en {host}:{puerto} (conexión rechazada). " +
                           "Verifique que el organizador ya creó la partida y que el puerto sea el mismo.";
                case SocketError.HostNotFound:
                case SocketError.NoData:
                case SocketError.TryAgain:
                    return $"No se encontró la computadora '{host}'. Revise que la IP esté bien escrita (por ejemplo, 192.168.1.20).";
                case SocketError.TimedOut:
                case SocketError.HostUnreachable:
                case SocketError.NetworkUnreachable:
                case SocketError.HostDown:
                    return sinRespuesta;
                case SocketError.AddressNotAvailable:
                    return $"La dirección {host} no es válida para conectarse.";
            }

            return $"No se pudo conectar a {host}:{puerto}: {socket.Message}";
        }

        return $"No se pudo conectar a {host}:{puerto}: {error.Message}";
    }

    /// <summary>Envía <c>CONECTAR|nombre</c>.</summary>
    /// <param name="nombre">Nombre del jugador.</param>
    public void Unirse(string nombre) => Enviar(Protocolo.Conectar, nombre);

    /// <summary>Envía <c>INICIAR_PARTIDA</c>.</summary>
    public void IniciarPartida() => Enviar(Protocolo.IniciarPartida);

    /// <summary>Envía <c>TIRAR_DADOS</c>.</summary>
    public void TirarDados() => Enviar(Protocolo.TirarDados);

    /// <summary>Envía <c>COMPRAR_PROPIEDAD</c>.</summary>
    public void ComprarPropiedad() => Enviar(Protocolo.ComprarPropiedad);

    /// <summary>Envía <c>NO_COMPRAR</c>.</summary>
    public void NoComprar() => Enviar(Protocolo.NoComprar);

    /// <summary>Envía <c>PAGAR_CON_TARJETA</c> (modo simulado).</summary>
    public void PagarConTarjeta() => Enviar(Protocolo.PagarConTarjeta);

    /// <summary>Envía <c>TERMINAR_TURNO</c>.</summary>
    public void TerminarTurno() => Enviar(Protocolo.TerminarTurno);

    /// <summary>Envía <c>CONSULTAR_ESTADO</c>.</summary>
    public void ConsultarEstado() => Enviar(Protocolo.ConsultarEstado);

    /// <summary>Envía <c>EXPORTAR_TRANSACCIONES</c>.</summary>
    public void ExportarTransacciones() => Enviar(Protocolo.ExportarTransacciones);

    /// <summary>Envía <c>VINCULAR_TARJETA|id</c>: la próxima tarjeta leída se vincula a ese jugador (0 cancela).</summary>
    /// <param name="idJugador">Jugador al que se vinculará la tarjeta.</param>
    public void VincularTarjeta(int idJugador) =>
        Enviar(Protocolo.VincularTarjeta, idJugador.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Envía <c>RETIRAR_JUGADOR|id</c> (solo el organizador, para jugadores desconectados).</summary>
    /// <param name="idJugador">Jugador a retirar.</param>
    public void RetirarJugador(int idJugador) =>
        Enviar(Protocolo.RetirarJugador, idJugador.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Envía <c>CONSULTAR_TRANSACCIONES|filtro[|valor]</c>.
    /// </summary>
    /// <param name="filtro">Filtro a aplicar.</param>
    /// <param name="valor">Nombre del jugador o del tipo, para los filtros que lo requieren.</param>
    public void ConsultarTransacciones(FiltroTransacciones filtro = FiltroTransacciones.Todas, string? valor = null)
    {
        string nombreFiltro = filtro.ToString().ToUpperInvariant();
        if (valor == null)
        {
            Enviar(Protocolo.ConsultarTransacciones, nombreFiltro);
        }
        else
        {
            Enviar(Protocolo.ConsultarTransacciones, nombreFiltro, valor);
        }
    }

    /// <summary>
    /// Envía una línea ya armada (para depuración).
    /// </summary>
    /// <param name="linea">Línea del protocolo.</param>
    public void EnviarLinea(string linea)
    {
        EscribirLinea(linea);
    }

    /// <summary>
    /// Envía un comando con sus campos.
    /// </summary>
    /// <param name="comando">Comando.</param>
    /// <param name="campos">Campos (se escapan).</param>
    public void Enviar(string comando, params string[] campos)
    {
        EscribirLinea(Protocolo.Codificar(comando, campos));
    }

    /// <summary>
    /// Envía <c>DESCONECTAR</c> (si puede) y cierra la conexión.
    /// </summary>
    public void Desconectar()
    {
        if (!Conectado)
        {
            return;
        }

        try
        {
            Enviar(Protocolo.Desconectar);
        }
        catch (InvalidOperationException)
        {
            // La conexión ya estaba caída.
        }

        Cerrar("Desconectado por el usuario.");
    }

    /// <summary>
    /// Cierra la conexión.
    /// </summary>
    public void Dispose()
    {
        Desconectar();
    }

    private void EscribirLinea(string linea)
    {
        lock (_candadoEscritura)
        {
            if (!Conectado || _escritor == null)
            {
                throw new InvalidOperationException("El cliente no está conectado.");
            }

            try
            {
                _escritor.WriteLine(linea);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
            {
                Cerrar("Se perdió la conexión al enviar: " + ex.Message);
                throw new InvalidOperationException("Se perdió la conexión con el servidor.", ex);
            }
        }
    }

    private void LeerMensajes()
    {
        string motivo = "El servidor cerró la conexión.";
        try
        {
            string? linea;
            while ((linea = _lector!.ReadLine()) != null)
            {
                if (linea.Length > 0)
                {
                    Despachar(linea);
                }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException || ex is InvalidOperationException)
        {
            // InvalidOperationException: el flujo ya se cerró desde este lado ("The stream does not support reading").
            motivo = "Se perdió la conexión con el servidor (¿se cayó la red o se cerró la computadora del organizador?).";
        }

        Cerrar(_motivoCierre ?? motivo);
    }

    private void Despachar(string linea)
    {
        try
        {
            Mensaje mensaje = Protocolo.Decodificar(linea);
            MensajeRecibido?.Invoke(mensaje);
            switch (mensaje.Comando)
            {
                case Protocolo.Bienvenida:
                    IdJugador = mensaje.Entero(0);
                    Nombre = mensaje.CantidadCampos > 1 ? mensaje.Campo(1) : null;
                    BienvenidaRecibida?.Invoke(IdJugador.Value, Nombre ?? string.Empty);
                    break;
                case Protocolo.Error:
                    ErrorRecibido?.Invoke(mensaje.Campo(0));
                    break;
                case Protocolo.Estado:
                    EstadoActualizado?.Invoke(SerializadorEstado.Decodificar(mensaje));
                    break;
                case Protocolo.Dados:
                    DadosRecibidos?.Invoke(mensaje.Entero(0), mensaje.Entero(1), mensaje.Entero(2));
                    break;
                case Protocolo.Evento:
                    EventoRecibido?.Invoke(mensaje.Campo(0));
                    break;
                case Protocolo.Transacciones:
                    TransaccionesRecibidas?.Invoke(mensaje.Campo(0), SerializadorTransacciones.Decodificar(mensaje));
                    break;
                case Protocolo.Fin:
                    FinRecibido?.Invoke(mensaje.Campo(0), mensaje.Campo(1));
                    break;
                case Protocolo.ServidorCerrado:
                    // El servidor cerrará la conexión enseguida: se usa este motivo al notificar la desconexión.
                    _motivoCierre = mensaje.CantidadCampos > 0 ? mensaje.Campo(0) : "El servidor cerró la partida.";
                    break;
            }
        }
        catch (FormatException ex)
        {
            ErrorRecibido?.Invoke($"Mensaje inválido del servidor ({ex.Message}): {linea}");
        }
    }

    private void Cerrar(string motivo)
    {
        lock (_candadoEscritura)
        {
            Conectado = false;
            _tcp?.Close();
        }

        if (Interlocked.Exchange(ref _desconexionNotificada, 1) == 0)
        {
            Desconectado?.Invoke(motivo);
        }
    }
}
