using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Hardware;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Red;

/// <summary>
/// Servidor TCP del juego (el "banco"), que corre en la máquina del organizador. Acepta clientes,
/// atiende a cada uno en su propio hilo, traduce sus solicitudes a llamadas a <see cref="Juego"/>
/// y, tras cada acción importante, difunde <c>EVENTO</c> y <c>ESTADO</c> a todos los clientes.
/// </summary>
/// <remarks>
/// <para>Las solicitudes se procesan de a una (candado de procesamiento), de modo que los eventos y
/// estados llegan a todos los clientes en el mismo orden.</para>
/// <para>Desconexiones: el jugador desconectado <b>sigue en la partida</b> (no se elimina ni se salta
/// su turno) y se avisa a los demás. Puede reconectarse enviando <c>CONECTAR</c> con el mismo nombre, y
/// recupera su id. Si nunca vuelve, la partida espera en su turno.</para>
/// </remarks>
public sealed class Servidor : IDisposable
{
    /// <summary>
    /// Puerto predeterminado.
    /// </summary>
    public const int PuertoPredeterminado = 5000;

    private readonly Juego _juego;
    private readonly TcpListener _escucha;
    private readonly ListaSimple<ConexionCliente> _clientes = new ListaSimple<ConexionCliente>();
    private readonly object _candadoClientes = new object();
    private readonly object _candadoProcesamiento = new object();

    private Thread? _hiloAceptar;
    private volatile bool _activo;
    private int _ultimoEventoEnviado;
    private bool _finEnviado;
    private int? _idUltimoMovimiento;
    private int[] _ultimasRecorridas = new int[0];
    private IDispositivoCajero _cajero = new CajeroSimulado();
    private CajeroSimulado? _cajeroPruebas;
    private bool _modoSinHardware;
    private int? _idVinculacion;
    private ConexionCliente? _conexionVinculacion;

    /// <summary>
    /// Crea el servidor (no empieza a escuchar hasta <see cref="Iniciar"/>).
    /// </summary>
    /// <param name="juego">Partida que administra el servidor.</param>
    /// <param name="puerto">Puerto TCP (0 = uno libre elegido por el sistema).</param>
    /// <param name="direccion">Dirección local; por defecto <see cref="IPAddress.Any"/> (todas las interfaces).</param>
    public Servidor(Juego juego, int puerto = PuertoPredeterminado, IPAddress? direccion = null)
    {
        ArgumentNullException.ThrowIfNull(juego);
        _juego = juego;
        _escucha = new TcpListener(direccion ?? IPAddress.Any, puerto);
        UsarCajero(_cajero);
    }

    /// <summary>
    /// Mensajes de diagnóstico del servidor (conexiones, solicitudes rechazadas, errores).
    /// Se invoca desde hilos del servidor.
    /// </summary>
    public event Action<string>? Registro;

    /// <summary>
    /// Puerto en el que escucha (útil si se pidió el puerto 0).
    /// </summary>
    public int Puerto => ((IPEndPoint)_escucha.LocalEndpoint).Port;

    /// <summary>
    /// Indica si el servidor está aceptando conexiones.
    /// </summary>
    public bool Activo => _activo;

    /// <summary>
    /// Cantidad de conexiones abiertas (incluidas las que aún no enviaron CONECTAR).
    /// </summary>
    public int CantidadConexiones
    {
        get
        {
            lock (_candadoClientes)
            {
                return _clientes.Cantidad;
            }
        }
    }

    /// <summary>
    /// Obtiene las direcciones IPv4 de esta máquina (sin la de loopback), para que los demás se conecten.
    /// </summary>
    /// <returns>Las direcciones en formato texto.</returns>
    public static ListaSimple<string> ObtenerIPv4Locales()
    {
        ListaSimple<string> direcciones = new ListaSimple<string>();
        try
        {
            foreach (IPAddress direccion in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                string texto = direccion.ToString();
                if (direccion.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(direccion) && !direcciones.Contiene(texto))
                {
                    direcciones.AgregarAlFinal(texto);
                }
            }
        }
        catch (SocketException)
        {
            // Sin resolución de nombres: se devuelve la lista vacía.
        }

        return direcciones;
    }

    /// <summary>
    /// Empieza a escuchar y a aceptar clientes en un hilo de fondo.
    /// </summary>
    public void Iniciar()
    {
        if (_activo)
        {
            return;
        }

        _escucha.Start();
        _activo = true;
        _hiloAceptar = new Thread(AceptarClientes) { IsBackground = true, Name = "Servidor-Aceptar" };
        _hiloAceptar.Start();
        Registrar($"Servidor escuchando en el puerto {Puerto}.");
    }

