using System;
using System.Drawing;
using System.Windows.Forms;
using Monopoly.Core.Estructuras;

namespace Monopoly.App.Estilo;

/// <summary>
/// Notificaciones tipo "toast": mensajes breves que entran deslizándose en la esquina inferior derecha del
/// formulario, se apilan y desaparecen solos (o con un clic). Colores para información, éxito y error.
/// </summary>
internal sealed class Notificaciones : IDisposable
{
    private const int Margen = 16;
    private const int Ancho = 340;
    private const int MaximoVisibles = 4;

    private readonly Form _formulario;
    private readonly ListaSimple<AvisoNotificacion> _visibles = new ListaSimple<AvisoNotificacion>();
    private readonly Timer _reloj = new Timer { Interval = 15 };

    /// <summary>
    /// Asocia las notificaciones a un formulario.
    /// </summary>
    public Notificaciones(Form formulario)
    {
        _formulario = formulario;
        _reloj.Tick += (s, e) => Animar();
        formulario.Resize += (s, e) => Reubicar(true);
    }

    /// <summary>
    /// Muestra un mensaje.
    /// </summary>
    /// <param name="texto">Mensaje (una o dos líneas).</param>
    /// <param name="tipo">Color e ícono.</param>
    /// <param name="duracionMs">Tiempo visible; los errores conviene dejarlos más tiempo.</param>
    public void Mostrar(string texto, TipoNotificacion tipo = TipoNotificacion.Informacion, int duracionMs = 3500)
    {
        if (_formulario.IsDisposed)
        {
            return;
        }

        if (_visibles.Cantidad >= MaximoVisibles)
        {
            Quitar(_visibles.Obtener(0));
        }

        AvisoNotificacion aviso = new AvisoNotificacion(texto, tipo, Ancho) { Vence = Environment.TickCount64 + duracionMs };
        aviso.Click += (s, e) => aviso.Vence = 0;
        _visibles.AgregarAlFinal(aviso);
        _formulario.Controls.Add(aviso);
        aviso.BringToFront();
        aviso.Location = new Point(_formulario.ClientSize.Width, _formulario.ClientSize.Height);
        Reubicar(false);
        _reloj.Start();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _reloj.Dispose();
    }

    /// <summary>
    /// Calcula la posición final de cada aviso (el más reciente abajo).
    /// </summary>
    private void Reubicar(bool inmediato)
    {
        int y = _formulario.ClientSize.Height - Margen;
        for (int i = _visibles.Cantidad - 1; i >= 0; i--)
        {
            AvisoNotificacion aviso = _visibles.Obtener(i);
            y -= aviso.Height;
            aviso.Destino = new Point(_formulario.ClientSize.Width - Ancho - Margen, y);
            if (inmediato)
            {
                aviso.Location = aviso.Destino;
            }

            y -= 8;
        }
    }

    private void Animar()
    {
        long ahora = Environment.TickCount64;
        ListaSimple<AvisoNotificacion> vencidos = _visibles.BuscarTodos(a => a.Vence <= ahora && !a.Saliendo);
        vencidos.Recorrer(a => a.Saliendo = true);

        bool hayMovimiento = false;
        ListaSimple<AvisoNotificacion> terminados = new ListaSimple<AvisoNotificacion>();
        _visibles.Recorrer(aviso =>
        {
            Point destino = aviso.Saliendo ? new Point(_formulario.ClientSize.Width + 10, aviso.Top) : aviso.Destino;
            int dx = destino.X - aviso.Left;
            int dy = destino.Y - aviso.Top;
            if (dx != 0 || dy != 0)
            {
                hayMovimiento = true;
                aviso.Location = new Point(aviso.Left + Paso(dx), aviso.Top + Paso(dy));
            }
            else if (aviso.Saliendo)
            {
                terminados.AgregarAlFinal(aviso);
            }
        });

        terminados.Recorrer(Quitar);
        if (!hayMovimiento && _visibles.Buscar(a => true) == null)
        {
            _reloj.Stop();
        }
    }

    private static int Paso(int distancia)
    {
        int paso = distancia / 4;
        return paso != 0 ? paso : Math.Sign(distancia);
    }

    private void Quitar(AvisoNotificacion aviso)
    {
        _visibles.Eliminar(aviso);
        _formulario.Controls.Remove(aviso);
        aviso.Dispose();
        Reubicar(false);
    }
}
