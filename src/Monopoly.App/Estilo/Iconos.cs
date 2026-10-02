using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Monopoly.App.Estilo;

/// <summary>
/// Íconos simples dibujados con GDI+ para las tarjetas: banco, red, chip (Pico W), tarjeta RFID y copiar.
/// Todos se dibujan en un círculo de color dentro del área indicada.
/// </summary>
internal static class Iconos
{
    /// <summary>Banco con columnas (crear partida: este equipo aloja al banco).</summary>
    public static void Banco(Graphics g, RectangleF area, Color color)
    {
        RectangleF r = Fondo(g, area, color);
        using SolidBrush blanco = new SolidBrush(Color.White);
        g.FillPolygon(blanco, new[] { new PointF(r.X + (r.Width * 0.5f), r.Y + (r.Height * 0.08f)), new PointF(r.Right, r.Y + (r.Height * 0.32f)), new PointF(r.X, r.Y + (r.Height * 0.32f)) });
        for (int i = 0; i < 4; i++)
        {
            float x = r.X + (r.Width * (0.1f + (i * 0.24f)));
            g.FillRectangle(blanco, x, r.Y + (r.Height * 0.38f), r.Width * 0.1f, r.Height * 0.42f);
        }

        g.FillRectangle(blanco, r.X, r.Y + (r.Height * 0.84f), r.Width, r.Height * 0.12f);
    }

    /// <summary>Dos computadoras unidas (unirse a una partida en red).</summary>
    public static void Red(Graphics g, RectangleF area, Color color)
    {
        RectangleF r = Fondo(g, area, color);
        using Pen blanco = new Pen(Color.White, Math.Max(1.5f, r.Width * 0.09f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawRectangle(blanco, r.X, r.Y + (r.Height * 0.08f), r.Width * 0.42f, r.Height * 0.32f);
        g.DrawRectangle(blanco, r.X + (r.Width * 0.58f), r.Y + (r.Height * 0.52f), r.Width * 0.42f, r.Height * 0.32f);
        g.DrawLines(blanco, new[]
        {
            new PointF(r.X + (r.Width * 0.21f), r.Y + (r.Height * 0.4f)),
            new PointF(r.X + (r.Width * 0.21f), r.Y + (r.Height * 0.68f)),
            new PointF(r.X + (r.Width * 0.58f), r.Y + (r.Height * 0.68f)),
        });
    }

    /// <summary>Chip con patas (la Raspberry Pi Pico W).</summary>
    public static void Chip(Graphics g, RectangleF area, Color color)
    {
        RectangleF r = Fondo(g, area, color);
        RectangleF cuerpo = RectangleF.Inflate(r, -r.Width * 0.2f, -r.Height * 0.2f);
        using SolidBrush blanco = new SolidBrush(Color.White);
        using GraphicsPath forma = Dibujo.Redondeado(cuerpo, r.Width * 0.08f);
        g.FillPath(blanco, forma);
        using Pen patas = new Pen(Color.White, Math.Max(1.2f, r.Width * 0.07f));
        for (int i = 0; i < 3; i++)
        {
            float t = cuerpo.X + (cuerpo.Width * (0.2f + (i * 0.3f)));
            g.DrawLine(patas, t, r.Y, t, cuerpo.Y);
            g.DrawLine(patas, t, cuerpo.Bottom, t, r.Bottom);
        }

        using SolidBrush punto = new SolidBrush(color);
        g.FillEllipse(punto, cuerpo.X + (cuerpo.Width * 0.18f), cuerpo.Y + (cuerpo.Height * 0.18f), cuerpo.Width * 0.22f, cuerpo.Height * 0.22f);
    }

    /// <summary>Tarjeta con ondas (lector RFID).</summary>
    public static void TarjetaRfid(Graphics g, RectangleF area, Color color)
    {
        RectangleF r = Fondo(g, area, color);
        RectangleF tarjeta = new RectangleF(r.X, r.Y + (r.Height * 0.25f), r.Width * 0.62f, r.Height * 0.5f);
        using SolidBrush blanco = new SolidBrush(Color.White);
        using (GraphicsPath forma = Dibujo.Redondeado(tarjeta, r.Width * 0.06f))
        {
            g.FillPath(blanco, forma);
        }

        using Pen onda = new Pen(Color.White, Math.Max(1.2f, r.Width * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (int i = 0; i < 2; i++)
        {
            float d = r.Width * (0.3f + (i * 0.22f));
            g.DrawArc(onda, r.Right - (d * 0.9f), r.Y + (r.Height * 0.5f) - (d / 2f), d, d, -50, 100);
        }
    }

    /// <summary>Dos hojas superpuestas (copiar).</summary>
    public static void Copiar(Graphics g, RectangleF area, Color color)
    {
        using Pen lapiz = new Pen(color, Math.Max(1.4f, area.Width * 0.1f)) { LineJoin = LineJoin.Round };
        using GraphicsPath atras = Dibujo.Redondeado(new RectangleF(area.X, area.Y, area.Width * 0.62f, area.Height * 0.72f), area.Width * 0.08f);
        g.DrawPath(lapiz, atras);
        RectangleF frente = new RectangleF(area.X + (area.Width * 0.3f), area.Y + (area.Height * 0.25f), area.Width * 0.62f, area.Height * 0.72f);
        using SolidBrush fondo = new SolidBrush(Tema.Marfil);
        using GraphicsPath delante = Dibujo.Redondeado(frente, area.Width * 0.08f);
        g.FillPath(fondo, delante);
        g.DrawPath(lapiz, delante);
    }

    /// <summary>
    /// Dibuja el círculo de color y devuelve el área interior para el pictograma.
    /// </summary>
    private static RectangleF Fondo(Graphics g, RectangleF area, Color color)
    {
        using (LinearGradientBrush relleno = new LinearGradientBrush(area, Tema.Mezclar(color, Color.White, 0.15f), color, 90f))
        {
            g.FillEllipse(relleno, area);
        }

        return RectangleF.Inflate(area, -area.Width * 0.26f, -area.Height * 0.26f);
    }
}
