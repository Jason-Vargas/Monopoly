using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Logica;

namespace Monopoly.App;

/// <summary>
/// Tarjeta de un jugador en la sala de espera: su ficha y color, el nombre, insignias (organizador, usted,
/// desconectado) y el estado de su tarjeta RFID (✓ vinculada o pendiente). Sin jugador, muestra un lugar
/// libre. Aparece con una animación suave (sube y se vuelve opaca) cuando el jugador se une.
/// </summary>
internal sealed class TarjetaJugadorSala : Control
{
    private const int DuracionAparicionMs = 450;
    private const int Margen = 7;

    private EstadoJugador? _jugador;
    private long _inicioAparicion;
    private bool _encima;

    /// <summary>
    /// Crea una tarjeta vacía (lugar libre).
    /// </summary>
    public TarjetaJugadorSala()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(292, 128);
    }

    /// <summary>Jugador mostrado, o <c>null</c> si el lugar está libre.</summary>
    public EstadoJugador? Jugador => _jugador;

    /// <summary>Si es el organizador (primer jugador).</summary>
    public bool EsOrganizador { get; set; }

    /// <summary>Si es el jugador de esta computadora.</summary>
    public bool EsUsted { get; set; }

    /// <summary>Si tiene conexión con el servidor.</summary>
    public bool Conectado { get; set; } = true;

    /// <summary>Si el organizador la eligió para vincular una tarjeta.</summary>
    public bool Seleccionada { get; set; }

    /// <summary>Si se puede elegir con un clic (solo el organizador).</summary>
    public bool Seleccionable { get; set; }

    /// <summary>Si se espera la tarjeta RFID de este jugador en el lector.</summary>
    public bool EsperandoTarjeta { get; set; }

    /// <summary>Indica si la animación de aparición sigue en curso.</summary>
    public bool Animando => _inicioAparicion != 0 && Environment.TickCount64 - _inicioAparicion < DuracionAparicionMs;

    /// <summary>
    /// Muestra un jugador; si antes el lugar estaba libre (o era otro), arranca la animación de aparición.
    /// </summary>
    /// <param name="jugador">Jugador, o <c>null</c> para dejar el lugar libre.</param>
    /// <param name="animar">Si se anima la aparición.</param>
    public void MostrarJugador(EstadoJugador? jugador, bool animar)
    {
        bool nuevo = jugador != null && (_jugador == null || _jugador.Id != jugador.Id);
        _jugador = jugador;
        if (nuevo && animar)
        {
            _inicioAparicion = Environment.TickCount64;
        }

        Cursor = Seleccionable && jugador != null ? Cursors.Hand : Cursors.Default;
        Invalidate();
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
        Invalidate();
        base.OnMouseLeave(e);
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        RectangleF cuerpo = new RectangleF(Margen, Margen - 3, Width - (2 * Margen) - 1, Height - (2 * Margen) - 1);

        if (_jugador == null)
        {
            DibujarLugarLibre(g, cuerpo);
            return;
        }

        float t = _inicioAparicion == 0 ? 1f : Math.Min(1f, (Environment.TickCount64 - _inicioAparicion) / (float)DuracionAparicionMs);
        float suave = 1f - ((1f - t) * (1f - t));
        cuerpo.Offset(0, (1f - suave) * 18f);
        float a = suave;

        Color color = Tema.ColorFicha(_jugador.ColorFicha);
        if (a >= 0.99f)
        {
            Dibujo.Sombra(g, cuerpo, 14, 6, Seleccionada || (_encima && Seleccionable) ? 52 : 34, 3f);
        }

        using (GraphicsPath forma = Dibujo.Redondeado(cuerpo, 14))
        {
            using SolidBrush fondo = new SolidBrush(Tema.ConOpacidad(Tema.Marfil, a));
            g.FillPath(fondo, forma);

            // Franja del color del jugador a la izquierda.
            GraphicsState estado = g.Save();
            g.SetClip(forma);
            using (SolidBrush franja = new SolidBrush(Tema.ConOpacidad(color, a)))
            {
                g.FillRectangle(franja, cuerpo.X, cuerpo.Y, 8, cuerpo.Height);
            }

            g.Restore(estado);
            Color colorBorde = Seleccionada ? Tema.Rojo : _encima && Seleccionable ? Tema.VerdeProfundo : Tema.Borde;
            using Pen borde = new Pen(Tema.ConOpacidad(colorBorde, a), Seleccionada ? 2.6f : 1.2f);
            g.DrawPath(borde, forma);
        }

        RectangleF disco = new RectangleF(cuerpo.X + 20, cuerpo.Y + 16, 64, 64);
        if (a >= 0.99f)
        {
            DibujoFicha.DibujarEnDisco(g, _jugador.FormaFicha, disco, Conectado ? color : Color.Gray);
        }
        else
        {
            using SolidBrush fantasma = new SolidBrush(Tema.ConOpacidad(color, a * 0.5f));
            g.FillEllipse(fantasma, disco);
        }

        float x = cuerpo.X + 98;
        float ancho = cuerpo.Right - x - 10;
        using (Font nombre = Tema.Texto(13f, FontStyle.Bold))
        using (SolidBrush tinta = new SolidBrush(Tema.ConOpacidad(Tema.Tinta, a)))
        using (StringFormat corte = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        {
            g.DrawString(_jugador.Nombre, nombre, tinta, new RectangleF(x, cuerpo.Y + 12, ancho, 26), corte);
        }

        using (Font detalle = Tema.Texto(9f))
        using (SolidBrush suaveTinta = new SolidBrush(Tema.ConOpacidad(Tema.TintaSuave, a)))
        {
            g.DrawString($"{Tema.NombreFicha(_jugador.FormaFicha)} · {_jugador.ColorFicha}", detalle, suaveTinta, x, cuerpo.Y + 38);
        }

        // Insignias: organizador, usted, desconectado.
        float xInsignia = x;
        if (EsOrganizador)
        {
            xInsignia += Insignia(g, "Organizador", Tema.Rojo, xInsignia, cuerpo.Y + 60, a) + 6;
        }

        if (EsUsted)
        {
            xInsignia += Insignia(g, "Usted", Tema.VerdeProfundo, xInsignia, cuerpo.Y + 60, a) + 6;
        }

        if (!Conectado)
        {
            Insignia(g, "Desconectado", Tema.Advertencia, xInsignia, cuerpo.Y + 60, a);
        }

        DibujarEstadoTarjeta(g, new RectangleF(x, cuerpo.Bottom - 30, ancho, 22), a);
    }

    private void DibujarEstadoTarjeta(Graphics g, RectangleF area, float a)
    {
        bool vinculada = _jugador!.TieneTarjetaFisica;
        Color color = vinculada ? Tema.Exito : EsperandoTarjeta ? Tema.Informacion : Tema.Advertencia;
        RectangleF circulo = new RectangleF(area.X, area.Y + 2, 18, 18);
        using (SolidBrush relleno = new SolidBrush(Tema.ConOpacidad(color, a)))
        {
            g.FillEllipse(relleno, circulo);
        }

        if (vinculada)
        {
            Dibujo.Verificacion(g, RectangleF.Inflate(circulo, -4, -4), Tema.ConOpacidad(Color.White, a), 2f);
        }
        else
        {
            using Font signo = Tema.Texto(8.5f, FontStyle.Bold);
            Dibujo.TextoCentrado(g, EsperandoTarjeta ? "…" : "!", signo, Tema.ConOpacidad(Color.White, a), circulo);
        }

        string texto = vinculada ? "RFID vinculada" : EsperandoTarjeta ? "Acerque la tarjeta..." : "RFID pendiente";
        using Font fuente = Tema.Texto(9f, FontStyle.Bold);
        using SolidBrush tinta = new SolidBrush(Tema.ConOpacidad(color, a));
        g.DrawString(texto, fuente, tinta, area.X + 24, area.Y + 2);
    }

    private static float Insignia(Graphics g, string texto, Color color, float x, float y, float a)
    {
        using Font fuente = Tema.Texto(8f, FontStyle.Bold);
        SizeF medida = g.MeasureString(texto, fuente);
        RectangleF caja = new RectangleF(x, y, medida.Width + 10, 20);
        using (GraphicsPath forma = Dibujo.Redondeado(caja, 10))
        {
            using SolidBrush fondo = new SolidBrush(Tema.ConOpacidad(Color.FromArgb(34, color), a));
            g.FillPath(fondo, forma);
        }

        Dibujo.TextoCentrado(g, texto, fuente, Tema.ConOpacidad(color, a), caja);
        return caja.Width;
    }

    private static void DibujarLugarLibre(Graphics g, RectangleF cuerpo)
    {
        using (GraphicsPath forma = Dibujo.Redondeado(cuerpo, 14))
        {
            using SolidBrush fondo = new SolidBrush(Color.FromArgb(90, Tema.Marfil));
            g.FillPath(fondo, forma);
            using Pen borde = new Pen(Tema.VerdeMentaLinea, 1.6f) { DashStyle = DashStyle.Dash };
            g.DrawPath(borde, forma);
        }

        RectangleF disco = new RectangleF(cuerpo.X + 20, cuerpo.Y + 24, 52, 52);
        using (Pen aro = new Pen(Tema.VerdeMentaLinea, 1.6f) { DashStyle = DashStyle.Dot })
        {
            g.DrawEllipse(aro, disco);
        }

        using Font fuente = Tema.Texto(10f);
        using SolidBrush tinta = new SolidBrush(Tema.Mezclar(Tema.VerdeProfundo, Tema.VerdeMenta, 0.35f));
        using StringFormat formato = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString("Esperando jugador...", fuente, tinta, new RectangleF(cuerpo.X + 90, cuerpo.Y, cuerpo.Width - 96, cuerpo.Height), formato);
    }
}
