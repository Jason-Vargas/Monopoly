using System;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Registro inmutable de una operación económica de la partida.
/// </summary>
public sealed class Transaccion
{
    /// <summary>
    /// Nombre usado como origen o destino cuando participa el banco.
    /// </summary>
    public const string Banco = "BANCO";

    /// <summary>
    /// Crea una transacción.
    /// </summary>
    /// <param name="id">Número de transacción (1 o más).</param>
    /// <param name="fechaHora">Fecha y hora en que ocurrió.</param>
    /// <param name="numeroTurno">Turno en que ocurrió (0 o más).</param>
    /// <param name="tipo">Tipo de transacción.</param>
    /// <param name="origen">Quien paga (nombre del jugador o <see cref="Banco"/>).</param>
    /// <param name="destino">Quien recibe (nombre del jugador o <see cref="Banco"/>).</param>
    /// <param name="monto">Monto transferido (mayor que 0).</param>
    /// <param name="descripcion">Detalle de la operación.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si el id, el turno o el monto no son válidos.</exception>
    /// <exception cref="ArgumentException">Si el origen o el destino están vacíos.</exception>
    public Transaccion(int id, DateTime fechaHora, int numeroTurno, TipoTransaccion tipo,
        string origen, string destino, int monto, string descripcion)
    {
        if (id < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El identificador debe ser 1 o más.");
        }

        if (numeroTurno < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numeroTurno), "El turno no puede ser negativo.");
        }

        if (monto <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto debe ser mayor que 0.");
        }

        if (string.IsNullOrWhiteSpace(origen))
        {
            throw new ArgumentException("El origen no puede estar vacío.", nameof(origen));
        }

        if (string.IsNullOrWhiteSpace(destino))
        {
            throw new ArgumentException("El destino no puede estar vacío.", nameof(destino));
        }

        Id = id;
        FechaHora = fechaHora;
        NumeroTurno = numeroTurno;
        Tipo = tipo;
        Origen = origen;
        Destino = destino;
        Monto = monto;
        Descripcion = descripcion ?? string.Empty;
    }

    /// <summary>
    /// Número de transacción.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Fecha y hora en que ocurrió.
    /// </summary>
    public DateTime FechaHora { get; }

    /// <summary>
    /// Turno en que ocurrió.
    /// </summary>
    public int NumeroTurno { get; }

    /// <summary>
    /// Tipo de transacción.
    /// </summary>
    public TipoTransaccion Tipo { get; }

    /// <summary>
    /// Quien paga (nombre del jugador o <see cref="Banco"/>).
    /// </summary>
    public string Origen { get; }

    /// <summary>
    /// Quien recibe (nombre del jugador o <see cref="Banco"/>).
    /// </summary>
    public string Destino { get; }

    /// <summary>
    /// Monto transferido.
    /// </summary>
    public int Monto { get; }

    /// <summary>
    /// Detalle de la operación.
    /// </summary>
    public string Descripcion { get; }

    /// <summary>
    /// Indica si el participante (nombre de jugador o <see cref="Banco"/>) es origen o destino.
    /// </summary>
    /// <param name="participante">Nombre a buscar.</param>
    /// <returns><c>true</c> si participa en la transacción.</returns>
    public bool Involucra(string participante)
    {
        return Origen == participante || Destino == participante;
    }

    /// <summary>
    /// Devuelve la transacción en una línea legible.
    /// </summary>
    /// <returns>La transacción como texto.</returns>
    public override string ToString()
    {
        return $"#{Id} T{NumeroTurno} {Formato.Nombre(Tipo)}: {Origen} -> {Destino} {Formato.Dinero(Monto)} ({Descripcion})";
    }
}
