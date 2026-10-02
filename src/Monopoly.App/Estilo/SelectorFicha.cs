using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Monopoly.Core.Modelo;

namespace Monopoly.App.Estilo;

/// <summary>
/// Selección de ficha: seis fichas dibujadas con GDI+ (en el color elegido) y una fila de colores debajo.
/// La ficha y el color seleccionados se resaltan; también se cambian con las flechas del teclado
/// (izquierda/derecha la ficha, arriba/abajo el color).
/// </summary>
internal sealed class SelectorFicha : Control
{
    private const int AltoFicha = 100;
    private const int Separacion = 10;
    private const int DiametroColor = 30;

    private static readonly FormaFicha[] Formas =
        { FormaFicha.Sombrero, FormaFicha.Carro, FormaFicha.Barco, FormaFicha.Perro, FormaFicha.Dedal, FormaFicha.Bota };

    private static readonly ColorFicha[] Colores =
        { ColorFicha.Rojo, ColorFicha.Azul, ColorFicha.Verde, ColorFicha.Amarillo, ColorFicha.Morado, ColorFicha.Naranja };

    private int _forma;
    private int _color;
    private int _fichaEncima = -1;
    private int _colorEncima = -1;

    /// <summary>
    /// Crea el selector.
    /// </summary>
    public SelectorFicha()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Font = Tema.Texto(9f, FontStyle.Bold);
        Size = new Size(560, AltoFicha + 52);
        TabStop = true;
    }

    /// <summary>Cambió la ficha o el color.</summary>
    public event Action? SeleccionCambiada;

    /// <summary>Forma elegida.</summary>
    public FormaFicha Forma
    {
        get => Formas[_forma];
        set => Seleccionar(Array.IndexOf(Formas, value), _color);
    }

    /// <summary>Color elegido.</summary>
    public ColorFicha ColorElegido
    {
        get => Colores[_color];
        set => Seleccionar(_forma, Array.IndexOf(Colores, value));
    }

    /// <inheritdoc/>
    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        int forma = _forma;
        int color = _color;
        if (e.KeyCode == Keys.Left)
        {
            forma = (forma + Formas.Length - 1) % Formas.Length;
        }
        else if (e.KeyCode == Keys.Right)
        {
            forma = (forma + 1) % Formas.Length;
        }
        else if (e.KeyCode == Keys.Up)
        {
            color = (color + Colores.Length - 1) % Colores.Length;
        }
        else if (e.KeyCode == Keys.Down)
        {
            color = (color + 1) % Colores.Length;
        }

        Seleccionar(forma, color);
        base.OnKeyDown(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int ficha = FichaEn(e.Location);
        int color = ColorEn(e.Location);
        if (ficha != _fichaEncima || color != _colorEncima)
        {
            _fichaEncima = ficha;
            _colorEncima = color;
            Cursor = ficha >= 0 || color >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(EventArgs e)
    {
        _fichaEncima = -1;
        _colorEncima = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        int ficha = FichaEn(e.Location);
        int color = ColorEn(e.Location);
        Seleccionar(ficha >= 0 ? ficha : _forma, color >= 0 ? color : _color);
        base.OnMouseDown(e);
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

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        Color colorJugador = Tema.ColorFicha(ColorElegido);

        for (int i = 0; i < Formas.Length; i++)
        {
            RectangleF caja = RectFicha(i);
            bool elegida = i == _forma;
            bool encima = i == _fichaEncima;
            if (encima && !elegida)
            {
                caja.Offset(0, -2);
            }

            if (elegida || encima)
            {
                Dibujo.Sombra(g, caja, 12, 5, elegida ? 46 : 30, 2f);
            }

            using (GraphicsPath forma = Dibujo.Redondeado(caja, 12))
            {
                using SolidBrush fondo = new SolidBrush(elegida ? Tema.Mezclar(Tema.Marfil, colorJugador, 0.12f) : Tema.Marfil);
                g.FillPath(fondo, forma);
                using Pen borde = new Pen(elegida ? colorJugador : Tema.Borde, elegida ? 2.8f : 1.1f);
                g.DrawPath(borde, forma);
            }

            Color colorFicha = elegida || encima ? colorJugador : Tema.Mezclar(colorJugador, Color.FromArgb(190, 186, 176), 0.55f);
            float lado = Math.Min(caja.Width - 16, caja.Height - 34);
            DibujoFicha.Dibujar(g, Formas[i], new RectangleF(caja.X + ((caja.Width - lado) / 2f), caja.Y + 8, lado, lado), colorFicha);
            using Font nombre = Tema.Texto(8.5f, elegida ? FontStyle.Bold : FontStyle.Regular);
            Dibujo.TextoCentrado(g, Tema.NombreFicha(Formas[i]), nombre, elegida ? Tema.Tinta : Tema.TintaSuave,
                new RectangleF(caja.X - 4, caja.Bottom - 26, caja.Width + 8, 24));

            if (elegida)
            {
                RectangleF insignia = new RectangleF(caja.Right - 20, caja.Y - 6, 22, 22);
                using SolidBrush relleno = new SolidBrush(colorJugador);
                g.FillEllipse(relleno, insignia);
                using Pen blanco = new Pen(Color.White, 2f);
                g.DrawEllipse(blanco, insignia);
                Dibujo.Verificacion(g, RectangleF.Inflate(insignia, -5, -5), Color.White, 2.2f);
            }
        }

        using (SolidBrush tinta = new SolidBrush(Tema.TintaSuave))
        {
            g.DrawString("Color:", Font, tinta, 0, AltoFicha + 19);
        }

        for (int i = 0; i < Colores.Length; i++)
        {
            RectangleF circulo = RectColor(i);
            Color color = Tema.ColorFicha(Colores[i]);
            if (i == _color)
            {
                using Pen anillo = new Pen(color, 2.4f);
                g.DrawEllipse(anillo, RectangleF.Inflate(circulo, 4, 4));
            }

            RectangleF disco = i == _colorEncima && i != _color ? RectangleF.Inflate(circulo, 1.5f, 1.5f) : circulo;
            using SolidBrush relleno = new SolidBrush(color);
            g.FillEllipse(relleno, disco);
            using Pen borde = new Pen(Tema.Mezclar(color, Color.Black, 0.3f), 1f);
            g.DrawEllipse(borde, disco);
            if (i == _color)
            {
                Dibujo.Verificacion(g, RectangleF.Inflate(circulo, -8, -8), Color.White, 2.4f);
            }
        }

        if (Focused && ShowFocusCues)
        {
            using Pen foco = new Pen(Tema.Dorado, 1.6f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(foco, 0, 0, Width - 1, Height - 1);
        }
    }

    private void Seleccionar(int forma, int color)
    {
        if (forma < 0 || color < 0 || (forma == _forma && color == _color))
        {
            return;
        }

        _forma = forma;
        _color = color;
        Invalidate();
        SeleccionCambiada?.Invoke();
    }

    private RectangleF RectFicha(int indice)
    {
        float ancho = (Width - 4 - (Separacion * (Formas.Length - 1))) / (float)Formas.Length;
        return new RectangleF(2 + (indice * (ancho + Separacion)), 8, ancho, AltoFicha - 8);
    }

    private RectangleF RectColor(int indice)
    {
        return new RectangleF(70 + (indice * (DiametroColor + 18)), AltoFicha + 14, DiametroColor, DiametroColor);
    }

    private int FichaEn(Point punto)
    {
        for (int i = 0; i < Formas.Length; i++)
        {
            if (RectFicha(i).Contains(punto))
            {
                return i;
            }
        }

        return -1;
    }

    private int ColorEn(Point punto)
    {
        for (int i = 0; i < Colores.Length; i++)
        {
            if (RectangleF.Inflate(RectColor(i), 4, 4).Contains(punto))
            {
                return i;
            }
        }

        return -1;
    }
}
