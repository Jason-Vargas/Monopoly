using System;

namespace Monopoly.Core.Estructuras;

/// <summary>
/// Lista simplemente enlazada genérica. Mantiene referencias a la cabeza y a la cola
/// para que agregar al inicio y al final sean operaciones O(1).
/// Se usa, entre otros, para las propiedades de cada jugador.
/// </summary>
/// <typeparam name="T">Tipo de los elementos.</typeparam>
public class ListaSimple<T>
{
    private NodoSimple<T>? _cabeza;
    private NodoSimple<T>? _cola;

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
        NodoSimple<T> nodo = new NodoSimple<T>(valor);
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
    /// Agrega un elemento al inicio de la lista. O(1).
    /// </summary>
    /// <param name="valor">Elemento a agregar.</param>
    public void AgregarAlInicio(T valor)
    {
        NodoSimple<T> nodo = new NodoSimple<T>(valor) { Siguiente = _cabeza };
        _cabeza = nodo;
        if (_cola == null)
        {
            _cola = nodo;
        }

        Cantidad++;
    }

    /// <summary>
    /// Elimina la primera aparición del elemento indicado. O(n).
    /// </summary>
    /// <param name="valor">Elemento a eliminar (se compara con <see cref="object.Equals(object, object)"/>).</param>
    /// <returns><c>true</c> si se encontró y eliminó; <c>false</c> en caso contrario.</returns>
    public bool Eliminar(T valor)
    {
        NodoSimple<T>? anterior = null;
        NodoSimple<T>? actual = _cabeza;
        while (actual != null)
        {
            if (object.Equals(actual.Valor, valor))
            {
                if (anterior == null)
                {
                    _cabeza = actual.Siguiente;
                }
                else
                {
                    anterior.Siguiente = actual.Siguiente;
                }

                if (actual == _cola)
                {
                    _cola = anterior;
                }

                Cantidad--;
                return true;
            }

            anterior = actual;
            actual = actual.Siguiente;
        }

        return false;
    }

    /// <summary>
    /// Obtiene el elemento en la posición indicada. O(n).
    /// </summary>
    /// <param name="indice">Posición, desde 0.</param>
    /// <returns>El elemento en esa posición.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el índice está fuera de rango.</exception>
    public T Obtener(int indice)
    {
        if (indice < 0 || indice >= Cantidad)
        {
            throw new ArgumentOutOfRangeException(nameof(indice), $"El índice debe estar entre 0 y {Cantidad - 1}.");
        }

        NodoSimple<T> actual = _cabeza!;
        for (int i = 0; i < indice; i++)
        {
            actual = actual.Siguiente!;
        }

        return actual.Valor;
    }

    /// <summary>
    /// Busca el primer elemento que cumple la condición.
    /// </summary>
    /// <param name="condicion">Condición a evaluar.</param>
    /// <returns>El primer elemento que la cumple, o <c>default</c> si ninguno la cumple.</returns>
    public T? Buscar(Predicate<T> condicion)
    {
        ArgumentNullException.ThrowIfNull(condicion);
        for (NodoSimple<T>? actual = _cabeza; actual != null; actual = actual.Siguiente)
        {
            if (condicion(actual.Valor))
            {
                return actual.Valor;
            }
        }

        return default;
    }

    /// <summary>
    /// Busca todos los elementos que cumplen la condición, conservando su orden.
    /// </summary>
    /// <param name="condicion">Condición a evaluar.</param>
    /// <returns>Una nueva lista con los elementos encontrados (vacía si no hay).</returns>
    public ListaSimple<T> BuscarTodos(Predicate<T> condicion)
    {
        ArgumentNullException.ThrowIfNull(condicion);
        ListaSimple<T> resultado = new ListaSimple<T>();
        for (NodoSimple<T>? actual = _cabeza; actual != null; actual = actual.Siguiente)
        {
            if (condicion(actual.Valor))
            {
                resultado.AgregarAlFinal(actual.Valor);
            }
        }

        return resultado;
    }

    /// <summary>
    /// Indica si la lista contiene el elemento.
    /// </summary>
    /// <param name="valor">Elemento a buscar.</param>
    /// <returns><c>true</c> si está en la lista.</returns>
    public bool Contiene(T valor)
    {
        for (NodoSimple<T>? actual = _cabeza; actual != null; actual = actual.Siguiente)
        {
            if (object.Equals(actual.Valor, valor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ejecuta una acción sobre cada elemento, desde la cabeza hasta la cola.
    /// </summary>
    /// <param name="accion">Acción a ejecutar.</param>
    public void Recorrer(Action<T> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);
        for (NodoSimple<T>? actual = _cabeza; actual != null; actual = actual.Siguiente)
        {
            accion(actual.Valor);
        }
    }
}
