using System;

namespace Monopoly.Core.Hardware;

/// <summary>
/// Módulo electrónico (cajero): botón del dado, lector de tarjetas y LED de pago.
/// El servidor funciona igual con cualquier implementación: la Pico W real (<see cref="CajeroPico"/>)
/// o el modo simulado (<see cref="CajeroSimulado"/>), en el que se juega con los botones de la interfaz.
/// </summary>
/// <remarks>
/// El dispositivo nunca decide nada: solo informa lo que ocurre (botón, tarjeta) y muestra lo que el
/// servidor le indica. Los dados los genera el servidor y la tarjeta solo identifica al jugador.
/// Los eventos pueden dispararse desde un hilo propio del dispositivo.
/// </remarks>
public interface IDispositivoCajero : IDisposable
{
    /// <summary>
    /// Se presionó el botón del dado.
    /// </summary>
    event Action? BotonPresionado;

    /// <summary>
    /// Se leyó una tarjeta (UID en hexadecimal, en mayúsculas).
    /// </summary>
    event Action<string>? TarjetaLeida;

    /// <summary>
    /// Cambió el estado de la conexión con el dispositivo.
    /// </summary>
    event Action<EstadoCajero>? EstadoCambiado;

    /// <summary>
    /// El dispositivo informó un problema propio (por ejemplo, lector RFID sin conexión).
    /// </summary>
    event Action<string>? ErrorDispositivo;

    /// <summary>
    /// Descripción para mostrar al usuario (por ejemplo "Pico W en COM5").
    /// </summary>
    string Descripcion { get; }

    /// <summary>
    /// Indica si es un dispositivo físico.
    /// </summary>
    bool EsFisico { get; }

    /// <summary>
    /// Estado actual de la conexión.
    /// </summary>
    EstadoCajero Estado { get; }

    /// <summary>
    /// Informa al dispositivo una tirada calculada por el servidor (la Pico confirma la recepción
    /// con un destello de su LED integrado).
    /// </summary>
    /// <param name="dado1">Primer dado (1 a 6).</param>
    /// <param name="dado2">Segundo dado (1 a 6).</param>
    void MostrarDados(int dado1, int dado2);

    /// <summary>
    /// Indica el resultado de un pago hecho con una tarjeta en el lector: aceptado (LED de pago
    /// encendido 2 s) o rechazado (3 parpadeos rápidos).
    /// </summary>
    /// <param name="aceptado"><c>true</c> si el pago se realizó.</param>
    void IndicarPago(bool aceptado);

    /// <summary>
    /// Apaga los indicadores del dispositivo (el LED de pago).
    /// </summary>
    void Limpiar();
}
