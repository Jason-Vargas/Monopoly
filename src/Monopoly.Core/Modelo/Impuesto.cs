using System;
using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla de impuesto (Impuesto sobre la Renta o Impuesto de Lujo): se paga un monto fijo al banco.
/// </summary>
public class Impuesto : CasillaEspecial
{
    /// <summary>
    /// Crea una casilla de impuesto.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero.</param>
    /// <param name="nombre">Nombre del impuesto.</param>
    /// <param name="monto">Monto a pagar (mayor que 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si el monto no es positivo.</exception>
    public Impuesto(int id, string nombre, int monto)
        : base(id, nombre)
    {
        if (monto <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto debe ser mayor que 0.");
        }

        Monto = monto;
    }

    /// <summary>
    /// Monto que se paga al banco.
    /// </summary>
    public int Monto { get; }

    /// <inheritdoc/>
    public override string Categoria => "Impuesto";

    /// <inheritdoc/>
    public override string Detalle => "Pague " + Formato.Dinero(Monto);

    /// <inheritdoc/>
    public override ResultadoCasilla AlCaer(Jugador jugador, Juego juego)
    {
        ArgumentNullException.ThrowIfNull(jugador);
        return new ResultadoCasilla(this, AccionCasilla.PagarImpuesto,
            $"{jugador.Nombre} debe pagar {Formato.Dinero(Monto)} de {Nombre} al banco.")
        {
            MontoAPagar = Monto,
            TipoPago = TipoTransaccion.PagoBanco,
        };
    }
}
