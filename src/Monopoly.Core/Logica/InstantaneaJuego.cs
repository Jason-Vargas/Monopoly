using Monopoly.Core.Modelo;

namespace Monopoly.Core.Logica;

/// <summary>
/// Copia de solo lectura del estado completo de la partida en un momento dado, para enviarla a los clientes.
/// </summary>
/// <param name="Estado">Estado de la partida.</param>
/// <param name="Fase">Fase del turno actual.</param>
/// <param name="NumeroTurno">Número de turno global.</param>
/// <param name="MaximoTurnos">Máximo de turnos configurado.</param>
/// <param name="IdJugadorEnTurno">Jugador que tiene el turno, o <c>null</c> si no hay partida en curso.</param>
/// <param name="IdGanador">Ganador, o <c>null</c> si la partida no ha terminado.</param>
/// <param name="IdPropiedadEnVenta">Propiedad ofrecida al jugador actual, o <c>null</c>.</param>
/// <param name="IdDeudor">Jugador con un pago pendiente, o <c>null</c>.</param>
/// <param name="DescripcionPagoPendiente">Descripción del pago pendiente, o <c>null</c>.</param>
/// <param name="MontoPagoPendiente">Monto total del pago pendiente (0 si no hay).</param>
/// <param name="UltimaTirada">Última tirada de dados, o <c>null</c>.</param>
/// <param name="Jugadores">Estado de cada jugador, en orden de ingreso.</param>
/// <param name="CantidadEventos">Cantidad de eventos registrados.</param>
/// <param name="CantidadTransacciones">Cantidad de transacciones registradas.</param>
public sealed record InstantaneaJuego(
    EstadoPartida Estado,
    FaseTurno Fase,
    int NumeroTurno,
    int MaximoTurnos,
    int? IdJugadorEnTurno,
    int? IdGanador,
    int? IdPropiedadEnVenta,
    int? IdDeudor,
    string? DescripcionPagoPendiente,
    int MontoPagoPendiente,
    TiradaDados? UltimaTirada,
    EstadoJugador[] Jugadores,
    int CantidadEventos,
    int CantidadTransacciones);
