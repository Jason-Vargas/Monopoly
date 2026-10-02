using Monopoly.Core.Estructuras;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Logica;

/// <summary>
/// Respuesta del juego a una solicitud de un jugador: si se aceptó o se rechazó, con un mensaje legible
/// y, según la acción, datos adicionales.
/// </summary>
/// <param name="Exito"><c>true</c> si la acción se realizó; <c>false</c> si el banco la rechazó.</param>
/// <param name="Mensaje">Explicación de lo ocurrido o motivo del rechazo.</param>
public sealed record ResultadoAccion(bool Exito, string Mensaje)
{
    /// <summary>
    /// Id asignado al jugador (en <see cref="Juego.UnirJugador"/>).
    /// </summary>
    public int? IdJugador { get; init; }

    /// <summary>
    /// UID de tarjeta asignado o vinculado (virtual en modo simulado).
    /// </summary>
    public string? UidTarjeta { get; init; }

    /// <summary>
    /// Tirada de dados (en <see cref="Juego.TirarDados"/>).
    /// </summary>
    public TiradaDados? Tirada { get; init; }

    /// <summary>
    /// Movimientos realizados durante la acción, en orden (el de los dados y los de las cartas),
    /// con las casillas recorridas para animarlos.
    /// </summary>
    public ListaSimple<ResultadoMovimiento> Movimientos { get; init; } = new ListaSimple<ResultadoMovimiento>();

    /// <summary>
    /// En una lectura de tarjeta con compra o pago pendiente: <c>true</c> si el pago o la compra se completó,
    /// <c>false</c> si se rechazó (tarjeta de otro, no registrada, saldo insuficiente para comprar o deudor
    /// eliminado por no cubrir el pago); <c>null</c> si no correspondía a un pago.
    /// </summary>
    public bool? PagoAceptado { get; init; }

    /// <summary>
    /// Estado de la partida (en <see cref="Juego.ConsultarEstado"/>).
    /// </summary>
    public InstantaneaJuego? Instantanea { get; init; }

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    /// <param name="mensaje">Descripción de lo ocurrido.</param>
    /// <returns>El resultado.</returns>
    public static ResultadoAccion Correcto(string mensaje)
    {
        return new ResultadoAccion(true, mensaje);
    }

    /// <summary>
    /// Crea un resultado de rechazo.
    /// </summary>
    /// <param name="mensaje">Motivo del rechazo.</param>
    /// <returns>El resultado.</returns>
    public static ResultadoAccion Fallido(string mensaje)
    {
        return new ResultadoAccion(false, mensaje);
    }

    /// <summary>
    /// Devuelve el mensaje con la indicación de éxito o error.
    /// </summary>
    /// <returns>El resultado como texto.</returns>
    public override string ToString()
    {
        return (Exito ? "OK: " : "ERROR: ") + Mensaje;
    }
}
