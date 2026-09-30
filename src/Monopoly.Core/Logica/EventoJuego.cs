using System;
using System.Globalization;

namespace Monopoly.Core.Logica;

/// <summary>
/// Entrada del registro de eventos de la partida (lo que se muestra a todos los jugadores).
/// </summary>
/// <param name="Numero">Número consecutivo del evento (desde 1).</param>
/// <param name="FechaHora">Momento en que ocurrió.</param>
/// <param name="NumeroTurno">Turno en que ocurrió (0 antes de iniciar).</param>
/// <param name="Texto">Descripción legible.</param>
public sealed record EventoJuego(int Numero, DateTime FechaHora, int NumeroTurno, string Texto)
{
    /// <summary>
    /// Devuelve el evento en formato "[15:30:00] T3 texto".
    /// </summary>
    /// <returns>El evento como texto.</returns>
    public override string ToString()
    {
        return $"[{FechaHora.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] T{NumeroTurno} {Texto}";
    }
}
