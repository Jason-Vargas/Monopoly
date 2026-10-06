using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Monopoly.App.Estilo;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Tarjeta de información que aparece al pasar el mouse por una casilla del tablero: franja de color,
/// nombre, precio, alquiler y dueño (o la categoría y el detalle si no es una propiedad). Hay una sola
/// instancia por tablero: se dibuja una vez en su propia imagen cuando cambia la casilla o el estado, y el
/// tablero solo la copia en su posición. Las fuentes, pinceles y lápices se crean una vez y se reutilizan.
/// </summary>
internal sealed class TarjetaCasilla : IDisposable
{
    /// <summary>Espacio alrededor de la tarjeta para la sombra.</summary>
    private const int MargenSombra = 14;

    private readonly Font _pequenia = Tema.Texto(7f, FontStyle.Bold);
    private readonly Font _titulo = Tema.Texto(10.5f, FontStyle.Bold);
    private readonly Font _normal = Tema.Texto(9.5f);
    private readonly Font _negrita = Tema.Texto(9.5f, FontStyle.Bold);
    private readonly SolidBrush _blanco = new SolidBrush(Color.White);
    private readonly SolidBrush _tinta = new SolidBrush(Tema.Tinta);
    private readonly SolidBrush _verde = new SolidBrush(Tema.Exito);
    private readonly SolidBrush _franja = new SolidBrush(Tema.Crema);
    private readonly Pen _borde = new Pen(Tema.Tinta, 1.6f);
    private readonly Pen _bordeFranja = new Pen(Tema.Tinta, 1.2f);
    private readonly Pen _separador = new Pen(Tema.Borde, 1.2f);
    private readonly StringFormat _derecha = new StringFormat { Alignment = StringAlignment.Far };
    private Bitmap? _imagen;

    /// <summary>
    /// Tamaño de la imagen preparada (tarjeta + margen de la sombra), o vacío si no hay ninguna.
    /// </summary>
    public Size Tamanio => _imagen?.Size ?? Size.Empty;

    /// <summary>
    /// Desplazamiento de la tarjeta visible dentro de la imagen (por el margen de la sombra).
    /// </summary>
    public static int Margen => MargenSombra;

