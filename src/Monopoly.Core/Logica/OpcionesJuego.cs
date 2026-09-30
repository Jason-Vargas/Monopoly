using System;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Logica;

/// <summary>
/// Configuración de una partida. Todos los valores tienen un predeterminado; las pruebas
/// los usan para controlar dados, cartas, reloj y carpeta de salida.
/// </summary>
public sealed class OpcionesJuego
{
    /// <summary>
    /// Máximo de turnos predeterminado.
    /// </summary>
    public const int MaximoTurnosPredeterminado = 100;

    /// <summary>
    /// Máximo de turnos globales; al completarse gana el jugador con mayor patrimonio.
    /// </summary>
    public int MaximoTurnos { get; init; } = MaximoTurnosPredeterminado;

    /// <summary>
    /// Saldo con el que empieza cada jugador.
    /// </summary>
    public int SaldoInicial { get; init; } = Jugador.SaldoInicial;

    /// <summary>
    /// Dados de la partida; por defecto, aleatorios.
    /// </summary>
    public Dado? Dado { get; init; }

    /// <summary>
    /// Generador para barajar los mazos; por defecto, uno nuevo sin semilla.
    /// </summary>
    public Random? Aleatorio { get; init; }

    /// <summary>
    /// Fuente de fecha y hora; por defecto, la hora local actual.
    /// </summary>
    public Func<DateTime>? Reloj { get; init; }

    /// <summary>
    /// Cartas de Casualidad a usar en ese orden, sin barajar; por defecto, las clásicas barajadas.
    /// </summary>
    public CartaEvento[]? CartasCasualidad { get; init; }

    /// <summary>
    /// Cartas de Arca Comunal a usar en ese orden, sin barajar; por defecto, las clásicas barajadas.
    /// </summary>
    public CartaEvento[]? CartasArcaComunal { get; init; }

    /// <summary>
    /// Carpeta donde se exporta el historial al finalizar la partida.
    /// </summary>
    public string CarpetaPartidas { get; init; } = "partidas";

    /// <summary>
    /// Indica si se exporta automáticamente el historial a TXT al finalizar.
    /// </summary>
    public bool ExportarAlFinalizar { get; init; } = true;
}
