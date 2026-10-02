using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Monopoly.App.Estilo;

/// <summary>
/// Logotipo propio: una marquesina roja con luces doradas alrededor y el nombre del juego en letras
/// blancas (diseño original; no reproduce el logotipo oficial).
/// </summary>
internal static class Logotipo
{
    /// <summary>
    /// Dibuja el logotipo ajustado al área indicada.
    /// </summary>
    /// <param name="g">Superficie (con antialiasing).</param>
    /// <param name="area">Área disponible; el logotipo mantiene su proporción y se centra.</param>
    /// <param name="opacidad">Opacidad de 0 a 1 (para la animación de entrada).</param>
    /// <param name="fase">Desplazamiento de las luces encendidas (para animarlas).</param>
    public static void Dibujar(Graphics g, RectangleF area, float opacidad = 1f, int fase = 0)
    {
        if (opacidad <= 0f || area.Width < 20 || area.Height < 10)
        {
            return;
        }

        // Proporción fija 4.6 : 1.
        float alto = Math.Min(area.Height, area.Width / 4.6f);
        float ancho = alto * 4.6f;
        RectangleF marco = new RectangleF(area.X + ((area.Width - ancho) / 2f), area.Y + ((area.Height - alto) / 2f), ancho, alto);
        float radio = alto * 0.16f;

        if (opacidad >= 0.99f)
        {
            Dibujo.Sombra(g, marco, radio, 8, 50, alto * 0.05f);
        }

        // Marco exterior rojo oscuro y panel rojo con un brillo arriba.
        using (GraphicsPath exterior = Dibujo.Redondeado(marco, radio))
        using (SolidBrush rojoOscuro = new SolidBrush(Tema.ConOpacidad(Tema.RojoOscuro, opacidad)))
        {
            g.FillPath(rojoOscuro, exterior);
        }

        RectangleF panel = RectangleF.Inflate(marco, -alto * 0.07f, -alto * 0.07f);
        using (GraphicsPath interior = Dibujo.Redondeado(panel, radio * 0.75f))
        {
            using LinearGradientBrush rojo = new LinearGradientBrush(panel,
                Tema.ConOpacidad(Tema.RojoHover, opacidad), Tema.ConOpacidad(Tema.RojoPresionado, opacidad), 90f);
            g.FillPath(rojo, interior);
            using Pen filete = new Pen(Tema.ConOpacidad(Color.FromArgb(235, 255, 244, 220), opacidad), Math.Max(1f, alto * 0.018f));
            using GraphicsPath linea = Dibujo.Redondeado(RectangleF.Inflate(panel, -alto * 0.1f, -alto * 0.1f), radio * 0.5f);
            g.DrawPath(filete, linea);
        }

        DibujarLuces(g, marco, alto, opacidad, fase);

        // Nombre del juego, ajustado al ancho disponible, con una sombra roja oscura.
        RectangleF zonaTexto = RectangleF.Inflate(panel, -alto * 0.2f, -alto * 0.14f);
        float tamanio = zonaTexto.Height * 0.62f;
        using StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Font fuente = new Font(Fuentes.Titulo, tamanio, FontStyle.Regular, GraphicsUnit.Pixel);
        SizeF medida = g.MeasureString(Tema.NombreJuego, fuente);
        if (medida.Width > zonaTexto.Width)
        {
            float nuevo = tamanio * zonaTexto.Width / medida.Width;
            fuente.Dispose();
            fuente = new Font(Fuentes.Titulo, nuevo, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        using (fuente)
        {
            RectangleF sombra = zonaTexto;
            sombra.Offset(alto * 0.025f, alto * 0.035f);
            using SolidBrush oscuro = new SolidBrush(Tema.ConOpacidad(Tema.RojoOscuro, opacidad));
            g.DrawString(Tema.NombreJuego, fuente, oscuro, sombra, centrado);
            using SolidBrush blanco = new SolidBrush(Tema.ConOpacidad(Color.FromArgb(255, 253, 246), opacidad));
            g.DrawString(Tema.NombreJuego, fuente, blanco, zonaTexto, centrado);
        }
    }

    /// <summary>
    /// Luces de la marquesina, repartidas por el borde; una de cada tres está apagada y la secuencia avanza
    /// con <paramref name="fase"/>.
    /// </summary>
    private static void DibujarLuces(Graphics g, RectangleF marco, float alto, float opacidad, int fase)
    {
        float margen = alto * 0.035f;
        float radioLuz = Math.Max(1.5f, alto * 0.022f);
        RectangleF recorrido = RectangleF.Inflate(marco, -margen, -margen);
        float separacion = alto * 0.11f;
        int porLado = Math.Max(2, (int)(recorrido.Width / separacion));
        int porAlto = Math.Max(2, (int)(recorrido.Height / separacion));

        int indice = 0;
        void Luz(float x, float y)
        {
            bool encendida = (indice + fase) % 3 != 0;
            Color color = encendida ? Tema.Dorado : Color.FromArgb(150, 110, 60);
            if (encendida)
            {
                using SolidBrush halo = new SolidBrush(Tema.ConOpacidad(Color.FromArgb(90, Tema.Dorado), opacidad));
                g.FillEllipse(halo, x - (radioLuz * 1.9f), y - (radioLuz * 1.9f), radioLuz * 3.8f, radioLuz * 3.8f);
            }

            using SolidBrush pincel = new SolidBrush(Tema.ConOpacidad(color, opacidad));
            g.FillEllipse(pincel, x - radioLuz, y - radioLuz, radioLuz * 2f, radioLuz * 2f);
            indice++;
        }

        float r = alto * 0.12f;
        for (int i = 0; i <= porLado; i++)
        {
            float x = recorrido.X + r + ((recorrido.Width - (2 * r)) * i / porLado);
            Luz(x, recorrido.Y);
        }

        for (int i = 1; i < porAlto; i++)
        {
            Luz(recorrido.Right, recorrido.Y + (recorrido.Height * i / porAlto));
        }

        for (int i = porLado; i >= 0; i--)
        {
            float x = recorrido.X + r + ((recorrido.Width - (2 * r)) * i / porLado);
            Luz(x, recorrido.Bottom);
        }

        for (int i = porAlto - 1; i >= 1; i--)
        {
            Luz(recorrido.X, recorrido.Y + (recorrido.Height * i / porAlto));
        }
    }
}
