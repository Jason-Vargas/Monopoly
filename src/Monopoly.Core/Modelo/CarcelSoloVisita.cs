using System;
using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla de la cárcel. Caer en ella es solo una visita; los enviados por
/// <see cref="VayaALaCarcel"/> terminan aquí y pierden un turno.
/// </summary>
public class CarcelSoloVisita : CasillaEspecial
{
    /// <summary>
    /// Crea la casilla de la cárcel.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero (normalmente 10).</param>
    public CarcelSoloVisita(int id)
        : base(id, "Cárcel / Solo de visita")
    {
    }

    /// <inheritdoc/>
    public override string Categoria => "Cárcel";

    /// <inheritdoc/>
    public override string Detalle => "Solo de visita";

    /// <inheritdoc/>
    public override ResultadoCasilla AlCaer(Jugador jugador, Juego juego)
    {
        ArgumentNullException.ThrowIfNull(jugador);
        return new ResultadoCasilla(this, AccionCasilla.Ninguna, $"{jugador.Nombre} está de visita en la cárcel.");
    }
}
