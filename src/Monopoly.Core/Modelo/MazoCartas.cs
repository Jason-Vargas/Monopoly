using System;
using Monopoly.Core.Estructuras;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Mazo de cartas de evento implementado con una <see cref="Cola{T}"/>: la carta usada
/// se desencola del frente y se vuelve a encolar al final para reutilizarse.
/// </summary>
public class MazoCartas
{
    private readonly Cola<CartaEvento> _cartas = new Cola<CartaEvento>();

    /// <summary>
    /// Crea un mazo con las cartas en el orden recibido.
    /// </summary>
    /// <param name="nombre">Nombre del mazo (por ejemplo "Casualidad").</param>
    /// <param name="cartas">Cartas iniciales (al menos una).</param>
    /// <exception cref="ArgumentException">Si no hay cartas o alguna es nula.</exception>
    public MazoCartas(string nombre, CartaEvento[] cartas)
    {
        ArgumentNullException.ThrowIfNull(cartas);
        if (cartas.Length == 0)
        {
            throw new ArgumentException("El mazo debe tener al menos una carta.", nameof(cartas));
        }

        Nombre = nombre;
        foreach (CartaEvento carta in cartas)
        {
            if (carta == null)
            {
                throw new ArgumentException("El mazo no puede contener cartas nulas.", nameof(cartas));
            }

            _cartas.Encolar(carta);
        }
    }

    /// <summary>
    /// Nombre del mazo.
    /// </summary>
    public string Nombre { get; }

    /// <summary>
    /// Cantidad de cartas del mazo (no cambia al sacar, porque la carta vuelve al final).
    /// </summary>
    public int Cantidad => _cartas.Cantidad;

    /// <summary>
    /// Devuelve la carta que se sacará a continuación, sin sacarla.
    /// </summary>
    /// <returns>La carta del frente.</returns>
    public CartaEvento VerSiguiente()
    {
        return _cartas.Frente();
    }

    /// <summary>
    /// Saca la carta del frente y la coloca al final del mazo.
    /// </summary>
    /// <returns>La carta sacada.</returns>
    public CartaEvento Sacar()
    {
        CartaEvento carta = _cartas.Desencolar();
        _cartas.Encolar(carta);
        return carta;
    }

    /// <summary>
    /// Baraja el mazo: pasa las cartas a un arreglo auxiliar, las mezcla con el algoritmo
    /// de Fisher-Yates y las vuelve a encolar.
    /// </summary>
    /// <param name="aleatorio">Generador de números aleatorios (se puede fijar la semilla en pruebas).</param>
    public void Barajar(Random aleatorio)
    {
        ArgumentNullException.ThrowIfNull(aleatorio);
        CartaEvento[] auxiliar = new CartaEvento[_cartas.Cantidad];
        for (int i = 0; i < auxiliar.Length; i++)
        {
            auxiliar[i] = _cartas.Desencolar();
        }

        for (int i = auxiliar.Length - 1; i > 0; i--)
        {
            int j = aleatorio.Next(i + 1);
            (auxiliar[i], auxiliar[j]) = (auxiliar[j], auxiliar[i]);
        }

        foreach (CartaEvento carta in auxiliar)
        {
            _cartas.Encolar(carta);
        }
    }

    /// <summary>
    /// Recorre las cartas en el orden en que saldrán, dejando el mazo exactamente como estaba
    /// (cada carta se desencola y se vuelve a encolar).
    /// </summary>
    /// <param name="accion">Acción a ejecutar con cada carta.</param>
    public void Recorrer(Action<CartaEvento> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);
        for (int i = 0; i < _cartas.Cantidad; i++)
        {
            CartaEvento carta = _cartas.Desencolar();
            _cartas.Encolar(carta);
            accion(carta);
        }
    }
}
