using System;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Red;

/// <summary>
/// Estado completo que el servidor envía en el mensaje <c>ESTADO</c>: la instantánea del juego,
/// los dueños de las propiedades y las casillas recorridas en el último movimiento (para animarlo).
/// </summary>
public sealed class EstadoRed
{
    /// <summary>
    /// Crea el estado calculando los dueños a partir de las propiedades de cada jugador.
    /// </summary>
    /// <param name="instantanea">Instantánea del juego.</param>
    /// <param name="idJugadorUltimoMovimiento">Jugador que se movió por última vez, o <c>null</c>.</param>
    /// <param name="casillasRecorridas">Casillas recorridas en ese movimiento, en orden.</param>
    public EstadoRed(InstantaneaJuego instantanea, int? idJugadorUltimoMovimiento, int[] casillasRecorridas)
        : this(instantanea, idJugadorUltimoMovimiento, casillasRecorridas, CalcularPropietarios(instantanea))
    {
    }

    /// <summary>
    /// Crea el estado con los dueños indicados (lo usa el decodificador).
    /// </summary>
    /// <param name="instantanea">Instantánea del juego.</param>
    /// <param name="idJugadorUltimoMovimiento">Jugador que se movió por última vez, o <c>null</c>.</param>
    /// <param name="casillasRecorridas">Casillas recorridas en ese movimiento, en orden.</param>
    /// <param name="propietarios">Id del dueño por índice de casilla (0 = sin dueño).</param>
    public EstadoRed(InstantaneaJuego instantanea, int? idJugadorUltimoMovimiento, int[] casillasRecorridas, int[] propietarios)
    {
        ArgumentNullException.ThrowIfNull(instantanea);
        Instantanea = instantanea;
        IdJugadorUltimoMovimiento = idJugadorUltimoMovimiento;
        CasillasRecorridas = casillasRecorridas ?? new int[0];
        Propietarios = propietarios ?? new int[Tablero.CantidadCasillas];
    }

    /// <summary>
    /// Instantánea del juego (turno, fase, jugadores, pago pendiente, últimos dados...).
    /// </summary>
    public InstantaneaJuego Instantanea { get; }

    /// <summary>
    /// Jugador que se movió por última vez, o <c>null</c> si nadie se ha movido.
    /// </summary>
    public int? IdJugadorUltimoMovimiento { get; }

    /// <summary>
    /// Casillas recorridas en el último movimiento, en orden (incluye los movimientos por cartas).
    /// </summary>
    public int[] CasillasRecorridas { get; }

    /// <summary>
    /// Id del dueño de cada casilla, por índice (0 = sin dueño o no es propiedad).
    /// </summary>
    public int[] Propietarios { get; }

    /// <summary>
    /// Devuelve el dueño de una casilla.
    /// </summary>
    /// <param name="casilla">Índice de la casilla.</param>
    /// <returns>El id del dueño, o <c>null</c> si no tiene.</returns>
    public int? PropietarioDe(int casilla)
    {
        if (casilla < 0 || casilla >= Propietarios.Length || Propietarios[casilla] == 0)
        {
            return null;
        }

        return Propietarios[casilla];
    }

    /// <summary>
    /// Busca el estado de un jugador por id.
    /// </summary>
    /// <param name="idJugador">Id del jugador.</param>
    /// <returns>El estado del jugador, o <c>null</c> si no existe.</returns>
    public EstadoJugador? BuscarJugador(int idJugador)
    {
        foreach (EstadoJugador jugador in Instantanea.Jugadores)
        {
            if (jugador.Id == idJugador)
            {
                return jugador;
            }
        }

        return null;
    }

    private static int[] CalcularPropietarios(InstantaneaJuego instantanea)
    {
        ArgumentNullException.ThrowIfNull(instantanea);
        int[] propietarios = new int[Tablero.CantidadCasillas];
        foreach (EstadoJugador jugador in instantanea.Jugadores)
        {
            foreach (int casilla in jugador.IdsPropiedades)
            {
                propietarios[casilla] = jugador.Id;
            }
        }

        return propietarios;
    }
}
