using System.Net.Sockets;

namespace Monopoly.Core.Red;

/// <summary>
/// Opciones comunes de los sockets del servidor y del cliente.
/// </summary>
internal static class ConfiguracionSocket
{
    /// <summary>
    /// Tiempo máximo (ms) de un envío. Si un cliente deja de leer (programa congelado), el envío
    /// falla en lugar de bloquear al servidor, y se cierra esa conexión.
    /// </summary>
    public const int TiempoMaximoEnvioMs = 5000;

    /// <summary>
    /// Segundos sin tráfico antes de enviar sondas keepalive.
    /// </summary>
    public const int KeepAliveInicioSegundos = 10;

    /// <summary>
    /// Segundos entre sondas keepalive.
    /// </summary>
    public const int KeepAliveIntervaloSegundos = 3;

    /// <summary>
    /// Sondas sin respuesta antes de dar la conexión por perdida (~20 s en total).
    /// </summary>
    public const int KeepAliveReintentos = 3;

    /// <summary>
    /// Configura el socket: sin retardo de Nagle, tiempo máximo de envío y keepalive TCP, para detectar
    /// computadoras que se apagan o pierden la red sin cerrar la conexión.
    /// </summary>
    /// <param name="tcp">Cliente TCP a configurar.</param>
    public static void Aplicar(TcpClient tcp)
    {
        tcp.NoDelay = true;
        tcp.SendTimeout = TiempoMaximoEnvioMs;
        Socket socket = tcp.Client;
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, KeepAliveInicioSegundos);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, KeepAliveIntervaloSegundos);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, KeepAliveReintentos);
        }
        catch (SocketException)
        {
            // Sistemas que no admiten ajustar el keepalive: se usa el valor por defecto.
        }
    }
}
