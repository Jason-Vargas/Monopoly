using Monopoly.Core.Modelo;

namespace Monopoly.Core.Logica;

/// <summary>
/// Copia de solo lectura del estado de un jugador, para enviarla a los clientes.
/// No incluye el UID de la tarjeta, que solo conoce el servidor.
/// </summary>
/// <param name="Id">Identificador.</param>
/// <param name="Nombre">Nombre.</param>
/// <param name="ColorFicha">Color de la ficha.</param>
/// <param name="Saldo">Saldo actual.</param>
/// <param name="Posicion">Índice de la casilla en la que está.</param>
/// <param name="Activo">Si sigue en la partida.</param>
/// <param name="TurnosPorPerder">Turnos que todavía debe perder.</param>
/// <param name="Patrimonio">Saldo más el precio de sus propiedades.</param>
/// <param name="IdsPropiedades">Posiciones de sus propiedades, en orden de adquisición.</param>
/// <param name="TieneTarjetaFisica"><c>true</c> si vinculó una tarjeta RFID real.</param>
public sealed record EstadoJugador(
    int Id,
    string Nombre,
    ColorFicha ColorFicha,
    int Saldo,
    int Posicion,
    bool Activo,
    int TurnosPorPerder,
    int Patrimonio,
    int[] IdsPropiedades,
    bool TieneTarjetaFisica);
