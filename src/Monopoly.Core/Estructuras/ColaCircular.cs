using System;

namespace Monopoly.Core.Estructuras;

/// <summary>
/// Cola circular genérica sobre un arreglo, usada para administrar los turnos.
/// </summary>
/// <remarks>
/// Decisión de diseño: se implementa sobre un arreglo con índices modulares (frente y cantidad)
/// en lugar de nodos enlazados porque:
/// <list type="bullet">
/// <item>Es la definición clásica de "cola circular": el final del arreglo continúa en su inicio,
/// y el cálculo <c>(frente + i) % capacidad</c> hace explícita esa circularidad.</item>
/// <item><see cref="Rotar"/> (pasar el turno) es O(1) y no crea nodos nuevos en cada turno;
/// con el arreglo lleno basta con mover el índice del frente.</item>
/// <item>La cantidad de jugadores es pequeña y casi fija (4), así que el costo O(n) de
/// <see cref="Eliminar"/> (desplazar elementos al eliminar a un jugador) es despreciable y poco frecuente.</item>
/// </list>
/// Si se llena, la capacidad se duplica, por lo que nunca rechaza elementos.
/// </remarks>
/// <typeparam name="T">Tipo de los elementos.</typeparam>
public class ColaCircular<T>
{
    private T[] _elementos;
    private int _frente;

    /// <summary>
    /// Crea una cola circular vacía.
    /// </summary>
    /// <param name="capacidadInicial">Capacidad inicial del arreglo (1 o más).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si la capacidad es menor que 1.</exception>
    public ColaCircular(int capacidadInicial = 4)
    {
        if (capacidadInicial < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacidadInicial), "La capacidad debe ser al menos 1.");
        }

        _elementos = new T[capacidadInicial];
    }

    /// <summary>
    /// Cantidad de elementos en la cola.
    /// </summary>
    public int Cantidad { get; private set; }

    /// <summary>
    /// Indica si la cola no tiene elementos.
    /// </summary>
    public bool EstaVacia => Cantidad == 0;

    /// <summary>
    /// Tamaño actual del arreglo interno.
    /// </summary>
    public int Capacidad => _elementos.Length;

    /// <summary>
    /// Agrega un elemento al final de la cola. O(1) amortizado.
    /// </summary>
    /// <param name="valor">Elemento a agregar.</param>
    public void Encolar(T valor)
    {
        if (Cantidad == Capacidad)
        {
            Redimensionar(Capacidad * 2);
        }

        _elementos[Posicion(Cantidad)] = valor;
        Cantidad++;
    }

    /// <summary>
    /// Quita y devuelve el elemento del frente. O(1).
    /// </summary>
    /// <returns>El elemento que estaba al frente.</returns>
    /// <exception cref="InvalidOperationException">Si la cola está vacía.</exception>
    public T Desencolar()
    {
        ValidarNoVacia();
        T valor = _elementos[_frente];
        _elementos[_frente] = default!;
        _frente = (_frente + 1) % Capacidad;
        Cantidad--;
        return valor;
    }

    /// <summary>
    /// Devuelve el elemento del frente sin quitarlo (en los turnos, el jugador actual).
    /// </summary>
    /// <returns>El elemento del frente.</returns>
    /// <exception cref="InvalidOperationException">Si la cola está vacía.</exception>
    public T Frente()
    {
        ValidarNoVacia();
        return _elementos[_frente];
    }

    /// <summary>
    /// Pasa el elemento del frente al final de la cola (en los turnos, avanzar al siguiente jugador). O(1).
    /// </summary>
    /// <exception cref="InvalidOperationException">Si la cola está vacía.</exception>
    public void Rotar()
    {
        ValidarNoVacia();
        if (Cantidad == Capacidad)
        {
            // Con el arreglo lleno, el frente ya está justo después del final: basta con mover el índice.
            _frente = (_frente + 1) % Capacidad;
        }
        else
        {
            Encolar(Desencolar());
        }
    }

    /// <summary>
    /// Elimina la primera aparición del elemento conservando el orden de los demás
    /// (en los turnos, quitar a un jugador eliminado). O(n).
    /// Si se elimina el frente, el elemento siguiente pasa a ser el frente.
    /// </summary>
    /// <param name="valor">Elemento a eliminar (se compara con <see cref="object.Equals(object, object)"/>).</param>
    /// <returns><c>true</c> si se encontró y eliminó; <c>false</c> en caso contrario.</returns>
    public bool Eliminar(T valor)
    {
        for (int i = 0; i < Cantidad; i++)
        {
            if (object.Equals(_elementos[Posicion(i)], valor))
            {
                for (int j = i; j < Cantidad - 1; j++)
                {
                    _elementos[Posicion(j)] = _elementos[Posicion(j + 1)];
                }

                _elementos[Posicion(Cantidad - 1)] = default!;
                Cantidad--;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ejecuta una acción sobre cada elemento, desde el frente hasta el final.
    /// </summary>
    /// <param name="accion">Acción a ejecutar.</param>
    public void Recorrer(Action<T> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);
        for (int i = 0; i < Cantidad; i++)
        {
            accion(_elementos[Posicion(i)]);
        }
    }

    /// <summary>
    /// Convierte una posición lógica (0 = frente) en un índice del arreglo.
    /// </summary>
    private int Posicion(int desplazamiento)
    {
        return (_frente + desplazamiento) % Capacidad;
    }

    /// <summary>
    /// Copia los elementos, en orden, a un arreglo nuevo que empieza en el índice 0.
    /// </summary>
    private void Redimensionar(int nuevaCapacidad)
    {
        T[] nuevos = new T[nuevaCapacidad];
        for (int i = 0; i < Cantidad; i++)
        {
            nuevos[i] = _elementos[Posicion(i)];
        }

        _elementos = nuevos;
        _frente = 0;
    }

    /// <summary>
    /// Lanza una excepción si la cola está vacía.
    /// </summary>
    private void ValidarNoVacia()
    {
        if (Cantidad == 0)
        {
            throw new InvalidOperationException("La cola está vacía.");
        }
    }
}
