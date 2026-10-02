using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Tarjetas de los jugadores: ficha, nombre, saldo, casilla, estado (activo o eliminado), patrimonio y
/// propiedades (cuadritos del color de su grupo). Resalta al jugador en turno.
/// </summary>
internal sealed class PanelJugadores : Control
{
    /// <summary>Alto de cada tarjeta.</summary>
    public const int AltoTarjeta = 74;

    /// <summary>Separación entre tarjetas.</summary>
    public const int Separacion = 6;

    private readonly Tablero _tablero = new Tablero();
    private EstadoRed? _estado;
    private int? _miId;

    /// <summary>
    /// Crea el panel con doble búfer.
    /// </summary>
    public PanelJugadores()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Paleta.FondoPanel;
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
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);
        if (_estado == null)
        {
            return;
        }

        using Font negrita = new Font(Paleta.Fuente, 11f, FontStyle.Bold);
        using Font normal = new Font(Paleta.Fuente, 8.5f);
        using StringFormat derecha = new StringFormat { Alignment = StringAlignment.Far };

        EstadoJugador[] jugadores = _estado.Instantanea.Jugadores;
        for (int k = 0; k < jugadores.Length; k++)
        {
            EstadoJugador j = jugadores[k];
            Rectangle tarjeta = new Rectangle(Separacion, Separacion + (k * (AltoTarjeta + Separacion)), Width - (2 * Separacion) - 1, AltoTarjeta);
            bool enTurno = _estado.Instantanea.IdJugadorEnTurno == j.Id;

            Color fondo = !j.Activo ? Color.FromArgb(222, 222, 222) : enTurno ? Color.FromArgb(255, 246, 205) : Color.White;
            using (GraphicsPath forma = PanelTablero.Redondeado(tarjeta, 8))
            {
                using SolidBrush relleno = new SolidBrush(fondo);
                g.FillPath(relleno, forma);
                using Pen borde = new Pen(enTurno ? Paleta.Resaltado : Color.FromArgb(170, 170, 160), enTurno ? 3f : 1f);
                g.DrawPath(borde, forma);
            }

            Rectangle ficha = new Rectangle(tarjeta.X + 8, tarjeta.Y + 8, 32, 32);
            Estilo.DibujoFicha.DibujarEnDisco(g, j.FormaFicha, ficha, j.Activo ? Paleta.ColorFicha(j.ColorFicha) : Color.Gray, false);

            string nombre = j.Nombre + (j.Id == _miId ? " (usted)" : string.Empty) + (enTurno ? "  ◄ turno" : string.Empty);
            g.DrawString(nombre, negrita, Brushes.Black, tarjeta.X + 44, tarjeta.Y + 6);
            using (SolidBrush colorSaldo = new SolidBrush(j.Saldo > 0 ? Color.FromArgb(20, 110, 40) : Color.FromArgb(170, 20, 20)))
            {
                g.DrawString(Formato.Dinero(j.Saldo), negrita, colorSaldo, new RectangleF(tarjeta.X, tarjeta.Y + 4, tarjeta.Width - 10, 28), derecha);
            }

            bool conectado = _estado.EstaConectado(j.Id);
            string situacion = !j.Activo ? "ELIMINADO"
                : !conectado ? "DESCONECTADO"
                : j.TurnosPorPerder > 0 ? $"Activo · pierde {j.TurnosPorPerder} turno(s)"
                : "Activo";
            string linea2 = $"{situacion} · en {Paleta.NombreCorto(_tablero.ObtenerCasilla(j.Posicion).Nombre)}";
            Color colorSituacion = !j.Activo ? Color.FromArgb(150, 20, 20) : !conectado ? Color.FromArgb(200, 100, 0) : Color.FromArgb(60, 60, 60);
            using (SolidBrush gris = new SolidBrush(colorSituacion))
            {
                g.DrawString(linea2, normal, gris, tarjeta.X + 44, tarjeta.Y + 27);
            }

            g.DrawString($"Patrimonio {Formato.Dinero(j.Patrimonio)} · {j.IdsPropiedades.Length} propiedad(es){(j.TieneTarjetaFisica ? " · tarjeta RFID" : string.Empty)}",
                normal, Brushes.DimGray, tarjeta.X + 44, tarjeta.Y + 43);

            DibujarPropiedades(g, j.IdsPropiedades, new Rectangle(tarjeta.X + 44, tarjeta.Y + 60, tarjeta.Width - 54, 14));
        }
    }

    private void DibujarPropiedades(Graphics g, int[] propiedades, Rectangle area)
    {
        const int lado = 12;
        const int espacio = 3;
        int x = area.X;
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

            Rectangle cuadro = new Rectangle(x, area.Y, lado, lado);
            using (SolidBrush color = new SolidBrush(Paleta.ColorGrupo(propiedad.Grupo)))
            {
                g.FillRectangle(color, cuadro);
            }

            g.DrawRectangle(Pens.Black, cuadro);
            x += lado + espacio;
        }
    }
}
