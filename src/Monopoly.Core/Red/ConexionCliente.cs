using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Monopoly.Core.Red;

/// <summary>
/// Conexión del servidor con un cliente: el socket, sus flujos de lectura y escritura y el jugador
/// asociado. El servidor identifica al jugador por esta conexión, nunca por un id enviado por el cliente.
/// </summary>
internal sealed class ConexionCliente
{
    /// <summary>
    /// Longitud máxima de una línea recibida; un cliente que envíe más sin salto de línea se desconecta
    /// (evita que un cliente defectuoso o malicioso agote la memoria del servidor).
    /// </summary>
    public const int LongitudMaximaLinea = 8192;

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
        ConfiguracionSocket.Aplicar(tcp);
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
    /// <exception cref="IOException">Si la línea supera <see cref="LongitudMaximaLinea"/>.</exception>
    public string? LeerLinea()
    {
        StringBuilder linea = new StringBuilder();
        while (true)
        {
            int caracter = _lector.Read();
            if (caracter < 0)
            {
                return linea.Length > 0 ? linea.ToString() : null;
            }

            if (caracter == '\n')
            {
                return linea.ToString().TrimEnd('\r');
            }

            if (linea.Length >= LongitudMaximaLinea)
            {
                throw new IOException($"Línea de más de {LongitudMaximaLinea} caracteres.");
            }

            linea.Append((char)caracter);
        }
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
