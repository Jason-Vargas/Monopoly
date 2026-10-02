using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace Monopoly.Core.Hardware;

/// <summary>
/// Cajero físico: Raspberry Pi Pico W con MicroPython conectada por USB serial (ver hardware/README.md).
/// Un hilo propio lee las líneas; la conexión se confirma con PING/PONG y se vigila con un PING periódico,
/// de modo que si se desconecta el cable el estado pasa a <see cref="EstadoCajero.Desconectado"/>.
/// </summary>
public sealed class CajeroPico : CajeroPorLineas
{
    /// <summary>Velocidad del puerto (en el USB de la Pico no influye, pero se fija por claridad).</summary>
    public const int Baudios = 115200;

    /// <summary>Tiempo máximo para recibir el primer PONG al conectar.</summary>
    public const int EsperaRespuestaMs = 2500;

    private const int TiempoLecturaMs = 500;
    private const int TiempoEscrituraMs = 1000;
    private const int IntervaloPingMs = 2000;
    private const int SilencioMaximoMs = 7000;

    private readonly object _candadoPuerto = new object();
    private readonly object _candadoEscritura = new object();
    private readonly ManualResetEventSlim _pongRecibido = new ManualResetEventSlim(false);
    private SerialPort? _puerto;
    private Thread? _hiloLector;
    private volatile bool _detener;
    private long _ultimaRecepcion;

