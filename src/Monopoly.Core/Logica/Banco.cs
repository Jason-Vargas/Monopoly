using System;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Logica;

/// <summary>
/// Banco de la partida: la única clase que mueve dinero. Cada operación valida el monto y el saldo,
/// modifica los saldos y registra su <see cref="Transaccion"/> en el historial. El banco puede ser
/// origen (<see cref="Pagar"/>) o destino (<see cref="Cobrar"/>) de una transacción.
/// </summary>
public class Banco
{
    private readonly HistorialTransacciones _historial;

    /// <summary>
    /// Crea el banco asociado a un historial.
    /// </summary>
    /// <param name="historial">Historial donde se registran las transacciones.</param>
    public Banco(HistorialTransacciones historial)
    {
        ArgumentNullException.ThrowIfNull(historial);
        _historial = historial;
    }

    /// <summary>
    /// El banco paga dinero a un jugador (premio de Salida, ganancia por carta).
    /// </summary>
    /// <param name="destino">Jugador que recibe.</param>
    /// <param name="monto">Monto (mayor que 0).</param>
    /// <param name="tipo">Tipo de transacción.</param>
    /// <param name="numeroTurno">Turno actual.</param>
    /// <param name="descripcion">Motivo.</param>
    /// <returns>La transacción registrada.</returns>
    public Transaccion Pagar(Jugador destino, int monto, TipoTransaccion tipo, int numeroTurno, string descripcion)
    {
        ArgumentNullException.ThrowIfNull(destino);
        ValidarOperacion(monto, numeroTurno);
        destino.Acreditar(monto);
        return _historial.Registrar(numeroTurno, tipo, Transaccion.Banco, destino.Nombre, monto, descripcion);
    }

    /// <summary>
    /// Un jugador paga dinero al banco (compra, impuesto, pérdida por carta).
    /// </summary>
    /// <param name="origen">Jugador que paga.</param>
    /// <param name="monto">Monto (mayor que 0).</param>
    /// <param name="tipo">Tipo de transacción.</param>
    /// <param name="numeroTurno">Turno actual.</param>
    /// <param name="descripcion">Motivo.</param>
    /// <returns>La transacción registrada.</returns>
    /// <exception cref="InvalidOperationException">Si el saldo no alcanza (no se modifica nada).</exception>
    public Transaccion Cobrar(Jugador origen, int monto, TipoTransaccion tipo, int numeroTurno, string descripcion)
    {
        ArgumentNullException.ThrowIfNull(origen);
        ValidarOperacion(monto, numeroTurno);
        ValidarSaldo(origen, monto);
        origen.Debitar(monto);
        return _historial.Registrar(numeroTurno, tipo, origen.Nombre, Transaccion.Banco, monto, descripcion);
    }

    /// <summary>
    /// Transfiere dinero de un jugador a otro (alquiler, pagos por cartas).
    /// </summary>
    /// <param name="origen">Jugador que paga.</param>
    /// <param name="destino">Jugador que recibe.</param>
    /// <param name="monto">Monto (mayor que 0).</param>
    /// <param name="tipo">Tipo de transacción.</param>
    /// <param name="numeroTurno">Turno actual.</param>
    /// <param name="descripcion">Motivo.</param>
    /// <returns>La transacción registrada.</returns>
    /// <exception cref="ArgumentException">Si origen y destino son el mismo jugador.</exception>
    /// <exception cref="InvalidOperationException">Si el saldo no alcanza (no se modifica nada).</exception>
    public Transaccion Transferir(Jugador origen, Jugador destino, int monto, TipoTransaccion tipo, int numeroTurno, string descripcion)
    {
        ArgumentNullException.ThrowIfNull(origen);
        ArgumentNullException.ThrowIfNull(destino);
        if (origen == destino)
        {
            throw new ArgumentException("Un jugador no puede transferirse dinero a sí mismo.", nameof(destino));
        }

        ValidarOperacion(monto, numeroTurno);
        ValidarSaldo(origen, monto);
        origen.Debitar(monto);
        destino.Acreditar(monto);
        return _historial.Registrar(numeroTurno, tipo, origen.Nombre, destino.Nombre, monto, descripcion);
    }

    private static void ValidarOperacion(int monto, int numeroTurno)
    {
        if (monto <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto debe ser mayor que 0.");
        }

        if (numeroTurno < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numeroTurno), "El turno no puede ser negativo.");
        }
    }

    private static void ValidarSaldo(Jugador origen, int monto)
    {
        if (!origen.PuedePagar(monto))
        {
            throw new InvalidOperationException(
                $"{origen.Nombre} no tiene saldo suficiente: tiene {Formato.Dinero(origen.Saldo)} y debe pagar {Formato.Dinero(monto)}.");
        }
    }
}
