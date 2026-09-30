using System;
using Monopoly.Core.Estructuras;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Jugador de la partida. Su estado solo cambia mediante métodos <c>internal</c> que validan cada operación:
/// fuera de Monopoly.Core (red, interfaz) es de solo lectura. El dinero solo lo mueve <see cref="Logica.Banco"/>.
/// </summary>
public class Jugador
{
    /// <summary>
    /// Saldo con el que empieza cada jugador.
    /// </summary>
    public const int SaldoInicial = 1500;

    private ListaSimple<Propiedad> _propiedades = new ListaSimple<Propiedad>();

    /// <summary>
    /// Crea un jugador activo con el saldo inicial y sin propiedades.
    /// </summary>
    /// <param name="id">Identificador del jugador.</param>
    /// <param name="nombre">Nombre visible.</param>
    /// <param name="colorFicha">Color de su ficha.</param>
    /// <param name="saldoInicial">Saldo inicial (0 o más).</param>
    /// <exception cref="ArgumentException">Si el nombre está vacío.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si el saldo inicial es negativo.</exception>
    public Jugador(int id, string nombre, ColorFicha colorFicha, int saldoInicial = SaldoInicial)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(nombre));
        }

        if (saldoInicial < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(saldoInicial), "El saldo inicial no puede ser negativo.");
        }

        Id = id;
        Nombre = nombre.Trim();
        ColorFicha = colorFicha;
        Saldo = saldoInicial;
        Activo = true;
    }

    /// <summary>
    /// Identificador del jugador.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Nombre visible del jugador.
    /// </summary>
    public string Nombre { get; }

    /// <summary>
    /// Dinero disponible.
    /// </summary>
    public int Saldo { get; private set; }

    /// <summary>
    /// Nodo del tablero en el que está la ficha, o <c>null</c> si aún no se colocó.
    /// Lo actualiza <see cref="Tablero"/> al mover al jugador.
    /// </summary>
    public NodoCircularDoble<Casilla>? Posicion { get; internal set; }

    /// <summary>
    /// Casilla en la que está la ficha, o <c>null</c> si aún no se colocó.
    /// </summary>
    public Casilla? CasillaActual => Posicion?.Valor;

    /// <summary>
    /// Indica si el jugador sigue en la partida.
    /// </summary>
    public bool Activo { get; private set; }

    /// <summary>
    /// Turnos que el jugador todavía debe perder.
    /// </summary>
    public int TurnosPorPerder { get; private set; }

    /// <summary>
    /// UID de la tarjeta RFID asociada, o <c>null</c> si no tiene (modo simulado).
    /// </summary>
    public string? UidTarjeta { get; internal set; }

    /// <summary>
    /// Color de la ficha.
    /// </summary>
    public ColorFicha ColorFicha { get; }

    /// <summary>
    /// Cantidad de propiedades del jugador.
    /// </summary>
    public int CantidadPropiedades => _propiedades.Cantidad;

    /// <summary>
    /// Indica si el saldo alcanza para pagar el monto.
    /// </summary>
    /// <param name="monto">Monto a pagar.</param>
    /// <returns><c>true</c> si el saldo es suficiente.</returns>
    public bool PuedePagar(int monto)
    {
        return monto <= Saldo;
    }

    /// <summary>
    /// Suma dinero al saldo.
    /// </summary>
    /// <param name="monto">Monto a acreditar (0 o más).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si el monto es negativo.</exception>
    internal void Acreditar(int monto)
    {
        ValidarMonto(monto);
        Saldo += monto;
    }

    /// <summary>
    /// Resta dinero del saldo.
    /// </summary>
    /// <param name="monto">Monto a debitar (0 o más).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si el monto es negativo.</exception>
    /// <exception cref="InvalidOperationException">Si el saldo no alcanza.</exception>
    internal void Debitar(int monto)
    {
        ValidarMonto(monto);
        if (!PuedePagar(monto))
        {
            throw new InvalidOperationException($"{Nombre} no tiene saldo suficiente para pagar {Formato.Dinero(monto)}.");
        }

        Saldo -= monto;
    }

    /// <summary>
    /// Agrega una propiedad sin dueño al jugador y lo registra como propietario.
    /// No cobra el precio: eso lo hace el banco al validar la compra.
    /// </summary>
    /// <param name="propiedad">Propiedad a agregar.</param>
    /// <exception cref="InvalidOperationException">Si la propiedad ya tiene dueño.</exception>
    internal void AgregarPropiedad(Propiedad propiedad)
    {
        ArgumentNullException.ThrowIfNull(propiedad);
        if (propiedad.Propietario != null)
        {
            throw new InvalidOperationException($"{propiedad.Nombre} ya pertenece a {propiedad.Propietario.Nombre}.");
        }

        _propiedades.AgregarAlFinal(propiedad);
        propiedad.Propietario = this;
    }

    /// <summary>
    /// Quita una propiedad al jugador y la devuelve al banco (queda sin dueño).
    /// </summary>
    /// <param name="propiedad">Propiedad a quitar.</param>
    /// <returns><c>true</c> si el jugador la tenía.</returns>
    internal bool QuitarPropiedad(Propiedad propiedad)
    {
        ArgumentNullException.ThrowIfNull(propiedad);
        if (!_propiedades.Eliminar(propiedad))
        {
            return false;
        }

        propiedad.Propietario = null;
        return true;
    }

    /// <summary>
    /// Indica si el jugador es dueño de la propiedad.
    /// </summary>
    /// <param name="propiedad">Propiedad a consultar.</param>
    /// <returns><c>true</c> si le pertenece.</returns>
    public bool TienePropiedad(Propiedad propiedad)
    {
        return _propiedades.Contiene(propiedad);
    }

    /// <summary>
    /// Cuenta las propiedades del jugador que cumplen una condición.
    /// </summary>
    /// <param name="condicion">Condición a evaluar.</param>
    /// <returns>La cantidad de propiedades que la cumplen.</returns>
    public int ContarPropiedades(Predicate<Propiedad> condicion)
    {
        return _propiedades.BuscarTodos(condicion).Cantidad;
    }

    /// <summary>
    /// Recorre las propiedades del jugador en el orden en que las adquirió.
    /// </summary>
    /// <param name="accion">Acción a ejecutar con cada propiedad.</param>
    public void RecorrerPropiedades(Action<Propiedad> accion)
    {
        _propiedades.Recorrer(accion);
    }

    /// <summary>
    /// Suma turnos que el jugador debe perder.
    /// </summary>
    /// <param name="turnos">Cantidad de turnos (0 o más).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si la cantidad es negativa.</exception>
    internal void PerderTurnos(int turnos)
    {
        if (turnos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnos), "La cantidad de turnos no puede ser negativa.");
        }

        TurnosPorPerder += turnos;
    }

    /// <summary>
    /// Si el jugador tiene turnos por perder, consume uno.
    /// </summary>
    /// <returns><c>true</c> si el jugador debe saltarse este turno.</returns>
    internal bool ConsumirTurnoPerdido()
    {
        if (TurnosPorPerder == 0)
        {
            return false;
        }

        TurnosPorPerder--;
        return true;
    }

    /// <summary>
    /// Marca al jugador como inactivo (eliminado de la partida).
    /// </summary>
    internal void Desactivar()
    {
        Activo = false;
    }

    /// <summary>
    /// Devuelve todas las propiedades del jugador al banco (quedan sin dueño).
    /// </summary>
    /// <returns>La cantidad de propiedades liberadas.</returns>
    internal int LiberarPropiedades()
    {
        int cantidad = _propiedades.Cantidad;
        _propiedades.Recorrer(propiedad => propiedad.Propietario = null);
        _propiedades = new ListaSimple<Propiedad>();
        return cantidad;
    }

    /// <summary>
    /// Calcula el patrimonio: saldo más el precio de compra de todas sus propiedades.
    /// </summary>
    /// <returns>El patrimonio del jugador.</returns>
    public int CalcularPatrimonio()
    {
        int patrimonio = Saldo;
        _propiedades.Recorrer(propiedad => patrimonio += propiedad.PrecioCompra);
        return patrimonio;
    }

    /// <summary>
    /// Devuelve el nombre del jugador.
    /// </summary>
    /// <returns>El nombre.</returns>
    public override string ToString()
    {
        return Nombre;
    }

    private static void ValidarMonto(int monto)
    {
        if (monto < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto no puede ser negativo.");
        }
    }
}
