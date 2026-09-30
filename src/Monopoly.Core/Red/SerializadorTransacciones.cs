using System;
using System.Globalization;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Red;

/// <summary>
/// Convierte una lista de transacciones en el mensaje <c>TRANSACCIONES</c> y viceversa.
/// </summary>
/// <remarks>
/// Campos: 0 filtro · 1 cantidad (N); luego N bloques de 8 campos: id · fecha y hora (ISO 8601,
/// <c>yyyy-MM-ddTHH:mm:ss</c>) · turno · tipo (nombre de <see cref="TipoTransaccion"/>) · origen · destino ·
/// monto · descripción.
/// </remarks>
public static class SerializadorTransacciones
{
    /// <summary>
    /// Campos por transacción.
    /// </summary>
    public const int CamposPorTransaccion = 8;

    private const string FormatoFecha = "yyyy-MM-ddTHH:mm:ss";

    /// <summary>
    /// Codifica las transacciones como línea <c>TRANSACCIONES|...</c>.
    /// </summary>
    /// <param name="filtro">Descripción del filtro aplicado (se devuelve tal cual al cliente).</param>
    /// <param name="transacciones">Transacciones en el orden en que se deben mostrar.</param>
    /// <returns>La línea del protocolo.</returns>
    public static string Codificar(string filtro, ListaSimple<Transaccion> transacciones)
    {
        ArgumentNullException.ThrowIfNull(transacciones);
        string[] campos = new string[2 + (transacciones.Cantidad * CamposPorTransaccion)];
        campos[0] = filtro;
        campos[1] = transacciones.Cantidad.ToString(CultureInfo.InvariantCulture);
        int b = 2;
        transacciones.Recorrer(t =>
        {
            campos[b] = t.Id.ToString(CultureInfo.InvariantCulture);
            campos[b + 1] = t.FechaHora.ToString(FormatoFecha, CultureInfo.InvariantCulture);
            campos[b + 2] = t.NumeroTurno.ToString(CultureInfo.InvariantCulture);
            campos[b + 3] = t.Tipo.ToString();
            campos[b + 4] = t.Origen;
            campos[b + 5] = t.Destino;
            campos[b + 6] = t.Monto.ToString(CultureInfo.InvariantCulture);
            campos[b + 7] = t.Descripcion;
            b += CamposPorTransaccion;
        });

        return Protocolo.Codificar(Protocolo.Transacciones, campos);
    }

    /// <summary>
    /// Decodifica un mensaje <c>TRANSACCIONES</c>.
    /// </summary>
    /// <param name="mensaje">Mensaje recibido.</param>
    /// <returns>Las transacciones en el orden recibido.</returns>
    /// <exception cref="FormatException">Si el mensaje no es válido.</exception>
    public static ListaSimple<Transaccion> Decodificar(Mensaje mensaje)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        int cantidad = mensaje.Entero(1);
        if (cantidad < 0 || mensaje.CantidadCampos != 2 + (cantidad * CamposPorTransaccion))
        {
            throw new FormatException("La cantidad de campos de TRANSACCIONES no coincide.");
        }

        ListaSimple<Transaccion> transacciones = new ListaSimple<Transaccion>();
        for (int i = 0; i < cantidad; i++)
        {
            int b = 2 + (i * CamposPorTransaccion);
            DateTime fecha = DateTime.ParseExact(mensaje.Campo(b + 1), FormatoFecha, CultureInfo.InvariantCulture);
            transacciones.AgregarAlFinal(new Transaccion(
                mensaje.Entero(b),
                fecha,
                mensaje.Entero(b + 2),
                mensaje.Enumeracion<TipoTransaccion>(b + 3),
                mensaje.Campo(b + 4),
                mensaje.Campo(b + 5),
                mensaje.Entero(b + 6),
                mensaje.Campo(b + 7)));
        }

        return transacciones;
    }
}
