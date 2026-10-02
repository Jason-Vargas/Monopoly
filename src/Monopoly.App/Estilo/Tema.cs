using System;
using System.Drawing;
using Monopoly.Core.Modelo;

namespace Monopoly.App.Estilo;

/// <summary>
/// Sistema de estilo de la interfaz: paleta inspirada en el Monopoly clásico (verde menta del tablero, rojo
/// de los títulos, fondos crema y marfil) y tipografías. Todo el diseño es propio: no usa el logotipo,
/// la mascota ni ilustraciones oficiales.
/// </summary>
internal static class Tema
{
    /// <summary>Nombre del juego que se muestra en el logotipo.</summary>
    public const string NombreJuego = "MONOPOLY TEC";

    /// <summary>Subtítulo bajo el logotipo.</summary>
    public const string Subtitulo = "Edición clásica · estructuras lineales";

    // ---------------------------------------------------------------- tablero

    /// <summary>Verde menta del tablero.</summary>
    public static readonly Color VerdeMenta = Color.FromArgb(200, 230, 210);

    /// <summary>Verde menta claro (centro de los fondos).</summary>
    public static readonly Color VerdeMentaClaro = Color.FromArgb(220, 240, 226);

    /// <summary>Líneas tenues del patrón de casillas.</summary>
    public static readonly Color VerdeMentaLinea = Color.FromArgb(168, 206, 182);

    /// <summary>Verde profundo para textos sobre menta y detalles.</summary>
    public static readonly Color VerdeProfundo = Color.FromArgb(24, 82, 56);

    // ---------------------------------------------------------------- acciones y títulos

    /// <summary>Rojo clásico de títulos y acciones principales.</summary>
    public static readonly Color Rojo = Color.FromArgb(200, 30, 45);

    /// <summary>Rojo al pasar el mouse.</summary>
    public static readonly Color RojoHover = Color.FromArgb(222, 52, 64);

    /// <summary>Rojo al presionar.</summary>
    public static readonly Color RojoPresionado = Color.FromArgb(158, 20, 32);

    /// <summary>Rojo oscuro de bordes y sombras del logotipo.</summary>
    public static readonly Color RojoOscuro = Color.FromArgb(120, 14, 24);

    /// <summary>Dorado de las luces de la marquesina y resaltados.</summary>
    public static readonly Color Dorado = Color.FromArgb(250, 204, 70);

    // ---------------------------------------------------------------- fondos y texto

    /// <summary>Fondo crema de las ventanas.</summary>
    public static readonly Color Crema = Color.FromArgb(248, 243, 228);

    /// <summary>Fondo marfil de tarjetas y campos.</summary>
    public static readonly Color Marfil = Color.FromArgb(255, 252, 244);

    /// <summary>Borde suave de tarjetas y campos.</summary>
    public static readonly Color Borde = Color.FromArgb(218, 208, 186);

    /// <summary>Texto principal.</summary>
    public static readonly Color Tinta = Color.FromArgb(38, 36, 32);

    /// <summary>Texto secundario.</summary>
    public static readonly Color TintaSuave = Color.FromArgb(112, 104, 92);

    /// <summary>Fondo de controles deshabilitados.</summary>
    public static readonly Color Deshabilitado = Color.FromArgb(226, 222, 212);

    /// <summary>Texto de controles deshabilitados.</summary>
    public static readonly Color TintaDeshabilitada = Color.FromArgb(150, 144, 132);

    // ---------------------------------------------------------------- estados

    /// <summary>Verde de éxito.</summary>
    public static readonly Color Exito = Color.FromArgb(34, 139, 84);

    /// <summary>Rojo de error.</summary>
    public static readonly Color Error = Color.FromArgb(198, 40, 40);

    /// <summary>Azul de información.</summary>
    public static readonly Color Informacion = Color.FromArgb(38, 98, 160);

    /// <summary>Naranja de advertencia (pendiente).</summary>
    public static readonly Color Advertencia = Color.FromArgb(214, 128, 20);

    // ---------------------------------------------------------------- fuentes

