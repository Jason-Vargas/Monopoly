namespace Monopoly.Core.Hardware;

/// <summary>
/// Estado de un dispositivo cajero.
/// </summary>
public enum EstadoCajero
{
    /// <summary>No hay dispositivo físico: se juega con los botones de la interfaz.</summary>
    Simulado,

    /// <summary>Dispositivo físico sin conexión (nunca conectado, desconectado o cable retirado).</summary>
    Desconectado,

    /// <summary>Abriendo el puerto y esperando la respuesta del dispositivo.</summary>
    Conectando,

    /// <summary>Dispositivo físico conectado y respondiendo.</summary>
    Conectado,
}
