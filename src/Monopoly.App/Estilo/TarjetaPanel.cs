using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Monopoly.App.Estilo;

/// <summary>
/// Panel con bordes redondeados, fondo marfil y sombra suave. Puede mostrar un título (fuente de títulos)
/// con un ícono dibujado a la izquierda; los controles hijos se colocan debajo, dentro del área útil.
/// </summary>
internal class TarjetaPanel : Panel
{
    /// <summary>Margen reservado alrededor para la sombra.</summary>
    public const int MargenSombra = 8;

    private string? _titulo;
    private bool _resaltada;

    /// <summary>
    /// Crea la tarjeta.
    /// </summary>
    public TarjetaPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = Tema.Tinta;
        Font = Tema.Texto(10f);
    }

    /// <summary>Título opcional dibujado arriba.</summary>
    public string? Titulo
    {
        get => _titulo;
        set
        {
            _titulo = value;
            Invalidate();
        }
    }

    /// <summary>Tamaño de la fuente del título.</summary>
    public float TamanioTitulo { get; set; } = 15f;

    /// <summary>Dibuja un ícono de 40 × 40 a la izquierda del título.</summary>
    public Action<Graphics, RectangleF>? Icono { get; set; }

    /// <summary>Color de fondo de la tarjeta.</summary>
    public Color ColorFondo { get; set; } = Tema.Marfil;

    /// <summary>Si se dibuja con un borde de color (por ejemplo, la seleccionada).</summary>
    public bool Resaltada
    {
        get => _resaltada;
        set
        {
            _resaltada = value;
            Invalidate();
        }
    }

    /// <summary>Color del borde cuando está resaltada.</summary>
    public Color ColorResaltado { get; set; } = Tema.Rojo;

    /// <summary>Radio de las esquinas.</summary>
    public float Radio { get; set; } = 16f;

    /// <summary>Coordenada Y donde termina el encabezado (título e ícono).</summary>
    public int AltoEncabezado => _titulo == null ? MargenSombra + 12 : MargenSombra + 64;

    /// <summary>Rectángulo de la tarjeta (sin el margen de la sombra).</summary>
    protected RectangleF Cuerpo => new RectangleF(MargenSombra, MargenSombra - 3, Width - (2 * MargenSombra) - 1, Height - (2 * MargenSombra) - 1);

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        RectangleF cuerpo = Cuerpo;
        Dibujo.Sombra(g, cuerpo, Radio, 7, 34, 3f);
        using (GraphicsPath forma = Dibujo.Redondeado(cuerpo, Radio))
        {
            using SolidBrush fondo = new SolidBrush(ColorFondo);
            g.FillPath(fondo, forma);
            using Pen borde = new Pen(_resaltada ? ColorResaltado : Tema.Borde, _resaltada ? 2.6f : 1.2f);
            g.DrawPath(borde, forma);
        }

        if (_titulo != null)
        {
            float x = cuerpo.X + 18;
            if (Icono != null)
            {
                Icono(g, new RectangleF(x, cuerpo.Y + 14, 40, 40));
                x += 52;
            }

            using Font fuente = Tema.Titulo(TamanioTitulo);
            using SolidBrush tinta = new SolidBrush(Tema.Rojo);
            using StringFormat formato = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(_titulo, fuente, tinta, new RectangleF(x, cuerpo.Y + 12, cuerpo.Right - x - 12, 44), formato);
        }

        base.OnPaint(e);
    }
}
