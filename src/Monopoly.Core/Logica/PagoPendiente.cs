using Monopoly.Core.Modelo;

namespace Monopoly.Core.Logica;

/// <summary>
/// Pago obligatorio que espera a que el deudor se identifique con su tarjeta.
/// </summary>
internal sealed class PagoPendiente
{
    /// <summary>
    /// Crea un pago pendiente.
    /// </summary>
    /// <param name="deudor">Jugador que debe pagar.</param>
    /// <param name="acreedor">Jugador que cobra, o <c>null</c> si cobra el banco.</param>
    /// <param name="monto">Monto a pagar (a cada jugador, si <paramref name="aCadaJugador"/> es verdadero).</param>
    /// <param name="tipo">Tipo de transacción.</param>
    /// <param name="descripcion">Motivo del pago.</param>
    /// <param name="aCadaJugador">Si el monto se paga a cada uno de los demás jugadores activos.</param>
    public PagoPendiente(Jugador deudor, Jugador? acreedor, int monto, TipoTransaccion tipo, string descripcion, bool aCadaJugador)
    {
        Deudor = deudor;
        Acreedor = acreedor;
        Monto = monto;
        Tipo = tipo;
        Descripcion = descripcion;
        ACadaJugador = aCadaJugador;
    }

    /// <summary>Jugador que debe pagar.</summary>
    public Jugador Deudor { get; }

    /// <summary>Jugador que cobra, o <c>null</c> si cobra el banco.</summary>
    public Jugador? Acreedor { get; }

    /// <summary>Monto a pagar (a cada jugador si <see cref="ACadaJugador"/>).</summary>
    public int Monto { get; }

    /// <summary>Tipo de transacción.</summary>
    public TipoTransaccion Tipo { get; }

    /// <summary>Motivo del pago.</summary>
    public string Descripcion { get; }

    /// <summary>Si el monto se paga a cada uno de los demás jugadores activos.</summary>
    public bool ACadaJugador { get; }
}
