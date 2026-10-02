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
/// Tablero clásico dibujado con GDI+: 40 casillas en el perímetro (11 por lado contando las esquinas),
/// en el orden de la lista circular, con franjas de color, precios, dueños y fichas. Se escala con el
/// control y anima el movimiento casilla por casilla cuando llega un estado tras una tirada.
/// </summary>
/// <remarks>
/// Geometría: el lado mide 13 unidades; cada esquina ocupa 2×2 y cada casilla 1×2. Las casillas de los
/// lados se dibujan rotadas para que la franja de color quede siempre hacia el centro.
/// </remarks>
internal sealed class PanelTablero : Control
{
    private const float Unidades = 13f;
    private const int MaximoIdJugador = 8;

    private readonly Tablero _tablero = new Tablero();
    private readonly int[] _posiciones = new int[MaximoIdJugador + 1];
    private readonly Timer _temporizador = new Timer { Interval = 170 };
    private readonly ToolTip _ayuda = new ToolTip { InitialDelay = 300, ReshowDelay = 100 };
    private EstadoRed? _estado;
    private int _idAnimado;
    private int[] _ruta = new int[0];
    private int _paso;
    private int _casillaBajoCursor = -1;
    private string _aviso = string.Empty;

    /// <summary>
    /// Crea el panel con doble búfer para evitar el parpadeo.
    /// </summary>
    public PanelTablero()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Paleta.Mesa;
        _temporizador.Tick += (s, e) => AvanzarAnimacion();
    }

    /// <summary>
    /// Indica si hay una ficha moviéndose.
    /// </summary>
    public bool Animando => _temporizador.Enabled;

    /// <summary>
    /// Aviso destacado en el centro del tablero (por ejemplo, "presione el botón físico para lanzar los dados").
    /// </summary>
    public string Aviso
    {
        get => _aviso;
        set
        {
            string nuevo = value ?? string.Empty;
            if (nuevo != _aviso)
            {
                _aviso = nuevo;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Muestra un nuevo estado. Si <paramref name="animar"/> es verdadero y el estado trae casillas
    /// recorridas, la ficha de quien se movió avanza (o retrocede) casilla por casilla.
    /// </summary>
    /// <param name="estado">Estado recibido del servidor.</param>
    /// <param name="animar">Si el estado corresponde a una tirada nueva.</param>
    public void MostrarEstado(EstadoRed estado, bool animar)
    {
        _estado = estado;
        int? idMovimiento = estado.IdJugadorUltimoMovimiento;
        bool nuevaAnimacion = animar && idMovimiento.HasValue && idMovimiento.Value <= MaximoIdJugador
            && estado.CasillasRecorridas.Length > 0;

        foreach (EstadoJugador jugador in estado.Instantanea.Jugadores)
        {
            bool enAnimacion = (nuevaAnimacion && jugador.Id == idMovimiento) || (Animando && jugador.Id == _idAnimado);
            if (jugador.Id <= MaximoIdJugador && !enAnimacion)
            {
                _posiciones[jugador.Id] = jugador.Posicion;
            }
        }

        if (nuevaAnimacion)
        {
            _idAnimado = idMovimiento!.Value;
            _ruta = estado.CasillasRecorridas;
            _paso = 0;
            _temporizador.Stop();
            _temporizador.Start();
        }

        Invalidate();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temporizador.Dispose();
            _ayuda.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int casilla = CasillaEn(e.Location);
        if (casilla == _casillaBajoCursor)
        {
            return;
        }

        _casillaBajoCursor = casilla;
        _ayuda.SetToolTip(this, casilla < 0 ? string.Empty : DescribirCasilla(casilla));
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        RectangleF tablero = AreaTablero();
        float u = tablero.Width / Unidades;

        using (SolidBrush sombra = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
        {
            g.FillRectangle(sombra, tablero.X + (u * 0.12f), tablero.Y + (u * 0.12f), tablero.Width, tablero.Height);
        }

        using (SolidBrush fondo = new SolidBrush(Paleta.FondoTablero))
        {
            g.FillRectangle(fondo, tablero);
        }

        using Font fuenteNombre = new Font(Paleta.Fuente, Math.Max(u * 0.14f, 5.5f), FontStyle.Bold, GraphicsUnit.Pixel);
        using Font fuenteDetalle = new Font(Paleta.Fuente, Math.Max(u * 0.14f, 5.5f), FontStyle.Regular, GraphicsUnit.Pixel);
        using Font fuenteEsquina = new Font(Paleta.Fuente, Math.Max(u * 0.24f, 7f), FontStyle.Bold, GraphicsUnit.Pixel);
        using Pen linea = new Pen(Paleta.Linea, Math.Max(u * 0.025f, 1f));

        for (int i = 0; i < _tablero.Cantidad; i++)
        {
            RectangleF area = RectCasilla(tablero, i);
            if (i % 10 == 0)
            {
                DibujarEsquina(g, i, area, u, fuenteEsquina, fuenteDetalle, linea);
            }
            else
            {
                DibujarCasilla(g, i, area, u, fuenteNombre, fuenteDetalle, linea);
            }
        }

        DibujarCentro(g, new RectangleF(tablero.X + (2 * u), tablero.Y + (2 * u), 9 * u, 9 * u), u);
        DibujarFichas(g, tablero, u);

        using Pen borde = new Pen(Paleta.Linea, Math.Max(u * 0.05f, 1.5f));
        g.DrawRectangle(borde, tablero.X, tablero.Y, tablero.Width, tablero.Height);
        g.DrawRectangle(borde, tablero.X + (2 * u), tablero.Y + (2 * u), 9 * u, 9 * u);
    }

    private RectangleF AreaTablero()
    {
        float lado = Math.Max(Math.Min(ClientSize.Width, ClientSize.Height) - 24f, 130f);
        return new RectangleF((ClientSize.Width - lado) / 2f, (ClientSize.Height - lado) / 2f, lado, lado);
    }

    /// <summary>
    /// Rectángulo de la casilla <paramref name="i"/>: 0 (Salida) abajo a la derecha, y en sentido horario
    /// hacia la izquierda, arriba y la derecha, como en el tablero clásico.
    /// </summary>
    private static RectangleF RectCasilla(RectangleF t, int i)
    {
        float u = t.Width / Unidades;
        float x0 = t.X;
        float y0 = t.Y;
        if (i == 0)
        {
            return new RectangleF(x0 + (11 * u), y0 + (11 * u), 2 * u, 2 * u);
        }

        if (i < 10)
        {
            return new RectangleF(x0 + ((11 - i) * u), y0 + (11 * u), u, 2 * u);
        }

        if (i == 10)
        {
            return new RectangleF(x0, y0 + (11 * u), 2 * u, 2 * u);
        }

        if (i < 20)
        {
            return new RectangleF(x0, y0 + ((21 - i) * u), 2 * u, u);
        }

        if (i == 20)
        {
            return new RectangleF(x0, y0, 2 * u, 2 * u);
        }

        if (i < 30)
        {
            return new RectangleF(x0 + ((i - 19) * u), y0, u, 2 * u);
        }

        if (i == 30)
        {
            return new RectangleF(x0 + (11 * u), y0, 2 * u, 2 * u);
        }

        return new RectangleF(x0 + (11 * u), y0 + ((i - 29) * u), 2 * u, u);
    }

    private int CasillaEn(Point punto)
    {
        RectangleF tablero = AreaTablero();
        for (int i = 0; i < _tablero.Cantidad; i++)
        {
            if (RectCasilla(tablero, i).Contains(punto))
            {
                return i;
            }
        }

        return -1;
    }

    private void DibujarCasilla(Graphics g, int indice, RectangleF area, float u, Font fuenteNombre, Font fuenteDetalle, Pen linea)
    {
        Casilla casilla = _tablero.ObtenerCasilla(indice);
        Propiedad? propiedad = _tablero.BuscarPropiedad(indice);
        int? dueno = _estado?.PropietarioDe(indice);

        // Los lados izquierdo y derecho se rotan (texto a lo largo de la columna). La fila de arriba no se
        // gira 180° para que el texto no quede al revés: se dibuja derecha con la franja abajo (hacia el centro).
        bool franjaAbajo = indice > 20 && indice < 30;
        float angulo = indice < 10 ? 0f : indice < 20 ? 90f : indice < 30 ? 0f : 270f;

        GraphicsState guardado = g.Save();
        g.TranslateTransform(area.X + (area.Width / 2f), area.Y + (area.Height / 2f));
        g.RotateTransform(angulo);
        RectangleF local = new RectangleF(-u / 2f, -u, u, 2 * u);

        bool tieneFranja = propiedad != null && Paleta.TieneFranja(propiedad.Grupo);
        float alturaFranja = tieneFranja ? u * 0.42f : 0f;
        float alturaDueno = dueno.HasValue ? u * 0.2f : 0f;
        float alturaDetalle = u * 0.4f;

        RectangleF franja = franjaAbajo
            ? new RectangleF(local.X, local.Bottom - alturaFranja, local.Width, alturaFranja)
            : new RectangleF(local.X, local.Y, local.Width, alturaFranja);
        RectangleF banda = franjaAbajo
            ? new RectangleF(local.X + 1, local.Y + 1, local.Width - 2, alturaDueno - 1)
            : new RectangleF(local.X + 1, local.Bottom - alturaDueno, local.Width - 2, alturaDueno - 1);
        float inicioNombre = franjaAbajo
            ? local.Y + alturaDueno + (u * 0.08f)
            : local.Y + (tieneFranja ? alturaFranja + (u * 0.06f) : u * 0.1f);
        RectangleF areaDetalle = franjaAbajo
            ? new RectangleF(local.X, franja.Y - alturaDetalle - (u * 0.02f), local.Width, alturaDetalle)
            : new RectangleF(local.X, banda.Y - alturaDetalle - (u * 0.02f), local.Width, alturaDetalle);

        if (tieneFranja)
        {
            using SolidBrush color = new SolidBrush(Paleta.ColorGrupo(propiedad!.Grupo));
            g.FillRectangle(color, franja);
            float yLinea = franjaAbajo ? franja.Top : franja.Bottom;
            g.DrawLine(linea, franja.Left, yLinea, franja.Right, yLinea);
        }

        using StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisWord };
        RectangleF areaNombre = new RectangleF(local.X + (u * 0.03f), inicioNombre, local.Width - (u * 0.06f), areaDetalle.Y - inicioNombre);
        g.DrawString(Paleta.NombreCorto(casilla.Nombre), fuenteNombre, Brushes.Black, areaNombre, centrado);

        using (StringFormat abajo = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far })
        {
            g.DrawString(casilla.Detalle, fuenteDetalle, Brushes.Black, areaDetalle, abajo);
        }

        if (dueno.HasValue)
        {
            using SolidBrush colorDueno = new SolidBrush(ColorDeJugador(dueno.Value));
            g.FillRectangle(colorDueno, banda);
        }

        g.DrawRectangle(linea, local.X, local.Y, local.Width, local.Height);

        if (_estado?.Instantanea.IdPropiedadEnVenta == indice)
        {
            float grosor = Math.Max(u * 0.08f, 2f);
            using Pen resaltado = new Pen(Paleta.Resaltado, grosor);
            g.DrawRectangle(resaltado, local.X + (grosor / 2f), local.Y + (grosor / 2f), local.Width - grosor, local.Height - grosor);
        }

        g.Restore(guardado);
    }

    private void DibujarEsquina(Graphics g, int indice, RectangleF area, float u, Font fuenteTitulo, Font fuenteDetalle, Pen linea)
    {
        Casilla casilla = _tablero.ObtenerCasilla(indice);
        g.DrawRectangle(linea, area.X, area.Y, area.Width, area.Height);

        using StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        RectangleF areaTitulo = new RectangleF(area.X + (u * 0.1f), area.Y + (u * 0.2f), area.Width - (u * 0.2f), u * 0.9f);
        Color colorTitulo = indice == Tablero.IndiceSalida ? Paleta.RojoTitulo : Paleta.Linea;
        using (SolidBrush pincel = new SolidBrush(colorTitulo))
        {
            g.DrawString(casilla.Categoria.ToUpperInvariant(), fuenteTitulo, pincel, areaTitulo, centrado);
        }

        RectangleF areaDetalle = new RectangleF(area.X + (u * 0.1f), area.Y + (u * 1.1f), area.Width - (u * 0.2f), u * 0.4f);
        g.DrawString(casilla.Detalle, fuenteDetalle, Brushes.Black, areaDetalle, centrado);

        if (indice == Tablero.IndiceSalida)
        {
            // Flecha roja que indica el sentido del recorrido.
            float y = area.Y + (u * 1.7f);
            PointF[] flecha =
            {
                new PointF(area.X + (u * 0.25f), y),
                new PointF(area.X + (u * 0.7f), y - (u * 0.22f)),
                new PointF(area.X + (u * 0.7f), y - (u * 0.08f)),
                new PointF(area.X + (u * 1.75f), y - (u * 0.08f)),
                new PointF(area.X + (u * 1.75f), y + (u * 0.08f)),
                new PointF(area.X + (u * 0.7f), y + (u * 0.08f)),
                new PointF(area.X + (u * 0.7f), y + (u * 0.22f)),
            };
            using SolidBrush rojo = new SolidBrush(Paleta.RojoTitulo);
            g.FillPolygon(rojo, flecha);
        }
    }

    private void DibujarCentro(Graphics g, RectangleF centro, float u)
    {
        using StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // Título propio (no es el logotipo oficial).
        using (Font titulo = new Font("Georgia", Math.Max(u * 0.46f, 9f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (SolidBrush rojo = new SolidBrush(Paleta.RojoTitulo))
        using (StringFormat unaLinea = new StringFormat(centrado) { FormatFlags = StringFormatFlags.NoWrap })
        {
            g.DrawString("ESTRUCTURAS LINEALES", titulo, rojo, new RectangleF(centro.X, centro.Y + (u * 0.5f), centro.Width, u * 1.0f), unaLinea);
        }

        using (Font subtitulo = new Font(Paleta.Fuente, Math.Max(u * 0.27f, 7f), FontStyle.Italic, GraphicsUnit.Pixel))
        using (SolidBrush verde = new SolidBrush(Color.FromArgb(30, 80, 45)))
        {
            g.DrawString("Monopoly distribuido · Algoritmos y Estructuras de Datos 1 · TEC", subtitulo, verde,
                new RectangleF(centro.X, centro.Y + (u * 1.45f), centro.Width, u * 0.5f), centrado);
        }

        // Mazos de cartas.
        DibujarMazo(g, new RectangleF(centro.X + (u * 0.7f), centro.Y + (u * 6.4f), u * 2.9f, u * 1.8f), "CASUALIDAD", Color.FromArgb(247, 148, 29), u);
        DibujarMazo(g, new RectangleF(centro.Right - (u * 3.6f), centro.Y + (u * 6.4f), u * 2.9f, u * 1.8f), "ARCA COMUNAL", Color.FromArgb(70, 150, 210), u);

        // Dados de la última tirada, visibles para todos.
        TiradaDados? tirada = _estado?.Instantanea.UltimaTirada;
        float ladoDado = u * 1.25f;
        float yDados = centro.Y + (u * 2.6f);
        float xDados = centro.X + (centro.Width / 2f) - ladoDado - (u * 0.2f);
        DibujarDado(g, new RectangleF(xDados, yDados, ladoDado, ladoDado), tirada?.Dado1 ?? 0);
        DibujarDado(g, new RectangleF(xDados + ladoDado + (u * 0.4f), yDados, ladoDado, ladoDado), tirada?.Dado2 ?? 0);

        string textoDados = "Aún no se han lanzado los dados";
        if (tirada.HasValue && _estado?.IdJugadorUltimoMovimiento is int idTiro)
        {
            textoDados = $"{NombreDeJugador(idTiro)} sacó {tirada.Value.Dado1} + {tirada.Value.Dado2} = {tirada.Value.Total}";
        }

        using Font fuenteDados = new Font(Paleta.Fuente, Math.Max(u * 0.3f, 8f), FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString(textoDados, fuenteDados, Brushes.Black, new RectangleF(centro.X, yDados + ladoDado + (u * 0.15f), centro.Width, u * 0.5f), centrado);

        if (_estado != null && _estado.Instantanea.IdJugadorEnTurno is int enTurno)
        {
            using SolidBrush pincel = new SolidBrush(ColorDeJugador(enTurno));
            g.DrawString($"Turno {_estado.Instantanea.NumeroTurno}: le toca a {NombreDeJugador(enTurno)}", fuenteDados, pincel,
                new RectangleF(centro.X, yDados + ladoDado + (u * 0.65f), centro.Width, u * 0.5f), centrado);
        }

        if (_aviso.Length > 0)
        {
            // Lo que se espera ahora (botón físico, tarjeta en el lector...), en un recuadro visible para todos.
            RectangleF recuadro = new RectangleF(centro.X + (u * 0.5f), yDados + ladoDado + (u * 1.2f), centro.Width - u, u * 1.25f);
            using (GraphicsPath forma = Redondeado(recuadro, u * 0.15f))
            using (SolidBrush fondo = new SolidBrush(Color.FromArgb(235, 255, 248, 214)))
            using (Pen borde = new Pen(Paleta.RojoTitulo, Math.Max(u * 0.04f, 1.5f)))
            {
                g.FillPath(fondo, forma);
                g.DrawPath(borde, forma);
            }

            using Font fuenteAviso = new Font(Paleta.Fuente, Math.Max(u * 0.26f, 8f), FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush rojo = new SolidBrush(Paleta.RojoTitulo);
            g.DrawString(_aviso, fuenteAviso, rojo, RectangleF.Inflate(recuadro, -u * 0.12f, -u * 0.05f), centrado);
        }
    }

    private static void DibujarMazo(Graphics g, RectangleF area, string texto, Color color, float u)
    {
        using GraphicsPath forma = Redondeado(area, u * 0.15f);
        using (SolidBrush relleno = new SolidBrush(Color.FromArgb(235, 255, 255, 250)))
        {
            g.FillPath(relleno, forma);
        }

        using (Pen borde = new Pen(color, Math.Max(u * 0.07f, 1.5f)) { DashStyle = DashStyle.Dash })
        {
            g.DrawPath(borde, forma);
        }

        using Font fuente = new Font(Paleta.Fuente, Math.Max(u * 0.28f, 7f), FontStyle.Bold, GraphicsUnit.Pixel);
        using SolidBrush pincel = new SolidBrush(color);
        using StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(texto, fuente, pincel, area, centrado);
    }

    private static void DibujarDado(Graphics g, RectangleF area, int valor)
    {
        float radio = area.Width * 0.18f;
        using (GraphicsPath forma = Redondeado(area, radio))
        {
            g.FillPath(Brushes.White, forma);
            using Pen borde = new Pen(Color.FromArgb(40, 40, 40), Math.Max(area.Width * 0.04f, 1f));
            g.DrawPath(borde, forma);
        }

        if (valor < 1 || valor > 6)
        {
            return;
        }

        // Puntos en una cuadrícula de 3×3: esquinas, centro y medios de los costados.
        float paso = area.Width / 4f;
        float r = area.Width * 0.09f;
        bool centro = valor % 2 == 1;
        bool diagonal = valor >= 2;
        bool contraDiagonal = valor >= 4;
        bool medios = valor == 6;
        using SolidBrush punto = new SolidBrush(Color.FromArgb(25, 25, 25));

        void Punto(int columna, int fila)
        {
            g.FillEllipse(punto, area.X + (paso * columna) - r, area.Y + (paso * fila) - r, 2 * r, 2 * r);
        }

        if (centro)
        {
            Punto(2, 2);
        }

        if (diagonal)
        {
            Punto(1, 1);
            Punto(3, 3);
        }

        if (contraDiagonal)
        {
            Punto(3, 1);
            Punto(1, 3);
        }

        if (medios)
        {
            Punto(1, 2);
            Punto(3, 2);
        }
    }

    private void DibujarFichas(Graphics g, RectangleF tablero, float u)
    {
        if (_estado == null)
        {
            return;
        }

        EstadoJugador[] jugadores = _estado.Instantanea.Jugadores;
        float radio = u * 0.2f;
        float desplazamiento = u * 0.24f;
        using Font inicial = new Font(Paleta.Fuente, Math.Max(radio * 1.1f, 6f), FontStyle.Bold, GraphicsUnit.Pixel);
        using StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int k = 0; k < jugadores.Length; k++)
        {
            EstadoJugador jugador = jugadores[k];
            if (!jugador.Activo || jugador.Id > MaximoIdJugador)
            {
                continue;
            }

            int posicion = _posiciones[jugador.Id];
            RectangleF casilla = RectCasilla(tablero, posicion);
            PointF exterior = HaciaElBorde(posicion, u * 0.45f);
            float cx = casilla.X + (casilla.Width / 2f) + exterior.X + (k % 2 == 0 ? -desplazamiento : desplazamiento);
            float cy = casilla.Y + (casilla.Height / 2f) + exterior.Y + (k < 2 ? -desplazamiento : desplazamiento);
            RectangleF ficha = new RectangleF(cx - radio, cy - radio, 2 * radio, 2 * radio);

            if (_estado.Instantanea.IdJugadorEnTurno == jugador.Id)
            {
                using SolidBrush brillo = new SolidBrush(Color.FromArgb(150, Paleta.Resaltado));
                g.FillEllipse(brillo, RectangleF.Inflate(ficha, radio * 0.45f, radio * 0.45f));
            }

            using (SolidBrush sombra = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillEllipse(sombra, ficha.X + (radio * 0.15f), ficha.Y + (radio * 0.2f), ficha.Width, ficha.Height);
            }

            using (SolidBrush color = new SolidBrush(Paleta.ColorFicha(jugador.ColorFicha)))
            {
                g.FillEllipse(color, ficha);
            }

            using (Pen blanco = new Pen(Color.White, Math.Max(radio * 0.18f, 1f)))
            {
                g.DrawEllipse(blanco, RectangleF.Inflate(ficha, -radio * 0.12f, -radio * 0.12f));
            }

            g.DrawEllipse(Pens.Black, ficha);
            g.DrawString(jugador.Nombre.Substring(0, 1).ToUpperInvariant(), inicial, Brushes.White, ficha, centrado);
        }
    }

    /// <summary>
    /// Desplazamiento hacia la zona del precio de la casilla, para que las fichas no tapen el nombre.
    /// Abajo, a la izquierda y a la derecha el precio está hacia el borde exterior; en la fila de arriba
    /// (que se dibuja sin girar) está junto a la franja, hacia el centro. En las esquinas no se desplaza.
    /// </summary>
    private static PointF HaciaElBorde(int indice, float distancia)
    {
        if (indice % 10 == 0)
        {
            return PointF.Empty;
        }

        return indice < 10 ? new PointF(0, distancia)
            : indice < 20 ? new PointF(-distancia, 0)
            : indice < 30 ? new PointF(0, distancia)
            : new PointF(distancia, 0);
    }

    private void AvanzarAnimacion()
    {
        if (_paso < _ruta.Length)
        {
            _posiciones[_idAnimado] = _ruta[_paso++];
            Invalidate();
            return;
        }

        _temporizador.Stop();
        if (_estado != null)
        {
            foreach (EstadoJugador jugador in _estado.Instantanea.Jugadores)
            {
                if (jugador.Id <= MaximoIdJugador)
                {
                    _posiciones[jugador.Id] = jugador.Posicion;
                }
            }
        }

        Invalidate();
    }

    private string DescribirCasilla(int indice)
    {
        Casilla casilla = _tablero.ObtenerCasilla(indice);
        string texto = $"{casilla.Nombre}\n{casilla.Categoria}";
        if (casilla.Detalle.Length > 0)
        {
            texto += " · " + casilla.Detalle;
        }

        Propiedad? propiedad = _tablero.BuscarPropiedad(indice);
        if (propiedad != null)
        {
            int? dueno = _estado?.PropietarioDe(indice);
            texto += $"\nAlquiler base: {Formato.Dinero(propiedad.Alquiler)}";
            texto += dueno.HasValue ? $"\nDueño: {NombreDeJugador(dueno.Value)}" : "\nDisponible";
        }

        return texto;
    }

    private string NombreDeJugador(int id)
    {
        return _estado?.BuscarJugador(id)?.Nombre ?? $"Jugador {id}";
    }

    private Color ColorDeJugador(int id)
    {
        EstadoJugador? jugador = _estado?.BuscarJugador(id);
        return jugador == null ? Color.Gray : Paleta.ColorFicha(jugador.ColorFicha);
    }

    /// <summary>
    /// Crea un rectángulo con esquinas redondeadas.
    /// </summary>
    internal static GraphicsPath Redondeado(RectangleF area, float radio)
    {
        float d = Math.Max(radio * 2f, 1f);
        GraphicsPath camino = new GraphicsPath();
        camino.AddArc(area.X, area.Y, d, d, 180, 90);
        camino.AddArc(area.Right - d, area.Y, d, d, 270, 90);
        camino.AddArc(area.Right - d, area.Bottom - d, d, d, 0, 90);
        camino.AddArc(area.X, area.Bottom - d, d, d, 90, 90);
        camino.CloseFigure();
        return camino;
    }
}
