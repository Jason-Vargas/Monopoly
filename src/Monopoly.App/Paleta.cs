using System.Drawing;
using Monopoly.Core.Modelo;

namespace Monopoly.App;

/// <summary>
/// Colores y fuentes de la estética clásica del tablero.
/// </summary>
internal static class Paleta
{
    /// <summary>Verde claro del tablero clásico.</summary>
    public static readonly Color FondoTablero = Color.FromArgb(206, 230, 208);

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
    /// Color de la franja de un grupo de propiedades (vacío para ferrocarriles y servicios).
    /// </summary>
    public static Color ColorGrupo(GrupoPropiedad grupo)
    {
        return grupo switch
        {
            GrupoPropiedad.Marron => Color.FromArgb(149, 84, 54),
            GrupoPropiedad.Celeste => Color.FromArgb(170, 224, 250),
            GrupoPropiedad.Rosa => Color.FromArgb(217, 58, 150),
            GrupoPropiedad.Naranja => Color.FromArgb(247, 148, 29),
            GrupoPropiedad.Rojo => Color.FromArgb(237, 27, 36),
            GrupoPropiedad.Amarillo => Color.FromArgb(254, 242, 0),
            GrupoPropiedad.Verde => Color.FromArgb(31, 178, 90),
            GrupoPropiedad.AzulOscuro => Color.FromArgb(0, 114, 187),
            GrupoPropiedad.Ferrocarril => Color.FromArgb(60, 60, 60),
            _ => Color.FromArgb(150, 150, 150),
        };
    }

    /// <summary>
    /// Indica si el grupo lleva franja de color en la casilla.
    /// </summary>
    public static bool TieneFranja(GrupoPropiedad grupo)
    {
        return grupo != GrupoPropiedad.Ferrocarril && grupo != GrupoPropiedad.Servicio;
    }

    /// <summary>
    /// Color de la ficha de un jugador.
    /// </summary>
    public static Color ColorFicha(ColorFicha color)
    {
        return color switch
        {
            Core.Modelo.ColorFicha.Rojo => Color.FromArgb(214, 40, 40),
            Core.Modelo.ColorFicha.Azul => Color.FromArgb(30, 96, 214),
            Core.Modelo.ColorFicha.Verde => Color.FromArgb(22, 150, 64),
            Core.Modelo.ColorFicha.Amarillo => Color.FromArgb(238, 190, 0),
            Core.Modelo.ColorFicha.Morado => Color.FromArgb(128, 60, 170),
            _ => Color.FromArgb(240, 120, 20),
        };
    }

    /// <summary>
    /// Acorta los nombres largos para que quepan en las casillas.
    /// </summary>
    public static string NombreCorto(string nombre)
    {
        return nombre.Replace("Avenida ", "Av. ").Replace("Ferrocarril ", "F.C. ").Replace("Compañía de ", "Cía. de ");
    }
}
