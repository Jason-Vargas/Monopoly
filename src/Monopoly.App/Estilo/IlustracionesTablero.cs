using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Monopoly.App.Estilo;

/// <summary>
/// Ilustraciones simples (diseño propio) para el tablero: flecha de Salida, rejas de la cárcel, auto de
/// Parada libre, silbato de "Vaya a la cárcel", tren, bombilla, gota, anillo, billete, signo de pregunta y
/// cofre. Cada una se dibuja dentro del rectángulo indicado, en coordenadas relativas (0 a 1).
/// </summary>
internal static class IlustracionesTablero
{
    /// <summary>Flecha grande hacia la izquierda (sentido del recorrido desde Salida).</summary>
    public static void FlechaSalida(Graphics g, RectangleF r)
    {
        using GraphicsPath flecha = new GraphicsPath();
        flecha.AddPolygon(new[]
        {
            P(r, 0.02f, 0.5f), P(r, 0.38f, 0.1f), P(r, 0.38f, 0.32f), P(r, 0.98f, 0.32f),
            P(r, 0.98f, 0.68f), P(r, 0.38f, 0.68f), P(r, 0.38f, 0.9f),
        });
        using LinearGradientBrush rojo = new LinearGradientBrush(r, Tema.RojoHover, Tema.RojoPresionado, 90f);
        g.FillPath(rojo, flecha);
        using Pen borde = new Pen(Tema.RojoOscuro, Math.Max(1f, r.Width * 0.025f)) { LineJoin = LineJoin.Round };
        g.DrawPath(borde, flecha);
    }

    /// <summary>Ventana de la cárcel con rejas.</summary>
    public static void Rejas(Graphics g, RectangleF r)
    {
        using (SolidBrush fondo = new SolidBrush(Color.FromArgb(236, 150, 60)))
        {
            g.FillRectangle(fondo, r);
        }

        using (SolidBrush interior = new SolidBrush(Color.FromArgb(60, 50, 44)))
        {
            g.FillRectangle(interior, RectangleF.Inflate(r, -r.Width * 0.12f, -r.Height * 0.12f));
        }

        using Pen barra = new Pen(Color.FromArgb(200, 200, 205), Math.Max(1.5f, r.Width * 0.07f));
        for (int i = 1; i <= 4; i++)
        {
            float x = r.X + (r.Width * (0.12f + (0.76f * i / 5f)));
            g.DrawLine(barra, x, r.Y + (r.Height * 0.12f), x, r.Bottom - (r.Height * 0.12f));
        }

        using Pen marco = new Pen(Tema.Tinta, Math.Max(1f, r.Width * 0.03f));
        g.DrawRectangle(marco, r.X, r.Y, r.Width, r.Height);
    }

    /// <summary>Auto rojo (Parada libre).</summary>
    public static void Auto(Graphics g, RectangleF r)
    {
        using GraphicsPath carroceria = new GraphicsPath();
        carroceria.AddPolygon(new[]
        {
            P(r, 0.04f, 0.72f), P(r, 0.06f, 0.52f), P(r, 0.26f, 0.46f), P(r, 0.38f, 0.26f), P(r, 0.68f, 0.26f),
            P(r, 0.8f, 0.46f), P(r, 0.96f, 0.52f), P(r, 0.96f, 0.72f),
        });
        using (SolidBrush rojo = new SolidBrush(Tema.Rojo))
        {
            g.FillPath(rojo, carroceria);
        }

        using (SolidBrush vidrio = new SolidBrush(Color.FromArgb(200, 230, 245)))
        {
            g.FillPolygon(vidrio, new[] { P(r, 0.3f, 0.46f), P(r, 0.4f, 0.31f), P(r, 0.51f, 0.31f), P(r, 0.51f, 0.46f) });
            g.FillPolygon(vidrio, new[] { P(r, 0.55f, 0.46f), P(r, 0.55f, 0.31f), P(r, 0.66f, 0.31f), P(r, 0.74f, 0.46f) });
        }

        using (Pen borde = new Pen(Tema.RojoOscuro, Math.Max(1f, r.Width * 0.025f)) { LineJoin = LineJoin.Round })
        {
            g.DrawPath(borde, carroceria);
        }

        foreach (float x in new[] { 0.26f, 0.74f })
        {
            RectangleF rueda = new RectangleF(r.X + (r.Width * (x - 0.1f)), r.Y + (r.Height * 0.62f), r.Width * 0.2f, r.Width * 0.2f);
            using SolidBrush negro = new SolidBrush(Tema.Tinta);
            g.FillEllipse(negro, rueda);
            using SolidBrush llanta = new SolidBrush(Color.FromArgb(190, 190, 190));
            g.FillEllipse(llanta, RectangleF.Inflate(rueda, -rueda.Width * 0.3f, -rueda.Height * 0.3f));
        }
    }

