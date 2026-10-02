using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Monopoly.App.Estilo;

/// <summary>
/// Utilidades de dibujo con GDI+: calidad (antialiasing), rectángulos redondeados, sombras suaves,
/// el fondo con patrón de tablero y un indicador de estado.
/// </summary>
internal static class Dibujo
{
    /// <summary>
    /// Activa el antialiasing y el suavizado de texto.
    /// </summary>
    public static void Calidad(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = CompositingQuality.HighQuality;
    }

    /// <summary>
    /// Crea un rectángulo con esquinas redondeadas (quien lo recibe debe liberarlo).
    /// </summary>
    public static GraphicsPath Redondeado(RectangleF area, float radio)
    {
        float d = Math.Min(Math.Max(radio * 2f, 1f), Math.Min(area.Width, area.Height));
        GraphicsPath camino = new GraphicsPath();
        camino.AddArc(area.X, area.Y, d, d, 180, 90);
        camino.AddArc(area.Right - d, area.Y, d, d, 270, 90);
        camino.AddArc(area.Right - d, area.Bottom - d, d, d, 0, 90);
        camino.AddArc(area.X, area.Bottom - d, d, d, 90, 90);
        camino.CloseFigure();
        return camino;
    }

    /// <summary>
    /// Dibuja una sombra suave (varias capas translúcidas) bajo un rectángulo redondeado.
    /// </summary>
    /// <param name="g">Superficie.</param>
    /// <param name="area">Rectángulo que proyecta la sombra.</param>
    /// <param name="radio">Radio de las esquinas.</param>
    /// <param name="tamanio">Difuminado en píxeles.</param>
    /// <param name="opacidad">Opacidad máxima (0 a 255).</param>
    /// <param name="desplazamientoY">Desplazamiento vertical de la sombra.</param>
    public static void Sombra(Graphics g, RectangleF area, float radio, int tamanio = 6, int opacidad = 36, float desplazamientoY = 3f)
    {
        for (int i = tamanio; i >= 1; i--)
        {
            RectangleF capa = RectangleF.Inflate(area, i * 0.8f, i * 0.8f);
            capa.Offset(0, desplazamientoY);
            int alfa = Math.Max(1, opacidad * (tamanio - i + 1) / (tamanio * tamanio));
            using GraphicsPath camino = Redondeado(capa, radio + i);
            using SolidBrush pincel = new SolidBrush(Color.FromArgb(alfa, 20, 30, 20));
            g.FillPath(pincel, camino);
        }
    }

    /// <summary>
    /// Fondo verde menta con un patrón sutil de casillas de tablero: un degradado claro al centro, una
    /// cuadrícula tenue y una franja de casillas con colores de grupo muy suaves en los bordes.
    /// </summary>
    public static void FondoTablero(Graphics g, Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        using (LinearGradientBrush degradado = new LinearGradientBrush(area, Tema.VerdeMentaClaro, Tema.VerdeMenta, 90f))
        {
            g.FillRectangle(degradado, area);
        }

        const int lado = 44;
        using (Pen linea = new Pen(Color.FromArgb(70, Tema.VerdeMentaLinea), 1f))
        {
            for (int x = area.Left; x < area.Right; x += lado)
            {
                g.DrawLine(linea, x, area.Top, x, area.Bottom);
            }

            for (int y = area.Top; y < area.Bottom; y += lado)
            {
                g.DrawLine(linea, area.Left, y, area.Right, y);
            }
        }

        // Franja de "casillas" arriba y abajo, con la banda de color de cada grupo muy tenue.
        Color[] grupos =
        {
            Tema.ColorGrupo(Core.Modelo.GrupoPropiedad.Marron), Tema.ColorGrupo(Core.Modelo.GrupoPropiedad.Celeste),
            Tema.ColorGrupo(Core.Modelo.GrupoPropiedad.Rosa), Tema.ColorGrupo(Core.Modelo.GrupoPropiedad.Naranja),
            Tema.ColorGrupo(Core.Modelo.GrupoPropiedad.Rojo), Tema.ColorGrupo(Core.Modelo.GrupoPropiedad.Amarillo),
            Tema.ColorGrupo(Core.Modelo.GrupoPropiedad.Verde), Tema.ColorGrupo(Core.Modelo.GrupoPropiedad.AzulOscuro),
        };
        int indice = 0;
        for (int x = area.Left; x < area.Right; x += lado)
        {
            using SolidBrush banda = new SolidBrush(Color.FromArgb(46, grupos[indice % grupos.Length]));
            g.FillRectangle(banda, x + 1, area.Top, lado - 1, 8);
            g.FillRectangle(banda, x + 1, area.Bottom - 8, lado - 1, 8);
            indice++;
        }
    }

    /// <summary>
    /// Indicador redondo de estado con un halo del mismo color.
    /// </summary>
    public static void Indicador(Graphics g, PointF centro, float radio, Color color)
    {
        using (SolidBrush halo = new SolidBrush(Color.FromArgb(60, color)))
        {
            g.FillEllipse(halo, centro.X - (radio * 1.8f), centro.Y - (radio * 1.8f), radio * 3.6f, radio * 3.6f);
        }

        using SolidBrush relleno = new SolidBrush(color);
        g.FillEllipse(relleno, centro.X - radio, centro.Y - radio, radio * 2f, radio * 2f);
    }

    /// <summary>
    /// Marca de verificación (✓) dibujada con líneas, sin depender de la fuente.
    /// </summary>
    public static void Verificacion(Graphics g, RectangleF area, Color color, float grosor = 2.4f)
    {
        using Pen lapiz = new Pen(color, grosor) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(lapiz, new[]
        {
            new PointF(area.X + (area.Width * 0.12f), area.Y + (area.Height * 0.55f)),
            new PointF(area.X + (area.Width * 0.4f), area.Y + (area.Height * 0.82f)),
            new PointF(area.X + (area.Width * 0.9f), area.Y + (area.Height * 0.2f)),
        });
    }

    /// <summary>
    /// Escribe un texto centrado en un rectángulo.
    /// </summary>
    public static void TextoCentrado(Graphics g, string texto, Font fuente, Color color, RectangleF area)
    {
        using StringFormat formato = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        using SolidBrush pincel = new SolidBrush(color);
        g.DrawString(texto, fuente, pincel, area, formato);
    }
}
