using System;
using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla que obliga a sacar una carta de evento. Cada variante (<see cref="CasillaCasualidad"/> y
/// <see cref="CasillaArcaComunal"/>) indica de qué mazo se saca.
/// </summary>
public abstract class CasillaEvento : Casilla
{
    /// <summary>
    /// Inicializa la casilla de evento.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero.</param>
    /// <param name="nombre">Nombre visible.</param>
    protected CasillaEvento(int id, string nombre)
        : base(id, nombre)
    {
    }

    /// <summary>
    /// Obtiene el mazo del que saca carta esta casilla.
    /// </summary>
    /// <param name="juego">Partida en curso.</param>
    /// <returns>El mazo correspondiente.</returns>
    protected abstract MazoCartas ObtenerMazo(Juego juego);

    /// <summary>
    /// Saca la carta del frente del mazo (que vuelve al final) y devuelve su efecto.
    /// </summary>
    /// <param name="jugador">Jugador que cae en la casilla.</param>
    /// <param name="juego">Partida en curso.</param>
    /// <returns>El resultado con el efecto de la carta.</returns>
    public override ResultadoCasilla AlCaer(Jugador jugador, Juego juego)
    {
        ArgumentNullException.ThrowIfNull(jugador);
        ArgumentNullException.ThrowIfNull(juego);
        CartaEvento carta = ObtenerMazo(juego).Sacar();
        return carta.CrearResultado(this);
    }
}
