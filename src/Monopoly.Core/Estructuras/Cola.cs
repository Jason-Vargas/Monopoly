using System;

namespace Monopoly.Core.Estructuras;

/// <summary>
/// Cola FIFO enlazada genérica. Se usa para los mazos de cartas de evento:
/// se toma la carta del frente y, tras usarla, se vuelve a encolar al final.
/// </summary>
/// <typeparam name="T">Tipo de los elementos.</typeparam>
public class Cola<T>
{
    private NodoSimple<T>? _frente;
    private NodoSimple<T>? _final;

    /// <summary>
    /// Cantidad de elementos en la cola.
    /// </summary>
    public int Cantidad { get; private set; }

    /// <summary>
    /// Indica si la cola no tiene elementos.
    /// </summary>
    public bool EstaVacia => Cantidad == 0;

    /// <summary>
    /// Agrega un elemento al final de la cola. O(1).
    /// </summary>
    /// <param name="valor">Elemento a agregar.</param>
    public void Encolar(T valor)
    {
        NodoSimple<T> nodo = new NodoSimple<T>(valor);
        if (_final == null)
        {
            _frente = nodo;
        }
        else
        {
            _final.Siguiente = nodo;
        }

        _final = nodo;
        Cantidad++;
    }

    /// <summary>
    /// Quita y devuelve el elemento del frente. O(1).
    /// </summary>
    /// <returns>El elemento que estaba al frente.</returns>
    /// <exception cref="InvalidOperationException">Si la cola está vacía.</exception>
    public T Desencolar()
    {
        if (_frente == null)
        {
            throw new InvalidOperationException("La cola está vacía.");
        }

        T valor = _frente.Valor;
        _frente = _frente.Siguiente;
        if (_frente == null)
        {
            _final = null;
        }

        Cantidad--;
        return valor;
    }

    /// <summary>
    /// Devuelve el elemento del frente sin quitarlo.
    /// </summary>
    /// <returns>El elemento del frente.</returns>
    /// <exception cref="InvalidOperationException">Si la cola está vacía.</exception>
    public T Frente()
    {
        if (_frente == null)
        {
            throw new InvalidOperationException("La cola está vacía.");
        }

        return _frente.Valor;
    }
}
