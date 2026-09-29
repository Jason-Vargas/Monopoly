using System;

namespace Monopoly.Core.Estructuras;

/// <summary>
/// Lista circular doblemente enlazada genérica: el último nodo apunta al primero y viceversa.
/// Se usa para el tablero; moverse es recorrer nodo a nodo con <see cref="Avanzar"/> o <see cref="Retroceder"/>.
/// </summary>
/// <typeparam name="T">Tipo de los elementos.</typeparam>
public class ListaCircularDoble<T>
{
    /// <summary>
    /// Primer nodo de la lista (índice 0), o <c>null</c> si está vacía.
    /// </summary>
    public NodoCircularDoble<T>? Cabeza { get; private set; }

    /// <summary>
    /// Cantidad de nodos de la lista.
    /// </summary>
    public int Cantidad { get; private set; }

    /// <summary>
    /// Indica si la lista no tiene nodos.
    /// </summary>
    public bool EstaVacia => Cantidad == 0;

    /// <summary>
    /// Agrega un elemento al final (entre el último nodo y la cabeza). O(1).
    /// </summary>
    /// <param name="valor">Elemento a agregar.</param>
    /// <returns>El nodo creado.</returns>
    public NodoCircularDoble<T> Agregar(T valor)
    {
        NodoCircularDoble<T> nodo = new NodoCircularDoble<T>(valor, this);
        if (Cabeza == null)
        {
            Cabeza = nodo;
        }
        else
        {
            NodoCircularDoble<T> ultimo = Cabeza.Anterior;
            nodo.Anterior = ultimo;
            nodo.Siguiente = Cabeza;
            ultimo.Siguiente = nodo;
            Cabeza.Anterior = nodo;
        }

        Cantidad++;
        return nodo;
    }

    /// <summary>
    /// Obtiene el nodo en la posición indicada, recorriendo en el sentido más corto.
    /// </summary>
    /// <param name="indice">Posición, desde 0 (la cabeza).</param>
    /// <returns>El nodo en esa posición.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el índice está fuera de rango.</exception>
    public NodoCircularDoble<T> ObtenerNodo(int indice)
    {
        if (indice < 0 || indice >= Cantidad)
        {
            throw new ArgumentOutOfRangeException(nameof(indice), $"El índice debe estar entre 0 y {Cantidad - 1}.");
        }

        NodoCircularDoble<T> actual = Cabeza!;
        if (indice <= Cantidad / 2)
        {
            for (int i = 0; i < indice; i++)
            {
                actual = actual.Siguiente;
            }
        }
        else
        {
            for (int i = Cantidad; i > indice; i--)
            {
                actual = actual.Anterior;
            }
        }

        return actual;
    }

    /// <summary>
    /// Avanza desde un nodo la cantidad de pasos indicada, siguiendo las referencias <c>Siguiente</c>.
    /// Puede dar varias vueltas completas.
    /// </summary>
    /// <param name="nodo">Nodo de partida; debe pertenecer a esta lista.</param>
    /// <param name="pasos">Cantidad de pasos (0 o más).</param>
    /// <param name="alPasar">Acción opcional que se ejecuta con cada nodo visitado (por ejemplo, para detectar el paso por la salida o animar el movimiento).</param>
    /// <returns>El nodo de llegada.</returns>
    /// <exception cref="ArgumentNullException">Si el nodo es nulo.</exception>
    /// <exception cref="ArgumentException">Si el nodo no pertenece a esta lista.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si los pasos son negativos.</exception>
    public NodoCircularDoble<T> Avanzar(NodoCircularDoble<T> nodo, int pasos, Action<NodoCircularDoble<T>>? alPasar = null)
    {
        ValidarMovimiento(nodo, pasos);
        NodoCircularDoble<T> actual = nodo;
        for (int i = 0; i < pasos; i++)
        {
            actual = actual.Siguiente;
            alPasar?.Invoke(actual);
        }

        return actual;
    }

    /// <summary>
    /// Retrocede desde un nodo la cantidad de pasos indicada, siguiendo las referencias <c>Anterior</c>.
    /// Puede dar varias vueltas completas.
    /// </summary>
    /// <param name="nodo">Nodo de partida; debe pertenecer a esta lista.</param>
    /// <param name="pasos">Cantidad de pasos (0 o más).</param>
    /// <param name="alPasar">Acción opcional que se ejecuta con cada nodo visitado.</param>
    /// <returns>El nodo de llegada.</returns>
    /// <exception cref="ArgumentNullException">Si el nodo es nulo.</exception>
    /// <exception cref="ArgumentException">Si el nodo no pertenece a esta lista.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si los pasos son negativos.</exception>
    public NodoCircularDoble<T> Retroceder(NodoCircularDoble<T> nodo, int pasos, Action<NodoCircularDoble<T>>? alPasar = null)
    {
        ValidarMovimiento(nodo, pasos);
        NodoCircularDoble<T> actual = nodo;
        for (int i = 0; i < pasos; i++)
        {
            actual = actual.Anterior;
            alPasar?.Invoke(actual);
        }

        return actual;
    }

    /// <summary>
    /// Obtiene la posición de un nodo contando desde la cabeza.
    /// </summary>
    /// <param name="nodo">Nodo a ubicar.</param>
    /// <returns>El índice del nodo, o -1 si no pertenece a esta lista.</returns>
    /// <exception cref="ArgumentNullException">Si el nodo es nulo.</exception>
    public int IndiceDe(NodoCircularDoble<T> nodo)
    {
        ArgumentNullException.ThrowIfNull(nodo);
        if (nodo.Lista != this)
        {
            return -1;
        }

        NodoCircularDoble<T> actual = Cabeza!;
        for (int i = 0; i < Cantidad; i++)
        {
            if (actual == nodo)
            {
                return i;
            }

            actual = actual.Siguiente;
        }

        return -1;
    }

    /// <summary>
    /// Ejecuta una acción sobre cada elemento, dando exactamente una vuelta desde la cabeza.
    /// </summary>
    /// <param name="accion">Acción a ejecutar.</param>
    public void Recorrer(Action<T> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);
        NodoCircularDoble<T>? actual = Cabeza;
        for (int i = 0; i < Cantidad; i++)
        {
            accion(actual!.Valor);
            actual = actual.Siguiente;
        }
    }

    /// <summary>
    /// Verifica que el nodo pertenezca a esta lista y que los pasos no sean negativos.
    /// </summary>
    private void ValidarMovimiento(NodoCircularDoble<T> nodo, int pasos)
    {
        ArgumentNullException.ThrowIfNull(nodo);
        if (nodo.Lista != this)
        {
            throw new ArgumentException("El nodo no pertenece a esta lista.", nameof(nodo));
        }

        if (pasos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pasos), "La cantidad de pasos no puede ser negativa.");
        }
    }
}
