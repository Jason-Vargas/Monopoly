namespace Monopoly.Core.Modelo;

/// <summary>
/// Compañía de servicios (Electricidad o Agua). En el juego clásico el alquiler es 4 o 10 veces
/// la tirada; por simplicidad se usa la tirada promedio (7): 28 con una compañía y 70 con ambas.
/// </summary>
public class CompaniaServicio : Propiedad
{
    /// <summary>
    /// Precio de compra clásico de una compañía.
    /// </summary>
    public const int PrecioClasico = 150;

    /// <summary>
    /// Alquiler cuando el dueño tiene una sola compañía (4 × 7).
    /// </summary>
    public const int AlquilerConUna = 28;

    /// <summary>
    /// Alquiler cuando el dueño tiene ambas compañías (10 × 7).
    /// </summary>
    public const int AlquilerConAmbas = 70;

    /// <summary>
    /// Crea una compañía de servicios con el precio y alquiler clásicos.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero.</param>
    /// <param name="nombre">Nombre de la compañía.</param>
    public CompaniaServicio(int id, string nombre)
        : base(id, nombre, PrecioClasico, AlquilerConUna, GrupoPropiedad.Servicio)
    {
    }

    /// <inheritdoc/>
    public override string Categoria => "Servicio";

    /// <summary>
    /// Calcula el alquiler según si el dueño tiene una o ambas compañías.
    /// </summary>
    /// <returns>El alquiler a cobrar.</returns>
    public override int CalcularAlquiler()
    {
        int cantidad = Propietario?.ContarPropiedades(p => p.Grupo == GrupoPropiedad.Servicio) ?? 1;
        return cantidad >= 2 ? AlquilerConAmbas : AlquilerConUna;
    }
}
