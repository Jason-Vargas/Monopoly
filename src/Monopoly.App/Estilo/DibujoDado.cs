using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Monopoly.App.Estilo;

/// <summary>
/// Dibuja un dado marfil con puntos, girado el ángulo indicado (para animaciones).
/// </summary>
internal static class DibujoDado
{
    // Posición de los puntos en una cuadrícula de 3 × 3 (fila, columna) para cada valor.
    private static readonly int[][] Puntos =
    {
        new[] { 1, 1 },
        new[] { 0, 0, 2, 2 },
        new[] { 0, 0, 1, 1, 2, 2 },
        new[] { 0, 0, 0, 2, 2, 0, 2, 2 },
        new[] { 0, 0, 0, 2, 1, 1, 2, 0, 2, 2 },
        new[] { 0, 0, 1, 0, 2, 0, 0, 2, 1, 2, 2, 2 },
    };

    /// <summary>
    /// Dibuja el dado.
    /// </summary>
    /// <param name="g">Superficie (con antialiasing).</param>
    /// <param name="centro">Centro del dado.</param>
    /// <param name="lado">Lado en píxeles.</param>
    /// <param name="angulo">Giro en grados.</param>
    /// <param name="valor">Cara visible (1 a 6).</param>
    /// <param name="opacidad">Opacidad (0 a 1).</param>
    public static void Dibujar(Graphics g, PointF centro, float lado, float angulo, int valor, float opacidad = 1f)
    {
        if (opacidad <= 0f)
        {
            return;
        }

        GraphicsState estado = g.Save();
        g.TranslateTransform(centro.X, centro.Y);
        g.RotateTransform(angulo);
        RectangleF cara = new RectangleF(-lado / 2f, -lado / 2f, lado, lado);
        float radio = lado * 0.2f;

        if (opacidad >= 0.99f)
        {
            Dibujo.Sombra(g, cara, radio, 5, 40, lado * 0.06f);
        }

        using (GraphicsPath forma = Dibujo.Redondeado(cara, radio))
        {
            using LinearGradientBrush relleno = new LinearGradientBrush(cara,
                Tema.ConOpacidad(Color.White, opacidad), Tema.ConOpacidad(Color.FromArgb(236, 228, 210), opacidad), 60f);
            g.FillPath(relleno, forma);
            using Pen borde = new Pen(Tema.ConOpacidad(Color.FromArgb(160, 150, 130), opacidad), Math.Max(1f, lado * 0.03f));
            g.DrawPath(borde, forma);
        }

        int[] puntos = Puntos[Math.Clamp(valor, 1, 6) - 1];
        float paso = lado * 0.27f;
        float diametro = lado * 0.17f;
        using SolidBrush tinta = new SolidBrush(Tema.ConOpacidad(valor == 1 ? Tema.Rojo : Tema.Tinta, opacidad));
        for (int i = 0; i < puntos.Length; i += 2)
        {
            float x = (puntos[i + 1] - 1) * paso;
            float y = (puntos[i] - 1) * paso;
            float d = valor == 1 ? diametro * 1.35f : diametro;
            g.FillEllipse(tinta, x - (d / 2f), y - (d / 2f), d, d);
        }

        g.Restore(estado);
    }
}
