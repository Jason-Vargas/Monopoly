using System;
using System.IO;
using System.Net.Sockets;

namespace Monopoly.Core.Red;

/// <summary>
/// Conexión del servidor con un cliente: el socket, sus flujos de lectura y escritura y el jugador
/// asociado. El servidor identifica al jugador por esta conexión, nunca por un id enviado por el cliente.
/// </summary>
internal sealed class ConexionCliente
{
    private readonly TcpClient _tcp;
    private readonly StreamReader _lector;
    private readonly StreamWriter _escritor;
    private readonly object _candadoEscritura = new object();
    private bool _cerrada;

    /// <summary>
    /// Envuelve un cliente TCP aceptado.
    /// </summary>
    /// <param name="tcp">Cliente aceptado por el <see cref="System.Net.Sockets.TcpListener"/>.</param>
    public ConexionCliente(TcpClient tcp)
    {
        _tcp = tcp;
        _tcp.NoDelay = true;
        NetworkStream flujo = tcp.GetStream();
        _lector = new StreamReader(flujo, Protocolo.Codificacion);
        _escritor = new StreamWriter(flujo, Protocolo.Codificacion) { NewLine = "\n", AutoFlush = true };
        Direccion = tcp.Client.RemoteEndPoint?.ToString() ?? "desconocida";
    }

    /// <summary>
    /// Dirección remota (IP:puerto), para los registros del servidor.
    /// </summary>
    public string Direccion { get; }

    /// <summary>
    /// Jugador asociado tras un <c>CONECTAR</c> exitoso, o <c>null</c> si aún no se unió.
    /// Solo lo modifica el servidor mientras procesa mensajes.
    /// </summary>
    public int? IdJugador { get; set; }

    /// <summary>
    /// Lee la siguiente línea (bloquea hasta recibirla).
    /// </summary>
    /// <returns>La línea, o <c>null</c> si el cliente cerró la conexión.</returns>
    public string? LeerLinea()
    {
        return _lector.ReadLine();
    }

    /// <summary>
    /// Envía una línea al cliente. Si la conexión falló, la cierra sin lanzar excepción
    /// (el hilo lector del servidor detectará la desconexión).
    /// </summary>
    /// <param name="linea">Línea del protocolo.</param>
    public void Enviar(string linea)
    {
        lock (_candadoEscritura)
        {
            if (_cerrada)
            {
                return;
            }

            try
            {
                _escritor.WriteLine(linea);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException)
            {
                CerrarSinCandado();
            }
        }
    }

    /// <summary>
    /// Cierra la conexión (se puede llamar varias veces).
    /// </summary>
    public void Cerrar()
    {
        lock (_candadoEscritura)
        {
            CerrarSinCandado();
        }
    }

    private void CerrarSinCandado()
    {
        if (_cerrada)
        {
            return;
        }

        _cerrada = true;
        _tcp.Close();
    }
}
