using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Monopoly.App.Estilo;

/// <summary>
/// Un mensaje: tarjeta de color con ícono y texto blanco.
/// </summary>
internal sealed class AvisoNotificacion : Control
{
    private readonly TipoNotificacion _tipo;

    /// <summary>Crea el aviso con su tamaño según el texto.</summary>
    public AvisoNotificacion(string texto, TipoNotificacion tipo, int ancho)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Text = texto;
        _tipo = tipo;
        Font = Tema.Texto(10f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        using Graphics g = CreateGraphics();
        SizeF medida = g.MeasureString(texto, Font, ancho - 64);
        Size = new Size(ancho, Math.Max(52, (int)medida.Height + 26));

        // Recorte redondeado: el aviso flota sobre otros controles, así que no puede tener esquinas transparentes.
        using GraphicsPath recorte = Dibujo.Redondeado(new RectangleF(0, 0, Width, Height), 10);
        Region = new Region(recorte);
    }

    /// <summary>Momento (TickCount64) en que debe salir.</summary>
    public long Vence { get; set; }

    /// <summary>Si ya está saliendo.</summary>
    public bool Saliendo { get; set; }

    /// <summary>Posición final en la pila.</summary>
    public Point Destino { get; set; }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        Color color = _tipo == TipoNotificacion.Exito ? Tema.Exito : _tipo == TipoNotificacion.Error ? Tema.Error : Tema.Informacion;
        RectangleF cuerpo = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        using (GraphicsPath forma = Dibujo.Redondeado(cuerpo, 10))
        {
            using SolidBrush fondo = new SolidBrush(color);
            g.FillPath(fondo, forma);
            using Pen borde = new Pen(Tema.Mezclar(color, Color.Black, 0.25f), 1f);
            g.DrawPath(borde, forma);
        }

        // Ícono: círculo blanco con ✓, ! o i.
        RectangleF icono = new RectangleF(14, (Height - 26) / 2f, 26, 26);
        using (SolidBrush blanco = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
        {
            g.FillEllipse(blanco, icono);
        }

        if (_tipo == TipoNotificacion.Exito)
        {
            Dibujo.Verificacion(g, RectangleF.Inflate(icono, -6, -6), color, 2.6f);
        }
        else
        {
            using Font simbolo = Tema.Texto(12f, FontStyle.Bold);
            Dibujo.TextoCentrado(g, _tipo == TipoNotificacion.Error ? "!" : "i", simbolo, color, icono);
        }

        using SolidBrush tinta = new SolidBrush(Color.White);
        using StringFormat formato = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, tinta, new RectangleF(50, 4, Width - 62, Height - 8), formato);
    }
}
