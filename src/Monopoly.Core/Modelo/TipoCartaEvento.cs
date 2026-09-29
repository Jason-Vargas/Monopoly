namespace Monopoly.Core.Modelo;

/// <summary>
/// Tipo de efecto de una <see cref="CartaEvento"/>.
/// </summary>
public enum TipoCartaEvento
{
    /// <summary>El banco paga <c>Valor</c> al jugador.</summary>
    RecibirDinero,

    /// <summary>El jugador paga <c>Valor</c> al banco.</summary>
    PagarDinero,

    /// <summary>El jugador avanza <c>Valor</c> casillas.</summary>
    Avanzar,

    /// <summary>El jugador retrocede <c>Valor</c> casillas.</summary>
    Retroceder,

    /// <summary>El jugador pierde <c>Valor</c> turnos.</summary>
    PerderTurno,

    /// <summary>El jugador avanza hasta la casilla de índice <c>Valor</c>, cobrando la Salida si pasa por ella.</summary>
    IrACasilla,

    /// <summary>El jugador paga <c>Valor</c> a cada uno de los demás jugadores activos.</summary>
    PagarACadaJugador,

    /// <summary>Cada uno de los demás jugadores activos paga <c>Valor</c> al jugador.</summary>
    CobrarACadaJugador,
}
