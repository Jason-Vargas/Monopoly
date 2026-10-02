using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;

namespace Monopoly.App;

/// <summary>
/// Lista de las propiedades de un jugador agrupadas por color (en el orden de los grupos del tablero),
/// con el precio de cada una. Se muestra desplegada al hacer clic en el jugador.
/// </summary>
internal sealed class ListaPropiedadesJugador : Control
{
    private const int Ancho = 440;
    private const int AltoEncabezado = 78;
    private const int AltoGrupo = 32;
    private const int AltoFila = 28;

    private static readonly GrupoPropiedad[] Grupos =
    {
        GrupoPropiedad.Marron, GrupoPropiedad.Celeste, GrupoPropiedad.Rosa, GrupoPropiedad.Naranja,
        GrupoPropiedad.Rojo, GrupoPropiedad.Amarillo, GrupoPropiedad.Verde, GrupoPropiedad.AzulOscuro,
        GrupoPropiedad.Ferrocarril, GrupoPropiedad.Servicio,
    };

    private readonly Tablero _tablero = new Tablero();
    private readonly EstadoJugador _jugador;
    private readonly Color _color;

    /// <summary>
    /// Crea la lista para un jugador y calcula su alto.
    /// </summary>
    public ListaPropiedadesJugador(EstadoJugador jugador, Color color)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _jugador = jugador;
        _color = color;
        BackColor = Tema.Marfil;

        int alto = AltoEncabezado + 12;
        foreach (GrupoPropiedad grupo in Grupos)
        {
            int cantidad = ContarDelGrupo(grupo);
            if (cantidad > 0)
            {
                alto += AltoGrupo + (cantidad * AltoFila) + 4;
            }
        }

        Size = new Size(Ancho, jugador.IdsPropiedades.Length == 0 ? AltoEncabezado + 40 : alto);
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        g.Clear(Tema.Marfil);
        using (Pen borde = new Pen(Tema.Borde, 1f))
        {
            g.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
        }

        DibujoFicha.DibujarEnDisco(g, _jugador.FormaFicha, new RectangleF(14, 12, 50, 50), _color, false);
        using (Font titulo = Tema.Titulo(12f))
        using (SolidBrush rojo = new SolidBrush(Tema.Rojo))
        {
            g.DrawString($"Propiedades de {_jugador.Nombre}", titulo, rojo, new RectangleF(74, 8, Width - 82, 34));
        }

        using Font normal = Tema.Texto(9.5f);
        using Font negrita = Tema.Texto(9f, FontStyle.Bold);
        using SolidBrush tinta = new SolidBrush(Tema.Tinta);
        using SolidBrush suave = new SolidBrush(Tema.TintaSuave);
        g.DrawString($"Patrimonio: {Formato.Dinero(_jugador.Patrimonio)}", normal, suave, 76, 42);

        float y = AltoEncabezado;
        if (_jugador.IdsPropiedades.Length == 0)
        {
            g.DrawString("Todavía no tiene propiedades.", normal, suave, 14, y + 8);
            return;
        }

        using StringFormat derecha = new StringFormat { Alignment = StringAlignment.Far };
        foreach (GrupoPropiedad grupo in Grupos)
        {
            if (ContarDelGrupo(grupo) == 0)
            {
                continue;
            }

            // Encabezado del grupo: muestra de color y nombre.
            RectangleF muestra = new RectangleF(16, y + 9, 30, 14);
            using (GraphicsPath forma = Dibujo.Redondeado(muestra, 3))
            using (SolidBrush color = new SolidBrush(Paleta.ColorGrupo(grupo)))
            {
                g.FillPath(color, forma);
            }

            g.DrawString(NombreGrupo(grupo), negrita, tinta, 56, y + 3);
            y += AltoGrupo;

            foreach (int id in _jugador.IdsPropiedades)
            {
                Propiedad? propiedad = _tablero.BuscarPropiedad(id);
                if (propiedad == null || propiedad.Grupo != grupo)
                {
                    continue;
                }

                g.DrawString(propiedad.Nombre, normal, tinta, 56, y);
                g.DrawString(Formato.Dinero(propiedad.PrecioCompra), normal, suave, new RectangleF(0, y, Width - 14, AltoFila), derecha);
                y += AltoFila;
            }

            y += 4;
        }
    }

    private int ContarDelGrupo(GrupoPropiedad grupo)
    {
        int cantidad = 0;
        foreach (int id in _jugador.IdsPropiedades)
        {
            if (_tablero.BuscarPropiedad(id)?.Grupo == grupo)
            {
                cantidad++;
            }
        }

        return cantidad;
    }

    private static string NombreGrupo(GrupoPropiedad grupo)
    {
        return grupo switch
        {
            GrupoPropiedad.Marron => "Marrón",
            GrupoPropiedad.Celeste => "Celeste",
            GrupoPropiedad.Rosa => "Rosa",
            GrupoPropiedad.Naranja => "Naranja",
            GrupoPropiedad.Rojo => "Rojo",
            GrupoPropiedad.Amarillo => "Amarillo",
            GrupoPropiedad.Verde => "Verde",
            GrupoPropiedad.AzulOscuro => "Azul oscuro",
            GrupoPropiedad.Ferrocarril => "Ferrocarriles",
            _ => "Servicios",
        };
    }
}
