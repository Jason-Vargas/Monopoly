namespace Monopoly.Core.Modelo;

/// <summary>
/// Clase base de las casillas que no son propiedades ni eventos: Salida, impuestos,
/// cárcel, Parada Libre y "Vaya a la Cárcel".
/// </summary>
public abstract class CasillaEspecial : Casilla
{
    /// <summary>
    /// Inicializa la casilla especial.
    /// </summary>
    /// <param name="id">Identificador y posición en el tablero.</param>
    /// <param name="nombre">Nombre visible.</param>
    protected CasillaEspecial(int id, string nombre)
        : base(id, nombre)
    {
    }
}
