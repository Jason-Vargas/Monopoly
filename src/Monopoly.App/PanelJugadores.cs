using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Una tarjeta por jugador: ficha, nombre, saldo grande, cantidad de propiedades (con sus colores), casilla
/// actual e indicador de tarjeta RFID. El jugador en turno se resalta con borde dorado e indicador; los
/// eliminados se ven en gris con la etiqueta "En bancarrota". Al hacer clic en un jugador se despliega la
/// lista de sus propiedades agrupadas por color.
/// </summary>
internal sealed class PanelJugadores : Control
{
    /// <summary>Alto de cada tarjeta.</summary>
    public const int AltoTarjeta = 118;

    /// <summary>Separación entre tarjetas.</summary>
    public const int Separacion = 6;

    private readonly Tablero _tablero = new Tablero();
    private EstadoRed? _estado;
    private int? _miId;
    private int _encima = -1;

    /// <summary>
    /// Crea el panel con doble búfer.
    /// </summary>
    public PanelJugadores()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    /// <summary>
    /// Alto necesario para mostrar cuatro jugadores.
    /// </summary>
    public static int AltoPreferido => (Juego.MaximoJugadores * (AltoTarjeta + Separacion)) + Separacion;

    /// <summary>
    /// Muestra el estado recibido.
    /// </summary>
    /// <param name="estado">Estado de la partida.</param>
    /// <param name="miId">Id del jugador de esta ventana.</param>
    public void MostrarEstado(EstadoRed estado, int? miId)
    {
        _estado = estado;
        _miId = miId;
        Invalidate();
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int indice = TarjetaEn(e.Location);
        if (indice != _encima)
        {
            _encima = indice;
            Cursor = indice >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(EventArgs e)
    {
        _encima = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int indice = TarjetaEn(e.Location);
        if (indice < 0 || _estado == null)
        {
            return;
        }

        // Lista desplegable con las propiedades del jugador, agrupadas por color.
        EstadoJugador jugador = _estado.Instantanea.Jugadores[indice];
        ListaPropiedadesJugador lista = new ListaPropiedadesJugador(jugador, Paleta.ColorFicha(jugador.ColorFicha));
        ToolStripDropDown desplegable = new ToolStripDropDown { Padding = Padding.Empty, DropShadowEnabled = true };
        desplegable.Items.Add(new ToolStripControlHost(lista) { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = lista.Size });
        desplegable.Closed += (s, a) => BeginInvoke(new Action(desplegable.Dispose));
        Rectangle tarjeta = RectTarjeta(indice);
        desplegable.Show(this, new Point(Math.Max(0, tarjeta.Right - lista.Width), tarjeta.Bottom - 6));
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        if (_estado == null)
        {
            return;
        }

        EstadoJugador[] jugadores = _estado.Instantanea.Jugadores;
        for (int k = 0; k < jugadores.Length; k++)
        {
            DibujarTarjeta(g, jugadores[k], RectTarjeta(k), k == _encima);
        }
    }

    private Rectangle RectTarjeta(int indice)
    {
        return new Rectangle(Separacion + 4, Separacion + (indice * (AltoTarjeta + Separacion)), Width - (2 * Separacion) - 8, AltoTarjeta);
    }

    private int TarjetaEn(Point punto)
    {
        int cantidad = _estado?.Instantanea.Jugadores.Length ?? 0;
        for (int i = 0; i < cantidad; i++)
        {
            if (RectTarjeta(i).Contains(punto))
            {
                return i;
            }
        }

        return -1;
    }

    private void DibujarTarjeta(Graphics g, EstadoJugador j, Rectangle tarjeta, bool encima)
    {
        bool enTurno = _estado!.Instantanea.IdJugadorEnTurno == j.Id && _estado.Instantanea.Estado == EstadoPartida.EnCurso;
        bool conectado = _estado.EstaConectado(j.Id);
        Color colorJugador = j.Activo ? Paleta.ColorFicha(j.ColorFicha) : Color.FromArgb(150, 150, 150);

        Dibujo.Sombra(g, tarjeta, 12, 5, encima || enTurno ? 46 : 28, 2f);
        using (GraphicsPath forma = Dibujo.Redondeado(tarjeta, 12))
        {
            Color fondo = !j.Activo ? Color.FromArgb(228, 226, 220) : enTurno ? Color.FromArgb(255, 249, 226) : Tema.Marfil;
            using SolidBrush relleno = new SolidBrush(fondo);
            g.FillPath(relleno, forma);

            // Franja del color del jugador a la izquierda.
            GraphicsState estado = g.Save();
            g.SetClip(forma);
            using (SolidBrush franja = new SolidBrush(colorJugador))
            {
                g.FillRectangle(franja, tarjeta.X, tarjeta.Y, 7, tarjeta.Height);
            }

            g.Restore(estado);
            Color colorBorde = enTurno ? Tema.Dorado : encima ? Tema.VerdeProfundo : Tema.Borde;
            using Pen borde = new Pen(colorBorde, enTurno ? 3f : 1.2f);
            g.DrawPath(borde, forma);
        }

        if (enTurno)
        {
            // Indicador del turno: triángulo dorado que apunta a la tarjeta.
            float cy = tarjeta.Y + (tarjeta.Height / 2f);
            using SolidBrush dorado = new SolidBrush(Tema.Dorado);
            g.FillPolygon(dorado, new[] { new PointF(tarjeta.X - 7, cy - 9), new PointF(tarjeta.X + 3, cy), new PointF(tarjeta.X - 7, cy + 9) });
        }

        RectangleF disco = new RectangleF(tarjeta.X + 16, tarjeta.Y + 14, 54, 54);
        DibujoFicha.DibujarEnDisco(g, j.FormaFicha, disco, colorJugador);

        float x = tarjeta.X + 82;
        Color tinta = j.Activo ? Tema.Tinta : Tema.TintaDeshabilitada;
        using (Font nombre = Tema.Texto(12f, FontStyle.Bold))
        using (SolidBrush pincel = new SolidBrush(tinta))
        using (StringFormat corte = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        {
            g.DrawString(j.Nombre, nombre, pincel, new RectangleF(x, tarjeta.Y + 8, tarjeta.Width - 250, 30), corte);
        }

        // Saldo grande a la derecha (o "En bancarrota").
        using (StringFormat derecha = new StringFormat { Alignment = StringAlignment.Far })
        {
            if (j.Activo)
            {
                using Font saldo = Tema.Texto(18f, FontStyle.Bold);
                using SolidBrush colorSaldo = new SolidBrush(j.Saldo > 0 ? Tema.Exito : Tema.Error);
                g.DrawString(Formato.Dinero(j.Saldo), saldo, colorSaldo, new RectangleF(tarjeta.X, tarjeta.Y + 4, tarjeta.Width - 14, 42), derecha);
            }
            else
            {
                Insignia(g, "En bancarrota", Tema.Error, tarjeta.Right - 14, tarjeta.Y + 12, true);
            }
        }

        // Insignias: usted, en turno, desconectado, pierde turnos.
        float xInsignia = x;
        if (j.Id == _miId)
        {
            xInsignia += Insignia(g, "Usted", Tema.VerdeProfundo, xInsignia, tarjeta.Y + 42, false) + 5;
        }

        if (enTurno)
        {
            xInsignia += Insignia(g, "En turno", Tema.Advertencia, xInsignia, tarjeta.Y + 42, false) + 5;
        }

        if (j.Activo && !conectado)
        {
            xInsignia += Insignia(g, "Desconectado", Tema.Error, xInsignia, tarjeta.Y + 42, false) + 5;
        }

        if (j.Activo && j.TurnosPorPerder > 0)
        {
            Insignia(g, $"Pierde {j.TurnosPorPerder} turno(s)", Tema.Informacion, xInsignia, tarjeta.Y + 42, false);
        }

        // Propiedades, casilla actual y tarjeta RFID.
        using (Font detalle = Tema.Texto(9f))
        using (SolidBrush suave = new SolidBrush(j.Activo ? Tema.TintaSuave : Tema.TintaDeshabilitada))
        using (StringFormat corte = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        {
            string propiedades = j.IdsPropiedades.Length == 1 ? "1 propiedad" : $"{j.IdsPropiedades.Length} propiedades";
            string lugar = j.Activo ? $" · en {Paleta.NombreCorto(_tablero.ObtenerCasilla(j.Posicion).Nombre)}" : string.Empty;
            g.DrawString(propiedades + lugar, detalle, suave, new RectangleF(x, tarjeta.Y + 70, tarjeta.Width - 250, 24), corte);
        }

        DibujarRfid(g, j.TieneTarjetaFisica, new RectangleF(tarjeta.Right - 160, tarjeta.Y + 70, 148, 24), j.Activo);
        DibujarColoresPropiedades(g, j.IdsPropiedades, new RectangleF(x, tarjeta.Y + 98, tarjeta.Width - 96, 12));
    }

    private static void DibujarRfid(Graphics g, bool vinculada, RectangleF area, bool activo)
    {
        Color color = !activo ? Tema.TintaDeshabilitada : vinculada ? Tema.Exito : Tema.Advertencia;
        RectangleF tarjeta = new RectangleF(area.X, area.Y + 4, 22, 16);
        using (GraphicsPath forma = Dibujo.Redondeado(tarjeta, 3))
        using (Pen borde = new Pen(color, 1.5f))
        {
            g.DrawPath(borde, forma);
        }

        if (vinculada)
        {
            Dibujo.Verificacion(g, RectangleF.Inflate(tarjeta, -4, -2), color, 1.8f);
        }

        using Font fuente = Tema.Texto(8.5f, FontStyle.Bold);
        using SolidBrush tinta = new SolidBrush(color);
        g.DrawString(vinculada ? "RFID vinculada" : "Sin RFID", fuente, tinta, area.X + 28, area.Y);
    }

    private void DibujarColoresPropiedades(Graphics g, int[] propiedades, RectangleF area)
    {
        const float lado = 12f;
        const float espacio = 3f;
        float x = area.X;
        foreach (int id in propiedades)
        {
            if (x + lado > area.Right)
            {
                break;
            }

            Propiedad? propiedad = _tablero.BuscarPropiedad(id);
            if (propiedad == null)
            {
                continue;
            }

            RectangleF cuadro = new RectangleF(x, area.Y, lado, lado);
            using (GraphicsPath forma = Dibujo.Redondeado(cuadro, 2.5f))
            using (SolidBrush color = new SolidBrush(Paleta.ColorGrupo(propiedad.Grupo)))
            using (Pen borde = new Pen(Color.FromArgb(120, 0, 0, 0), 1f))
            {
                g.FillPath(color, forma);
                g.DrawPath(borde, forma);
            }

            x += lado + espacio;
        }
    }

    /// <summary>
    /// Dibuja una insignia redondeada y devuelve su ancho. Si <paramref name="alineadaDerecha"/>, la
    /// coordenada X es el borde derecho.
    /// </summary>
    private static float Insignia(Graphics g, string texto, Color color, float x, float y, bool alineadaDerecha)
    {
        using Font fuente = Tema.Texto(8f, FontStyle.Bold);
        SizeF medida = g.MeasureString(texto, fuente);
        float ancho = medida.Width + 10;
        RectangleF caja = new RectangleF(alineadaDerecha ? x - ancho : x, y, ancho, 24);
        using (GraphicsPath forma = Dibujo.Redondeado(caja, 12f))
        {
            using SolidBrush fondo = new SolidBrush(Color.FromArgb(36, color));
            g.FillPath(fondo, forma);
        }

        Dibujo.TextoCentrado(g, texto, fuente, color, caja);
        return ancho;
    }
}
