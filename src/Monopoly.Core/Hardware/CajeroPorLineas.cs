using System;
using System.IO;
using System.Threading;

namespace Monopoly.Core.Hardware;

/// <summary>
/// Base de los cajeros que hablan el protocolo de líneas de la Pico W: interpreta cada línea recibida
/// (BOTON, RFID, PONG, LISTO, ERROR) y envía DADOS y LIMPIAR. Las líneas que no son del protocolo
/// (mensajes de arranque de MicroPython, restos de la consola, UIDs mal formados) se ignoran.
/// </summary>
public abstract class CajeroPorLineas : IDispositivoCajero
{
    private EstadoCajero _estado = EstadoCajero.Desconectado;
    private int _lineasIgnoradas;

    /// <inheritdoc/>
    public event Action? BotonPresionado;

    /// <inheritdoc/>
    public event Action<string>? TarjetaLeida;

    /// <inheritdoc/>
    public event Action<EstadoCajero>? EstadoCambiado;

    /// <inheritdoc/>
    public event Action<string>? ErrorDispositivo;

    /// <inheritdoc/>
    public abstract string Descripcion { get; }

    /// <inheritdoc/>
    public bool EsFisico => true;

    /// <inheritdoc/>
    public EstadoCajero Estado => _estado;

    /// <summary>
    /// Cantidad de líneas recibidas que no pertenecían al protocolo (diagnóstico).
    /// </summary>
    public int LineasIgnoradas => _lineasIgnoradas;

    /// <inheritdoc/>
    public void MostrarDados(int dado1, int dado2)
    {
        if (dado1 < 1 || dado1 > 6 || dado2 < 1 || dado2 > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(dado1), "Los dados deben estar entre 1 y 6.");
        }

        EnviarSiConectado(ProtocoloCajero.PrefijoDados + dado1 + "," + dado2);
    }

    /// <inheritdoc/>
    public void LimpiarDisplays()
    {
        EnviarSiConectado(ProtocoloCajero.Limpiar);
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Envía una línea al dispositivo (sin el salto de línea).
    /// </summary>
    /// <param name="linea">Línea del protocolo.</param>
    protected abstract void EnviarLinea(string linea);

    /// <summary>
    /// Cambia el estado y avisa si es distinto del anterior.
    /// </summary>
    /// <param name="nuevo">Nuevo estado.</param>
    protected void CambiarEstado(EstadoCajero nuevo)
    {
        if (_estado == nuevo)
        {
            return;
        }

        _estado = nuevo;
        EstadoCambiado?.Invoke(nuevo);
    }

    /// <summary>
    /// Se recibió <c>PONG</c> (lo usa la implementación para confirmar la conexión).
    /// </summary>
    protected virtual void AlRecibirPong()
    {
    }

    /// <summary>
    /// Falló un envío: por defecto el dispositivo pasa a desconectado.
    /// </summary>
    /// <param name="error">Error producido.</param>
    protected virtual void AlFallarEnvio(Exception error)
    {
        CambiarEstado(EstadoCajero.Desconectado);
    }

    /// <summary>
    /// Interpreta una línea recibida del dispositivo.
    /// </summary>
    /// <param name="linea">Línea recibida (puede traer espacios o <c>\r</c>).</param>
    protected void ProcesarLinea(string? linea)
    {
        string texto = (linea ?? string.Empty).Trim();
        if (texto.Length == 0)
        {
            return;
        }

        string mayusculas = texto.ToUpperInvariant();
        if (mayusculas == ProtocoloCajero.Boton)
        {
            BotonPresionado?.Invoke();
        }
        else if (mayusculas.StartsWith(ProtocoloCajero.PrefijoRfid, StringComparison.Ordinal))
        {
            string uid = mayusculas.Substring(ProtocoloCajero.PrefijoRfid.Length).Trim();
            if (ProtocoloCajero.EsUidValido(uid))
            {
                TarjetaLeida?.Invoke(uid);
            }
            else
            {
                Interlocked.Increment(ref _lineasIgnoradas);
            }
        }
        else if (mayusculas == ProtocoloCajero.Pong)
        {
            AlRecibirPong();
        }
        else if (mayusculas.StartsWith(ProtocoloCajero.PrefijoError, StringComparison.Ordinal))
        {
            ErrorDispositivo?.Invoke(texto.Substring(ProtocoloCajero.PrefijoError.Length).Trim());
        }
        else if (mayusculas != ProtocoloCajero.Listo)
        {
            // Mensajes de arranque de MicroPython, eco de la consola, basura por ruido...
            Interlocked.Increment(ref _lineasIgnoradas);
        }
    }

    private void EnviarSiConectado(string linea)
    {
        if (_estado != EstadoCajero.Conectado)
        {
            return;
        }

        try
        {
            EnviarLinea(linea);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is TimeoutException || ex is UnauthorizedAccessException)
        {
            AlFallarEnvio(ex);
        }
    }
}
