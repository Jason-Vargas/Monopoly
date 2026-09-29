using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla "Arca Comunal": saca una carta del mazo de Arca Comunal.
/// </summary>
public class CasillaArcaComunal : CasillaEvento
{
    /// <summary>
    /// Crea una casilla de Arca Comunal.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero.</param>
    public CasillaArcaComunal(int id)
        : base(id, "Arca Comunal")
    {
    }

    /// <inheritdoc/>
    public override string Categoria => "Arca Comunal";

    /// <inheritdoc/>
    protected override MazoCartas ObtenerMazo(Juego juego)
    {
        return juego.MazoArcaComunal;
    }
}
