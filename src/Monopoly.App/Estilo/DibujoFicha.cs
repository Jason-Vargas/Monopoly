using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Monopoly.Core.Modelo;

namespace Monopoly.App.Estilo;

/// <summary>
/// Dibuja las seis fichas (sombrero, carro, barco, perro, dedal y bota) como siluetas simples con GDI+,
/// en el color del jugador. Cada forma se define en una caja de 100 × 100 y se escala al área pedida.
/// </summary>
internal static class DibujoFicha
{
    /// <summary>
    /// Dibuja solo la silueta de la ficha.
    /// </summary>
    /// <param name="g">Superficie (con antialiasing).</param>
    /// <param name="forma">Forma de la ficha.</param>
    /// <param name="area">Área cuadrada donde se dibuja.</param>
    /// <param name="color">Color del jugador.</param>
    public static void Dibujar(Graphics g, FormaFicha forma, RectangleF area, Color color)
    {
        using GraphicsPath silueta = CrearSilueta(forma);
        using GraphicsPath detalles = CrearDetalles(forma);
        float escala = Math.Min(area.Width, area.Height) / 100f;
        using Matrix ajuste = new Matrix();
        ajuste.Translate(area.X + ((area.Width - (100 * escala)) / 2f), area.Y + ((area.Height - (100 * escala)) / 2f));
        ajuste.Scale(escala, escala);
        silueta.Transform(ajuste);
        detalles.Transform(ajuste);

        RectangleF limites = silueta.GetBounds();
        if (limites.Width < 1 || limites.Height < 1)
        {
            return;
        }

        using (LinearGradientBrush relleno = new LinearGradientBrush(limites, Tema.Mezclar(color, Color.White, 0.35f), Tema.Mezclar(color, Color.Black, 0.15f), 90f))
        {
            g.FillPath(relleno, silueta);
        }

        using (SolidBrush oscuro = new SolidBrush(Tema.Mezclar(color, Color.Black, 0.45f)))
        {
            g.FillPath(oscuro, detalles);
        }

        using Pen contorno = new Pen(Tema.Mezclar(color, Color.Black, 0.55f), Math.Max(1f, 2.6f * escala)) { LineJoin = LineJoin.Round };
        g.DrawPath(contorno, silueta);
    }

    /// <summary>
    /// Dibuja la ficha sobre un disco del color del jugador con borde blanco (como se ve en el tablero y en
    /// las tarjetas de jugador).
    /// </summary>
    public static void DibujarEnDisco(Graphics g, FormaFicha forma, RectangleF area, Color color, bool conSombra = true)
    {
        if (conSombra)
        {
            using SolidBrush sombra = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
            g.FillEllipse(sombra, area.X + (area.Width * 0.05f), area.Y + (area.Height * 0.09f), area.Width, area.Height);
        }

        using (SolidBrush fondo = new SolidBrush(Tema.Marfil))
        {
            g.FillEllipse(fondo, area);
        }

        using (Pen aro = new Pen(color, Math.Max(1.5f, area.Width * 0.09f)))
        {
            g.DrawEllipse(aro, RectangleF.Inflate(area, -area.Width * 0.045f, -area.Height * 0.045f));
        }

        Dibujar(g, forma, RectangleF.Inflate(area, -area.Width * 0.17f, -area.Height * 0.17f), color);
    }

    private static GraphicsPath CrearSilueta(FormaFicha forma)
    {
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        switch (forma)
        {
            case FormaFicha.Sombrero:
                p.AddEllipse(8, 70, 84, 18);
                p.AddPolygon(new PointF[] { new(28, 22), new(72, 22), new(68, 78), new(32, 78) });
                p.AddEllipse(28, 16, 44, 12);
                break;

            case FormaFicha.Carro:
                p.AddPolygon(new PointF[] { new(6, 70), new(8, 52), new(26, 48), new(36, 32), new(66, 32), new(78, 48), new(94, 54), new(94, 70) });
                p.AddEllipse(16, 60, 22, 22);
                p.AddEllipse(62, 60, 22, 22);
                break;

            case FormaFicha.Barco:
                p.AddPolygon(new PointF[] { new(6, 62), new(94, 62), new(80, 86), new(20, 86) });
                p.AddPolygon(new PointF[] { new(52, 12), new(52, 58), new(86, 58) });
                p.AddPolygon(new PointF[] { new(47, 20), new(47, 58), new(20, 58) });
                p.AddRectangle(new RectangleF(47, 10, 5, 52));
                break;

            case FormaFicha.Perro:
                p.AddEllipse(20, 40, 56, 28);
                p.AddEllipse(62, 22, 26, 24);
                p.AddPolygon(new PointF[] { new(82, 30), new(96, 36), new(94, 44), new(80, 42) });
                p.AddRectangle(new RectangleF(26, 58, 8, 26));
                p.AddRectangle(new RectangleF(40, 60, 8, 24));
                p.AddRectangle(new RectangleF(56, 60, 8, 24));
                p.AddRectangle(new RectangleF(66, 58, 8, 26));
                p.AddPolygon(new PointF[] { new(22, 50), new(6, 34), new(10, 30), new(26, 44) });
                break;

            case FormaFicha.Dedal:
                p.AddPolygon(new PointF[] { new(30, 30), new(70, 30), new(76, 80), new(24, 80) });
                p.AddEllipse(30, 14, 40, 32);
                p.AddPolygon(new PointF[] { new(18, 78), new(82, 78), new(82, 88), new(18, 88) });
                break;

            default:
                p.AddPolygon(new PointF[] { new(28, 10), new(60, 10), new(60, 54), new(80, 60), new(92, 70), new(92, 86), new(28, 86) });
                p.AddEllipse(78, 66, 16, 20);
                break;
        }

        return p;
    }

    private static GraphicsPath CrearDetalles(FormaFicha forma)
    {
        GraphicsPath p = new GraphicsPath();
        switch (forma)
        {
            case FormaFicha.Sombrero:
                p.AddPolygon(new PointF[] { new(31, 60), new(69, 60), new(68.4f, 68), new(31.6f, 68) });
                break;

            case FormaFicha.Carro:
                p.AddPolygon(new PointF[] { new(30, 48), new(39, 36), new(50, 36), new(50, 48) });
                p.AddPolygon(new PointF[] { new(54, 48), new(54, 36), new(64, 36), new(72, 48) });
                p.AddEllipse(22, 66, 10, 10);
                p.AddEllipse(68, 66, 10, 10);
                break;

            case FormaFicha.Barco:
                p.AddRectangle(new RectangleF(14, 66, 72, 4));
                p.AddEllipse(26, 72, 6, 6);
                p.AddEllipse(46, 72, 6, 6);
                p.AddEllipse(66, 72, 6, 6);
                break;

            case FormaFicha.Perro:
                p.AddEllipse(74, 30, 6, 6);
                p.AddEllipse(60, 20, 12, 22);
                p.AddEllipse(30, 40, 30, 8);
                break;

            case FormaFicha.Dedal:
                for (int fila = 0; fila < 4; fila++)
                {
                    for (int columna = 0; columna < 4; columna++)
                    {
                        float x = 34 + (columna * 9) + (fila % 2 * 4);
                        float y = 36 + (fila * 10);
                        p.AddEllipse(x, y, 4.5f, 4.5f);
                    }
                }

                break;

            default:
                p.AddRectangle(new RectangleF(28, 10, 32, 8));
                p.AddRectangle(new RectangleF(28, 80, 64, 6));
                p.AddEllipse(46, 28, 4, 4);
                p.AddEllipse(46, 38, 4, 4);
                break;
        }

        return p;
    }
}