    /// <summary>Silbato de policía (Vaya a la cárcel).</summary>
    public static void Silbato(Graphics g, RectangleF r)
    {
        using SolidBrush azul = new SolidBrush(Color.FromArgb(44, 80, 150));
        using Pen borde = new Pen(Color.FromArgb(20, 40, 90), Math.Max(1f, r.Width * 0.03f));
        RectangleF cuerpo = new RectangleF(r.X + (r.Width * 0.08f), r.Y + (r.Height * 0.32f), r.Width * 0.52f, r.Height * 0.52f);
        g.FillEllipse(azul, cuerpo);
        g.DrawEllipse(borde, cuerpo);
        PointF[] boquilla = { P(r, 0.5f, 0.34f), P(r, 0.94f, 0.34f), P(r, 0.94f, 0.54f), P(r, 0.5f, 0.6f) };
        g.FillPolygon(azul, boquilla);
        g.DrawPolygon(borde, boquilla);
        using SolidBrush hueco = new SolidBrush(Color.FromArgb(230, 236, 245));
        g.FillEllipse(hueco, r.X + (r.Width * 0.22f), r.Y + (r.Height * 0.46f), r.Width * 0.24f, r.Height * 0.24f);

        // Ondas del pitido.
        using Pen onda = new Pen(Tema.Rojo, Math.Max(1.2f, r.Width * 0.04f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(onda, P(r, 0.66f, 0.22f), P(r, 0.74f, 0.06f));
        g.DrawLine(onda, P(r, 0.8f, 0.24f), P(r, 0.94f, 0.12f));
        g.DrawLine(onda, P(r, 0.52f, 0.2f), P(r, 0.52f, 0.04f));
    }

    /// <summary>Locomotora (ferrocarril).</summary>
    public static void Tren(Graphics g, RectangleF r)
    {
        using SolidBrush negro = new SolidBrush(Color.FromArgb(44, 44, 48));
        g.FillRectangle(negro, r.X + (r.Width * 0.1f), r.Y + (r.Height * 0.42f), r.Width * 0.62f, r.Height * 0.3f);
        g.FillRectangle(negro, r.X + (r.Width * 0.56f), r.Y + (r.Height * 0.22f), r.Width * 0.3f, r.Height * 0.5f);
        g.FillRectangle(negro, r.X + (r.Width * 0.18f), r.Y + (r.Height * 0.2f), r.Width * 0.12f, r.Height * 0.22f);
        g.FillPolygon(negro, new[] { P(r, 0.1f, 0.72f), P(r, 0.02f, 0.82f), P(r, 0.1f, 0.82f) });
        using (SolidBrush ventana = new SolidBrush(Color.FromArgb(240, 214, 120)))
        {
            g.FillRectangle(ventana, r.X + (r.Width * 0.62f), r.Y + (r.Height * 0.28f), r.Width * 0.18f, r.Height * 0.14f);
        }

        foreach (float x in new[] { 0.24f, 0.46f, 0.72f })
        {
            g.FillEllipse(negro, r.X + (r.Width * (x - 0.09f)), r.Y + (r.Height * 0.68f), r.Width * 0.18f, r.Width * 0.18f);
        }
    }

    /// <summary>Bombilla (compañía de electricidad).</summary>
    public static void Bombilla(Graphics g, RectangleF r)
    {
        RectangleF globo = new RectangleF(r.X + (r.Width * 0.2f), r.Y + (r.Height * 0.04f), r.Width * 0.6f, r.Height * 0.6f);
        using (SolidBrush halo = new SolidBrush(Color.FromArgb(80, Tema.Dorado)))
        {
            g.FillEllipse(halo, RectangleF.Inflate(globo, r.Width * 0.08f, r.Height * 0.08f));
        }

        using (LinearGradientBrush amarillo = new LinearGradientBrush(globo, Color.FromArgb(255, 240, 150), Tema.Dorado, 90f))
        {
            g.FillEllipse(amarillo, globo);
        }

        using (Pen borde = new Pen(Color.FromArgb(170, 130, 30), Math.Max(1f, r.Width * 0.03f)))
        {
            g.DrawEllipse(borde, globo);
        }

        using SolidBrush gris = new SolidBrush(Color.FromArgb(140, 140, 150));
        g.FillRectangle(gris, r.X + (r.Width * 0.36f), r.Y + (r.Height * 0.62f), r.Width * 0.28f, r.Height * 0.22f);
        using Pen rosca = new Pen(Color.FromArgb(90, 90, 100), Math.Max(1f, r.Width * 0.03f));
        for (int i = 0; i < 3; i++)
        {
            float y = r.Y + (r.Height * (0.67f + (i * 0.06f)));
            g.DrawLine(rosca, r.X + (r.Width * 0.36f), y, r.X + (r.Width * 0.64f), y);
        }
    }

    /// <summary>Gota de agua (compañía de agua).</summary>
    public static void Gota(Graphics g, RectangleF r)
    {
        using GraphicsPath gota = new GraphicsPath();
        gota.AddBezier(P(r, 0.5f, 0.04f), P(r, 0.62f, 0.3f), P(r, 0.86f, 0.46f), P(r, 0.84f, 0.66f));
        gota.AddArc(r.X + (r.Width * 0.16f), r.Y + (r.Height * 0.32f), r.Width * 0.68f, r.Height * 0.64f, 0, 180);
        gota.AddBezier(P(r, 0.16f, 0.66f), P(r, 0.14f, 0.46f), P(r, 0.38f, 0.3f), P(r, 0.5f, 0.04f));
        using LinearGradientBrush azul = new LinearGradientBrush(r, Color.FromArgb(120, 190, 240), Color.FromArgb(30, 100, 190), 90f);
        g.FillPath(azul, gota);
        using Pen borde = new Pen(Color.FromArgb(20, 70, 140), Math.Max(1f, r.Width * 0.03f));
        g.DrawPath(borde, gota);
        using SolidBrush brillo = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
        g.FillEllipse(brillo, r.X + (r.Width * 0.32f), r.Y + (r.Height * 0.5f), r.Width * 0.12f, r.Height * 0.2f);
    }

    /// <summary>Anillo con diamante (impuesto de lujo).</summary>
    public static void Anillo(Graphics g, RectangleF r)
    {
        using Pen oro = new Pen(Color.FromArgb(214, 170, 40), Math.Max(2f, r.Width * 0.1f));
        g.DrawEllipse(oro, r.X + (r.Width * 0.18f), r.Y + (r.Height * 0.36f), r.Width * 0.64f, r.Height * 0.58f);
        PointF[] diamante = { P(r, 0.3f, 0.18f), P(r, 0.4f, 0.06f), P(r, 0.6f, 0.06f), P(r, 0.7f, 0.18f), P(r, 0.5f, 0.4f) };
        using SolidBrush celeste = new SolidBrush(Color.FromArgb(170, 220, 245));
        g.FillPolygon(celeste, diamante);
        using Pen borde = new Pen(Color.FromArgb(60, 120, 170), Math.Max(1f, r.Width * 0.03f)) { LineJoin = LineJoin.Round };
        g.DrawPolygon(borde, diamante);
        g.DrawLine(borde, P(r, 0.3f, 0.18f), P(r, 0.7f, 0.18f));
    }

    /// <summary>Billete (impuesto sobre la renta).</summary>
    public static void Billete(Graphics g, RectangleF r)
    {
        RectangleF billete = new RectangleF(r.X + (r.Width * 0.04f), r.Y + (r.Height * 0.24f), r.Width * 0.92f, r.Height * 0.52f);
        using (SolidBrush verde = new SolidBrush(Color.FromArgb(150, 200, 140)))
        {
            g.FillRectangle(verde, billete);
        }

        using Pen borde = new Pen(Color.FromArgb(50, 110, 50), Math.Max(1f, r.Width * 0.035f));
        g.DrawRectangle(borde, billete.X, billete.Y, billete.Width, billete.Height);
        g.DrawRectangle(borde, billete.X + (billete.Width * 0.08f), billete.Y + (billete.Height * 0.14f), billete.Width * 0.84f, billete.Height * 0.72f);
        using Font simbolo = new Font(Fuentes.Texto, Math.Max(5f, billete.Height * 0.5f), FontStyle.Bold, GraphicsUnit.Pixel);
        Dibujo.TextoCentrado(g, "₡", simbolo, Color.FromArgb(40, 90, 40), billete);
    }

    /// <summary>Signo de pregunta grande (Casualidad).</summary>
    public static void Pregunta(Graphics g, RectangleF r, Color color)
    {
        using Font fuente = new Font(Fuentes.Titulo, Math.Max(6f, r.Height * 0.8f), FontStyle.Regular, GraphicsUnit.Pixel);
        RectangleF sombra = r;
        sombra.Offset(r.Width * 0.04f, r.Height * 0.04f);
        Dibujo.TextoCentrado(g, "?", fuente, Color.FromArgb(70, 0, 0, 0), sombra);
        Dibujo.TextoCentrado(g, "?", fuente, color, r);
    }

    /// <summary>Cofre del tesoro (Arca Comunal).</summary>
    public static void Cofre(Graphics g, RectangleF r)
    {
        Color madera = Color.FromArgb(150, 92, 48);
        Color oscura = Color.FromArgb(96, 56, 26);
        RectangleF tapa = new RectangleF(r.X + (r.Width * 0.08f), r.Y + (r.Height * 0.18f), r.Width * 0.84f, r.Height * 0.36f);
        RectangleF caja = new RectangleF(r.X + (r.Width * 0.08f), r.Y + (r.Height * 0.48f), r.Width * 0.84f, r.Height * 0.38f);
        using SolidBrush relleno = new SolidBrush(madera);
        using Pen borde = new Pen(oscura, Math.Max(1f, r.Width * 0.035f));
        g.FillPie(relleno, tapa.X, tapa.Y, tapa.Width, tapa.Height * 1.6f, 180, 180);
        g.DrawArc(borde, tapa.X, tapa.Y, tapa.Width, tapa.Height * 1.6f, 180, 180);
        g.FillRectangle(relleno, caja);
        g.DrawRectangle(borde, caja.X, caja.Y, caja.Width, caja.Height);
        using SolidBrush dorado = new SolidBrush(Tema.Dorado);
        g.FillRectangle(dorado, caja.X, caja.Y - (r.Height * 0.03f), caja.Width, r.Height * 0.07f);
        g.FillRectangle(dorado, r.X + (r.Width * 0.44f), caja.Y - (r.Height * 0.04f), r.Width * 0.12f, r.Height * 0.18f);
        using SolidBrush cerradura = new SolidBrush(oscura);
        g.FillEllipse(cerradura, r.X + (r.Width * 0.475f), caja.Y + (r.Height * 0.04f), r.Width * 0.05f, r.Width * 0.05f);
    }

    private static PointF P(RectangleF r, float x, float y)
    {
        return new PointF(r.X + (r.Width * x), r.Y + (r.Height * y));
    }
}
