using System;
using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla de Salida. El premio se cobra al pasar por ella avanzando, lo que incluye caer en ella;
/// eso lo detecta <see cref="Tablero.MoverJugador"/> con <see cref="ResultadoMovimiento.VecesPorSalida"/>,
/// por lo que <see cref="AlCaer"/> no vuelve a pagarlo (evita cobrar dos veces).
/// </summary>
public class Salida : CasillaEspecial
{
    /// <summary>
    /// Premio clásico por pasar o caer en la Salida.
    /// </summary>
    public const int PremioClasico = 200;

    /// <summary>
    /// Crea la casilla de Salida.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero (normalmente 0).</param>
    /// <param name="premio">Premio por pasar (0 o más).</param>
    public Salida(int id, int premio = PremioClasico)
        : base(id, "Salida")
    {
        if (premio < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(premio), "El premio no puede ser negativo.");
        }

        Premio = premio;
    }

    /// <summary>
    /// Monto que el banco paga cada vez que un jugador pasa por la Salida.
    /// </summary>
    public int Premio { get; }

    /// <inheritdoc/>
    public override string Categoria => "Salida";

    /// <inheritdoc/>
    public override ResultadoCasilla AlCaer(Jugador jugador, Juego juego)
    {
        ArgumentNullException.ThrowIfNull(jugador);
        return new ResultadoCasilla(this, AccionCasilla.Ninguna,
            $"{jugador.Nombre} está en la Salida (el premio de {Formato.Dinero(Premio)} se cobra al llegar).");
    }
}
