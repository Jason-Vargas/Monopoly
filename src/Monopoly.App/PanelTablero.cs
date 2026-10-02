using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Tablero dibujado con GDI+ con el aspecto de un tablero real: 40 casillas en el perímetro, en el orden
/// de la lista circular, con franja de color arriba, nombre y precio, y el texto girado según el lado para
/// que se lea desde afuera. Esquinas grandes ilustradas, íconos en ferrocarriles, servicios, impuestos y
/// cartas, centro con el logotipo en diagonal, los dos mazos, los dados y el aviso del turno. Las fichas se
/// acomodan para no taparse, las propiedades compradas llevan el color del dueño y, al pasar el mouse por
/// una propiedad, aparece su tarjeta de título. Se escala con el control manteniendo la proporción.
/// </summary>
/// <remarks>
/// Geometría: el lado mide 13 unidades; cada esquina ocupa 2×2 y cada casilla 1×2. Cada casilla se dibuja
/// en un sistema local (franja hacia el centro del tablero) y se gira 0°, 90°, 180° o 270° según su lado.
/// Rendimiento: los rectángulos de las casillas se precalculan al cambiar el tamaño; la parte estática se
/// guarda en dos imágenes (la base, que solo cambia con el tamaño, y el fondo, que se regenera con cada
/// estado o aviso) y encima se dibujan los dados, el resaltado del cursor, las fichas y la tarjeta
/// (<see cref="TarjetaCasilla"/>). El mouse solo repinta al cambiar de casilla y las animaciones invalidan
/// únicamente su zona.
/// </remarks>
internal sealed class PanelTablero : Control
{
    private const float Unidades = 13f;
    private const int MaximoIdJugador = 8;
    private const int DuracionDadosMs = 650;
    private const int RetrasoTarjetaMs = 150;
    private const int CantidadCasillas = 40;

    private static readonly Color ColorCasualidad = Color.FromArgb(236, 140, 50);
    private static readonly Color ColorArca = Color.FromArgb(96, 170, 222);

    private readonly Tablero _tablero = new Tablero();
    private readonly int[] _posiciones = new int[MaximoIdJugador + 1];
    private readonly Timer _temporizador = new Timer { Interval = 170 };
    private readonly Timer _temporizadorDados = new Timer { Interval = 30 };
    private readonly Timer _temporizadorTarjeta = new Timer { Interval = RetrasoTarjetaMs };
    private readonly Random _azar = new Random();
    private readonly TarjetaCasilla _tarjeta = new TarjetaCasilla();

    // Geometría precalculada al cambiar de tamaño: área del tablero, unidad y rectángulo de cada casilla.
    private readonly RectangleF[] _rectCasillas = new RectangleF[CantidadCasillas];
    private RectangleF _areaTablero;
    private RectangleF _centro;
    private Rectangle _zonaDados;
    private float _u;

