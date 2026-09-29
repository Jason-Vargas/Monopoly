using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla "Casualidad": saca una carta del mazo de Casualidad.
/// </summary>
public class CasillaCasualidad : CasillaEvento
{
    /// <summary>
    /// Crea una casilla de Casualidad.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero.</param>
    public CasillaCasualidad(int id)
        : base(id, "Casualidad")
    {
    }

    /// <inheritdoc/>
    public override string Categoria => "Casualidad";

    /// <inheritdoc/>
    protected override MazoCartas ObtenerMazo(Juego juego)
    {
        return juego.MazoCasualidad;
    }
}
