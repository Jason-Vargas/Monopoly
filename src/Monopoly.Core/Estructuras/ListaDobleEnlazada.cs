using System;

namespace Monopoly.Core.Estructuras;

/// <summary>
/// Lista doblemente enlazada genérica con cabeza y cola. Permite recorrer en ambos sentidos
/// y agregar al final en O(1). Se usa para el historial de transacciones
/// (recorrer desde la más antigua o desde la más reciente).
/// </summary>
/// <typeparam name="T">Tipo de los elementos.</typeparam>
public class ListaDobleEnlazada<T>
{
    private NodoDoble<T>? _cabeza;
    private NodoDoble<T>? _cola;

    /// <summary>
    /// Cantidad de elementos de la lista.
    /// </summary>
    public int Cantidad { get; private set; }

    /// <summary>
    /// Indica si la lista no tiene elementos.
    /// </summary>
    public bool EstaVacia => Cantidad == 0;

    /// <summary>
    /// Agrega un elemento al final de la lista. O(1).
    /// </summary>
    /// <param name="valor">Elemento a agregar.</param>
    public void AgregarAlFinal(T valor)
    {
        NodoDoble<T> nodo = new NodoDoble<T>(valor) { Anterior = _cola };
        if (_cola == null)
        {
            _cabeza = nodo;
        }
        else
        {
            _cola.Siguiente = nodo;
        }

        _cola = nodo;
        Cantidad++;
    }

    /// <summary>
    /// Ejecuta una acción sobre cada elemento, desde la cabeza (el más antiguo) hasta la cola.
    /// </summary>
    /// <param name="accion">Acción a ejecutar.</param>
    public void RecorrerDesdeInicio(Action<T> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);
        for (NodoDoble<T>? actual = _cabeza; actual != null; actual = actual.Siguiente)
        {
            accion(actual.Valor);
        }
    }

    /// <summary>
    /// Ejecuta una acción sobre cada elemento, desde la cola (el más reciente) hasta la cabeza.
    /// </summary>
    /// <param name="accion">Acción a ejecutar.</param>
    public void RecorrerDesdeFinal(Action<T> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);
        for (NodoDoble<T>? actual = _cola; actual != null; actual = actual.Anterior)
        {
            accion(actual.Valor);
        }
    }

    /// <summary>
    /// Busca todos los elementos que cumplen la condición, conservando su orden.
    /// </summary>
    /// <param name="condicion">Condición a evaluar.</param>
    /// <returns>Una nueva lista con los elementos encontrados (vacía si no hay).</returns>
    public ListaDobleEnlazada<T> BuscarTodos(Predicate<T> condicion)
    {
        ArgumentNullException.ThrowIfNull(condicion);
        ListaDobleEnlazada<T> resultado = new ListaDobleEnlazada<T>();
        for (NodoDoble<T>? actual = _cabeza; actual != null; actual = actual.Siguiente)
        {
            if (condicion(actual.Valor))
            {
                resultado.AgregarAlFinal(actual.Valor);
            }
        }

        return resultado;
    }

    /// <summary>
    /// Obtiene el elemento en la posición indicada. Recorre desde el extremo más cercano,
    /// por lo que visita como máximo la mitad de los nodos.
    /// </summary>
    /// <param name="indice">Posición, desde 0 (el más antiguo).</param>
    /// <returns>El elemento en esa posición.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el índice está fuera de rango.</exception>
    public T Obtener(int indice)
    {
        if (indice < 0 || indice >= Cantidad)
        {
            throw new ArgumentOutOfRangeException(nameof(indice), $"El índice debe estar entre 0 y {Cantidad - 1}.");
        }

        NodoDoble<T> actual;
        if (indice < Cantidad / 2)
        {
            actual = _cabeza!;
            for (int i = 0; i < indice; i++)
            {
                actual = actual.Siguiente!;
            }
        }
        else
        {
            actual = _cola!;
            for (int i = Cantidad - 1; i > indice; i--)
            {
                actual = actual.Anterior!;
            }
        }

        return actual.Valor;
    }
}
