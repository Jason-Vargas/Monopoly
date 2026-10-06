using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Monopoly.App.Estilo;

/// <summary>
/// Botón con un ícono dibujado y, opcionalmente, un texto a su derecha (por ejemplo, el engranaje de
/// opciones o "Historial"). Fondo marfil redondeado que se ilumina al pasar el mouse.
/// </summary>
internal sealed class BotonIcono : Button
{
    private readonly ToolTip _ayuda = new ToolTip();
    private bool _encima;
    private bool _presionado;

    /// <summary>
    /// Crea el botón.
    /// </summary>
    /// <param name="icono">Dibuja el ícono en el rectángulo con el color indicado.</param>
    /// <param name="ayuda">Texto de ayuda (también es el nombre accesible si no hay texto).</param>
    public BotonIcono(Action<Graphics, RectangleF, Color> icono, string ayuda)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Icono = icono;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Tema.Texto(10f, FontStyle.Bold);
        Size = new Size(44, 44);
        AccessibleName = ayuda;
        _ayuda.SetToolTip(this, ayuda);
    }

    /// <summary>Dibujo del ícono.</summary>
    public Action<Graphics, RectangleF, Color> Icono { get; set; }

    /// <summary>Color del ícono y del texto.</summary>
    public Color ColorIcono { get; set; } = Tema.VerdeProfundo;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ayuda.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        ButtonRenderer.DrawParentBackground(g, ClientRectangle, this);
        Dibujo.Calidad(g);
        RectangleF cuerpo = new RectangleF(1.5f, 1.5f, Width - 4, Height - 4);
        if (_presionado)
        {
            cuerpo.Offset(0, 1);
        }

        Color fondo = !Enabled ? Tema.Deshabilitado : _presionado ? Color.FromArgb(232, 224, 204) : _encima ? Color.White : Tema.Marfil;
        using (GraphicsPath forma = Dibujo.Redondeado(cuerpo, Math.Min(12f, cuerpo.Height / 2f)))
        {
            using SolidBrush relleno = new SolidBrush(fondo);
            g.FillPath(relleno, forma);
            using Pen borde = new Pen(_encima && Enabled ? ColorIcono : Tema.Borde, Focused && ShowFocusCues ? 2.2f : 1.2f);
            g.DrawPath(borde, forma);
        }

        Color color = Enabled ? ColorIcono : Tema.TintaDeshabilitada;
        float lado = Math.Min(cuerpo.Height - 16, 26);
        float xIcono = Text.Length == 0 ? cuerpo.X + ((cuerpo.Width - lado) / 2f) : cuerpo.X + 12;
        Icono(g, new RectangleF(xIcono, cuerpo.Y + ((cuerpo.Height - lado) / 2f), lado, lado), color);
        if (Text.Length > 0)
        {
            using SolidBrush tinta = new SolidBrush(color);
            using StringFormat formato = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(Text, Font, tinta, new RectangleF(xIcono + lado + 8, cuerpo.Y, cuerpo.Right - xIcono - lado - 8, cuerpo.Height), formato);
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseEnter(EventArgs e)
    {
        _encima = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(EventArgs e)
    {
        _encima = false;
        _presionado = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        _presionado = e.Button == MouseButtons.Left;
        Invalidate();
        base.OnMouseDown(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        _presionado = false;
        Invalidate();
        base.OnMouseUp(e);
    }
}
