using System;
using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Clase base de todas las casillas del tablero. Cada tipo de casilla define su propio
/// comportamiento al caer en ella sobrescribiendo <see cref="AlCaer"/> (polimorfismo), de modo que
/// la lógica del juego nunca necesita preguntar por el tipo concreto de la casilla.
/// </summary>
public abstract class Casilla
{
    /// <summary>
    /// Inicializa los datos comunes de la casilla.
    /// </summary>
    /// <param name="id">Identificador; coincide con la posición en el tablero (0 = Salida).</param>
    /// <param name="nombre">Nombre visible.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si el id es negativo.</exception>
    /// <exception cref="ArgumentException">Si el nombre está vacío.</exception>
    protected Casilla(int id, string nombre)
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El identificador no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(nombre));
        }

        Id = id;
        Nombre = nombre;
    }

    /// <summary>
    /// Identificador de la casilla; coincide con su posición en el tablero.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Nombre visible de la casilla.
    /// </summary>
    public string Nombre { get; }

    /// <summary>
    /// Categoría legible de la casilla (por ejemplo "Propiedad" o "Casualidad"), para la interfaz y los mensajes.
    /// </summary>
    public abstract string Categoria { get; }

    /// <summary>
    /// Texto secundario que se muestra en el tablero (precio, monto del impuesto, indicación...).
    /// Cada tipo de casilla lo define; por defecto está vacío.
    /// </summary>
    public virtual string Detalle => string.Empty;

    /// <summary>
    /// Determina lo que ocurre cuando un jugador cae en la casilla. No modifica saldos, posiciones
    /// ni propietarios: describe los efectos en el resultado para que el banco los valide y aplique.
    /// </summary>
    /// <param name="jugador">Jugador que cae en la casilla.</param>
    /// <param name="juego">Partida en curso (da acceso al tablero y a los mazos).</param>
    /// <returns>Lo ocurrido y la acción requerida.</returns>
    public abstract ResultadoCasilla AlCaer(Jugador jugador, Juego juego);

    /// <summary>
    /// Devuelve el nombre de la casilla.
    /// </summary>
    /// <returns>El nombre.</returns>
    public override string ToString()
    {
        return Nombre;
    }
}
