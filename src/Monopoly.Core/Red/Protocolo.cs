using System;
using System.Text;
using Monopoly.Core.Estructuras;

namespace Monopoly.Core.Red;

/// <summary>
/// Protocolo de texto del juego: un mensaje por línea en UTF-8 con el formato
/// <c>COMANDO|campo1|campo2...</c>. Dentro de los campos se escapan la barra invertida (<c>\\</c>),
/// el separador (<c>\|</c>) y los saltos de línea (<c>\n</c>, <c>\r</c>). Ver docs/protocolo.md.
/// </summary>
public static class Protocolo
{
    /// <summary>
    /// Separador de campos.
    /// </summary>
    public const char Separador = '|';

    /// <summary>
    /// Carácter de escape.
    /// </summary>
    public const char Escape = '\\';

    /// <summary>
    /// Codificación del texto en la red (UTF-8 sin BOM).
    /// </summary>
    public static readonly Encoding Codificacion = new UTF8Encoding(false);

    // Solicitudes del cliente.

    /// <summary>Cliente → servidor: <c>CONECTAR|nombre</c>.</summary>
    public const string Conectar = "CONECTAR";

    /// <summary>Cliente → servidor: <c>INICIAR_PARTIDA</c> (solo el organizador).</summary>
    public const string IniciarPartida = "INICIAR_PARTIDA";

    /// <summary>Cliente → servidor: <c>TIRAR_DADOS</c>.</summary>
    public const string TirarDados = "TIRAR_DADOS";

    /// <summary>Cliente → servidor: <c>COMPRAR_PROPIEDAD</c>.</summary>
    public const string ComprarPropiedad = "COMPRAR_PROPIEDAD";

    /// <summary>Cliente → servidor: <c>NO_COMPRAR</c>.</summary>
    public const string NoComprar = "NO_COMPRAR";

    /// <summary>Cliente → servidor: <c>PAGAR_CON_TARJETA</c> (modo simulado: UID virtual del propio jugador).</summary>
    public const string PagarConTarjeta = "PAGAR_CON_TARJETA";

    /// <summary>Cliente → servidor: <c>TERMINAR_TURNO</c>.</summary>
    public const string TerminarTurno = "TERMINAR_TURNO";

    /// <summary>Cliente → servidor: <c>CONSULTAR_ESTADO</c>.</summary>
    public const string ConsultarEstado = "CONSULTAR_ESTADO";

    /// <summary>Cliente → servidor: <c>CONSULTAR_TRANSACCIONES|filtro[|valor]</c>.</summary>
    public const string ConsultarTransacciones = "CONSULTAR_TRANSACCIONES";

    /// <summary>Cliente → servidor: <c>EXPORTAR_TRANSACCIONES</c>.</summary>
    public const string ExportarTransacciones = "EXPORTAR_TRANSACCIONES";

    /// <summary>Cliente → servidor: <c>DESCONECTAR</c>.</summary>
    public const string Desconectar = "DESCONECTAR";

    /// <summary>Cliente → servidor: <c>RETIRAR_JUGADOR|idJugador</c> (solo el organizador, solo jugadores desconectados).</summary>
    public const string RetirarJugador = "RETIRAR_JUGADOR";

    /// <summary>
    /// Cliente → servidor: <c>VINCULAR_TARJETA|idJugador</c> (solo el organizador, con el cajero físico conectado):
    /// la próxima tarjeta que lea el cajero queda vinculada a ese jugador. <c>0</c> cancela.
    /// </summary>
    public const string VincularTarjeta = "VINCULAR_TARJETA";

    // Mensajes del servidor.

    /// <summary>Servidor → cliente: <c>BIENVENIDA|idJugador|nombre</c>.</summary>
    public const string Bienvenida = "BIENVENIDA";

    /// <summary>Servidor → cliente: <c>ERROR|mensaje</c>.</summary>
    public const string Error = "ERROR";

    /// <summary>Servidor → clientes: <c>ESTADO|...</c> (ver <see cref="SerializadorEstado"/>).</summary>
    public const string Estado = "ESTADO";

