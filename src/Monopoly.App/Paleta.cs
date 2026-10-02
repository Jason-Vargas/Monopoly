using System.Drawing;
using Monopoly.App.Estilo;
using Monopoly.Core.Modelo;

namespace Monopoly.App;

/// <summary>
/// Colores y fuentes de la estética clásica del tablero.
/// </summary>
internal static class Paleta
{
    /// <summary>Verde claro del tablero clásico.</summary>
    public static readonly Color FondoTablero = Tema.VerdeMenta;

    /// <summary>Color de la mesa alrededor del tablero.</summary>
    public static readonly Color Mesa = Color.FromArgb(34, 76, 52);

    /// <summary>Color de las líneas del tablero.</summary>
    public static readonly Color Linea = Color.FromArgb(25, 25, 25);

    /// <summary>Rojo oscuro de los títulos.</summary>
    public static readonly Color RojoTitulo = Color.FromArgb(180, 20, 30);

    /// <summary>Resaltado del jugador en turno y de la propiedad en venta.</summary>
    public static readonly Color Resaltado = Color.FromArgb(255, 196, 0);

    /// <summary>Fondo de los paneles laterales.</summary>
    public static readonly Color FondoPanel = Color.FromArgb(244, 241, 230);

    /// <summary>Nombre de la fuente de la interfaz.</summary>
    public const string Fuente = "Segoe UI";

    /// <summary>
    /// Color de la franja de un grupo de propiedades (del sistema de estilo, <see cref="Tema"/>).
    /// </summary>
    public static Color ColorGrupo(GrupoPropiedad grupo) => Tema.ColorGrupo(grupo);

    /// <summary>
    /// Indica si el grupo lleva franja de color en la casilla.
    /// </summary>
    public static bool TieneFranja(GrupoPropiedad grupo)
    {
        return grupo != GrupoPropiedad.Ferrocarril && grupo != GrupoPropiedad.Servicio;
    }

    /// <summary>
    /// Color de la ficha de un jugador (del sistema de estilo, <see cref="Tema"/>).
    /// </summary>
    public static Color ColorFicha(ColorFicha color) => Tema.ColorFicha(color);

    /// <summary>
    /// Acorta los nombres largos para que quepan en las casillas.
    /// </summary>
    public static string NombreCorto(string nombre)
    {
        return nombre.Replace("Avenida ", "Av. ").Replace("Ferrocarril ", "F.C. ").Replace("Compañía de ", "Cía. de ");
    }
}
