using System;
using Monopoly.Core.Logica;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Casilla que se puede comprar. Esta clase representa las calles (alquiler fijo);
/// <see cref="Ferrocarril"/> y <see cref="CompaniaServicio"/> redefinen el cálculo del alquiler.
/// </summary>
public class Propiedad : Casilla
{
    /// <summary>
    /// Crea una propiedad sin dueño.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero.</param>
    /// <param name="nombre">Nombre de la propiedad.</param>
    /// <param name="precioCompra">Precio de compra (mayor que 0).</param>
    /// <param name="alquiler">Alquiler base (0 o más).</param>
    /// <param name="grupo">Grupo de color o tipo al que pertenece.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si el precio o el alquiler no son válidos.</exception>
    public Propiedad(int id, string nombre, int precioCompra, int alquiler, GrupoPropiedad grupo)
        : base(id, nombre)
    {
        if (precioCompra <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precioCompra), "El precio debe ser mayor que 0.");
        }

        if (alquiler < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alquiler), "El alquiler no puede ser negativo.");
        }

        PrecioCompra = precioCompra;
        Alquiler = alquiler;
        Grupo = grupo;
    }

    /// <summary>
    /// Precio de compra al banco.
    /// </summary>
    public int PrecioCompra { get; }

    /// <summary>
    /// Alquiler base.
    /// </summary>
    public int Alquiler { get; }

    /// <summary>
    /// Grupo de color o tipo de la propiedad.
    /// </summary>
    public GrupoPropiedad Grupo { get; }

    /// <summary>
    /// Dueño actual, o <c>null</c> si pertenece al banco. Solo lo cambia <see cref="Jugador"/>
    /// al agregar o quitar la propiedad, para mantener ambos lados consistentes.
    /// </summary>
    public Jugador? Propietario { get; internal set; }

    /// <summary>
    /// Indica si la propiedad no tiene dueño y puede comprarse.
    /// </summary>
    public bool EstaDisponible => Propietario == null;

    /// <inheritdoc/>
    public override string Categoria => "Propiedad";

    /// <summary>
    /// Calcula el alquiler que se cobra actualmente. En las calles es el alquiler fijo.
    /// </summary>
    /// <returns>El alquiler a cobrar.</returns>
    public virtual int CalcularAlquiler()
    {
        return Alquiler;
    }

    /// <summary>
    /// Si la propiedad está disponible, ofrece su compra; si es de otro jugador, exige el alquiler;
    /// si es del mismo jugador, no ocurre nada.
    /// </summary>
    /// <param name="jugador">Jugador que cae en la casilla.</param>
    /// <param name="juego">Partida en curso.</param>
    /// <returns>El resultado con la acción requerida.</returns>
    public override ResultadoCasilla AlCaer(Jugador jugador, Juego juego)
    {
        ArgumentNullException.ThrowIfNull(jugador);

        if (Propietario == null)
        {
            return new ResultadoCasilla(this, AccionCasilla.OfrecerCompra,
                $"{Nombre} está disponible por {Formato.Dinero(PrecioCompra)}.")
            {
                PropiedadEnVenta = this,
            };
        }

        if (Propietario == jugador)
        {
            return new ResultadoCasilla(this, AccionCasilla.Ninguna,
                $"{Nombre} es de {jugador.Nombre}; no paga nada.");
        }

        int alquiler = CalcularAlquiler();
        return new ResultadoCasilla(this, AccionCasilla.PagarAlquiler,
            $"{jugador.Nombre} debe pagar {Formato.Dinero(alquiler)} de alquiler a {Propietario.Nombre} por {Nombre}.")
        {
            MontoAPagar = alquiler,
            Acreedor = Propietario,
            TipoPago = TipoTransaccion.PagoAlquiler,
        };
    }
}
