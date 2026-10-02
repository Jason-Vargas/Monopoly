using System;
using System.Globalization;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Hardware;

namespace Monopoly.App;

/// <summary>
/// Acompaña al cajero del organizador mientras dura la sesión (sala de espera y partida): guarda las
/// últimas líneas de diagnóstico (BOTON, RFID, errores, conexión) y avisa si la Pico W se pierde sin que
/// el organizador la haya desconectado. Los eventos se disparan en el hilo de la interfaz.
/// </summary>
internal sealed class MonitorCajero : IDisposable
{
    private const int MaximoLineas = 40;

    private readonly SesionJuego _sesion;
    private readonly ColaCircular<string> _lineas = new ColaCircular<string>(MaximoLineas + 1);
    private IDispositivoCajero? _observado;
    private bool _picoConectadaAntes;
    private bool _desconexionPedida;

    /// <summary>
    /// Empieza a observar el cajero de la sesión (y los que lo reemplacen).
    /// </summary>
    public MonitorCajero(SesionJuego sesion)
    {
        _sesion = sesion;
        _sesion.CajeroCambiado += AlCambiarCajero;
        AlCambiarCajero();
    }

    /// <summary>Se agregó una línea al monitor.</summary>
    public event Action<string>? LineaAgregada;

    /// <summary>
    /// La Pico se desconectó sin que el organizador lo pidiera (cable retirado, por ejemplo): la partida
    /// queda en pausa hasta reconectarla.
    /// </summary>
    public event Action? PicoPerdida;

    /// <summary>Indica si hay una Pico física conectada.</summary>
    public bool PicoConectada => _sesion.Cajero.EsFisico && _sesion.Cajero.Estado == EstadoCajero.Conectado;

    /// <summary>
    /// Avisa que la próxima desconexión la pidió el organizador (no hay que alertar).
    /// </summary>
    public void MarcarDesconexionPedida()
    {
        _desconexionPedida = true;
    }

    /// <summary>
    /// Agrega una línea al monitor (por ejemplo, lo que se envió para probar el LED).
    /// </summary>
    public void Registrar(string texto)
    {
        string linea = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + texto;
        _lineas.Encolar(linea);
        while (_lineas.Cantidad > MaximoLineas)
        {
            _lineas.Desencolar();
        }

        LineaAgregada?.Invoke(linea);
    }

    /// <summary>
    /// Recorre las líneas guardadas, de la más antigua a la más reciente.
    /// </summary>
    public void RecorrerLineas(Action<string> accion)
    {
        _lineas.Recorrer(accion);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _sesion.CajeroCambiado -= AlCambiarCajero;
        Observar(null);
    }

    private void AlCambiarCajero()
    {
        if (!ReferenceEquals(_observado, _sesion.Cajero))
        {
            Observar(_sesion.Cajero);
            Registrar(_sesion.Cajero.EsFisico ? $"· usando {_sesion.Cajero.Descripcion}" : "· sin Pico (cajero simulado)");
        }

        bool conectada = PicoConectada;
        bool sinHardware = _sesion.ModoSinHardware;
        if (_picoConectadaAntes && !conectada && !_desconexionPedida && !sinHardware)
        {
            PicoPerdida?.Invoke();
        }

        if (_picoConectadaAntes != conectada)
        {
            Registrar(conectada ? "· conectada (PING/PONG correcto)" : "· desconectada");
        }

        _picoConectadaAntes = conectada;
        _desconexionPedida = false;
    }

    private void Observar(IDispositivoCajero? cajero)
    {
        if (_observado != null)
        {
            _observado.BotonPresionado -= AlPresionarBoton;
            _observado.TarjetaLeida -= AlLeerTarjeta;
            _observado.ErrorDispositivo -= AlRecibirError;
        }

        _observado = cajero;
        if (cajero != null)
        {
            cajero.BotonPresionado += AlPresionarBoton;
            cajero.TarjetaLeida += AlLeerTarjeta;
            cajero.ErrorDispositivo += AlRecibirError;
        }
    }

    // Los eventos del cajero llegan en su propio hilo: se pasan al de la interfaz.
    private void AlPresionarBoton() => _sesion.EnHiloInterfaz(() => Registrar("← BOTON"));

    private void AlLeerTarjeta(string uid) => _sesion.EnHiloInterfaz(() => Registrar("← RFID:" + uid));

    private void AlRecibirError(string mensaje) => _sesion.EnHiloInterfaz(() => Registrar("← ERROR:" + mensaje));
}
