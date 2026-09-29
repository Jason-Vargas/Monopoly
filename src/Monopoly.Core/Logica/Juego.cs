using System;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Logica;

/// <summary>
/// Partida en curso. Por ahora solo reúne los componentes del modelo (tablero, mazos, dado e historial);
/// los jugadores, los turnos y la aplicación de reglas se implementan en la etapa de lógica.
/// </summary>
public class Juego
{
    /// <summary>
    /// Crea una partida con dados aleatorios y mazos barajados al azar.
    /// </summary>
    public Juego()
        : this(new Dado(), new Random())
    {
    }

    /// <summary>
    /// Crea una partida con los dados y el generador indicados (útil para pruebas reproducibles).
    /// </summary>
    /// <param name="dado">Dados de la partida.</param>
    /// <param name="aleatorio">Generador usado para barajar los mazos.</param>
    /// <param name="reloj">Fuente de fecha y hora; por defecto, la hora local actual.</param>
    public Juego(Dado dado, Random aleatorio, Func<DateTime>? reloj = null)
    {
        ArgumentNullException.ThrowIfNull(dado);
        ArgumentNullException.ThrowIfNull(aleatorio);

        Func<DateTime> fuenteHora = reloj ?? (() => DateTime.Now);
        Dado = dado;
        Tablero = new Tablero();
        Historial = new HistorialTransacciones(fuenteHora);
        FechaInicio = fuenteHora();

        MazoCasualidad = new MazoCartas("Casualidad", CartasClasicas.CrearCasualidad());
        MazoArcaComunal = new MazoCartas("Arca Comunal", CartasClasicas.CrearArcaComunal());
        MazoCasualidad.Barajar(aleatorio);
        MazoArcaComunal.Barajar(aleatorio);
    }

    /// <summary>
    /// Tablero de la partida.
    /// </summary>
    public Tablero Tablero { get; }

    /// <summary>
    /// Mazo de cartas de Casualidad.
    /// </summary>
    public MazoCartas MazoCasualidad { get; }

    /// <summary>
    /// Mazo de cartas de Arca Comunal.
    /// </summary>
    public MazoCartas MazoArcaComunal { get; }

    /// <summary>
    /// Dados de la partida.
    /// </summary>
    public Dado Dado { get; }

    /// <summary>
    /// Historial de transacciones.
    /// </summary>
    public HistorialTransacciones Historial { get; }

    /// <summary>
    /// Fecha y hora de inicio de la partida.
    /// </summary>
    public DateTime FechaInicio { get; }
}
