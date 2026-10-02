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
/// </remarks>
internal sealed class PanelTablero : Control
{
    private const float Unidades = 13f;
    private const int MaximoIdJugador = 8;
    private const int DuracionDadosMs = 650;

    private static readonly Color ColorCasualidad = Color.FromArgb(236, 140, 50);
    private static readonly Color ColorArca = Color.FromArgb(96, 170, 222);

    private readonly Tablero _tablero = new Tablero();
    private readonly int[] _posiciones = new int[MaximoIdJugador + 1];
    private readonly Timer _temporizador = new Timer { Interval = 170 };
    private readonly Timer _temporizadorDados = new Timer { Interval = 30 };
    private readonly Random _azar = new Random();
    private EstadoRed? _estado;
    private int _idAnimado;
    private int[] _ruta = new int[0];
    private int _paso;
    private int _casillaBajoCursor = -1;
    private Point _cursor;
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

            Invalidate();
        };
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

        Invalidate();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temporizador.Dispose();
            _temporizadorDados.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int casilla = CasillaEn(e.Location);
        bool cambio = casilla != _casillaBajoCursor || (casilla >= 0 && e.Location != _cursor);
        _cursor = e.Location;
        if (cambio)
        {
            _casillaBajoCursor = casilla;
            Invalidate();
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_casillaBajoCursor >= 0)
        {
            _casillaBajoCursor = -1;
            Invalidate();
        }
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        using (LinearGradientBrush mesa = new LinearGradientBrush(ClientRectangle, Color.FromArgb(36, 96, 66), Color.FromArgb(18, 58, 40), 90f))
        {
            g.FillRectangle(mesa, ClientRectangle);
        }

        RectangleF tablero = AreaTablero();
        float u = tablero.Width / Unidades;

        Dibujo.Sombra(g, tablero, u * 0.1f, 10, 90, u * 0.12f);
        using (SolidBrush fondo = new SolidBrush(Tema.VerdeMenta))
        {
            g.FillRectangle(fondo, tablero);
        }

        using Pen linea = new Pen(Color.FromArgb(40, 54, 44), Math.Max(u * 0.02f, 1f));
        for (int i = 0; i < _tablero.Cantidad; i++)
        {
            RectangleF area = RectCasilla(tablero, i);
            if (i % 10 == 0)
            {
                DibujarEsquina(g, i, area, u, linea);
            }
            else
            {
                DibujarCasilla(g, i, area, u, linea);
            }
        }

        DibujarCentro(g, new RectangleF(tablero.X + (2 * u), tablero.Y + (2 * u), 9 * u, 9 * u), u);
        using (Pen borde = new Pen(Color.FromArgb(30, 40, 34), Math.Max(u * 0.05f, 1.5f)))
        {
            g.DrawRectangle(borde, tablero.X, tablero.Y, tablero.Width, tablero.Height);
            g.DrawRectangle(borde, tablero.X + (2 * u), tablero.Y + (2 * u), 9 * u, 9 * u);
        }

        DibujarFichas(g, tablero, u);
        if (_casillaBajoCursor >= 0)
        {
            DibujarTarjetaTitulo(g, _casillaBajoCursor, u);
        }
    }

    // ------------------------------------------------------------------ geometría

    private RectangleF AreaTablero()
    {
        float lado = Math.Max(Math.Min(ClientSize.Width, ClientSize.Height) - 28f, 130f);
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

    /// <summary>
    /// Giro de la casilla: la franja (arriba en el sistema local) queda hacia el centro y el texto se lee
    /// desde afuera del tablero, como en el juego físico.
    /// </summary>
    private static float AnguloLado(int indice)
    {
        return indice < 10 ? 0f : indice < 20 ? 90f : indice < 30 ? 180f : 270f;
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

        using (SolidBrush fondo = new SolidBrush(Tema.VerdeMentaClaro))
        {
            g.FillRectangle(fondo, local);
        }

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
        using (Font fuenteNombre = FuenteQueQuepa(g, nombre, Math.Max(u * 0.155f, 5f), local.Width - (u * 0.08f)))
        using (SolidBrush tinta = new SolidBrush(Tema.Tinta))
        using (StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisWord })
        {
            g.DrawString(nombre, fuenteNombre, tinta,
                new RectangleF(local.X + (u * 0.03f), yNombre, local.Width - (u * 0.06f), alturaNombre), centrado);
        }

        // Ícono de ferrocarriles, servicios, impuestos y cartas, entre el nombre y el precio.
        if (!tieneFranja)
        {
            float lado = u * 0.62f;
            IlustrarCasilla(g, casilla, new RectangleF(-lado / 2f, local.Y + (u * 0.66f), lado, lado), u);
        }

        // Precio (o detalle) abajo, hacia el borde exterior.
        float alturaMarcador = dueno.HasValue ? u * 0.16f : 0f;
        using (Font fuenteDetalle = new Font(Fuentes.Texto, Math.Max(u * 0.15f, 5f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (SolidBrush tinta = new SolidBrush(Tema.Tinta))
        using (StringFormat abajo = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far })
        {
            g.DrawString(casilla.Detalle, fuenteDetalle, tinta,
                new RectangleF(local.X, local.Bottom - alturaMarcador - (u * 0.44f), local.Width, u * 0.4f), abajo);
        }

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
        DibujarResaltados(g, indice, local, u);
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

    private void DibujarResaltados(Graphics g, int indice, RectangleF local, float u)
    {
        if (_estado?.Instantanea.IdPropiedadEnVenta == indice)
        {
            float grosor = Math.Max(u * 0.07f, 2f);
            using Pen resaltado = new Pen(Tema.Dorado, grosor);
            g.DrawRectangle(resaltado, local.X + (grosor / 2f), local.Y + (grosor / 2f), local.Width - grosor, local.Height - grosor);
        }

        if (indice == _casillaBajoCursor)
        {
            using SolidBrush brillo = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
            g.FillRectangle(brillo, local);
            using Pen borde = new Pen(Tema.Rojo, Math.Max(u * 0.04f, 1.5f));
            g.DrawRectangle(borde, local.X, local.Y, local.Width, local.Height);
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
        using (SolidBrush fondo = new SolidBrush(Tema.VerdeMentaClaro))
        {
            g.FillRectangle(fondo, area);
        }

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
        if (indice == _casillaBajoCursor)
        {
            using Pen borde = new Pen(Tema.Rojo, Math.Max(u * 0.04f, 1.5f));
            g.DrawRectangle(borde, area.X, area.Y, area.Width, area.Height);
        }
    }

    // ------------------------------------------------------------------ centro

    private void DibujarCentro(Graphics g, RectangleF centro, float u)
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

        // Dados (giran un momento al llegar una tirada nueva).
        TiradaDados? tirada = _estado?.Instantanea.UltimaTirada;
        float lado = u * 1.05f;
        float yDados = centro.Y + (u * 5.6f);
        long transcurrido = Environment.TickCount64 - _inicioDados;
        bool girando = _temporizadorDados.Enabled && transcurrido < DuracionDadosMs;
        float giro = girando ? (1f - (transcurrido / (float)DuracionDadosMs)) * 360f : 0f;
        if (tirada.HasValue)
        {
            int cara1 = girando ? _azar.Next(1, 7) : tirada.Value.Dado1;
            int cara2 = girando ? _azar.Next(1, 7) : tirada.Value.Dado2;
            DibujoDado.Dibujar(g, new PointF(centro.X + (u * 3.85f), yDados), lado, -8f + giro, cara1);
            DibujoDado.Dibujar(g, new PointF(centro.X + (u * 5.15f), yDados), lado, 9f - giro, cara2);
        }
        else
        {
            DibujarHuecoDado(g, new PointF(centro.X + (u * 3.85f), yDados), lado);
            DibujarHuecoDado(g, new PointF(centro.X + (u * 5.15f), yDados), lado);
        }

        using StringFormat centrado = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        string textoDados = "Aún no se han lanzado los dados";
        if (tirada.HasValue && _estado?.IdJugadorUltimoMovimiento is int idTiro)
        {
            textoDados = girando ? "Lanzando los dados..." : $"{NombreDeJugador(idTiro)} sacó {tirada.Value.Dado1} + {tirada.Value.Dado2} = {tirada.Value.Total}";
        }

        using (Font fuenteDados = new Font(Fuentes.Texto, Math.Max(u * 0.26f, 8f), FontStyle.Bold, GraphicsUnit.Pixel))
        using (SolidBrush verde = new SolidBrush(Tema.VerdeProfundo))
        {
            g.DrawString(textoDados, fuenteDados, verde, new RectangleF(centro.X, centro.Y + (u * 6.25f), centro.Width, u * 0.45f), centrado);
        }

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

    private void DibujarFichas(Graphics g, RectangleF tablero, float u)
    {
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
            RectangleF casilla = RectCasilla(tablero, posicion);
            PointF exterior = HaciaElBorde(posicion, u * 0.28f);
            PointF desplazamiento = Reparto(cantidadEn[posicion], colocadasEn[posicion]++, u * 0.26f);
            float cx = casilla.X + (casilla.Width / 2f) + exterior.X + desplazamiento.X;
            float cy = casilla.Y + (casilla.Height / 2f) + exterior.Y + desplazamiento.Y;
            RectangleF ficha = new RectangleF(cx - (diametro / 2f), cy - (diametro / 2f), diametro, diametro);

            if (_estado.Instantanea.IdJugadorEnTurno == jugador.Id)
            {
                using SolidBrush brillo = new SolidBrush(Color.FromArgb(150, Tema.Dorado));
                g.FillEllipse(brillo, RectangleF.Inflate(ficha, diametro * 0.2f, diametro * 0.2f));
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

    // ------------------------------------------------------------------ tarjeta de título

    /// <summary>
    /// Tarjeta de título junto al cursor: franja de color, nombre, precio, alquiler y dueño. En las casillas
    /// que no son propiedades muestra su categoría y su detalle.
    /// </summary>
    private void DibujarTarjetaTitulo(Graphics g, int indice, float u)
    {
        Casilla casilla = _tablero.ObtenerCasilla(indice);
        Propiedad? propiedad = _tablero.BuscarPropiedad(indice);
        using Font pequenia = Tema.Texto(7f, FontStyle.Bold);
        using Font titulo = Tema.Texto(10.5f, FontStyle.Bold);
        using Font normal = Tema.Texto(9.5f);
        using Font negrita = Tema.Texto(9.5f, FontStyle.Bold);
        float linea = normal.GetHeight(g) + 6;
        float ancho = Math.Max(300f, u * 3.6f);
        float altoFranja = pequenia.GetHeight(g) + (titulo.GetHeight(g) * 2) + 14;
        int filas = propiedad == null ? 2 : propiedad.Grupo == GrupoPropiedad.Ferrocarril || propiedad.Grupo == GrupoPropiedad.Servicio ? 3 : 2;
        float alto = propiedad == null ? altoFranja + (linea * 2) + 28 : altoFranja + (linea * filas) + linea + 46;

        float x = Math.Min(_cursor.X + 18f, ClientSize.Width - ancho - 8f);
        float y = Math.Min(_cursor.Y + 18f, ClientSize.Height - alto - 8f);
        if (x < _cursor.X && y < _cursor.Y + 18f)
        {
            y = Math.Max(8f, _cursor.Y - alto - 12f);
        }

        RectangleF tarjeta = new RectangleF(x, y, ancho, alto);
        Dibujo.Sombra(g, tarjeta, 12, 7, 70, 4f);
        using (GraphicsPath forma = Dibujo.Redondeado(tarjeta, 12))
        {
            using SolidBrush fondo = new SolidBrush(Color.White);
            g.FillPath(fondo, forma);
            using Pen borde = new Pen(Tema.Tinta, 1.6f);
            g.DrawPath(borde, forma);
        }

        RectangleF franja = new RectangleF(tarjeta.X + 10, tarjeta.Y + 10, tarjeta.Width - 20, altoFranja);
        RectangleF zonaEtiqueta = new RectangleF(franja.X, franja.Y + 6, franja.Width, pequenia.GetHeight(g));
        RectangleF zonaNombre = new RectangleF(franja.X + 6, zonaEtiqueta.Bottom, franja.Width - 12, franja.Bottom - zonaEtiqueta.Bottom - 4);
        if (propiedad == null)
        {
            using (SolidBrush fondoFranja = new SolidBrush(Tema.Crema))
            {
                g.FillRectangle(fondoFranja, franja);
            }

            Dibujo.TextoCentrado(g, casilla.Categoria.ToUpperInvariant(), pequenia, Tema.TintaSuave, zonaEtiqueta);
            Dibujo.TextoCentrado(g, casilla.Nombre, titulo, Tema.Tinta, zonaNombre);
            Dibujo.TextoCentrado(g, casilla.Detalle, normal, Tema.Tinta, new RectangleF(tarjeta.X + 12, franja.Bottom + 6, tarjeta.Width - 24, tarjeta.Bottom - franja.Bottom - 14));
            return;
        }

        bool conFranja = Paleta.TieneFranja(propiedad.Grupo);
        Color colorFranja = conFranja ? Paleta.ColorGrupo(propiedad.Grupo) : Tema.Crema;
        Color colorTexto = conFranja && (propiedad.Grupo == GrupoPropiedad.Celeste || propiedad.Grupo == GrupoPropiedad.Amarillo) ? Tema.Tinta
            : conFranja ? Color.White : Tema.Tinta;
        using (SolidBrush fondoFranja = new SolidBrush(colorFranja))
        {
            g.FillRectangle(fondoFranja, franja);
        }

        using (Pen bordeFranja = new Pen(Tema.Tinta, 1.2f))
        {
            g.DrawRectangle(bordeFranja, franja.X, franja.Y, franja.Width, franja.Height);
        }

        Dibujo.TextoCentrado(g, "TÍTULO DE PROPIEDAD", pequenia, colorTexto, zonaEtiqueta);
        Dibujo.TextoCentrado(g, propiedad.Nombre.ToUpperInvariant(), titulo, colorTexto, zonaNombre);

        float yLinea = franja.Bottom + 10;
        using SolidBrush tinta = new SolidBrush(Tema.Tinta);
        using StringFormat derecha = new StringFormat { Alignment = StringAlignment.Far };
        void Fila(string etiqueta, string valor)
        {
            g.DrawString(etiqueta, normal, tinta, tarjeta.X + 16, yLinea);
            g.DrawString(valor, negrita, tinta, new RectangleF(tarjeta.X + 16, yLinea, tarjeta.Width - 32, linea), derecha);
            yLinea += linea;
        }

        Fila("Precio", Formato.Dinero(propiedad.PrecioCompra));
        if (propiedad.Grupo == GrupoPropiedad.Ferrocarril)
        {
            Fila("Alquiler con 1 ferrocarril", Formato.Dinero(Ferrocarril.AlquilerClasico));
            Fila("Con 2 / 3 / 4", $"{Formato.Dinero(Ferrocarril.AlquilerClasico * 2)} / {Formato.Dinero(Ferrocarril.AlquilerClasico * 4)} / {Formato.Dinero(Ferrocarril.AlquilerClasico * 8)}");
        }
        else if (propiedad.Grupo == GrupoPropiedad.Servicio)
        {
            Fila("Con una compañía", Formato.Dinero(CompaniaServicio.AlquilerConUna));
            Fila("Con ambas compañías", Formato.Dinero(CompaniaServicio.AlquilerConAmbas));
        }
        else
        {
            Fila("Alquiler", Formato.Dinero(propiedad.Alquiler));
        }

        using (Pen separador = new Pen(Tema.Borde, 1.2f))
        {
            g.DrawLine(separador, tarjeta.X + 16, yLinea + 4, tarjeta.Right - 16, yLinea + 4);
        }

        yLinea += 14;
        int? dueno = _estado?.PropietarioDe(indice);
        EstadoJugador? jugadorDueno = dueno.HasValue ? _estado?.BuscarJugador(dueno.Value) : null;
        if (jugadorDueno != null)
        {
            float disco = linea + 4;
            DibujoFicha.DibujarEnDisco(g, jugadorDueno.FormaFicha, new RectangleF(tarjeta.X + 16, yLinea - 2, disco, disco), ColorDeJugador(jugadorDueno.Id), false);
            g.DrawString($"Dueño: {jugadorDueno.Nombre}", negrita, tinta, tarjeta.X + 24 + disco, yLinea + 2);
        }
        else
        {
            using SolidBrush verde = new SolidBrush(Tema.Exito);
            g.DrawString("Disponible para comprar", negrita, verde, tarjeta.X + 16, yLinea + 2);
        }
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
