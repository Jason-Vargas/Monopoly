namespace Monopoly.Core.Estructuras;

/// <summary>
/// Nodo de una <see cref="ListaDobleEnlazada{T}"/>, con referencias al anterior y al siguiente.
/// </summary>
/// <typeparam name="T">Tipo del valor almacenado.</typeparam>
internal sealed class NodoDoble<T>
{
    /// <summary>
    /// Valor almacenado en el nodo.
    /// </summary>
    public T Valor { get; }

    /// <summary>
    /// Nodo anterior, o <c>null</c> si este es la cabeza.
    /// </summary>
    public NodoDoble<T>? Anterior { get; set; }

    /// <summary>
    /// Nodo siguiente, o <c>null</c> si este es la cola.
    /// </summary>
    public NodoDoble<T>? Siguiente { get; set; }

    /// <summary>
    /// Crea un nodo sin vecinos.
    /// </summary>
    /// <param name="valor">Valor a almacenar.</param>
    public NodoDoble(T valor)
    {
        Valor = valor;
    }
}
