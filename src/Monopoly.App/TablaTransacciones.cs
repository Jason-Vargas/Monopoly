using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Modelo;

namespace Monopoly.App;

/// <summary>
/// Tabla de transacciones dibujada con GDI+: encabezado fijo, filas alternadas, ícono por tipo y monto en
/// verde (ingreso) o rojo (pago) desde el punto de vista de un jugador de referencia. Se desplaza con la
/// rueda del mouse o con la barra lateral.
/// </summary>
internal sealed class TablaTransacciones : Control
{
    private const int AltoEncabezado = 36;
    private const int AltoFila = 32;

    private static readonly string[] Titulos = { "N°", "Turno", "Tipo", "Origen", "Destino", "Monto", "Descripción" };
    private static readonly int[] Anchos = { 56, 80, 290, 124, 124, 120, 0 };

    private readonly VScrollBar _barra = new VScrollBar { Dock = DockStyle.Right, Visible = false };
    private Transaccion[] _filas = new Transaccion[0];
    private string? _referencia;
    private int _encima = -1;

    /// <summary>
    /// Crea la tabla vacía.
    /// </summary>
    public TablaTransacciones()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Marfil;
        Controls.Add(_barra);
        _barra.ValueChanged += (s, e) => Invalidate();
    }

    /// <summary>Cantidad de filas mostradas.</summary>
    public int Cantidad => _filas.Length;

    /// <summary>
    /// Muestra las transacciones en el orden recibido.
    /// </summary>
    /// <param name="transacciones">Resultado de la consulta al servidor.</param>
    /// <param name="referencia">Jugador desde cuyo punto de vista se colorean los montos.</param>
    public void Mostrar(ListaSimple<Transaccion> transacciones, string? referencia)
    {
        Transaccion[] filas = new Transaccion[transacciones.Cantidad];
        int i = 0;
        transacciones.Recorrer(t => filas[i++] = t);
        _filas = filas;
        _referencia = referencia;
        AjustarBarra();
        _barra.Value = 0;
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        AjustarBarra();
    }

    /// <inheritdoc/>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!_barra.Visible)
        {
            return;
        }

        int maximo = _barra.Maximum - _barra.LargeChange + 1;
        _barra.Value = Math.Clamp(_barra.Value - (Math.Sign(e.Delta) * 3), 0, Math.Max(0, maximo));
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int fila = e.Y < AltoEncabezado ? -1 : ((e.Y - AltoEncabezado) / AltoFila) + _barra.Value;
        if (fila >= _filas.Length)
        {
            fila = -1;
        }

        if (fila != _encima)
        {
            _encima = fila;
            Invalidate();
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _encima = -1;
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        g.Clear(Tema.Marfil);
        int ancho = Width - (_barra.Visible ? _barra.Width : 0);
        int[] x = Columnas(ancho);

        // Encabezado.
        using (SolidBrush fondo = new SolidBrush(Tema.VerdeProfundo))
        {
            g.FillRectangle(fondo, 0, 0, ancho, AltoEncabezado);
        }

        using Font encabezado = Tema.Texto(9.5f, FontStyle.Bold);
        using Font normal = Tema.Texto(9.5f);
        using Font negrita = Tema.Texto(10f, FontStyle.Bold);
        using StringFormat izquierda = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using StringFormat derecha = new StringFormat(izquierda) { Alignment = StringAlignment.Far };
        using SolidBrush blanco = new SolidBrush(Color.White);
        for (int c = 0; c < Titulos.Length; c++)
        {
            RectangleF celda = Celda(x, c, 0, AltoEncabezado, ancho);
            g.DrawString(Titulos[c], encabezado, blanco, celda, EsNumerica(c) ? derecha : izquierda);
        }

        if (_filas.Length == 0)
        {
            Dibujo.TextoCentrado(g, "No hay transacciones con este filtro.", normal, Tema.TintaSuave, new RectangleF(0, AltoEncabezado, ancho, 80));
            return;
        }

        using SolidBrush tinta = new SolidBrush(Tema.Tinta);
        using SolidBrush suave = new SolidBrush(Tema.TintaSuave);
        int visibles = ((Height - AltoEncabezado) / AltoFila) + 1;
        for (int k = 0; k < visibles; k++)
        {
            int indice = _barra.Value + k;
            if (indice >= _filas.Length)
            {
                break;
            }

            Transaccion t = _filas[indice];
            int y = AltoEncabezado + (k * AltoFila);
            Color fondoFila = indice == _encima ? Color.FromArgb(255, 244, 210) : indice % 2 == 0 ? Tema.Marfil : Color.FromArgb(242, 238, 224);
            using (SolidBrush fondo = new SolidBrush(fondoFila))
            {
                g.FillRectangle(fondo, 0, y, ancho, AltoFila);
            }

            g.DrawString(t.Id.ToString(CultureInfo.InvariantCulture), normal, suave, Celda(x, 0, y, AltoFila, ancho), derecha);
            g.DrawString(t.NumeroTurno.ToString(CultureInfo.InvariantCulture), normal, suave, Celda(x, 1, y, AltoFila, ancho), derecha);

            RectangleF tipo = Celda(x, 2, y, AltoFila, ancho);
            IconoTransaccion.Dibujar(g, t.Tipo, new RectangleF(tipo.X, y + 5, 22, 22));
            g.DrawString(Formato.Nombre(t.Tipo), normal, tinta, new RectangleF(tipo.X + 28, tipo.Y, tipo.Width - 28, tipo.Height), izquierda);

            g.DrawString(Nombre(t.Origen), normal, tinta, Celda(x, 3, y, AltoFila, ancho), izquierda);
            g.DrawString(Nombre(t.Destino), normal, tinta, Celda(x, 4, y, AltoFila, ancho), izquierda);

            bool ingreso = EsIngreso(t);
            using (SolidBrush colorMonto = new SolidBrush(ingreso ? Tema.Exito : Tema.Error))
            {
                g.DrawString((ingreso ? "+" : "−") + Formato.Dinero(t.Monto), negrita, colorMonto, Celda(x, 5, y, AltoFila, ancho), derecha);
            }

            g.DrawString(t.Descripcion, normal, suave, Celda(x, 6, y, AltoFila, ancho), izquierda);
        }
    }

    /// <summary>
    /// Ingreso para el jugador de referencia si es el destino; pago si es el origen. Si no participa, se
    /// considera ingreso lo que paga el banco y pago todo lo demás.
    /// </summary>
    private bool EsIngreso(Transaccion t)
    {
        if (_referencia != null && string.Equals(t.Destino, _referencia, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_referencia != null && string.Equals(t.Origen, _referencia, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return t.Origen == Transaccion.Banco;
    }

    private static string Nombre(string participante)
    {
        return participante == Transaccion.Banco ? "Banco" : participante;
    }

    private static bool EsNumerica(int columna) => columna == 0 || columna == 1 || columna == 5;

    private static int[] Columnas(int ancho)
    {
        int[] x = new int[Anchos.Length + 1];
        x[0] = 10;
        for (int c = 0; c < Anchos.Length; c++)
        {
            int w = Anchos[c] > 0 ? Anchos[c] : Math.Max(120, ancho - x[c] - 10);
            x[c + 1] = x[c] + w;
        }

        return x;
    }

    private static RectangleF Celda(int[] x, int columna, int y, int alto, int ancho)
    {
        float margen = 8f;
        float derecha = Math.Min(x[columna + 1], ancho - 4);
        return new RectangleF(x[columna] + (columna == 0 ? 0 : margen), y, Math.Max(10, derecha - x[columna] - (2 * margen)), alto);
    }

    private void AjustarBarra()
    {
        int visibles = Math.Max(1, (Height - AltoEncabezado) / AltoFila);
        bool necesaria = _filas.Length > visibles;
        _barra.Visible = necesaria;
        if (necesaria)
        {
            _barra.Minimum = 0;
            _barra.LargeChange = visibles;
            _barra.SmallChange = 1;
            _barra.Maximum = _filas.Length - 1;
            if (_barra.Value > _filas.Length - visibles)
            {
                _barra.Value = Math.Max(0, _filas.Length - visibles);
            }
        }
        else if (_barra.Value != 0)
        {
            _barra.Value = 0;
        }
    }
}
