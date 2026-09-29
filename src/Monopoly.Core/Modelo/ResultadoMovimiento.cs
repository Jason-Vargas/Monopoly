using Monopoly.Core.Estructuras;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Resultado de mover a un jugador por el tablero.
/// </summary>
public sealed class ResultadoMovimiento
{
    /// <summary>
    /// Crea el resultado de un movimiento.
    /// </summary>
    /// <param name="casillaInicial">Casilla de partida.</param>
    /// <param name="casillaFinal">Casilla de llegada.</param>
    /// <param name="casillasRecorridas">Casillas visitadas en orden, sin la inicial y con la final.</param>
    /// <param name="vecesPorSalida">Veces que se pasó (o cayó) por la Salida avanzando.</param>
    public ResultadoMovimiento(Casilla casillaInicial, Casilla casillaFinal, ListaSimple<Casilla> casillasRecorridas, int vecesPorSalida)
    {
        CasillaInicial = casillaInicial;
        CasillaFinal = casillaFinal;
        CasillasRecorridas = casillasRecorridas;
        VecesPorSalida = vecesPorSalida;
    }

    /// <summary>
    /// Casilla de partida.
    /// </summary>
    public Casilla CasillaInicial { get; }

    /// <summary>
    /// Casilla de llegada.
    /// </summary>
    public Casilla CasillaFinal { get; }

    /// <summary>
    /// Casillas visitadas en orden (sin la inicial, con la final), para animar el movimiento.
    /// Vacía si el jugador fue enviado directamente.
    /// </summary>
    public ListaSimple<Casilla> CasillasRecorridas { get; }

    /// <summary>
    /// Veces que el jugador pasó o cayó en la Salida avanzando; cada una da derecho al premio.
    /// Retroceder sobre la Salida no cuenta.
    /// </summary>
    public int VecesPorSalida { get; }

    /// <summary>
    /// Indica si el jugador pasó (o cayó) al menos una vez por la Salida avanzando.
    /// </summary>
    public bool PasoPorSalida => VecesPorSalida > 0;
}
