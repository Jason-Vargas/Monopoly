namespace Monopoly.Core.Logica;

/// <summary>
/// Estado general de la partida.
/// </summary>
public enum EstadoPartida
{
    /// <summary>Se aceptan jugadores; aún no se juega.</summary>
    EsperandoJugadores,

    /// <summary>La partida se está jugando.</summary>
    EnCurso,

    /// <summary>La partida terminó y hay un ganador.</summary>
    Finalizada,
}
