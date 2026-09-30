using System;

namespace Monopoly.Core.Hardware;

/// <summary>
/// Modo simulado (sin placa): los jugadores tiran los dados y pagan con los botones de la interfaz
/// (UID virtual). Mostrar dados no hace nada. Permite simular el botón y las tarjetas para depurar.
/// </summary>
public sealed class CajeroSimulado : IDispositivoCajero
{
    /// <inheritdoc/>
    public event Action? BotonPresionado;

    /// <inheritdoc/>
    public event Action<string>? TarjetaLeida;

    /// <inheritdoc/>
    /// <remarks>El modo simulado no cambia de estado.</remarks>
    public event Action<EstadoCajero>? EstadoCambiado
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    /// <remarks>El modo simulado no produce errores de dispositivo.</remarks>
    public event Action<string>? ErrorDispositivo
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public string Descripcion => "Modo simulado (sin cajero físico)";

    /// <inheritdoc/>
    public bool EsFisico => false;

    /// <inheritdoc/>
    public EstadoCajero Estado => EstadoCajero.Simulado;

    /// <inheritdoc/>
    public void MostrarDados(int dado1, int dado2)
    {
    }

    /// <inheritdoc/>
    public void LimpiarDisplays()
    {
    }

    /// <summary>
    /// Simula la pulsación del botón del dado.
    /// </summary>
    public void SimularBoton()
    {
        BotonPresionado?.Invoke();
    }

    /// <summary>
    /// Simula la lectura de una tarjeta.
    /// </summary>
    /// <param name="uid">UID a informar.</param>
    public void SimularTarjeta(string uid)
    {
        TarjetaLeida?.Invoke(uid);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
