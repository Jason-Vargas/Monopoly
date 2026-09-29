namespace Monopoly.Core.Modelo;

/// <summary>
/// Tipos de transacción económica que registra el banco.
/// </summary>
public enum TipoTransaccion
{
    /// <summary>Un jugador compra una propiedad al banco.</summary>
    CompraPropiedad,

    /// <summary>Un jugador paga alquiler al dueño de una propiedad.</summary>
    PagoAlquiler,

    /// <summary>Un jugador paga al banco (por ejemplo, impuestos).</summary>
    PagoBanco,

    /// <summary>Un jugador paga a otro fuera del alquiler (por ejemplo, por una carta).</summary>
    PagoEntreJugadores,

    /// <summary>Un jugador recibe dinero del banco por una carta de evento.</summary>
    GananciaEvento,

    /// <summary>Un jugador paga dinero al banco por una carta de evento.</summary>
    PerdidaEvento,

    /// <summary>El banco paga el premio por pasar por la Salida.</summary>
    PremioInicio,
}