    /// <summary>
    /// Avisa a todos los clientes con <c>SERVIDOR_CERRADO</c>, deja de aceptar clientes y cierra todas las conexiones.
    /// </summary>
    /// <param name="motivo">Motivo que verán los jugadores.</param>
    public void Detener(string motivo = "El organizador cerró la partida.")
    {
        if (!_activo)
        {
            return;
        }

        lock (_candadoProcesamiento)
        {
            _activo = false;
            _escucha.Stop();
            foreach (ConexionCliente conexion in CopiarClientes())
            {
                conexion.Enviar(Protocolo.Codificar(Protocolo.ServidorCerrado, motivo));
                conexion.Cerrar();
            }
        }

        Registrar("Servidor detenido.");
    }

    /// <summary>
    /// Detiene el servidor.
    /// </summary>
    public void Dispose()
    {
        Detener();
    }

    private void AceptarClientes()
    {
        while (_activo)
        {
            TcpClient tcp;
            try
            {
                tcp = _escucha.AcceptTcpClient();
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                if (!_activo)
                {
                    return;
                }

                continue;
            }

            ConexionCliente conexion;
            try
            {
                conexion = new ConexionCliente(tcp);
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException || ex is InvalidOperationException || ex is IOException)
            {
                // El cliente se desconectó justo al conectarse: se descarta sin afectar al servidor.
                tcp.Dispose();
                continue;
            }

            lock (_candadoClientes)
            {
                _clientes.AgregarAlFinal(conexion);
            }

            Registrar($"Nueva conexión desde {conexion.Direccion}.");
            Thread hilo = new Thread(() => AtenderCliente(conexion)) { IsBackground = true, Name = "Servidor-Cliente-" + conexion.Direccion };
            hilo.Start();
        }
    }

