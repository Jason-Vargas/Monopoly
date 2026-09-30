namespace Monopoly.App;

/// <summary>
/// Elemento de un ComboBox con un texto visible y un valor asociado.
/// </summary>
/// <param name="Texto">Texto que ve el usuario.</param>
/// <param name="Valor">Valor que se envía al servidor.</param>
internal sealed record OpcionLista(string Texto, string Valor)
{
    /// <summary>
    /// Devuelve el texto visible.
    /// </summary>
    public override string ToString()
    {
        return Texto;
    }
}
