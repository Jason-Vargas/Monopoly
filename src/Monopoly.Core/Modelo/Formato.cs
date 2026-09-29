using System.Globalization;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Utilidades de formato de textos compartidas por el modelo.
/// </summary>
public static class Formato
{
    /// <summary>
    /// Da formato a una cantidad de dinero, por ejemplo <c>$1,500</c>.
    /// </summary>
    /// <param name="monto">Monto a mostrar.</param>
    /// <returns>El monto con símbolo y separador de miles.</returns>
    public static string Dinero(int monto)
    {
        return "$" + monto.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Devuelve el nombre legible de un tipo de transacción.
    /// </summary>
    /// <param name="tipo">Tipo de transacción.</param>
    /// <returns>El nombre en español.</returns>
    public static string Nombre(TipoTransaccion tipo)
    {
        return tipo switch
        {
            TipoTransaccion.CompraPropiedad => "Compra de propiedad",
            TipoTransaccion.PagoAlquiler => "Pago de alquiler",
            TipoTransaccion.PagoBanco => "Pago al banco",
            TipoTransaccion.PagoEntreJugadores => "Pago entre jugadores",
            TipoTransaccion.GananciaEvento => "Ganancia por evento",
            TipoTransaccion.PerdidaEvento => "Pérdida por evento",
            TipoTransaccion.PremioInicio => "Premio por pasar por inicio",
            _ => tipo.ToString(),
        };
    }
}