    /// <summary>Servidor → clientes: <c>DADOS|idJugador|dado1|dado2</c>.</summary>
    public const string Dados = "DADOS";

    /// <summary>Servidor → clientes: <c>EVENTO|texto</c>.</summary>
    public const string Evento = "EVENTO";

    /// <summary>Servidor → cliente: <c>TRANSACCIONES|filtro|cantidad|...</c> (ver <see cref="SerializadorTransacciones"/>).</summary>
    public const string Transacciones = "TRANSACCIONES";

    /// <summary>Servidor → clientes: <c>FIN|ganador|resumen</c>.</summary>
    public const string Fin = "FIN";

    /// <summary>Servidor → clientes: <c>SERVIDOR_CERRADO|motivo</c>, justo antes de cerrar todas las conexiones.</summary>
    public const string ServidorCerrado = "SERVIDOR_CERRADO";

    /// <summary>
    /// Escapa un campo para que no contenga separadores ni saltos de línea sin escapar.
    /// </summary>
    /// <param name="texto">Texto original.</param>
    /// <returns>El texto escapado.</returns>
    public static string EscaparCampo(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        StringBuilder resultado = new StringBuilder(texto.Length);
        foreach (char caracter in texto)
        {
            switch (caracter)
            {
                case Escape:
                    resultado.Append(Escape).Append(Escape);
                    break;
                case Separador:
                    resultado.Append(Escape).Append(Separador);
                    break;
                case '\n':
                    resultado.Append(Escape).Append('n');
                    break;
                case '\r':
                    resultado.Append(Escape).Append('r');
                    break;
                default:
                    resultado.Append(caracter);
                    break;
            }
        }

        return resultado.ToString();
    }

    /// <summary>
    /// Arma una línea del protocolo (sin el salto de línea final).
    /// </summary>
    /// <param name="comando">Comando en mayúsculas.</param>
    /// <param name="campos">Campos, que se escapan uno por uno.</param>
    /// <returns>La línea codificada.</returns>
    public static string Codificar(string comando, params string?[] campos)
    {
        if (string.IsNullOrWhiteSpace(comando) || comando.IndexOf(Separador) >= 0)
        {
            throw new ArgumentException("Comando inválido.", nameof(comando));
        }

        StringBuilder linea = new StringBuilder(comando);
        foreach (string? campo in campos)
        {
            linea.Append(Separador).Append(EscaparCampo(campo));
        }

        return linea.ToString();
    }

    /// <summary>
    /// Interpreta una línea recibida: separa el comando y los campos y deshace los escapes.
    /// </summary>
    /// <param name="linea">Línea recibida (sin salto de línea).</param>
    /// <returns>El mensaje.</returns>
    /// <exception cref="FormatException">Si la línea está vacía o no tiene comando.</exception>
    public static Mensaje Decodificar(string linea)
    {
        if (string.IsNullOrWhiteSpace(linea))
        {
            throw new FormatException("La línea está vacía.");
        }

        ListaSimple<string> partes = new ListaSimple<string>();
        StringBuilder actual = new StringBuilder();
        for (int i = 0; i < linea.Length; i++)
        {
            char caracter = linea[i];
            if (caracter == Escape && i + 1 < linea.Length)
            {
                char siguiente = linea[++i];
                actual.Append(siguiente switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    _ => siguiente,
                });
            }
            else if (caracter == Separador)
            {
                partes.AgregarAlFinal(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(caracter);
            }
        }

        partes.AgregarAlFinal(actual.ToString());

        string comando = partes.Obtener(0).Trim().ToUpperInvariant();
        if (comando.Length == 0)
        {
            throw new FormatException("El mensaje no tiene comando.");
        }

        string[] campos = new string[partes.Cantidad - 1];
        for (int i = 1; i < partes.Cantidad; i++)
        {
            campos[i - 1] = partes.Obtener(i);
        }

        return new Mensaje(comando, campos);
    }
}
