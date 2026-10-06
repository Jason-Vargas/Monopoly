using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace Monopoly.App.Estilo;

/// <summary>
/// Campo de texto con etiqueta arriba, caja redondeada y un mensaje de validación debajo. El borde se
/// pone verde al tener el foco y rojo cuando la validación falla (<see cref="MostrarError"/>); el error
/// se borra solo al escribir. Puede aceptar solo dígitos (<see cref="SoloNumeros"/>).
/// </summary>
internal sealed class CampoTexto : UserControl
{
    private const int AltoEtiqueta = 23;
    private const int AltoCaja = 38;
    private const int AltoError = 18;

    private readonly TextBox _caja = new TextBox { BorderStyle = BorderStyle.None };
    private string? _error;
    private string _etiqueta = string.Empty;

    /// <summary>
    /// Crea el campo.
    /// </summary>
    public CampoTexto()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Tema.Texto(9.5f, FontStyle.Bold);
        _caja.Font = Tema.Texto(11.5f);
        _caja.BackColor = Tema.Marfil;
        _caja.ForeColor = Tema.Tinta;
        Controls.Add(_caja);
        Height = AltoEtiqueta + AltoCaja + AltoError;
        Width = 220;

        _caja.GotFocus += (s, e) => Invalidate();
        _caja.LostFocus += (s, e) => Invalidate();
        _caja.TextChanged += (s, e) =>
        {
            LimpiarError();
            OnTextChanged(EventArgs.Empty);
        };
        _caja.KeyPress += (s, e) =>
        {
            if (SoloNumeros && !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        };
        _caja.KeyDown += (s, e) => OnKeyDown(e);
    }

    /// <summary>Texto de la etiqueta sobre la caja.</summary>
    public string Etiqueta
    {
        get => _etiqueta;
        set
        {
            _etiqueta = value;
            Invalidate();
        }
    }

    /// <summary>Texto de ayuda cuando está vacío.</summary>
    public string Marcador
    {
        get => _caja.PlaceholderText;
        set => _caja.PlaceholderText = value;
    }

    /// <summary>Si solo acepta dígitos.</summary>
    public bool SoloNumeros { get; set; }

    /// <summary>Longitud máxima.</summary>
    public int LongitudMaxima
    {
        get => _caja.MaxLength;
        set => _caja.MaxLength = value;
    }

    /// <summary>Texto escrito.</summary>
    public string Valor
    {
        get => _caja.Text;
        set => _caja.Text = value;
    }

    /// <summary>Valor entero, o <c>null</c> si no es un número.</summary>
    public int? ValorEntero => int.TryParse(_caja.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : null;

    /// <summary>
    /// Marca el campo como inválido: borde rojo y el mensaje debajo.
    /// </summary>
    public void MostrarError(string mensaje)
    {
        _error = mensaje;
        Invalidate();
    }

    /// <summary>
    /// Quita la marca de error.
    /// </summary>
    public void LimpiarError()
    {
        if (_error != null)
        {
            _error = null;
            Invalidate();
        }
    }

    /// <summary>
    /// Pone el foco en la caja y selecciona el texto.
    /// </summary>
    public void Enfocar()
    {
        _caja.Focus();
        _caja.SelectAll();
    }

    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e)
    {
        _caja.Enabled = Enabled;
        _caja.BackColor = Enabled ? Tema.Marfil : Tema.Deshabilitado;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int altoTexto = _caja.PreferredHeight;
        _caja.SetBounds(12, AltoEtiqueta + ((AltoCaja - altoTexto) / 2), Math.Max(10, Width - 24), altoTexto);
    }

    /// <inheritdoc/>
    protected override void OnClick(EventArgs e)
    {
        _caja.Focus();
        base.OnClick(e);
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        using (SolidBrush tinta = new SolidBrush(Enabled ? Tema.TintaSuave : Tema.TintaDeshabilitada))
        {
            g.DrawString(_etiqueta, Font, tinta, 2, 0);
        }

        RectangleF caja = new RectangleF(1, AltoEtiqueta, Width - 3, AltoCaja - 1);
        Color borde = _error != null ? Tema.Error : _caja.Focused ? Tema.VerdeProfundo : Tema.Borde;
        using (GraphicsPath forma = Dibujo.Redondeado(caja, 9))
        {
            using SolidBrush fondo = new SolidBrush(Enabled ? Tema.Marfil : Tema.Deshabilitado);
            g.FillPath(fondo, forma);
            using Pen lapiz = new Pen(borde, _error != null || _caja.Focused ? 2f : 1.2f);
            g.DrawPath(lapiz, forma);
        }

        if (_error != null)
        {
            using Font pequenia = Tema.Texto(8.5f);
            using SolidBrush rojo = new SolidBrush(Tema.Error);
            using StringFormat formato = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(_error, pequenia, rojo, new RectangleF(2, AltoEtiqueta + AltoCaja + 1, Width - 4, AltoError), formato);
        }
    }
}
