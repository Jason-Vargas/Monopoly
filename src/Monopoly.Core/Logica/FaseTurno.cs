namespace Monopoly.Core.Logica;

/// <summary>
/// Fase del turno del jugador actual: indica qué acción espera el banco.
/// </summary>
public enum FaseTurno
{
    /// <summary>El jugador debe lanzar los dados.</summary>
    EsperandoDados,

    /// <summary>El jugador cayó en una propiedad libre y debe decidir si la compra.</summary>
    EsperandoDecisionCompra,

    /// <summary>El jugador eligió comprar: se espera su tarjeta en el lector (o que cancele con "No comprar").</summary>
    EsperandoTarjetaCompra,

    /// <summary>El jugador tiene un pago obligatorio y debe identificarse con su tarjeta.</summary>
    EsperandoPago,

    /// <summary>No hay nada pendiente: el jugador puede terminar su turno.</summary>
    PuedeTerminar,
}
