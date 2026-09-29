namespace Monopoly.Core.Modelo;

/// <summary>
/// Resultado de lanzar los dos dados.
/// </summary>
/// <param name="Dado1">Valor del primer dado (1 a 6).</param>
/// <param name="Dado2">Valor del segundo dado (1 a 6).</param>
public readonly record struct TiradaDados(int Dado1, int Dado2)
{
    /// <summary>
    /// Suma de ambos dados.
    /// </summary>
    public int Total => Dado1 + Dado2;

    /// <summary>
    /// Indica si ambos dados muestran el mismo valor.
    /// </summary>
    public bool EsDoble => Dado1 == Dado2;

    /// <summary>
    /// Devuelve la tirada en formato "3 + 4 = 7".
    /// </summary>
    /// <returns>La tirada como texto.</returns>
    public override string ToString()
    {
        return $"{Dado1} + {Dado2} = {Total}";
    }
}