    // Imágenes en caché y recursos reutilizados. La base (mesa, sombra, esquinas, logotipo y mazos) solo
    // cambia con el tamaño; el fondo agrega lo que depende del estado (dueños, turno y aviso).
    private readonly Font?[] _fuentesNombre = new Font?[CantidadCasillas];
    private readonly SolidBrush _fondoCasilla = new SolidBrush(Tema.VerdeMentaClaro);
    private readonly SolidBrush _tinta = new SolidBrush(Tema.Tinta);
    private readonly SolidBrush _brilloCursor = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
    private readonly SolidBrush _brilloTurno = new SolidBrush(Color.FromArgb(150, Tema.Dorado));
    private readonly SolidBrush _verdeProfundo = new SolidBrush(Tema.VerdeProfundo);
    private readonly StringFormat _centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    private readonly StringFormat _arriba = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisWord };
    private readonly StringFormat _abajo = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far };
    private Bitmap? _base;
    private Bitmap? _fondo;
    private bool _fondoValido;
    private Font? _fuenteDetalle;
    private Font? _fuenteDados;
    private Pen? _lineaCasilla;
    private Pen? _bordeCursor;

    private EstadoRed? _estado;
    private int _idAnimado;
    private int[] _ruta = new int[0];
    private int _paso;
    private int _casillaBajoCursor = -1;
    private int _casillaTarjeta = -1;
    private Rectangle _rectTarjeta;
    private string _aviso = string.Empty;
    private long _inicioDados;

    /// <summary>
    /// Crea el panel con doble búfer para evitar el parpadeo.
    /// </summary>
    public PanelTablero()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Paleta.Mesa;
        _temporizador.Tick += (s, e) => AvanzarAnimacion();
        _temporizadorDados.Tick += (s, e) =>
        {
            if (Environment.TickCount64 - _inicioDados > DuracionDadosMs)
            {
                _temporizadorDados.Stop();
            }

            // Solo la zona de los dados: el resto del tablero no cambia mientras giran.
            Invalidate(_zonaDados);
        };
        _temporizadorTarjeta.Tick += (s, e) =>
        {
            _temporizadorTarjeta.Stop();
            MostrarTarjeta();
        };
        RecalcularGeometria();
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
                InvalidarFondo();
            }
        }
    }

    /// <summary>
    /// Muestra un nuevo estado. Si <paramref name="animar"/> es verdadero y el estado trae casillas
    /// recorridas, los dados giran un momento y la ficha de quien se movió avanza (o retrocede) casilla
    /// por casilla.
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

        if (animar && estado.Instantanea.UltimaTirada.HasValue)
        {
            _inicioDados = Environment.TickCount64;
            _temporizadorDados.Start();
        }

        if (nuevaAnimacion)
        {
            _idAnimado = idMovimiento!.Value;
            _ruta = estado.CasillasRecorridas;
            _paso = 0;
            _temporizador.Stop();
            _temporizador.Start();
        }

        // Cambiaron los dueños, el turno o la propiedad en venta: se regenera la imagen estática.
        if (_casillaTarjeta >= 0)
        {
            PrepararTarjeta(_casillaTarjeta);
        }

        InvalidarFondo();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temporizador.Dispose();
            _temporizadorDados.Dispose();
            _temporizadorTarjeta.Dispose();
            _tarjeta.Dispose();
            LiberarRecursosDeTamanio();
            _fondoCasilla.Dispose();
            _tinta.Dispose();
            _brilloCursor.Dispose();
            _brilloTurno.Dispose();
            _verdeProfundo.Dispose();
            _centrado.Dispose();
            _arriba.Dispose();
            _abajo.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        OcultarTarjeta();
        RecalcularGeometria();
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int casilla = CasillaEn(e.Location);
        if (casilla == _casillaBajoCursor)
        {
            // Sigue sobre la misma casilla: no hay nada que redibujar.
            return;
        }

        CambiarCasillaBajoCursor(casilla);
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        CambiarCasillaBajoCursor(-1);
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle recorte = e.ClipRectangle;
        if (!_fondoValido || _fondo == null)
        {
            GenerarFondo();
        }

        // La parte estática sale de la imagen en caché: solo se copia la región que hay que repintar.
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(_fondo!, recorte, recorte, GraphicsUnit.Pixel);
        g.CompositingMode = CompositingMode.SourceOver;
        Dibujo.Calidad(g);

        if (recorte.IntersectsWith(_zonaDados))
        {
            DibujarDados(g);
        }

        if (_casillaBajoCursor >= 0)
        {
            DibujarResaltadoCursor(g, _casillaBajoCursor);
        }

        DibujarFichas(g, recorte);
        if (_casillaTarjeta >= 0 && recorte.IntersectsWith(_rectTarjeta))
        {
            _tarjeta.Dibujar(g, _rectTarjeta.Location);
        }
    }

    // ------------------------------------------------------------------ caché de la parte estática

    /// <summary>
    /// Marca la imagen estática como desactualizada y repinta el tablero (al llegar un estado nuevo o
    /// cambiar el aviso).
    /// </summary>
    private void InvalidarFondo()
    {
        _fondoValido = false;
        Invalidate();
    }

    /// <summary>
    /// Dibuja en la imagen en caché todo lo que no cambia con el mouse ni con las animaciones: copia la base
    /// y le agrega las casillas con sus dueños, el turno, el aviso y los bordes.
    /// </summary>
    private void GenerarFondo()
    {
        Size tamanio = new Size(Math.Max(ClientSize.Width, 1), Math.Max(ClientSize.Height, 1));
        if (_base == null || _base.Size != tamanio)
        {
            _base?.Dispose();
            _base = new Bitmap(tamanio.Width, tamanio.Height, PixelFormat.Format32bppPArgb);
            GenerarBase(_base);
        }

        if (_fondo == null || _fondo.Size != tamanio)
        {
            _fondo?.Dispose();
            _fondo = new Bitmap(tamanio.Width, tamanio.Height, PixelFormat.Format32bppPArgb);
        }

        using Graphics g = Graphics.FromImage(_fondo);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImageUnscaled(_base, 0, 0);
        g.CompositingMode = CompositingMode.SourceOver;
        Dibujo.Calidad(g);

        float u = _u;
        for (int i = 0; i < _tablero.Cantidad; i++)
        {
            if (i % 10 != 0)
            {
                DibujarCasilla(g, i, _rectCasillas[i], u, _lineaCasilla!);
            }
        }

        DibujarTurnoYAviso(g, _centro, u);
        using (Pen borde = new Pen(Color.FromArgb(30, 40, 34), Math.Max(u * 0.05f, 1.5f)))
        {
            g.DrawRectangle(borde, _areaTablero.X, _areaTablero.Y, _areaTablero.Width, _areaTablero.Height);
            g.DrawRectangle(borde, _centro.X, _centro.Y, _centro.Width, _centro.Height);
        }

        _fondoValido = true;
    }

    /// <summary>
    /// Parte que solo depende del tamaño: la mesa, la sombra del tablero, las cuatro esquinas, el
    /// logotipo y los mazos.
    /// </summary>
    private void GenerarBase(Bitmap imagen)
    {
        using Graphics g = Graphics.FromImage(imagen);
        Dibujo.Calidad(g);
        Rectangle todo = new Rectangle(Point.Empty, imagen.Size);
        using (LinearGradientBrush mesa = new LinearGradientBrush(todo, Color.FromArgb(36, 96, 66), Color.FromArgb(18, 58, 40), 90f))
        {
            g.FillRectangle(mesa, todo);
        }

        float u = _u;
        Dibujo.Sombra(g, _areaTablero, u * 0.1f, 10, 90, u * 0.12f);
        using (SolidBrush fondo = new SolidBrush(Tema.VerdeMenta))
        {
            g.FillRectangle(fondo, _areaTablero);
        }

        for (int i = 0; i < _tablero.Cantidad; i += 10)
        {
            DibujarEsquina(g, i, _rectCasillas[i], u, _lineaCasilla!);
        }

        DibujarLogotipoYMazos(g, _centro, u);
    }

    // ------------------------------------------------------------------ geometría

    /// <summary>
    /// Precalcula el área del tablero, la unidad, el rectángulo de las 40 casillas y la zona de los dados,
    /// y recrea los recursos que dependen del tamaño. Se llama solo al cambiar el tamaño del control.
    /// </summary>
    private void RecalcularGeometria()
    {
        float lado = Math.Max(Math.Min(ClientSize.Width, ClientSize.Height) - 28f, 130f);
        _areaTablero = new RectangleF((ClientSize.Width - lado) / 2f, (ClientSize.Height - lado) / 2f, lado, lado);
        _u = lado / Unidades;
        float u = _u;
        for (int i = 0; i < CantidadCasillas; i++)
        {
            _rectCasillas[i] = RectCasilla(_areaTablero, i);
        }

        _centro = new RectangleF(_areaTablero.X + (2 * u), _areaTablero.Y + (2 * u), 9 * u, 9 * u);

        // Dados girados (hasta ~0,75 de su lado desde el centro) y la línea de texto de la tirada.
        float yDados = _centro.Y + (u * 5.6f);
        _zonaDados = Rectangle.Ceiling(new RectangleF(_centro.X, yDados - (u * 0.8f), _centro.Width, u * 1.55f));
        _zonaDados.Inflate(2, 2);

        LiberarRecursosDeTamanio();
        _fuenteDetalle = new Font(Fuentes.Texto, Math.Max(u * 0.15f, 5f), FontStyle.Bold, GraphicsUnit.Pixel);
        _fuenteDados = new Font(Fuentes.Texto, Math.Max(u * 0.26f, 8f), FontStyle.Bold, GraphicsUnit.Pixel);
        _lineaCasilla = new Pen(Color.FromArgb(40, 54, 44), Math.Max(u * 0.02f, 1f));
        _bordeCursor = new Pen(Tema.Rojo, Math.Max(u * 0.04f, 1.5f));
        _fondoValido = false;
    }

    /// <summary>
    /// Libera las fuentes, los lápices y la imagen que dependen del tamaño del tablero.
    /// </summary>
    private void LiberarRecursosDeTamanio()
    {
        for (int i = 0; i < _fuentesNombre.Length; i++)
        {
            _fuentesNombre[i]?.Dispose();
            _fuentesNombre[i] = null;
        }

        _fuenteDetalle?.Dispose();
        _fuenteDados?.Dispose();
        _lineaCasilla?.Dispose();
        _bordeCursor?.Dispose();
        _fondo?.Dispose();
        _fondo = null;
        _base?.Dispose();
        _base = null;
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

    /// <summary>
    /// Giro de la casilla: la franja (arriba en el sistema local) queda hacia el centro y el texto se lee
    /// desde afuera del tablero, como en el juego físico.
    /// </summary>
    private static float AnguloLado(int indice)
    {
        return indice < 10 ? 0f : indice < 20 ? 90f : indice < 30 ? 180f : 270f;
    }

    /// <summary>
    /// Casilla bajo un punto, con los rectángulos precalculados (sin recorrer la lista del tablero).
    /// </summary>
    private int CasillaEn(Point punto)
    {
        if (!_areaTablero.Contains(punto) || _centro.Contains(punto))
        {
            return -1;
        }

        for (int i = 0; i < CantidadCasillas; i++)
        {
            if (_rectCasillas[i].Contains(punto))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Región a repintar de una casilla, con margen para el brillo de las fichas que sobresale un poco.
    /// </summary>
    private void InvalidarCasilla(int indice)
    {
        if (indice < 0 || indice >= CantidadCasillas)
        {
            return;
        }

        RectangleF area = RectangleF.Inflate(_rectCasillas[indice], _u * 0.3f, _u * 0.3f);
        Invalidate(Rectangle.Ceiling(area));
    }

    // ------------------------------------------------------------------ cursor y tarjeta de información

    /// <summary>
    /// El cursor pasó a otra casilla (o salió): se repintan solo la casilla anterior, la nueva y la tarjeta,
    /// y la tarjeta de la nueva casilla aparece tras un breve retraso para que no parpadee al mover el
    /// mouse rápido por el tablero.
    /// </summary>
    private void CambiarCasillaBajoCursor(int casilla)
    {
        if (casilla == _casillaBajoCursor)
        {
            return;
        }

        _temporizadorTarjeta.Stop();
        OcultarTarjeta();
        InvalidarCasilla(_casillaBajoCursor);
        _casillaBajoCursor = casilla;
        InvalidarCasilla(casilla);
        if (casilla >= 0)
        {
            _temporizadorTarjeta.Start();
        }
    }

    /// <summary>
    /// Muestra la tarjeta de la casilla bajo el cursor, junto a él y sin salirse del control.
    /// </summary>
    private void MostrarTarjeta()
    {
        if (_casillaBajoCursor < 0 || IsDisposed)
        {
            return;
        }

        PrepararTarjeta(_casillaBajoCursor);
        Size imagen = _tarjeta.Tamanio;
        int margen = TarjetaCasilla.Margen;
        int ancho = imagen.Width - (2 * margen);
        int alto = imagen.Height - (2 * margen);

        // Abajo a la derecha del cursor; si no cabe, se pone arriba.
        Point cursor = PointToClient(Cursor.Position);
        int x = Math.Min(cursor.X + 18, ClientSize.Width - ancho - 8);
        int y = Math.Min(cursor.Y + 18, ClientSize.Height - alto - 8);
        if (x < cursor.X && y < cursor.Y + 18)
        {
            y = Math.Max(8, cursor.Y - alto - 12);
        }

        _rectTarjeta = new Rectangle(x - margen, y - margen + 4, imagen.Width, imagen.Height);
        Invalidate(_rectTarjeta);
    }

    /// <summary>
    /// Vuelve a dibujar el contenido de la tarjeta (única) para una casilla, con el último estado.
    /// </summary>
    private void PrepararTarjeta(int indice)
    {
        _casillaTarjeta = indice;
        _tarjeta.Preparar(_tablero.ObtenerCasilla(indice), _tablero.BuscarPropiedad(indice), _estado, _u);
        Invalidate(_rectTarjeta);
    }

    private void OcultarTarjeta()
    {
        if (_casillaTarjeta >= 0)
        {
            _casillaTarjeta = -1;
            Invalidate(_rectTarjeta);
        }
    }

    /// <summary>
    /// Resaltado de la casilla bajo el cursor, dibujado encima de la imagen estática.
    /// </summary>
    private void DibujarResaltadoCursor(Graphics g, int indice)
    {
        RectangleF area = _rectCasillas[indice];
        if (indice % 10 != 0)
        {
            g.FillRectangle(_brilloCursor, area);
        }

        g.DrawRectangle(_bordeCursor!, area.X, area.Y, area.Width, area.Height);
    }

    // ------------------------------------------------------------------ casillas

    private void DibujarCasilla(Graphics g, int indice, RectangleF area, float u, Pen linea)
    {
        Casilla casilla = _tablero.ObtenerCasilla(indice);
        Propiedad? propiedad = _tablero.BuscarPropiedad(indice);
        int? dueno = _estado?.PropietarioDe(indice);

        GraphicsState guardado = g.Save();
        g.TranslateTransform(area.X + (area.Width / 2f), area.Y + (area.Height / 2f));
        g.RotateTransform(AnguloLado(indice));
        RectangleF local = new RectangleF(-u / 2f, -u, u, 2 * u);

        g.FillRectangle(_fondoCasilla, local);

        bool tieneFranja = propiedad != null && Paleta.TieneFranja(propiedad.Grupo);
        float alturaFranja = u * 0.44f;
        float yNombre = local.Y + (tieneFranja ? alturaFranja + (u * 0.06f) : u * 0.08f);
        if (tieneFranja)
        {
            RectangleF franja = new RectangleF(local.X, local.Y, local.Width, alturaFranja);
            using (LinearGradientBrush color = new LinearGradientBrush(franja, Tema.Mezclar(Paleta.ColorGrupo(propiedad!.Grupo), Color.White, 0.12f), Paleta.ColorGrupo(propiedad.Grupo), 90f))
            {
                g.FillRectangle(color, franja);
            }

            g.DrawLine(linea, franja.Left, franja.Bottom, franja.Right, franja.Bottom);
        }

        // Nombre centrado.
        float alturaNombre = tieneFranja ? u * 0.74f : u * 0.5f;
        string nombre = Paleta.NombreCorto(casilla.Nombre).ToUpperInvariant();
        _fuentesNombre[indice] ??= FuenteQueQuepa(g, nombre, Math.Max(u * 0.155f, 5f), local.Width - (u * 0.08f));
        g.DrawString(nombre, _fuentesNombre[indice]!, _tinta,
            new RectangleF(local.X + (u * 0.03f), yNombre, local.Width - (u * 0.06f), alturaNombre), _arriba);

        // Ícono de ferrocarriles, servicios, impuestos y cartas, entre el nombre y el precio.
        if (!tieneFranja)
        {
            float lado = u * 0.62f;
            IlustrarCasilla(g, casilla, new RectangleF(-lado / 2f, local.Y + (u * 0.66f), lado, lado), u);
        }

        // Precio (o detalle) abajo, hacia el borde exterior.
        float alturaMarcador = dueno.HasValue ? u * 0.16f : 0f;
        g.DrawString(casilla.Detalle, _fuenteDetalle!, _tinta,
            new RectangleF(local.X, local.Bottom - alturaMarcador - (u * 0.44f), local.Width, u * 0.4f), _abajo);

        // Marcador del dueño: banda de su color en el borde exterior.
        if (dueno.HasValue)
        {
            RectangleF banda = new RectangleF(local.X + (u * 0.04f), local.Bottom - alturaMarcador - (u * 0.02f), local.Width - (u * 0.08f), alturaMarcador);
            using GraphicsPath forma = Dibujo.Redondeado(banda, alturaMarcador / 2f);
            using SolidBrush colorDueno = new SolidBrush(ColorDeJugador(dueno.Value));
            g.FillPath(colorDueno, forma);
            using Pen blanco = new Pen(Color.White, Math.Max(1f, u * 0.02f));
            g.DrawPath(blanco, forma);
        }

        g.DrawRectangle(linea, local.X, local.Y, local.Width, local.Height);
        DibujarResaltadoVenta(g, indice, local, u);
        g.Restore(guardado);
    }

    /// <summary>
    /// Fuente en negrita del tamaño indicado, reducida si la palabra más larga no cabe en el ancho (para no
    /// cortar palabras como "CONNECTICUT" a la mitad).
    /// </summary>
    private static Font FuenteQueQuepa(Graphics g, string texto, float tamanio, float ancho)
    {
        string larga = string.Empty;
        foreach (string palabra in texto.Split(' '))
        {
            if (palabra.Length > larga.Length)
            {
                larga = palabra;
            }
        }

        Font fuente = new Font(Fuentes.Texto, tamanio, FontStyle.Bold, GraphicsUnit.Pixel);
        float medida = g.MeasureString(larga, fuente).Width;
        if (medida > ancho && medida > 0)
        {
            float nuevo = Math.Max(4f, tamanio * ancho / medida);
            fuente.Dispose();
            fuente = new Font(Fuentes.Texto, nuevo, FontStyle.Bold, GraphicsUnit.Pixel);
        }

        return fuente;
    }

    /// <summary>
    /// Borde dorado de la propiedad que se está ofreciendo en venta (parte de la imagen estática; el
    /// resaltado del cursor se dibuja aparte, encima).
    /// </summary>
    private void DibujarResaltadoVenta(Graphics g, int indice, RectangleF local, float u)
    {
        if (_estado?.Instantanea.IdPropiedadEnVenta == indice)
        {
            float grosor = Math.Max(u * 0.07f, 2f);
            using Pen resaltado = new Pen(Tema.Dorado, grosor);
            g.DrawRectangle(resaltado, local.X + (grosor / 2f), local.Y + (grosor / 2f), local.Width - grosor, local.Height - grosor);
        }
    }

    /// <summary>
    /// Ícono de una casilla sin franja de color, según su categoría (dato polimórfico de la casilla).
    /// </summary>
    private static void IlustrarCasilla(Graphics g, Casilla casilla, RectangleF r, float u)
    {
        string categoria = casilla.Categoria;
        if (categoria == "Ferrocarril")
        {
            IlustracionesTablero.Tren(g, r);
        }
        else if (categoria == "Servicio")
        {
            if (casilla.Nombre.Contains("Agua", StringComparison.OrdinalIgnoreCase))
            {
                IlustracionesTablero.Gota(g, r);
            }
            else
            {
                IlustracionesTablero.Bombilla(g, r);
            }
        }
        else if (categoria == "Impuesto")
        {
            if (casilla.Nombre.Contains("Lujo", StringComparison.OrdinalIgnoreCase))
            {
                IlustracionesTablero.Anillo(g, r);
            }
            else
            {
                IlustracionesTablero.Billete(g, r);
            }
        }
        else if (categoria == "Casualidad")
        {
            IlustracionesTablero.Pregunta(g, RectangleF.Inflate(r, u * 0.08f, u * 0.08f), ColorCasualidad);
        }
        else if (categoria == "Arca Comunal")
        {
            IlustracionesTablero.Cofre(g, r);
        }
    }

    private void DibujarEsquina(Graphics g, int indice, RectangleF area, float u, Pen linea)
    {
        Casilla casilla = _tablero.ObtenerCasilla(indice);
        g.FillRectangle(_fondoCasilla, area);

        bool esSalida = indice == Tablero.IndiceSalida;
        GraphicsState guardado = g.Save();
        g.TranslateTransform(area.X + (area.Width / 2f), area.Y + (area.Height / 2f));

        // Contenido en diagonal, con la parte de arriba hacia el centro del tablero.
        g.RotateTransform(-45f + (90f * (indice / 10)));
        using StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Color colorTitulo = esSalida ? Tema.Rojo : Tema.Tinta;
        using (Font titulo = new Font(esSalida ? Fuentes.Titulo : Fuentes.Texto, Math.Max(u * (esSalida ? 0.36f : 0.19f), 6f), esSalida ? FontStyle.Regular : FontStyle.Bold, GraphicsUnit.Pixel))
        using (SolidBrush pincel = new SolidBrush(colorTitulo))
        {
            RectangleF areaTitulo = esSalida ? new RectangleF(-u * 0.95f, -u * 0.55f, u * 1.9f, u * 0.6f) : new RectangleF(-u * 0.8f, -u * 0.98f, u * 1.6f, u * 0.52f);
            g.DrawString(casilla.Categoria.ToUpperInvariant(), titulo, pincel, areaTitulo, centrado);
        }

        using (Font detalle = new Font(Fuentes.Texto, Math.Max(u * 0.13f, 5f), FontStyle.Regular, GraphicsUnit.Pixel))
        using (SolidBrush tinta = new SolidBrush(Tema.Tinta))
        {
            RectangleF areaDetalle = esSalida ? new RectangleF(-u * 0.75f, -u * 0.98f, u * 1.5f, u * 0.4f) : new RectangleF(-u * 0.75f, u * 0.5f, u * 1.5f, u * 0.4f);
            g.DrawString(casilla.Detalle, detalle, tinta, areaDetalle, centrado);
        }

        RectangleF dibujo = new RectangleF(-u * 0.44f, -u * 0.42f, u * 0.88f, u * 0.88f);
        if (indice == 10)
        {
            IlustracionesTablero.Rejas(g, RectangleF.Inflate(dibujo, -u * 0.06f, -u * 0.06f));
        }
        else if (indice == 20)
        {
            IlustracionesTablero.Auto(g, dibujo);
        }
        else if (indice == 30)
        {
            IlustracionesTablero.Silbato(g, dibujo);
        }

        g.Restore(guardado);

        if (esSalida)
        {
            // La flecha marca el sentido del recorrido (hacia la izquierda, a la casilla 1).
            IlustracionesTablero.FlechaSalida(g, new RectangleF(area.X + (u * 0.2f), area.Bottom - (u * 0.62f), u * 1.6f, u * 0.5f));
        }

        g.DrawRectangle(linea, area.X, area.Y, area.Width, area.Height);
    }

    // ------------------------------------------------------------------ centro

    private static void DibujarLogotipoYMazos(Graphics g, RectangleF centro, float u)
    {
        // Logotipo propio en diagonal, en la mitad superior izquierda.
        GraphicsState guardado = g.Save();
        g.TranslateTransform(centro.X + (u * 2.75f), centro.Y + (u * 2.5f));
        g.RotateTransform(-45f);
        Logotipo.Dibujar(g, new RectangleF(-u * 2.8f, -u * 0.62f, u * 5.6f, u * 1.24f));
        g.Restore(guardado);

        // Mazos en sus espacios marcados.
        DibujarMazo(g, new RectangleF(centro.X + (u * 5.35f), centro.Y + (u * 0.4f), u * 3.2f, u * 2.05f), "CASUALIDAD", ColorCasualidad, -5f, u, true);
        DibujarMazo(g, new RectangleF(centro.X + (u * 5.35f), centro.Y + (u * 2.65f), u * 3.2f, u * 2.05f), "ARCA COMUNAL", ColorArca, 4f, u, false);
    }

    /// <summary>
    /// Quién tiene el turno y el aviso de lo que se espera (dependen del estado).
    /// </summary>
    private void DibujarTurnoYAviso(Graphics g, RectangleF centro, float u)
    {
        if (_estado != null && _estado.Instantanea.IdJugadorEnTurno is int enTurno && _estado.Instantanea.Estado == EstadoPartida.EnCurso)
        {
            EstadoJugador? jugador = _estado.BuscarJugador(enTurno);
            string texto = $"Turno {_estado.Instantanea.NumeroTurno}: le toca a {NombreDeJugador(enTurno)}";
            using Font fuenteTurno = new Font(Fuentes.Texto, Math.Max(u * 0.28f, 8f), FontStyle.Bold, GraphicsUnit.Pixel);
            SizeF medida = g.MeasureString(texto, fuenteTurno);
            float disco = u * 0.42f;
            float x = centro.X + ((centro.Width - medida.Width - disco - (u * 0.12f)) / 2f);
            float y = centro.Y + (u * 6.72f);
            if (jugador != null)
            {
                DibujoFicha.DibujarEnDisco(g, jugador.FormaFicha, new RectangleF(x, y + ((u * 0.45f) - disco) / 2f, disco, disco), ColorDeJugador(enTurno), false);
            }

            using SolidBrush pincel = new SolidBrush(Tema.Mezclar(ColorDeJugador(enTurno), Color.Black, 0.2f));
            using StringFormat medio = new StringFormat { LineAlignment = StringAlignment.Center };
            g.DrawString(texto, fuenteTurno, pincel, new RectangleF(x + disco + (u * 0.12f), y, medida.Width + 4, u * 0.45f), medio);
        }

        if (_aviso.Length > 0)
        {
            // Lo que se espera ahora (botón físico, tarjeta en el lector...), en un recuadro visible para todos.
            RectangleF recuadro = new RectangleF(centro.X + (u * 0.45f), centro.Y + (u * 7.3f), centro.Width - (u * 0.9f), u * 1.35f);
            Dibujo.Sombra(g, recuadro, u * 0.18f, 5, 40, u * 0.04f);
            using (GraphicsPath forma = Dibujo.Redondeado(recuadro, u * 0.18f))
            using (SolidBrush fondo = new SolidBrush(Tema.Marfil))
            using (Pen borde = new Pen(Tema.Rojo, Math.Max(u * 0.035f, 1.5f)))
            {
                g.FillPath(fondo, forma);
                g.DrawPath(borde, forma);
            }

            using Font fuenteAviso = new Font(Fuentes.Texto, Math.Max(u * 0.24f, 8f), FontStyle.Bold, GraphicsUnit.Pixel);
            Dibujo.TextoCentrado(g, _aviso, fuenteAviso, Tema.RojoPresionado, RectangleF.Inflate(recuadro, -u * 0.18f, -u * 0.06f));
        }
    }

    /// <summary>
    /// Dados y texto de la última tirada. Van encima de la imagen estática porque giran un momento al
    /// llegar una tirada nueva; mientras giran solo se repinta su zona.
    /// </summary>
    private void DibujarDados(Graphics g)
    {
        TiradaDados? tirada = _estado?.Instantanea.UltimaTirada;
        float u = _u;
        float lado = u * 1.05f;
        float yDados = _centro.Y + (u * 5.6f);
        long transcurrido = Environment.TickCount64 - _inicioDados;
        bool girando = _temporizadorDados.Enabled && transcurrido < DuracionDadosMs;
        float giro = girando ? (1f - (transcurrido / (float)DuracionDadosMs)) * 360f : 0f;
        if (tirada.HasValue)
        {
            int cara1 = girando ? _azar.Next(1, 7) : tirada.Value.Dado1;
            int cara2 = girando ? _azar.Next(1, 7) : tirada.Value.Dado2;
            DibujoDado.Dibujar(g, new PointF(_centro.X + (u * 3.85f), yDados), lado, -8f + giro, cara1);
            DibujoDado.Dibujar(g, new PointF(_centro.X + (u * 5.15f), yDados), lado, 9f - giro, cara2);
        }
        else
        {
            DibujarHuecoDado(g, new PointF(_centro.X + (u * 3.85f), yDados), lado);
            DibujarHuecoDado(g, new PointF(_centro.X + (u * 5.15f), yDados), lado);
        }

        string textoDados = "Aún no se han lanzado los dados";
        if (tirada.HasValue && _estado?.IdJugadorUltimoMovimiento is int idTiro)
        {
            textoDados = girando ? "Lanzando los dados..." : $"{NombreDeJugador(idTiro)} sacó {tirada.Value.Dado1} + {tirada.Value.Dado2} = {tirada.Value.Total}";
        }

        g.DrawString(textoDados, _fuenteDados!, _verdeProfundo, new RectangleF(_centro.X, _centro.Y + (u * 6.25f), _centro.Width, u * 0.45f), _centrado);
    }

    private static void DibujarHuecoDado(Graphics g, PointF centro, float lado)
    {
        RectangleF hueco = new RectangleF(centro.X - (lado / 2f), centro.Y - (lado / 2f), lado, lado);
        using GraphicsPath forma = Dibujo.Redondeado(hueco, lado * 0.2f);
        using Pen borde = new Pen(Tema.VerdeMentaLinea, Math.Max(1.5f, lado * 0.04f)) { DashStyle = DashStyle.Dash };
        g.DrawPath(borde, forma);
    }

    /// <summary>
    /// Espacio marcado del mazo con un montón de cartas apiladas (desplazadas y con sombra); la de arriba
    /// muestra el ícono y el nombre del mazo.
    /// </summary>
    private static void DibujarMazo(Graphics g, RectangleF espacio, string texto, Color color, float angulo, float u, bool esCasualidad)
    {
        using (GraphicsPath marca = Dibujo.Redondeado(espacio, u * 0.16f))
        {
            using SolidBrush fondo = new SolidBrush(Color.FromArgb(40, color));
            g.FillPath(fondo, marca);
            using Pen borde = new Pen(Color.FromArgb(170, color), Math.Max(u * 0.035f, 1.2f)) { DashStyle = DashStyle.Dash };
            g.DrawPath(borde, marca);
        }

        GraphicsState guardado = g.Save();
        g.TranslateTransform(espacio.X + (espacio.Width / 2f), espacio.Y + (espacio.Height / 2f));
        g.RotateTransform(angulo);
        float ancho = espacio.Width * 0.78f;
        float alto = espacio.Height * 0.66f;
        for (int i = 4; i >= 0; i--)
        {
            RectangleF carta = new RectangleF((-ancho / 2f) + (i * u * 0.045f), (-alto / 2f) - (i * u * 0.045f), ancho, alto);
            using GraphicsPath forma = Dibujo.Redondeado(carta, u * 0.1f);
            if (i == 4)
            {
                Dibujo.Sombra(g, carta, u * 0.1f, 4, 50, u * 0.05f);
            }

            using SolidBrush relleno = new SolidBrush(i == 0 ? color : Tema.Mezclar(color, Color.White, 0.25f + (i * 0.06f)));
            g.FillPath(relleno, forma);
            using Pen borde = new Pen(Tema.Mezclar(color, Color.Black, 0.35f), Math.Max(1f, u * 0.02f));
            g.DrawPath(borde, forma);
            if (i == 0)
            {
                using Pen filete = new Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(1f, u * 0.025f));
                using GraphicsPath interior = Dibujo.Redondeado(RectangleF.Inflate(carta, -u * 0.08f, -u * 0.08f), u * 0.06f);
                g.DrawPath(filete, interior);

                RectangleF icono = new RectangleF(carta.X + (u * 0.14f), carta.Y + ((alto - (u * 0.82f)) / 2f), u * 0.82f, u * 0.82f);
                if (esCasualidad)
                {
                    IlustracionesTablero.Pregunta(g, icono, Color.White);
                }
                else
                {
                    IlustracionesTablero.Cofre(g, icono);
                }

                using Font fuente = new Font(Fuentes.Titulo, Math.Max(u * 0.2f, 6f), FontStyle.Regular, GraphicsUnit.Pixel);
                RectangleF zonaTexto = new RectangleF(icono.Right, carta.Y, carta.Right - icono.Right - (u * 0.08f), alto);
                Dibujo.TextoCentrado(g, texto, fuente, Color.White, zonaTexto);
            }
        }

        g.Restore(guardado);
    }

    // ------------------------------------------------------------------ fichas

    private void DibujarFichas(Graphics g, Rectangle recorte)
    {
        float u = _u;
        if (_estado == null)
        {
            return;
        }

        // Cuántas fichas hay en cada casilla, para repartirlas sin que se tapen.
        EstadoJugador[] jugadores = _estado.Instantanea.Jugadores;
        int[] cantidadEn = new int[_tablero.Cantidad];
        foreach (EstadoJugador jugador in jugadores)
        {
            if (jugador.Activo && jugador.Id <= MaximoIdJugador)
            {
                cantidadEn[_posiciones[jugador.Id]]++;
            }
        }

        int[] colocadasEn = new int[_tablero.Cantidad];
        float diametro = u * 0.5f;
        foreach (EstadoJugador jugador in jugadores)
        {
            if (!jugador.Activo || jugador.Id > MaximoIdJugador)
            {
                continue;
            }

            int posicion = _posiciones[jugador.Id];
            RectangleF casilla = _rectCasillas[posicion];
            PointF exterior = HaciaElBorde(posicion, u * 0.28f);
            PointF desplazamiento = Reparto(cantidadEn[posicion], colocadasEn[posicion]++, u * 0.26f);
            float cx = casilla.X + (casilla.Width / 2f) + exterior.X + desplazamiento.X;
            float cy = casilla.Y + (casilla.Height / 2f) + exterior.Y + desplazamiento.Y;
            RectangleF ficha = new RectangleF(cx - (diametro / 2f), cy - (diametro / 2f), diametro, diametro);
            RectangleF conBrillo = RectangleF.Inflate(ficha, diametro * 0.2f, diametro * 0.2f);
            if (!recorte.IntersectsWith(Rectangle.Ceiling(conBrillo)))
            {
                continue;
            }

            if (_estado.Instantanea.IdJugadorEnTurno == jugador.Id)
            {
                g.FillEllipse(_brilloTurno, conBrillo);
            }

            DibujoFicha.DibujarEnDisco(g, jugador.FormaFicha, ficha, ColorDeJugador(jugador.Id));
        }
    }

    /// <summary>
    /// Desplazamiento de la ficha número <paramref name="orden"/> entre <paramref name="cantidad"/> fichas
    /// de la misma casilla: una al centro, dos lado a lado, tres en triángulo y cuatro en cuadro.
    /// </summary>
    private static PointF Reparto(int cantidad, int orden, float d)
    {
        if (cantidad <= 1)
        {
            return PointF.Empty;
        }

        if (cantidad == 2)
        {
            return new PointF(orden == 0 ? -d : d, 0);
        }

        if (cantidad == 3)
        {
            return orden == 0 ? new PointF(-d, -d * 0.8f) : orden == 1 ? new PointF(d, -d * 0.8f) : new PointF(0, d * 0.8f);
        }

        return new PointF(orden % 2 == 0 ? -d : d, orden < 2 ? -d : d);
    }

    /// <summary>
    /// Desplazamiento hacia el borde exterior (zona del precio), para que las fichas no tapen la franja.
    /// En las esquinas no se desplaza.
    /// </summary>
    private static PointF HaciaElBorde(int indice, float distancia)
    {
        if (indice % 10 == 0)
        {
            return PointF.Empty;
        }

        return indice < 10 ? new PointF(0, distancia)
            : indice < 20 ? new PointF(-distancia, 0)
            : indice < 30 ? new PointF(0, -distancia)
            : new PointF(distancia, 0);
    }

    private void AvanzarAnimacion()
    {
        if (_paso < _ruta.Length)
        {
            MoverFicha(_idAnimado, _ruta[_paso++]);
            return;
        }

        _temporizador.Stop();
        if (_estado != null)
        {
            foreach (EstadoJugador jugador in _estado.Instantanea.Jugadores)
            {
                if (jugador.Id <= MaximoIdJugador)
                {
                    MoverFicha(jugador.Id, jugador.Posicion);
                }
            }
        }
    }

    /// <summary>
    /// Cambia la casilla donde se dibuja una ficha y repinta solo la casilla que deja y la nueva.
    /// </summary>
    private void MoverFicha(int idJugador, int posicion)
    {
        int anterior = _posiciones[idJugador];
        if (anterior == posicion)
        {
            return;
        }

        _posiciones[idJugador] = posicion;
        InvalidarCasilla(anterior);
        InvalidarCasilla(posicion);
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
}
