using System;
using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla de Parada Libre: no ocurre nada.
/// </summary>
public class ParadaLibre : CasillaEspecial
{
    /// <summary>
    /// Crea la casilla de Parada Libre.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero (normalmente 20).</param>
    public ParadaLibre(int id)
        : base(id, "Parada Libre")
    {
    }

    /// <inheritdoc/>
    public override string Categoria => "Parada Libre";

    /// <inheritdoc/>
    public override ResultadoCasilla AlCaer(Jugador jugador, Juego juego)
    {
        ArgumentNullException.ThrowIfNull(jugador);
        return new ResultadoCasilla(this, AccionCasilla.Ninguna, $"{jugador.Nombre} descansa en la Parada Libre.");
    }
}
