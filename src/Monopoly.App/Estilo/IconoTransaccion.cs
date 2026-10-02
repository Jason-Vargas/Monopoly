using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Monopoly.Core.Modelo;

namespace Monopoly.App.Estilo;

/// <summary>
/// Ícono de cada tipo de transacción: un círculo de color con un pictograma blanco (casa, llave, banco,
/// flechas, estrella...).
/// </summary>
internal static class IconoTransaccion
{
    /// <summary>
    /// Color asociado al tipo.
    /// </summary>
    public static Color ColorDe(TipoTransaccion tipo)
    {
        return tipo switch
        {
            TipoTransaccion.CompraPropiedad => Tema.Rojo,
            TipoTransaccion.PagoAlquiler => Tema.Advertencia,
            TipoTransaccion.PagoBanco => Color.FromArgb(90, 100, 120),
            TipoTransaccion.PagoEntreJugadores => Color.FromArgb(132, 70, 176),
            TipoTransaccion.GananciaEvento => Tema.Exito,
            TipoTransaccion.PerdidaEvento => Tema.Error,
            _ => Color.FromArgb(214, 160, 20),
        };
    }

    /// <summary>
    /// Dibuja el ícono del tipo en el área indicada.
    /// </summary>
    public static void Dibujar(Graphics g, TipoTransaccion tipo, RectangleF area)
    {
        Color color = ColorDe(tipo);
        using (SolidBrush fondo = new SolidBrush(color))
        {
            g.FillEllipse(fondo, area);
        }

        RectangleF r = RectangleF.Inflate(area, -area.Width * 0.26f, -area.Height * 0.26f);
        using SolidBrush blanco = new SolidBrush(Color.White);
        using Pen lapiz = new Pen(Color.White, Math.Max(1.4f, area.Width * 0.09f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (tipo)
        {
            case TipoTransaccion.CompraPropiedad:
                g.FillPolygon(blanco, new[] { P(r, 0.5f, 0.02f), P(r, 1f, 0.45f), P(r, 0.86f, 0.45f), P(r, 0.86f, 0.98f), P(r, 0.14f, 0.98f), P(r, 0.14f, 0.45f), P(r, 0f, 0.45f) });
                break;
            case TipoTransaccion.PagoAlquiler:
                g.DrawEllipse(lapiz, r.X, r.Y + (r.Height * 0.1f), r.Width * 0.45f, r.Height * 0.45f);
                g.DrawLine(lapiz, P(r, 0.38f, 0.48f), P(r, 0.95f, 0.98f));
                g.DrawLine(lapiz, P(r, 0.72f, 0.78f), P(r, 0.86f, 0.64f));
                break;
            case TipoTransaccion.PagoBanco:
                g.FillPolygon(blanco, new[] { P(r, 0.5f, 0f), P(r, 1f, 0.3f), P(r, 0f, 0.3f) });
                g.FillRectangle(blanco, r.X + (r.Width * 0.1f), r.Y + (r.Height * 0.38f), r.Width * 0.18f, r.Height * 0.42f);
                g.FillRectangle(blanco, r.X + (r.Width * 0.41f), r.Y + (r.Height * 0.38f), r.Width * 0.18f, r.Height * 0.42f);
                g.FillRectangle(blanco, r.X + (r.Width * 0.72f), r.Y + (r.Height * 0.38f), r.Width * 0.18f, r.Height * 0.42f);
                g.FillRectangle(blanco, r.X, r.Y + (r.Height * 0.86f), r.Width, r.Height * 0.14f);
                break;
            case TipoTransaccion.PagoEntreJugadores:
                g.DrawLine(lapiz, P(r, 0f, 0.3f), P(r, 0.95f, 0.3f));
                g.DrawLines(lapiz, new[] { P(r, 0.72f, 0.08f), P(r, 0.95f, 0.3f), P(r, 0.72f, 0.52f) });
                g.DrawLine(lapiz, P(r, 1f, 0.72f), P(r, 0.05f, 0.72f));
                g.DrawLines(lapiz, new[] { P(r, 0.28f, 0.5f), P(r, 0.05f, 0.72f), P(r, 0.28f, 0.94f) });
                break;
            case TipoTransaccion.GananciaEvento:
                g.DrawLine(lapiz, P(r, 0.5f, 0.98f), P(r, 0.5f, 0.05f));
                g.DrawLines(lapiz, new[] { P(r, 0.12f, 0.42f), P(r, 0.5f, 0.05f), P(r, 0.88f, 0.42f) });
                break;
            case TipoTransaccion.PerdidaEvento:
                g.DrawLine(lapiz, P(r, 0.5f, 0.02f), P(r, 0.5f, 0.95f));
                g.DrawLines(lapiz, new[] { P(r, 0.12f, 0.58f), P(r, 0.5f, 0.95f), P(r, 0.88f, 0.58f) });
                break;
            default:
                // Premio por pasar por Salida: estrella.
                PointF[] estrella = new PointF[10];
                for (int i = 0; i < 10; i++)
                {
                    double angulo = (-Math.PI / 2) + (i * Math.PI / 5);
                    float radio = i % 2 == 0 ? 0.52f : 0.22f;
                    estrella[i] = new PointF(r.X + (r.Width * (0.5f + (float)(Math.Cos(angulo) * radio))), r.Y + (r.Height * (0.52f + (float)(Math.Sin(angulo) * radio))));
                }

                g.FillPolygon(blanco, estrella);
                break;
        }
    }

    private static PointF P(RectangleF r, float x, float y)
    {
        return new PointF(r.X + (r.Width * x), r.Y + (r.Height * y));
    }
}