    /// <summary>
    /// Crea el cajero para un puerto (todavía sin abrir).
    /// </summary>
    /// <param name="nombrePuerto">Nombre del puerto, por ejemplo "COM5".</param>
    public CajeroPico(string nombrePuerto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombrePuerto);
        NombrePuerto = nombrePuerto;
    }

    /// <summary>
    /// Nombre del puerto serie.
    /// </summary>
    public string NombrePuerto { get; }

    /// <inheritdoc/>
    public override string Descripcion => $"Pico W en {NombrePuerto}";

    /// <summary>
    /// Puertos serie disponibles en esta computadora, ordenados.
    /// </summary>
    /// <returns>Los nombres de los puertos.</returns>
    public static string[] PuertosDisponibles()
    {
        string[] puertos = SerialPort.GetPortNames();
        Array.Sort(puertos, StringComparer.OrdinalIgnoreCase);
        return puertos;
    }

    /// <summary>
    /// Prueba cada puerto enviando PING y esperando PONG.
    /// </summary>
    /// <param name="esperaMs">Tiempo de espera por puerto.</param>
    /// <returns>El primer puerto donde respondió una Pico, o <c>null</c> si no hay ninguna.</returns>
    public static string? DetectarPuerto(int esperaMs = 1500)
    {
        foreach (string nombre in PuertosDisponibles())
        {
            using CajeroPico prueba = new CajeroPico(nombre);
            try
            {
                prueba.Conectar(esperaMs);
                return nombre;
            }
            catch (Exception ex) when (ex is IOException || ex is TimeoutException)
            {
                // Ese puerto no es la Pico (o está ocupado): se prueba el siguiente.
            }
        }

        return null;
    }

    /// <summary>
    /// Abre el puerto y espera PONG. Si la Pico no responde, cierra el puerto y lanza una excepción.
    /// </summary>
    /// <param name="esperaMs">Tiempo máximo de espera del PONG.</param>
    /// <exception cref="IOException">Si el puerto no existe o está en uso.</exception>
    /// <exception cref="TimeoutException">Si el dispositivo no responde como la Pico del juego.</exception>
    public void Conectar(int esperaMs = EsperaRespuestaMs)
    {
        lock (_candadoPuerto)
        {
            if (Estado == EstadoCajero.Conectado)
            {
                return;
            }

            CerrarPuerto();
            CambiarEstado(EstadoCajero.Conectando);
            SerialPort puerto = new SerialPort(NombrePuerto, Baudios)
            {
                NewLine = "\n",
                DtrEnable = true,   // sin DTR la Pico puede no enviar datos al PC
                RtsEnable = true,
                ReadTimeout = TiempoLecturaMs,
                WriteTimeout = TiempoEscrituraMs,
                Encoding = Encoding.ASCII,
            };

            try
            {
                puerto.Open();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is ArgumentException || ex is InvalidOperationException)
            {
                puerto.Dispose();
                CambiarEstado(EstadoCajero.Desconectado);
                string causa = ex is UnauthorizedAccessException ? "está en uso por otro programa (¿Thonny abierto?)" : ex.Message;
                throw new IOException($"No se pudo abrir {NombrePuerto}: {causa}", ex);
            }

            _puerto = puerto;
            _detener = false;
            _pongRecibido.Reset();
            _ultimaRecepcion = Environment.TickCount64;
            _hiloLector = new Thread(LeerLineas) { IsBackground = true, Name = "CajeroPico-" + NombrePuerto };
            _hiloLector.Start();
        }

        // La Pico puede estar terminando de arrancar: se insiste con PING hasta recibir PONG.
        long limite = Environment.TickCount64 + esperaMs;
        while (Environment.TickCount64 < limite)
        {
            try
            {
                Escribir(ProtocoloCajero.Ping);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is TimeoutException)
            {
                break;
            }

            if (_pongRecibido.Wait(500))
            {
                CambiarEstado(EstadoCajero.Conectado);
                return;
            }
        }

        Desconectar();
        throw new TimeoutException($"{NombrePuerto} no respondió a PING. ¿Es la Pico W con main.py cargado?");
    }

    /// <summary>
    /// Cierra el puerto (se puede volver a conectar después).
    /// </summary>
    public void Desconectar()
    {
        Thread? hilo;
        lock (_candadoPuerto)
        {
            _detener = true;
            hilo = _hiloLector;
            _hiloLector = null;
            CerrarPuerto();
        }

        if (hilo != null && hilo != Thread.CurrentThread)
        {
            hilo.Join(1000);
        }

        CambiarEstado(EstadoCajero.Desconectado);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        Desconectar();
        _pongRecibido.Dispose();
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override void EnviarLinea(string linea)
    {
        Escribir(linea);
    }

    /// <inheritdoc/>
    protected override void AlRecibirPong()
    {
        _pongRecibido.Set();
    }

    /// <inheritdoc/>
    protected override void AlFallarEnvio(Exception error)
    {
        PerderConexion();
    }

    private void Escribir(string linea)
    {
        lock (_candadoEscritura)
        {
            SerialPort puerto = _puerto ?? throw new InvalidOperationException("El puerto no está abierto.");
            puerto.WriteLine(linea);
        }
    }

    private void LeerLineas()
    {
        SerialPort? puerto = _puerto;
        long ultimoPing = Environment.TickCount64;
        while (!_detener && puerto != null)
        {
            try
            {
                string linea = puerto.ReadLine();
                _ultimaRecepcion = Environment.TickCount64;
                ProcesarLinea(linea);
            }
            catch (TimeoutException)
            {
                if (Estado != EstadoCajero.Conectado)
                {
                    continue;
                }

                // Latido: la Pico responde PONG; si deja de responder, se considera desconectada.
                long ahora = Environment.TickCount64;
                if (ahora - _ultimaRecepcion > SilencioMaximoMs)
                {
                    PerderConexion();
                    return;
                }

                if (ahora - ultimoPing >= IntervaloPingMs)
                {
                    ultimoPing = ahora;
                    try
                    {
                        Escribir(ProtocoloCajero.Ping);
                    }
                    catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is TimeoutException || ex is UnauthorizedAccessException)
                    {
                        PerderConexion();
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException
                                       || ex is ObjectDisposedException || ex is OperationCanceledException)
            {
                // Cable desconectado o puerto cerrado (al cerrarlo, la lectura pendiente se cancela).
                if (!_detener)
                {
                    PerderConexion();
                }

                return;
            }
            catch (Exception)
            {
                // Cualquier otro error del controlador serie: una excepción en este hilo cerraría todo
                // el programa, así que se trata como una desconexión.
                if (!_detener)
                {
                    PerderConexion();
                }

                return;
            }
        }
    }

    private void PerderConexion()
    {
        lock (_candadoPuerto)
        {
            _detener = true;
            CerrarPuerto();
        }

        CambiarEstado(EstadoCajero.Desconectado);
    }

    private void CerrarPuerto()
    {
        SerialPort? puerto = _puerto;
        _puerto = null;
        if (puerto == null)
        {
            return;
        }

        try
        {
            puerto.Close();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            // El dispositivo ya no existe (cable retirado).
        }

        puerto.Dispose();
    }
}
