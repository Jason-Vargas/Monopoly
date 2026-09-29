using System;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;

namespace Monopoly.Tests.Modelo;

/// <summary>
/// Crea objetos del modelo con valores reproducibles para las pruebas.
/// </summary>
internal static class Fabrica
{
    public static readonly DateTime FechaFija = new DateTime(2026, 9, 29, 15, 30, 0);

    public static Juego CrearJuego(int semilla = 42)
    {
        return new Juego(new Dado(semilla), new Random(semilla), () => FechaFija);
    }

    public static Jugador CrearJugador(string nombre = "Ana", int id = 1)
    {
        return new Jugador(id, nombre, ColorFicha.Rojo);
    }

    public static Propiedad PropiedadEn(Tablero tablero, int indice)
    {
        return (Propiedad)tablero.ObtenerCasilla(indice);
    }
}
