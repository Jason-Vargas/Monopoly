using System.Windows.Forms;

namespace Monopoly.App;

/// <summary>
/// Controla el paso entre ventanas: inicio → sala de espera → juego. Al cerrar la ventana activa
/// (sin estar cambiando de ventana) se cierra la sesión (y el servidor, si es el organizador) y la aplicación.
/// Si un jugador perdió la conexión y pidió reconectarse, se vuelve a la ventana de inicio ya rellena.
/// </summary>
internal sealed class ContextoAplicacion : ApplicationContext
{
    private SesionJuego? _sesion;
    private bool _cambiandoVentana;

    /// <summary>
    /// Muestra la ventana de inicio.
    /// </summary>
    public ContextoAplicacion(ArgumentosInicio argumentos)
    {
        MostrarInicio(argumentos);
    }

    private void MostrarInicio(ArgumentosInicio argumentos)
    {
        FormularioInicio inicio = new FormularioInicio(argumentos);
        inicio.SesionCreada += sesion =>
        {
            _sesion = sesion;
            MostrarSala(inicio);
        };
        inicio.FormClosed += AlCerrarVentana;
        inicio.Show();
    }

    private void MostrarSala(Form anterior)
    {
        FormularioSalaEspera sala = new FormularioSalaEspera(_sesion!);
        sala.PartidaIniciada += () => MostrarJuego(sala);
        sala.FormClosed += AlCerrarVentana;
        sala.Show();
        CerrarSinSalir(anterior);
    }

    private void MostrarJuego(Form anterior)
    {
        FormularioJuego juego = new FormularioJuego(_sesion!);
        juego.FormClosed += AlCerrarVentana;
        juego.Show();
        CerrarSinSalir(anterior);
    }

    private void CerrarSinSalir(Form ventana)
    {
        _cambiandoVentana = true;
        ventana.Close();
        _cambiandoVentana = false;
    }

    private void AlCerrarVentana(object? remitente, FormClosedEventArgs e)
    {
        if (_cambiandoVentana)
        {
            return;
        }

        SesionJuego? sesion = _sesion;
        _sesion = null;
        sesion?.Dispose();

        bool reconectar = (remitente as FormularioJuego)?.VolverParaReconectar == true
                          || (remitente as FormularioSalaEspera)?.VolverParaReconectar == true;
        if (reconectar && sesion != null)
        {
            MostrarInicio(ArgumentosInicio.ParaReconectar(sesion.Nombre, sesion.Host, sesion.Puerto));
            return;
        }

        ExitThread();
    }
}
