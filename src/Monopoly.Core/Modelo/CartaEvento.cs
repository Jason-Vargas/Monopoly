using System;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Carta de los mazos de Casualidad y Arca Comunal.
/// </summary>
public sealed class CartaEvento
{
    /// <summary>
    /// Crea una carta de evento.
    /// </summary>
    /// <param name="descripcion">Texto de la carta.</param>
    /// <param name="tipo">Tipo de efecto.</param>
    /// <param name="valor">Monto, cantidad de casillas, turnos o índice de casilla, según el tipo (0 o más).</param>
    /// <exception cref="ArgumentException">Si la descripción está vacía.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si el valor es negativo.</exception>
    public CartaEvento(string descripcion, TipoCartaEvento tipo, int valor)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            throw new ArgumentException("La descripción no puede estar vacía.", nameof(descripcion));
        }

        if (valor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "El valor no puede ser negativo.");
        }

        Descripcion = descripcion;
        Tipo = tipo;
        Valor = valor;
    }

    /// <summary>
    /// Texto de la carta.
    /// </summary>
    public string Descripcion { get; }

    /// <summary>
    /// Tipo de efecto.
    /// </summary>
    public TipoCartaEvento Tipo { get; }

    /// <summary>
    /// Monto, casillas, turnos o índice de casilla, según <see cref="Tipo"/>.
    /// </summary>
    public int Valor { get; }

    /// <summary>
    /// Traduce el efecto de la carta a un <see cref="ResultadoCasilla"/>.
    /// </summary>
    /// <param name="casilla">Casilla de evento en la que se sacó la carta.</param>
    /// <returns>El resultado con el efecto de la carta.</returns>
    public ResultadoCasilla CrearResultado(Casilla casilla)
    {
        ArgumentNullException.ThrowIfNull(casilla);
        string texto = $"{casilla.Nombre}: {Descripcion}";
        return Tipo switch
        {
            TipoCartaEvento.RecibirDinero => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto)
            {
                Carta = this,
                MontoARecibir = Valor,
                TipoCobro = TipoTransaccion.GananciaEvento,
            },
            TipoCartaEvento.PagarDinero => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto)
            {
                Carta = this,
                MontoAPagar = Valor,
                TipoPago = TipoTransaccion.PerdidaEvento,
            },
            TipoCartaEvento.Avanzar => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto)
            {
                Carta = this,
                Movimiento = Valor,
            },
            TipoCartaEvento.Retroceder => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto)
            {
                Carta = this,
                Movimiento = -Valor,
            },
            TipoCartaEvento.PerderTurno => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto)
            {
                Carta = this,
                TurnosAPerder = Valor,
            },
            TipoCartaEvento.IrACasilla => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto)
            {
                Carta = this,
                DestinoIndice = Valor,
                MovimientoDirecto = false,
            },
            TipoCartaEvento.PagarACadaJugador => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto)
            {
                Carta = this,
                MontoACadaJugador = Valor,
            },
            TipoCartaEvento.CobrarACadaJugador => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto)
            {
                Carta = this,
                MontoDeCadaJugador = Valor,
            },
            _ => new ResultadoCasilla(casilla, AccionCasilla.SacarCarta, texto) { Carta = this },
        };
    }

    /// <summary>
    /// Devuelve el texto de la carta.
    /// </summary>
    /// <returns>La descripción.</returns>
    public override string ToString()
    {
        return Descripcion;
    }
}
