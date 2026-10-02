using System.Drawing;
using System.Windows.Forms;

namespace Monopoly.App.Estilo;

/// <summary>
/// Etiqueta transparente con la tipografía del tema, para colocar sobre tarjetas y fondos dibujados. Se
/// dibuja con GDI+ (como el resto del estilo), con ajuste de línea, y opcionalmente un indicador de color
/// a la izquierda del texto.
/// </summary>
internal sealed class Etiqueta : Label
{
    /// <summary>
    /// Crea la etiqueta.
    /// </summary>
    /// <param name="texto">Texto.</param>
    /// <param name="tamanio">Tamaño en puntos.</param>
    /// <param name="estilo">Normal o negrita.</param>
    /// <param name="color">Color del texto (por defecto, el secundario).</param>
    public Etiqueta(string texto, float tamanio = 10f, FontStyle estilo = FontStyle.Regular, Color? color = null)
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        Text = texto;
        Font = Tema.Texto(tamanio, estilo);
        ForeColor = color ?? Tema.TintaSuave;
        BackColor = Color.Transparent;
        AutoSize = false;
        UseMnemonic = false;
    }

    /// <summary>
    /// Si tiene valor, se dibuja un punto de ese color a la izquierda del texto (indicador de estado).
    /// </summary>
    public Color? ColorIndicador { get; set; }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        RectangleF area = new RectangleF(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
        if (ColorIndicador.HasValue)
        {
            Dibujo.Indicador(g, new PointF(area.X + 7, area.Y + (Font.GetHeight(g) / 2f) + 1), 5f, ColorIndicador.Value);
            area.X += 22;
            area.Width -= 22;
        }

        bool centrado = TextAlign == ContentAlignment.MiddleLeft || TextAlign == ContentAlignment.MiddleCenter || TextAlign == ContentAlignment.MiddleRight;
        using StringFormat formato = new StringFormat
        {
            LineAlignment = centrado ? StringAlignment.Center : StringAlignment.Near,
            Trimming = StringTrimming.EllipsisWord,
        };
        using SolidBrush tinta = new SolidBrush(Enabled ? ForeColor : Tema.TintaDeshabilitada);
        g.DrawString(Text, Font, tinta, area, formato);
    }
}