    /// <summary>
    /// Fuente de títulos (Abril Fatface). Quien la crea debe liberarla si no la asigna a un control.
    /// </summary>
    /// <param name="tamanio">Tamaño en puntos.</param>
    public static Font Titulo(float tamanio)
    {
        return new Font(Fuentes.Titulo, tamanio, FontStyle.Regular, GraphicsUnit.Point);
    }

    /// <summary>
    /// Fuente de texto (Lato).
    /// </summary>
    /// <param name="tamanio">Tamaño en puntos.</param>
    /// <param name="estilo">Normal o negrita.</param>
    public static Font Texto(float tamanio, FontStyle estilo = FontStyle.Regular)
    {
        return new Font(Fuentes.Texto, tamanio, estilo, GraphicsUnit.Point);
    }

    // ---------------------------------------------------------------- colores del juego

    /// <summary>
    /// Color de un grupo de propiedades, un poco menos saturado que el clásico.
    /// </summary>
    /// <param name="grupo">Grupo de la propiedad.</param>
    public static Color ColorGrupo(GrupoPropiedad grupo)
    {
        return grupo switch
        {
            GrupoPropiedad.Marron => Color.FromArgb(140, 92, 68),
            GrupoPropiedad.Celeste => Color.FromArgb(160, 210, 232),
            GrupoPropiedad.Rosa => Color.FromArgb(204, 88, 154),
            GrupoPropiedad.Naranja => Color.FromArgb(236, 150, 60),
            GrupoPropiedad.Rojo => Color.FromArgb(214, 64, 64),
            GrupoPropiedad.Amarillo => Color.FromArgb(240, 214, 70),
            GrupoPropiedad.Verde => Color.FromArgb(56, 160, 98),
            GrupoPropiedad.AzulOscuro => Color.FromArgb(44, 98, 168),
            GrupoPropiedad.Ferrocarril => Color.FromArgb(64, 64, 64),
            _ => Color.FromArgb(150, 150, 150),
        };
    }

    /// <summary>
    /// Color de la ficha de un jugador.
    /// </summary>
    /// <param name="color">Color elegido.</param>
    public static Color ColorFicha(ColorFicha color)
    {
        return color switch
        {
            Core.Modelo.ColorFicha.Rojo => Color.FromArgb(214, 48, 48),
            Core.Modelo.ColorFicha.Azul => Color.FromArgb(40, 104, 210),
            Core.Modelo.ColorFicha.Verde => Color.FromArgb(30, 150, 76),
            Core.Modelo.ColorFicha.Amarillo => Color.FromArgb(232, 184, 16),
            Core.Modelo.ColorFicha.Morado => Color.FromArgb(132, 70, 176),
            _ => Color.FromArgb(236, 120, 30),
        };
    }

    /// <summary>
    /// Nombre en español de una forma de ficha, para mostrar.
    /// </summary>
    /// <param name="forma">Forma.</param>
    public static string NombreFicha(FormaFicha forma)
    {
        return forma switch
        {
            FormaFicha.Sombrero => "Sombrero",
            FormaFicha.Carro => "Carro",
            FormaFicha.Barco => "Barco",
            FormaFicha.Perro => "Perro",
            FormaFicha.Dedal => "Dedal",
            _ => "Bota",
        };
    }

    /// <summary>
    /// Mezcla dos colores (0 = solo <paramref name="a"/>, 1 = solo <paramref name="b"/>).
    /// </summary>
    public static Color Mezclar(Color a, Color b, float proporcion)
    {
        float p = Math.Clamp(proporcion, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + ((b.A - a.A) * p)),
            (int)(a.R + ((b.R - a.R) * p)),
            (int)(a.G + ((b.G - a.G) * p)),
            (int)(a.B + ((b.B - a.B) * p)));
    }

    /// <summary>
    /// Aplica una opacidad (0 a 1) a un color.
    /// </summary>
    public static Color ConOpacidad(Color color, float opacidad)
    {
        return Color.FromArgb((int)(color.A * Math.Clamp(opacidad, 0f, 1f)), color);
    }
}
