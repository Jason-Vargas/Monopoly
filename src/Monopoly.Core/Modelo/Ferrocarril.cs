namespace Monopoly.Core.Modelo;

/// <summary>
/// Ferrocarril: su alquiler se duplica por cada ferrocarril adicional que tenga el dueño
/// (25, 50, 100 y 200 con el alquiler base clásico de 25).
/// </summary>
public class Ferrocarril : Propiedad
{
    /// <summary>
    /// Precio de compra clásico de un ferrocarril.
    /// </summary>
    public const int PrecioClasico = 200;

    /// <summary>
    /// Alquiler clásico con un solo ferrocarril.
    /// </summary>
    public const int AlquilerClasico = 25;

    /// <summary>
    /// Crea un ferrocarril con el precio y alquiler clásicos.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero.</param>
    /// <param name="nombre">Nombre del ferrocarril.</param>
    public Ferrocarril(int id, string nombre)
        : base(id, nombre, PrecioClasico, AlquilerClasico, GrupoPropiedad.Ferrocarril)
    {
    }

    /// <inheritdoc/>
    public override string Categoria => "Ferrocarril";

    /// <summary>
    /// Calcula el alquiler según cuántos ferrocarriles tiene el dueño: base × 2^(cantidad − 1).
    /// </summary>
    /// <returns>El alquiler a cobrar.</returns>
    public override int CalcularAlquiler()
    {
        int cantidad = Propietario?.ContarPropiedades(p => p.Grupo == GrupoPropiedad.Ferrocarril) ?? 1;
        int alquiler = Alquiler;
        for (int i = 1; i < cantidad; i++)
        {
            alquiler *= 2;
        }

        return alquiler;
    }
}