    /// <summary>
    /// Dibuja en la imagen interna la tarjeta de una casilla. Reutiliza la imagen si el tamaño no cambia.
    /// </summary>
    /// <param name="casilla">Casilla consultada.</param>
    /// <param name="propiedad">La propiedad de esa casilla, o <c>null</c> si no es una propiedad.</param>
    /// <param name="estado">Último estado recibido (para el dueño), o <c>null</c>.</param>
    /// <param name="unidad">Unidad del tablero en píxeles (el ancho crece con el tablero).</param>
    public void Preparar(Casilla casilla, Propiedad? propiedad, EstadoRed? estado, float unidad)
    {
        float linea = _normal.Height + 6;
        float ancho = Math.Max(300f, unidad * 3.6f);
        float altoFranja = _pequenia.Height + (_titulo.Height * 2) + 14;
        int filas = propiedad != null && (propiedad.Grupo == GrupoPropiedad.Ferrocarril || propiedad.Grupo == GrupoPropiedad.Servicio) ? 3 : 2;
        float alto = propiedad == null ? altoFranja + (linea * 2) + 28 : altoFranja + (linea * filas) + linea + 46;

        Size tamanio = new Size((int)Math.Ceiling(ancho) + (2 * MargenSombra), (int)Math.Ceiling(alto) + (2 * MargenSombra));
        if (_imagen == null || _imagen.Size != tamanio)
        {
            _imagen?.Dispose();
            _imagen = new Bitmap(tamanio.Width, tamanio.Height);
        }

        using Graphics g = Graphics.FromImage(_imagen);
        g.Clear(Color.Transparent);
        Dibujo.Calidad(g);
        RectangleF tarjeta = new RectangleF(MargenSombra, MargenSombra - 4, ancho, alto);
        Dibujo.Sombra(g, tarjeta, 12, 7, 70, 4f);
        using (GraphicsPath forma = Dibujo.Redondeado(tarjeta, 12))
        {
            g.FillPath(_blanco, forma);
            g.DrawPath(_borde, forma);
        }

        RectangleF franja = new RectangleF(tarjeta.X + 10, tarjeta.Y + 10, tarjeta.Width - 20, altoFranja);
        RectangleF zonaEtiqueta = new RectangleF(franja.X, franja.Y + 6, franja.Width, _pequenia.Height);
        RectangleF zonaNombre = new RectangleF(franja.X + 6, zonaEtiqueta.Bottom, franja.Width - 12, franja.Bottom - zonaEtiqueta.Bottom - 4);
        if (propiedad == null)
        {
            _franja.Color = Tema.Crema;
            g.FillRectangle(_franja, franja);
            Dibujo.TextoCentrado(g, casilla.Categoria.ToUpperInvariant(), _pequenia, Tema.TintaSuave, zonaEtiqueta);
            Dibujo.TextoCentrado(g, casilla.Nombre, _titulo, Tema.Tinta, zonaNombre);
            Dibujo.TextoCentrado(g, casilla.Detalle, _normal, Tema.Tinta, new RectangleF(tarjeta.X + 12, franja.Bottom + 6, tarjeta.Width - 24, tarjeta.Bottom - franja.Bottom - 14));
            return;
        }

        bool conFranja = Paleta.TieneFranja(propiedad.Grupo);
        Color colorTexto = conFranja && (propiedad.Grupo == GrupoPropiedad.Celeste || propiedad.Grupo == GrupoPropiedad.Amarillo) ? Tema.Tinta
            : conFranja ? Color.White : Tema.Tinta;
        _franja.Color = conFranja ? Paleta.ColorGrupo(propiedad.Grupo) : Tema.Crema;
        g.FillRectangle(_franja, franja);
        g.DrawRectangle(_bordeFranja, franja.X, franja.Y, franja.Width, franja.Height);
        Dibujo.TextoCentrado(g, "TÍTULO DE PROPIEDAD", _pequenia, colorTexto, zonaEtiqueta);
        Dibujo.TextoCentrado(g, propiedad.Nombre.ToUpperInvariant(), _titulo, colorTexto, zonaNombre);

        float yLinea = franja.Bottom + 10;
        void Fila(string etiqueta, string valor)
        {
            g.DrawString(etiqueta, _normal, _tinta, tarjeta.X + 16, yLinea);
            g.DrawString(valor, _negrita, _tinta, new RectangleF(tarjeta.X + 16, yLinea, tarjeta.Width - 32, linea), _derecha);
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

        g.DrawLine(_separador, tarjeta.X + 16, yLinea + 4, tarjeta.Right - 16, yLinea + 4);
        yLinea += 14;
        int? dueno = estado?.PropietarioDe(casilla.Id);
        EstadoJugador? jugadorDueno = dueno.HasValue ? estado?.BuscarJugador(dueno.Value) : null;
        if (jugadorDueno != null)
        {
            float disco = linea + 4;
            DibujoFicha.DibujarEnDisco(g, jugadorDueno.FormaFicha, new RectangleF(tarjeta.X + 16, yLinea - 2, disco, disco), Paleta.ColorFicha(jugadorDueno.ColorFicha), false);
            g.DrawString($"Dueño: {jugadorDueno.Nombre}", _negrita, _tinta, tarjeta.X + 24 + disco, yLinea + 2);
        }
        else
        {
            g.DrawString("Disponible para comprar", _negrita, _verde, tarjeta.X + 16, yLinea + 2);
        }
    }

    /// <summary>
    /// Copia la tarjeta preparada en la superficie indicada.
    /// </summary>
    /// <param name="g">Superficie del tablero.</param>
    /// <param name="posicion">Esquina superior izquierda de la imagen (incluye el margen de la sombra).</param>
    public void Dibujar(Graphics g, Point posicion)
    {
        if (_imagen != null)
        {
            g.DrawImageUnscaled(_imagen, posicion);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _imagen?.Dispose();
        _pequenia.Dispose();
        _titulo.Dispose();
        _normal.Dispose();
        _negrita.Dispose();
        _blanco.Dispose();
        _tinta.Dispose();
        _verde.Dispose();
        _franja.Dispose();
        _borde.Dispose();
        _bordeFranja.Dispose();
        _separador.Dispose();
        _derecha.Dispose();
    }
}
