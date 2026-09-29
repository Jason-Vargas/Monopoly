using System;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Describe lo que ocurre al caer en una casilla. Los efectos se expresan como datos
/// (montos, movimiento, turnos perdidos, propiedad en venta), de modo que la lógica del juego los aplica
/// de forma genérica sin conocer el tipo de casilla. Un campo en cero o nulo significa "sin ese efecto".
/// </summary>
public sealed class ResultadoCasilla
{
    /// <summary>
    /// Crea un resultado sin efectos adicionales.
    /// </summary>
    /// <param name="casilla">Casilla que lo produjo.</param>
    /// <param name="accion">Acción principal.</param>
    /// <param name="descripcion">Texto que describe lo ocurrido.</param>
    public ResultadoCasilla(Casilla casilla, AccionCasilla accion, string descripcion)
    {
        ArgumentNullException.ThrowIfNull(casilla);
        Casilla = casilla;
        Accion = accion;
        Descripcion = descripcion ?? string.Empty;
    }

    /// <summary>
    /// Casilla que produjo el resultado.
    /// </summary>
    public Casilla Casilla { get; }

    /// <summary>
    /// Acción principal (informativa).
    /// </summary>
    public AccionCasilla Accion { get; }

    /// <summary>
    /// Texto que describe lo ocurrido.
    /// </summary>
    public string Descripcion { get; }

    /// <summary>
    /// Propiedad que el jugador puede comprar, o <c>null</c> si no se ofrece ninguna.
    /// </summary>
    public Propiedad? PropiedadEnVenta { get; init; }

    /// <summary>
    /// Monto obligatorio que el jugador debe pagar.
    /// </summary>
    public int MontoAPagar { get; init; }

    /// <summary>
    /// Jugador que recibe <see cref="MontoAPagar"/>; <c>null</c> significa que lo recibe el banco.
    /// </summary>
    public Jugador? Acreedor { get; init; }

    /// <summary>
    /// Tipo de transacción del pago de <see cref="MontoAPagar"/>.
    /// </summary>
    public TipoTransaccion TipoPago { get; init; } = TipoTransaccion.PagoBanco;

    /// <summary>
    /// Monto que el banco paga al jugador.
    /// </summary>
    public int MontoARecibir { get; init; }

    /// <summary>
    /// Tipo de transacción del cobro de <see cref="MontoARecibir"/>.
    /// </summary>
    public TipoTransaccion TipoCobro { get; init; } = TipoTransaccion.GananciaEvento;

    /// <summary>
    /// Monto que el jugador debe pagar a cada uno de los demás jugadores activos.
    /// </summary>
    public int MontoACadaJugador { get; init; }

    /// <summary>
    /// Monto que cada uno de los demás jugadores activos debe pagar al jugador.
    /// </summary>
    public int MontoDeCadaJugador { get; init; }

    /// <summary>
    /// Casillas que debe moverse el jugador: positivo hacia adelante, negativo hacia atrás.
    /// </summary>
    public int Movimiento { get; init; }

    /// <summary>
    /// Índice de la casilla a la que debe ir el jugador, o <c>null</c> si no aplica.
    /// </summary>
    public int? DestinoIndice { get; init; }

    /// <summary>
    /// Si es <c>true</c>, el jugador va a <see cref="DestinoIndice"/> directamente, sin recorrer
    /// el tablero ni cobrar la Salida (como al ir a la cárcel). Si es <c>false</c>, avanza nodo a nodo.
    /// </summary>
    public bool MovimientoDirecto { get; init; }

    /// <summary>
    /// Cantidad de turnos que pierde el jugador.
    /// </summary>
    public int TurnosAPerder { get; init; }

    /// <summary>
    /// Carta que se sacó, o <c>null</c> si no se sacó ninguna.
    /// </summary>
    public CartaEvento? Carta { get; init; }

    /// <summary>
    /// Devuelve la descripción del resultado.
    /// </summary>
    /// <returns>La descripción.</returns>
    public override string ToString()
    {
        return Descripcion;
    }
}
