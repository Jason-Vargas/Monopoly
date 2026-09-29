namespace Monopoly.Core.Estructuras;

/// <summary>
/// Nodo de una <see cref="ListaCircularDoble{T}"/>. Como la lista es circular, <see cref="Anterior"/>
/// y <see cref="Siguiente"/> nunca son nulos: un nodo solo apunta a sí mismo en ambos sentidos.
/// En el tablero, cada nodo representa una casilla.
/// </summary>
/// <typeparam name="T">Tipo del valor almacenado.</typeparam>
public sealed class NodoCircularDoble<T>
{
    /// <summary>
    /// Valor almacenado en el nodo.
    /// </summary>
    public T Valor { get; }

    /// <summary>
    /// Nodo anterior en la lista circular.
    /// </summary>
    public NodoCircularDoble<T> Anterior { get; internal set; }

    /// <summary>
    /// Nodo siguiente en la lista circular.
    /// </summary>
    public NodoCircularDoble<T> Siguiente { get; internal set; }

    /// <summary>
    /// Lista a la que pertenece el nodo; sirve para validar que un nodo recibido sea de la lista.
    /// </summary>
    internal ListaCircularDoble<T> Lista { get; }

    /// <summary>
    /// Crea un nodo que apunta a sí mismo en ambos sentidos.
    /// </summary>
    /// <param name="valor">Valor a almacenar.</param>
    /// <param name="lista">Lista propietaria.</param>
    internal NodoCircularDoble(T valor, ListaCircularDoble<T> lista)
    {
        Valor = valor;
        Anterior = this;
        Siguiente = this;
        Lista = lista;
    }
}
