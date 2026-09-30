using System;
using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla "Vaya a la Cárcel": envía al jugador directamente a la cárcel, sin pasar por la Salida,
/// y le hace perder un turno.
/// </summary>
public class VayaALaCarcel : CasillaEspecial
{
    /// <summary>
    /// Turnos que pierde el jugador enviado a la cárcel.
    /// </summary>
    public const int TurnosPerdidos = 1;

    /// <summary>
    /// Crea la casilla "Vaya a la Cárcel".
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero (normalmente 30).</param>
    public VayaALaCarcel(int id)
        : base(id, "Vaya a la Cárcel")
    {
    }

    /// <inheritdoc/>
    public override string Categoria => "Vaya a la Cárcel";

    /// <inheritdoc/>
    public override string Detalle => "Y pierda un turno";

    /// <inheritdoc/>
    public override ResultadoCasilla AlCaer(Jugador jugador, Juego juego)
    {
        ArgumentNullException.ThrowIfNull(jugador);
        return new ResultadoCasilla(this, AccionCasilla.IrALaCarcel,
            $"{jugador.Nombre} va directo a la cárcel y pierde {TurnosPerdidos} turno.")
        {
            DestinoIndice = Tablero.IndiceCarcel,
            MovimientoDirecto = true,
            TurnosAPerder = TurnosPerdidos,
        };
    }
}
