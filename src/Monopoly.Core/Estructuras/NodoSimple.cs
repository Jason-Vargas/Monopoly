namespace Monopoly.Core.Estructuras;

/// <summary>
/// Nodo de una estructura simplemente enlazada (<see cref="ListaSimple{T}"/> y <see cref="Cola{T}"/>).
/// </summary>
/// <typeparam name="T">Tipo del valor almacenado.</typeparam>
internal sealed class NodoSimple<T>
{
    /// <summary>
    /// Valor almacenado en el nodo.
    /// </summary>
    public T Valor { get; }

    /// <summary>
    /// Nodo siguiente, o <c>null</c> si este es el último.
    /// </summary>
    public NodoSimple<T>? Siguiente { get; set; }

    /// <summary>
    /// Crea un nodo sin sucesor.
    /// </summary>
    /// <param name="valor">Valor a almacenar.</param>
    public NodoSimple(T valor)
    {
        Valor = valor;
    }
}
