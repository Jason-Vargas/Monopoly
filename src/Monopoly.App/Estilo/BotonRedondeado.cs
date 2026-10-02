using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Monopoly.App.Estilo;

/// <summary>
/// Botón dibujado con GDI+: esquinas redondeadas, sombra suave, color al pasar el mouse y al presionar,
/// anillo de foco con el teclado y un estilo deshabilitado claramente distinto. Hereda de <see cref="Button"/>
/// para conservar el teclado, el botón predeterminado del formulario y la accesibilidad, pero se dibuja
/// completo con GDI+.
/// </summary>
internal sealed class BotonRedondeado : Button
{
    private const int MargenSombra = 5;
    private bool _encima;
    private bool _presionado;
    private EstiloBoton _estilo = EstiloBoton.Principal;

    /// <summary>
    /// Crea el botón.
    /// </summary>
    public BotonRedondeado()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Tema.Texto(11f, FontStyle.Bold);
        Size = new Size(200, 48);
    }

    /// <summary>Variante de color.</summary>
    public EstiloBoton Estilo
    {
        get => _estilo;
        set
        {
            _estilo = value;
            Invalidate();
        }
    }

    /// <summary>Radio de las esquinas.</summary>
    public float Radio { get; set; } = 12f;

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        // Las esquinas fuera del rectángulo redondeado muestran el fondo del contenedor.
        ButtonRenderer.DrawParentBackground(g, ClientRectangle, this);
        Dibujo.Calidad(g);
        RectangleF cuerpo = new RectangleF(MargenSombra, MargenSombra - 2, Width - (2 * MargenSombra) - 1, Height - (2 * MargenSombra) - 1);
        if (_presionado)
        {
            cuerpo.Offset(0, 1.5f);
        }

        (Color fondo, Color texto, Color? borde) = Colores();
        if (Enabled && !_presionado)
        {
            Dibujo.Sombra(g, cuerpo, Radio, 5, _encima ? 60 : 40, _encima ? 3f : 2f);
        }

        using (GraphicsPath forma = Dibujo.Redondeado(cuerpo, Radio))
        {
            using LinearGradientBrush relleno = new LinearGradientBrush(cuerpo, Tema.Mezclar(fondo, Color.White, 0.08f), fondo, 90f);
            g.FillPath(relleno, forma);
            if (borde.HasValue)
            {
                using Pen lapiz = new Pen(borde.Value, 1.4f);
                g.DrawPath(lapiz, forma);
            }

            if (Focused && ShowFocusCues && Enabled)
            {
                using Pen foco = new Pen(Tema.Dorado, 2.2f);
                using GraphicsPath anillo = Dibujo.Redondeado(RectangleF.Inflate(cuerpo, 2f, 2f), Radio + 2);
                g.DrawPath(foco, anillo);
            }
        }

        Dibujo.TextoCentrado(g, Text, Font, texto, cuerpo);
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
        if (e.Button == MouseButtons.Left)
        {
            _presionado = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        _presionado = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        _encima = false;
        _presionado = false;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    /// <inheritdoc/>
    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    private (Color Fondo, Color Texto, Color? Borde) Colores()
    {
        if (!Enabled)
        {
            return (Tema.Deshabilitado, Tema.TintaDeshabilitada, Color.FromArgb(206, 200, 188));
        }

        return _estilo switch
        {
            EstiloBoton.Secundario => (_presionado ? Color.FromArgb(234, 226, 206) : _encima ? Color.White : Tema.Marfil,
                Tema.VerdeProfundo, _encima ? Tema.VerdeProfundo : Tema.Borde),
            EstiloBoton.Exito => (_presionado ? Color.FromArgb(24, 110, 64) : _encima ? Color.FromArgb(44, 160, 98) : Tema.Exito, Color.White, null),
            _ => (_presionado ? Tema.RojoPresionado : _encima ? Tema.RojoHover : Tema.Rojo, Color.White, null),
        };
    }
}