    private void AtenderCliente(ConexionCliente conexion)
    {
        try
        {
            string? linea;
            while (_activo && (linea = conexion.LeerLinea()) != null)
            {
                if (string.IsNullOrWhiteSpace(linea))
                {
                    continue;
                }

                Mensaje mensaje;
                try
                {
                    mensaje = Protocolo.Decodificar(linea);
                }
                catch (FormatException ex)
                {
                    conexion.Enviar(Protocolo.Codificar(Protocolo.Error, "Mensaje mal formado: " + ex.Message));
                    continue;
                }

                bool continuar;
                try
                {
                    continuar = Procesar(conexion, mensaje);
                }
                catch (Exception ex) when (!(ex is IOException || ex is ObjectDisposedException || ex is SocketException))
                {
                    // Un error inesperado al procesar una solicitud no debe tumbar la conexión ni el servidor.
                    Registrar($"Error al procesar {mensaje.Comando} de {conexion.Direccion}: {ex}");
                    conexion.Enviar(Protocolo.Codificar(Protocolo.Error, $"Error interno del servidor al procesar {mensaje.Comando}."));
                    continuar = true;
                }

                if (!continuar)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException)
        {
            // El cliente se desconectó abruptamente.
        }
        catch (Exception ex)
        {
            Registrar($"Error inesperado con {conexion.Direccion}: {ex.Message}");
        }
        finally
        {
            try
            {
                ManejarDesconexion(conexion);
            }
            catch (Exception ex)
            {
                // Una excepción en un hilo de fondo cerraría todo el programa: se registra y se sigue.
                Registrar($"Error al cerrar la conexión de {conexion.Direccion}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Procesa una solicitud. Devuelve <c>false</c> si el cliente pidió desconectarse.
    /// </summary>
    private bool Procesar(ConexionCliente conexion, Mensaje mensaje)
    {
        lock (_candadoProcesamiento)
        {
            if (mensaje.Comando == Protocolo.Desconectar)
            {
                return false;
            }

            if (mensaje.Comando == Protocolo.Conectar)
            {
                ProcesarConectar(conexion, mensaje);
                return true;
            }

            if (conexion.IdJugador == null)
            {
                ResponderError(conexion, $"Debe enviar {Protocolo.Conectar}|nombre antes de cualquier otra solicitud.");
                return true;
            }

            int id = conexion.IdJugador.Value;
            switch (mensaje.Comando)
            {
                case Protocolo.IniciarPartida:
                    ProcesarIniciarPartida(conexion, id);
                    break;
                case Protocolo.TirarDados:
                    if (!_modoSinHardware)
                    {
                        ResponderError(conexion, "Los dados se lanzan con el botón físico del cajero.");
                        break;
                    }

                    ProcesarTirarDados(conexion, id);
                    break;
                case Protocolo.ComprarPropiedad:
                    // Solo inicia la compra: se completa cuando el jugador acerca su tarjeta.
                    Responder(conexion, _juego.SolicitarCompra(id));
                    break;
                case Protocolo.NoComprar:
                    Responder(conexion, _juego.NoComprar(id));
                    break;
                case Protocolo.PagarConTarjeta:
                    ProcesarPagarConTarjeta(conexion, id);
                    break;
                case Protocolo.VincularTarjeta:
                    ProcesarVincularTarjeta(conexion, id, mensaje);
                    break;
                case Protocolo.TerminarTurno:
                    Responder(conexion, _juego.TerminarTurno(id));
                    break;
                case Protocolo.ConsultarEstado:
                    conexion.Enviar(CodificarEstado());
                    break;
                case Protocolo.ConsultarTransacciones:
                    ProcesarConsultarTransacciones(conexion, mensaje);
                    break;
                case Protocolo.ExportarTransacciones:
                    Responder(conexion, _juego.ExportarHistorial());
                    break;
                case Protocolo.RetirarJugador:
                    ProcesarRetirarJugador(conexion, id, mensaje);
                    break;
                default:
                    ResponderError(conexion, $"Comando desconocido: {mensaje.Comando}.");
                    break;
            }

            return true;
        }
    }

    /// <summary>
    /// <c>RETIRAR_JUGADOR|id</c>: el organizador retira a un jugador que está desconectado (para que la
    /// partida no quede esperando indefinidamente). A un jugador conectado no se le puede retirar.
    /// </summary>
    private void ProcesarRetirarJugador(ConexionCliente conexion, int idSolicitante, Mensaje mensaje)
    {
        int idRetirado;
        try
        {
            idRetirado = mensaje.Entero(0);
        }
        catch (FormatException)
        {
            ResponderError(conexion, $"Uso: {Protocolo.RetirarJugador}|idJugador");
            return;
        }

        if (EstaConectado(idRetirado))
        {
            string nombre = _juego.ObtenerJugador(idRetirado)?.Nombre ?? $"El jugador {idRetirado}";
            ResponderError(conexion, $"{nombre} está conectado; solo se puede retirar a jugadores desconectados.");
            return;
        }

        Responder(conexion, _juego.RetirarJugador(idSolicitante, idRetirado));
    }

    private void ProcesarConectar(ConexionCliente conexion, Mensaje mensaje)
    {
        if (conexion.IdJugador != null)
        {
            ResponderError(conexion, "Esta conexión ya está asociada a un jugador.");
            return;
        }

        if (mensaje.CantidadCampos < 1 || string.IsNullOrWhiteSpace(mensaje.Campo(0)))
        {
            ResponderError(conexion, $"Uso: {Protocolo.Conectar}|nombre");
            return;
        }

        string nombre = mensaje.Campo(0).Trim();

        // Reconexión: un jugador existente con ese nombre y sin conexión activa recupera su lugar.
        EstadoJugador? existente = BuscarJugadorPorNombre(nombre);
        if (existente != null && EstaConectado(existente.Id))
        {
            ResponderError(conexion, $"{existente.Nombre} ya está conectado desde otra conexión.");
            return;
        }

        if (existente != null)
        {
            conexion.IdJugador = existente.Id;
            conexion.Enviar(Protocolo.Codificar(Protocolo.Bienvenida, Texto(existente.Id), existente.Nombre));
            Registrar($"{existente.Nombre} se reconectó desde {conexion.Direccion}.");
            EnviarATodos(Protocolo.Codificar(Protocolo.Evento, $"{existente.Nombre} se reconectó."));
            DifundirCambios();
            return;
        }

        // Ficha elegida (opcional): CONECTAR|nombre|forma|color.
        FormaFicha? forma = null;
        ColorFicha? color = null;
        try
        {
            if (mensaje.CantidadCampos > 1 && mensaje.Campo(1).Length > 0)
            {
                forma = mensaje.Enumeracion<FormaFicha>(1);
            }

            if (mensaje.CantidadCampos > 2 && mensaje.Campo(2).Length > 0)
            {
                color = mensaje.Enumeracion<ColorFicha>(2);
            }
        }
        catch (FormatException ex)
        {
            ResponderError(conexion, $"Ficha no válida: {ex.Message}");
            return;
        }

        ResultadoAccion resultado = _juego.UnirJugador(nombre, forma, color);
        if (!resultado.Exito)
        {
            ResponderError(conexion, resultado.Mensaje);
            return;
        }

        int id = resultado.IdJugador!.Value;
        conexion.IdJugador = id;
        conexion.Enviar(Protocolo.Codificar(Protocolo.Bienvenida, Texto(id), _juego.ObtenerJugador(id)!.Nombre));
        Registrar($"{nombre} se unió como jugador {id} desde {conexion.Direccion}.");
        DifundirCambios();
    }

    /// <summary>
    /// Tira los dados del jugador indicado. La solicitud puede venir de un cliente (<paramref name="conexion"/>)
    /// o del botón del cajero (<c>null</c>): los rechazos se responden al cliente o se avisan a todos.
    /// La tirada también se envía al cajero.
    /// </summary>
    private void ProcesarTirarDados(ConexionCliente? conexion, int id)
    {
        ResultadoAccion resultado = _juego.TirarDados(id);
        if (!resultado.Exito)
        {
            if (conexion != null)
            {
                ResponderError(conexion, resultado.Mensaje);
            }
            else
            {
                // Botón presionado en un momento que no corresponde: se ignora y queda en el registro.
                AvisarCajero("Se ignoró el botón: " + resultado.Mensaje);
            }

            return;
        }

        TiradaDados tirada = resultado.Tirada!.Value;
        _idUltimoMovimiento = id;
        _ultimasRecorridas = SerializadorEstado.CasillasRecorridas(resultado.Movimientos);
        _cajero.MostrarDados(tirada.Dado1, tirada.Dado2);
        EnviarATodos(Protocolo.Codificar(Protocolo.Dados, Texto(id), Texto(tirada.Dado1), Texto(tirada.Dado2)));
        DifundirCambios();
    }

    // ------------------------------------------------------------------------------------------
    // Cajero (Pico W o simulado)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Cajero en uso (por defecto, el simulado).
    /// </summary>
    public IDispositivoCajero Cajero
    {
        get
        {
            lock (_candadoProcesamiento)
            {
                return _cajero;
            }
        }
    }

    private bool CajeroFisicoActivo => _cajero.EsFisico && _cajero.Estado == EstadoCajero.Conectado;

    /// <summary>
    /// Cambia el cajero del servidor (por ejemplo, al conectar la Pico W o al volver al modo simulado).
    /// El servidor no toma posesión del dispositivo: quien lo creó debe liberarlo.
    /// </summary>
    /// <param name="cajero">Nuevo cajero.</param>
    public void UsarCajero(IDispositivoCajero cajero)
    {
        ArgumentNullException.ThrowIfNull(cajero);
        lock (_candadoProcesamiento)
        {
            _cajero.BotonPresionado -= AlPresionarBoton;
            _cajero.TarjetaLeida -= AlLeerTarjeta;
            _cajero.EstadoCambiado -= AlCambiarEstadoCajero;
            _cajero.ErrorDispositivo -= AlRecibirErrorCajero;
            _cajero = cajero;
            _cajero.BotonPresionado += AlPresionarBoton;
            _cajero.TarjetaLeida += AlLeerTarjeta;
            _cajero.EstadoCambiado += AlCambiarEstadoCajero;
            _cajero.ErrorDispositivo += AlRecibirErrorCajero;
            _idVinculacion = null;
            _conexionVinculacion = null;
            InformarCambioCajero();
        }
    }

    /// <summary>
    /// <c>PAGAR_CON_TARJETA</c>: solo en modo sin hardware (pruebas), usa el UID del propio
    /// jugador y pasa por el mismo flujo que una tarjeta leída. En modo hardware se paga y se compra
    /// únicamente acercando la tarjeta al lector.
    /// </summary>
    private void ProcesarPagarConTarjeta(ConexionCliente conexion, int id)
    {
        if (!_modoSinHardware)
        {
            ResponderError(conexion, "Acerque su tarjeta al lector del cajero.");
            return;
        }

        ProcesarLecturaTarjeta(_juego.ObtenerJugador(id)!.UidTarjeta ?? string.Empty, conexion);
    }

    /// <summary>
    /// <c>INICIAR_PARTIDA</c>: en modo hardware, todos los jugadores deben tener una tarjeta física vinculada.
    /// </summary>
    private void ProcesarIniciarPartida(ConexionCliente conexion, int id)
    {
        if (!_modoSinHardware)
        {
            string faltan = string.Empty;
            foreach (EstadoJugador jugador in _juego.ObtenerInstantanea().Jugadores)
            {
                if (!jugador.TieneTarjetaFisica)
                {
                    faltan += (faltan.Length > 0 ? ", " : string.Empty) + jugador.Nombre;
                }
            }

            if (faltan.Length > 0)
            {
                ResponderError(conexion, $"Todos los jugadores deben tener una tarjeta vinculada antes de iniciar. Faltan: {faltan}.");
                return;
            }
        }

        Responder(conexion, _juego.IniciarPartida(id));
    }

    // ------------------------------------------------------------------------------------------
    // Modo sin hardware (pruebas)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Indica si el servidor está en modo sin hardware (pruebas): el organizador simula el botón y las
    /// tarjetas y no se envían mensajes a la Pico. Por defecto es <c>false</c> (modo hardware).
    /// </summary>
    public bool ModoSinHardware
    {
        get
        {
            lock (_candadoProcesamiento)
            {
                return _modoSinHardware;
            }
        }
    }

    /// <summary>
    /// Activa o desactiva el modo sin hardware. Al activarlo se usa un cajero simulado propio del servidor
    /// (el que conecten <see cref="SimularBoton"/> y <see cref="SimularTarjetaDelJugadorEnTurno"/>); al
    /// desactivarlo, la partida espera a que se conecte la Pico W.
    /// </summary>
    /// <param name="activo">Si se activa el modo sin hardware.</param>
    public void EstablecerModoSinHardware(bool activo)
    {
        lock (_candadoProcesamiento)
        {
            if (_modoSinHardware == activo)
            {
                return;
            }

            _modoSinHardware = activo;
            if (activo)
            {
                _cajeroPruebas = new CajeroSimulado();
                UsarCajero(_cajeroPruebas);
            }
            else
            {
                _cajeroPruebas = null;
                InformarCambioCajero();
            }
        }
    }

    /// <summary>
    /// Modo sin hardware: simula una pulsación del botón del dado (mismo flujo que <c>BOTON</c> de la Pico).
    /// </summary>
    /// <returns>Rechazo si no está activo el modo sin hardware.</returns>
    public ResultadoAccion SimularBoton()
    {
        lock (_candadoProcesamiento)
        {
            if (!_modoSinHardware || _cajeroPruebas == null)
            {
                return ResultadoAccion.Fallido("La simulación solo está disponible en el modo sin hardware.");
            }

            _cajeroPruebas.SimularBoton();
            return ResultadoAccion.Correcto("Botón simulado.");
        }
    }

    /// <summary>
    /// Modo sin hardware: simula que el jugador en turno acerca su tarjeta (mismo flujo que <c>RFID:uid</c>,
    /// incluidas las compras y los pagos).
    /// </summary>
    /// <returns>Rechazo si no está activo el modo sin hardware o no hay jugador en turno.</returns>
    public ResultadoAccion SimularTarjetaDelJugadorEnTurno()
    {
        lock (_candadoProcesamiento)
        {
            if (!_modoSinHardware || _cajeroPruebas == null)
            {
                return ResultadoAccion.Fallido("La simulación solo está disponible en el modo sin hardware.");
            }

            int? idEnTurno = _juego.ObtenerInstantanea().IdJugadorEnTurno;
            if (!idEnTurno.HasValue)
            {
                return ResultadoAccion.Fallido("No hay un jugador en turno.");
            }

            _cajeroPruebas.SimularTarjeta(_juego.ObtenerJugador(idEnTurno.Value)!.UidTarjeta ?? string.Empty);
            return ResultadoAccion.Correcto("Tarjeta simulada.");
        }
    }

    /// <summary>
    /// <c>VINCULAR_TARJETA|id</c>: el organizador indica que la próxima tarjeta leída por el cajero
    /// se vincula a ese jugador (0 cancela la espera).
    /// </summary>
    private void ProcesarVincularTarjeta(ConexionCliente conexion, int idSolicitante, Mensaje mensaje)
    {
        int idJugador;
        try
        {
            idJugador = mensaje.Entero(0);
        }
        catch (FormatException)
        {
            ResponderError(conexion, $"Uso: {Protocolo.VincularTarjeta}|idJugador (0 para cancelar)");
            return;
        }

        if (!EsOrganizador(idSolicitante))
        {
            ResponderError(conexion, "Solo el organizador puede vincular tarjetas.");
            return;
        }

        if (idJugador == 0)
        {
            _idVinculacion = null;
            _conexionVinculacion = null;
            AvisarCajero("Se canceló la vinculación de tarjeta.");
            return;
        }

        if (!CajeroFisicoActivo)
        {
            ResponderError(conexion, "No hay un cajero conectado: conecte la Pico W para vincular tarjetas.");
            return;
        }

        Jugador? jugador = _juego.ObtenerJugador(idJugador);
        if (jugador == null || !jugador.Activo)
        {
            ResponderError(conexion, $"No existe un jugador activo con id {idJugador}.");
            return;
        }

        _idVinculacion = idJugador;
        _conexionVinculacion = conexion;
        AvisarCajero($"Acerque al lector la tarjeta de {jugador.Nombre}.");
    }

    private void AlPresionarBoton()
    {
        lock (_candadoProcesamiento)
        {
            if (!_activo)
            {
                return;
            }

            InstantaneaJuego instantanea = _juego.ObtenerInstantanea();
            if (instantanea.Estado != EstadoPartida.EnCurso || !instantanea.IdJugadorEnTurno.HasValue)
            {
                AvisarCajero("Se ignoró el botón: la partida no está en curso.");
                return;
            }

            // Mismas validaciones que TIRAR_DADOS, a nombre del jugador en turno.
            ProcesarTirarDados(null, instantanea.IdJugadorEnTurno.Value);
        }
    }

    private void AlLeerTarjeta(string uid)
    {
        lock (_candadoProcesamiento)
        {
            if (!_activo)
            {
                return;
            }

            Registrar($"Tarjeta leída: {uid}.");
            if (_idVinculacion.HasValue)
            {
                int idJugador = _idVinculacion.Value;
                ConexionCliente? solicitante = _conexionVinculacion;
                _idVinculacion = null;
                _conexionVinculacion = null;
                ResultadoAccion vinculacion = _juego.VincularTarjeta(idJugador, uid);
                if (vinculacion.Exito)
                {
                    DifundirCambios();
                }
                else
                {
                    if (solicitante != null)
                    {
                        ResponderError(solicitante, vinculacion.Mensaje);
                    }

                    AvisarCajero(vinculacion.Mensaje);
                }

                return;
            }

            ProcesarLecturaTarjeta(uid, null);
        }
    }

    /// <summary>
    /// Atiende una tarjeta leída (o simulada): con una compra o un pago pendiente la usa para completarlo y
    /// enciende el LED de pago (PAGO_OK / PAGO_RECHAZADO); fuera de ellos solo informa de quién es y su saldo.
    /// </summary>
    /// <param name="uid">UID leído.</param>
    /// <param name="conexion">Cliente que lo pidió (PAGAR_CON_TARJETA en modo sin hardware), o <c>null</c> si vino del cajero.</param>
    private void ProcesarLecturaTarjeta(string uid, ConexionCliente? conexion)
    {
        InstantaneaJuego instantanea = _juego.ObtenerInstantanea();
        bool pendiente = instantanea.Estado == EstadoPartida.EnCurso
                         && (instantanea.Fase == FaseTurno.EsperandoPago || instantanea.Fase == FaseTurno.EsperandoTarjetaCompra);
        if (!pendiente)
        {
            ResultadoAccion consulta = _juego.ConsultarTarjeta(uid);
            if (conexion != null && !consulta.Exito)
            {
                ResponderError(conexion, consulta.Mensaje);
            }

            AvisarCajero(consulta.Mensaje);
            return;
        }

        ResultadoAccion resultado = _juego.IdentificarTarjeta(uid);
        _cajero.IndicarPago(resultado.PagoAceptado == true);  // LED: encendido 2 s o 3 parpadeos rápidos
        if (!resultado.Exito)
        {
            if (conexion != null)
            {
                ResponderError(conexion, resultado.Mensaje);
            }

            AvisarCajero(resultado.Mensaje);
        }

        // También tras un rechazo: por ejemplo, sin saldo para comprar la partida vuelve a "Comprar / No comprar".
        DifundirCambios();
    }

    private void AlCambiarEstadoCajero(EstadoCajero estado)
    {
        lock (_candadoProcesamiento)
        {
            if (estado == EstadoCajero.Desconectado)
            {
                _idVinculacion = null;
                _conexionVinculacion = null;
            }

            if (estado == EstadoCajero.Conectado || estado == EstadoCajero.Desconectado)
            {
                InformarCambioCajero();
            }
        }
    }

    private void AlRecibirErrorCajero(string detalle)
    {
        lock (_candadoProcesamiento)
        {
            Registrar("Error del cajero: " + detalle);
            AvisarCajero("Error del módulo: " + detalle);
        }
    }

    /// <summary>
    /// Avisa a todos el estado del cajero y envía el ESTADO (que indica si hay cajero físico).
    /// </summary>
    private void InformarCambioCajero()
    {
        if (!_activo)
        {
            return;
        }

        AvisarCajero(_modoSinHardware
            ? "Modo sin hardware (pruebas): el organizador simula el botón y las tarjetas."
            : CajeroFisicoActivo
                ? $"{_cajero.Descripcion} conectado: el botón tira los dados y se paga con la tarjeta."
                : "Cajero desconectado: la partida queda en pausa hasta que se conecte la Pico W.");
        EnviarATodos(CodificarEstado());
    }

    /// <summary>
    /// Mensaje del cajero para todos los jugadores (no forma parte del registro de la partida).
    /// </summary>
    private void AvisarCajero(string texto)
    {
        Registrar("Cajero: " + texto);
        EnviarATodos(Protocolo.Codificar(Protocolo.Evento, "Cajero: " + texto));
    }

    private bool EsOrganizador(int idJugador)
    {
        EstadoJugador[] jugadores = _juego.ObtenerInstantanea().Jugadores;
        return jugadores.Length > 0 && jugadores[0].Id == idJugador;
    }

    private void ProcesarConsultarTransacciones(ConexionCliente conexion, Mensaje mensaje)
    {
        string textoFiltro = mensaje.CantidadCampos > 0 && mensaje.Campo(0).Length > 0 ? mensaje.Campo(0) : nameof(FiltroTransacciones.Todas);
        FiltroTransacciones filtro;
        try
        {
            filtro = mensaje.CantidadCampos > 0 && mensaje.Campo(0).Length > 0
                ? mensaje.Enumeracion<FiltroTransacciones>(0)
                : FiltroTransacciones.Todas;
        }
        catch (FormatException)
        {
            ResponderError(conexion, $"Filtro desconocido: {textoFiltro}. Use TODAS, ANTIGUAS, RECIENTES, JUGADOR|nombre o TIPO|tipo.");
            return;
        }

        string valor = mensaje.CantidadCampos > 1 ? mensaje.Campo(1) : string.Empty;
        TipoTransaccion tipo = default;
        if ((filtro == FiltroTransacciones.Jugador || filtro == FiltroTransacciones.Tipo) && valor.Length == 0)
        {
            ResponderError(conexion, $"El filtro {filtro.ToString().ToUpperInvariant()} requiere un valor.");
            return;
        }

        if (filtro == FiltroTransacciones.Tipo && (!Enum.TryParse(valor, true, out tipo) || !Enum.IsDefined(tipo)))
        {
            ResponderError(conexion, $"Tipo de transacción desconocido: {valor}.");
            return;
        }

        ListaSimple<Transaccion> lista = _juego.LeerHistorial(historial =>
        {
            ListaSimple<Transaccion> resultado = new ListaSimple<Transaccion>();
            switch (filtro)
            {
                case FiltroTransacciones.Recientes:
                    historial.RecorrerDesdeMasReciente(resultado.AgregarAlFinal);
                    break;
                case FiltroTransacciones.Jugador:
                    historial.BuscarPorJugador(valor).RecorrerDesdeInicio(resultado.AgregarAlFinal);
                    break;
                case FiltroTransacciones.Tipo:
                    historial.BuscarPorTipo(tipo).RecorrerDesdeInicio(resultado.AgregarAlFinal);
                    break;
                default:
                    historial.RecorrerDesdeMasAntigua(resultado.AgregarAlFinal);
                    break;
            }

            return resultado;
        });

        string descripcionFiltro = valor.Length > 0 ? $"{filtro.ToString().ToUpperInvariant()} {valor}" : filtro.ToString().ToUpperInvariant();
        conexion.Enviar(SerializadorTransacciones.Codificar(descripcionFiltro, lista));
    }

    /// <summary>
    /// Si la acción fue rechazada, responde ERROR solo al solicitante; si se aceptó, difunde los cambios a todos.
    /// </summary>
    private void Responder(ConexionCliente conexion, ResultadoAccion resultado)
    {
        if (resultado.Exito)
        {
            DifundirCambios();
        }
        else
        {
            ResponderError(conexion, resultado.Mensaje);
        }
    }

    private void ResponderError(ConexionCliente conexion, string mensaje)
    {
        conexion.Enviar(Protocolo.Codificar(Protocolo.Error, mensaje));
    }

    /// <summary>
    /// Envía a todos los eventos nuevos del juego, el ESTADO actual y, si la partida acaba de terminar, FIN.
    /// </summary>
    private void DifundirCambios()
    {
        foreach (EventoJuego evento in _juego.ObtenerEventosDesde(_ultimoEventoEnviado + 1))
        {
            EnviarATodos(Protocolo.Codificar(Protocolo.Evento, evento.Texto));
            _ultimoEventoEnviado = evento.Numero;
        }

        InstantaneaJuego instantanea = _juego.ObtenerInstantanea();
        EnviarATodos(SerializadorEstado.Codificar(CrearEstado(instantanea)));

        if (!_finEnviado && instantanea.Estado == EstadoPartida.Finalizada)
        {
            _finEnviado = true;
            EnviarATodos(CodificarFin(instantanea));
        }
    }

    private string CodificarEstado()
    {
        return SerializadorEstado.Codificar(CrearEstado(_juego.ObtenerInstantanea()));
    }

    /// <summary>
    /// Arma el estado de red: la instantánea más el último recorrido y los jugadores sin conexión.
    /// </summary>
    private EstadoRed CrearEstado(InstantaneaJuego instantanea)
    {
        ListaSimple<int> desconectados = new ListaSimple<int>();
        foreach (EstadoJugador jugador in instantanea.Jugadores)
        {
            if (!EstaConectado(jugador.Id))
            {
                desconectados.AgregarAlFinal(jugador.Id);
            }
        }

        int[] ids = new int[desconectados.Cantidad];
        int i = 0;
        desconectados.Recorrer(id => ids[i++] = id);
        return new EstadoRed(instantanea, _idUltimoMovimiento, _ultimasRecorridas, ids, CajeroFisicoActivo, _modoSinHardware);
    }

    private string CodificarFin(InstantaneaJuego instantanea)
    {
        StringBuilder resumen = new StringBuilder();
        string ganador = string.Empty;
        foreach (EstadoJugador jugador in instantanea.Jugadores)
        {
            if (jugador.Id == instantanea.IdGanador)
            {
                ganador = jugador.Nombre;
            }

            if (resumen.Length > 0)
            {
                resumen.Append("; ");
            }

            resumen.Append($"{jugador.Nombre}: patrimonio {Formato.Dinero(jugador.Patrimonio)}{(jugador.Activo ? string.Empty : " (eliminado)")}");
        }

        string? ruta = _juego.RutaHistorialExportado;
        if (ruta != null)
        {
            resumen.Append(". Historial: ").Append(ruta);
        }

        return Protocolo.Codificar(Protocolo.Fin, ganador, resumen.ToString());
    }

    /// <summary>
    /// Envía una línea a todos los clientes que ya se unieron como jugadores.
    /// </summary>
    private void EnviarATodos(string linea)
    {
        foreach (ConexionCliente conexion in CopiarClientes())
        {
            if (conexion.IdJugador != null)
            {
                conexion.Enviar(linea);
            }
        }
    }

    private void ManejarDesconexion(ConexionCliente conexion)
    {
        lock (_candadoClientes)
        {
            _clientes.Eliminar(conexion);
        }

        conexion.Cerrar();
        if (conexion.IdJugador == null || !_activo)
        {
            Registrar($"Se cerró la conexión de {conexion.Direccion}.");
            return;
        }

        lock (_candadoProcesamiento)
        {
            if (!_activo)
            {
                return;
            }

            int id = conexion.IdJugador.Value;
            string nombre = _juego.ObtenerJugador(id)?.Nombre ?? "Un jugador";
            InstantaneaJuego instantanea = _juego.ObtenerInstantanea();
            string aviso = $"{nombre} se desconectó. Sigue en la partida y puede reconectarse con el mismo nombre.";
            if (instantanea.Estado == EstadoPartida.EnCurso && instantanea.IdJugadorEnTurno == id)
            {
                aviso += " Es su turno: la partida lo espera, o el organizador puede retirarlo.";
            }

            Registrar($"{nombre} se desconectó ({conexion.Direccion}).");
            EnviarATodos(Protocolo.Codificar(Protocolo.Evento, aviso));
            EnviarATodos(SerializadorEstado.Codificar(CrearEstado(instantanea)));
        }
    }

    private bool EstaConectado(int idJugador)
    {
        foreach (ConexionCliente conexion in CopiarClientes())
        {
            if (conexion.IdJugador == idJugador)
            {
                return true;
            }
        }

        return false;
    }

    private EstadoJugador? BuscarJugadorPorNombre(string nombre)
    {
        foreach (EstadoJugador jugador in _juego.ObtenerInstantanea().Jugadores)
        {
            if (string.Equals(jugador.Nombre, nombre, StringComparison.OrdinalIgnoreCase))
            {
                return jugador;
            }
        }

        return null;
    }

    private ConexionCliente[] CopiarClientes()
    {
        lock (_candadoClientes)
        {
            ConexionCliente[] copia = new ConexionCliente[_clientes.Cantidad];
            int i = 0;
            _clientes.Recorrer(conexion => copia[i++] = conexion);
            return copia;
        }
    }

    private void Registrar(string texto)
    {
        Registro?.Invoke(texto);
    }

    private static string Texto(int valor)
    {
        return valor.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
