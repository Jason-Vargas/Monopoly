using System;
using Monopoly.Core.Estructuras;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Tablero del Monopoly clásico (edición Atlantic City, nombres en español) con sus 40 casillas
/// en el orden original, almacenadas en una <see cref="ListaCircularDoble{T}"/>. Cada nodo es una casilla
/// y los jugadores se mueven recorriendo los nodos uno a uno.
/// </summary>
public class Tablero
{
    /// <summary>
    /// Cantidad de casillas del tablero clásico.
    /// </summary>
    public const int CantidadCasillas = 40;

    /// <summary>
    /// Índice de la Salida.
    /// </summary>
    public const int IndiceSalida = 0;

    /// <summary>
    /// Índice de la cárcel.
    /// </summary>
    public const int IndiceCarcel = 10;

    private readonly ListaCircularDoble<Casilla> _casillas = new ListaCircularDoble<Casilla>();

    /// <summary>
    /// Crea el tablero clásico con precios y alquileres base.
    /// </summary>
    public Tablero()
    {
        Salida = new Salida(0);
        _casillas.Agregar(Salida);
        _casillas.Agregar(new Propiedad(1, "Avenida Mediterráneo", 60, 2, GrupoPropiedad.Marron));
        _casillas.Agregar(new CasillaArcaComunal(2));
        _casillas.Agregar(new Propiedad(3, "Avenida Báltica", 60, 4, GrupoPropiedad.Marron));
        _casillas.Agregar(new Impuesto(4, "Impuesto sobre la Renta", 200));
        _casillas.Agregar(new Ferrocarril(5, "Ferrocarril Reading"));
        _casillas.Agregar(new Propiedad(6, "Avenida Oriental", 100, 6, GrupoPropiedad.Celeste));
        _casillas.Agregar(new CasillaCasualidad(7));
        _casillas.Agregar(new Propiedad(8, "Avenida Vermont", 100, 6, GrupoPropiedad.Celeste));
        _casillas.Agregar(new Propiedad(9, "Avenida Connecticut", 120, 8, GrupoPropiedad.Celeste));
        _casillas.Agregar(new CarcelSoloVisita(10));
        _casillas.Agregar(new Propiedad(11, "Plaza San Carlos", 140, 10, GrupoPropiedad.Rosa));
        _casillas.Agregar(new CompaniaServicio(12, "Compañía de Electricidad"));
        _casillas.Agregar(new Propiedad(13, "Avenida de los Estados", 140, 10, GrupoPropiedad.Rosa));
        _casillas.Agregar(new Propiedad(14, "Avenida Virginia", 160, 12, GrupoPropiedad.Rosa));
        _casillas.Agregar(new Ferrocarril(15, "Ferrocarril Pensilvania"));
        _casillas.Agregar(new Propiedad(16, "Plaza Santiago", 180, 14, GrupoPropiedad.Naranja));
        _casillas.Agregar(new CasillaArcaComunal(17));
        _casillas.Agregar(new Propiedad(18, "Avenida Tennessee", 180, 14, GrupoPropiedad.Naranja));
        _casillas.Agregar(new Propiedad(19, "Avenida Nueva York", 200, 16, GrupoPropiedad.Naranja));
        _casillas.Agregar(new ParadaLibre(20));
        _casillas.Agregar(new Propiedad(21, "Avenida Kentucky", 220, 18, GrupoPropiedad.Rojo));
        _casillas.Agregar(new CasillaCasualidad(22));
        _casillas.Agregar(new Propiedad(23, "Avenida Indiana", 220, 18, GrupoPropiedad.Rojo));
        _casillas.Agregar(new Propiedad(24, "Avenida Illinois", 240, 20, GrupoPropiedad.Rojo));
        _casillas.Agregar(new Ferrocarril(25, "Ferrocarril B&O"));
        _casillas.Agregar(new Propiedad(26, "Avenida Atlántico", 260, 22, GrupoPropiedad.Amarillo));
        _casillas.Agregar(new Propiedad(27, "Avenida Ventnor", 260, 22, GrupoPropiedad.Amarillo));
        _casillas.Agregar(new CompaniaServicio(28, "Compañía de Agua"));
        _casillas.Agregar(new Propiedad(29, "Jardines Marvin", 280, 24, GrupoPropiedad.Amarillo));
        _casillas.Agregar(new VayaALaCarcel(30));
        _casillas.Agregar(new Propiedad(31, "Avenida Pacífico", 300, 26, GrupoPropiedad.Verde));
        _casillas.Agregar(new Propiedad(32, "Avenida Carolina del Norte", 300, 26, GrupoPropiedad.Verde));
        _casillas.Agregar(new CasillaArcaComunal(33));
        _casillas.Agregar(new Propiedad(34, "Avenida Pensilvania", 320, 28, GrupoPropiedad.Verde));
        _casillas.Agregar(new Ferrocarril(35, "Ferrocarril Vía Corta"));
        _casillas.Agregar(new CasillaCasualidad(36));
        _casillas.Agregar(new Propiedad(37, "Plaza del Parque", 350, 35, GrupoPropiedad.AzulOscuro));
        _casillas.Agregar(new Impuesto(38, "Impuesto de Lujo", 100));
        _casillas.Agregar(new Propiedad(39, "Paseo Marítimo", 400, 50, GrupoPropiedad.AzulOscuro));
    }

    /// <summary>
    /// Casilla de Salida (da acceso al premio por pasar).
    /// </summary>
    public Salida Salida { get; }

    /// <summary>
    /// Cantidad de casillas del tablero.
    /// </summary>
    public int Cantidad => _casillas.Cantidad;

    /// <summary>
    /// Obtiene la casilla en la posición indicada.
    /// </summary>
    /// <param name="indice">Posición (0 a 39).</param>
    /// <returns>La casilla.</returns>
    public Casilla ObtenerCasilla(int indice)
    {
        return _casillas.ObtenerNodo(indice).Valor;
    }

    /// <summary>
    /// Recorre las casillas en orden, desde la Salida.
    /// </summary>
    /// <param name="accion">Acción a ejecutar con cada casilla.</param>
    public void Recorrer(Action<Casilla> accion)
    {
        _casillas.Recorrer(accion);
    }

    /// <summary>
    /// Busca las casillas que cumplen una condición, en orden.
    /// </summary>
    /// <param name="condicion">Condición a evaluar.</param>
    /// <returns>Una lista nueva con las casillas encontradas.</returns>
    public ListaSimple<Casilla> BuscarCasillas(Predicate<Casilla> condicion)
    {
        ArgumentNullException.ThrowIfNull(condicion);
        ListaSimple<Casilla> resultado = new ListaSimple<Casilla>();
        _casillas.Recorrer(casilla =>
        {
            if (condicion(casilla))
            {
                resultado.AgregarAlFinal(casilla);
            }
        });
        return resultado;
    }

    /// <summary>
    /// Coloca al jugador en la Salida, sin cobrar premio.
    /// </summary>
    /// <param name="jugador">Jugador a colocar.</param>
    public void ColocarEnSalida(Jugador jugador)
    {
        ArgumentNullException.ThrowIfNull(jugador);
        jugador.Posicion = _casillas.Cabeza;
    }

    /// <summary>
    /// Mueve al jugador nodo por nodo: hacia adelante si los pasos son positivos y hacia atrás si son negativos.
    /// </summary>
    /// <param name="jugador">Jugador a mover; debe estar en el tablero.</param>
    /// <param name="pasos">Casillas a moverse (negativo para retroceder).</param>
    /// <returns>Las casillas recorridas y las veces que pasó por la Salida avanzando.</returns>
    /// <exception cref="InvalidOperationException">Si el jugador no está en el tablero.</exception>
    public ResultadoMovimiento MoverJugador(Jugador jugador, int pasos)
    {
        NodoCircularDoble<Casilla> inicio = ObtenerPosicion(jugador);
        ListaSimple<Casilla> recorridas = new ListaSimple<Casilla>();
        int vecesPorSalida = 0;
        NodoCircularDoble<Casilla> nodoSalida = _casillas.Cabeza!;

        NodoCircularDoble<Casilla> fin;
        if (pasos >= 0)
        {
            fin = _casillas.Avanzar(inicio, pasos, nodo =>
            {
                recorridas.AgregarAlFinal(nodo.Valor);
                if (nodo == nodoSalida)
                {
                    vecesPorSalida++;
                }
            });
        }
        else
        {
            fin = _casillas.Retroceder(inicio, -pasos, nodo => recorridas.AgregarAlFinal(nodo.Valor));
        }

        jugador.Posicion = fin;
        return new ResultadoMovimiento(inicio.Valor, fin.Valor, recorridas, vecesPorSalida);
    }

    /// <summary>
    /// Avanza al jugador nodo por nodo hasta la casilla indicada (por ejemplo, por una carta
    /// "Avance hasta..."). Si pasa por la Salida, se indica en el resultado.
    /// </summary>
    /// <param name="jugador">Jugador a mover; debe estar en el tablero.</param>
    /// <param name="indiceDestino">Posición de destino (0 a 39).</param>
    /// <returns>Las casillas recorridas y las veces que pasó por la Salida.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el destino no existe.</exception>
    public ResultadoMovimiento MoverJugadorHasta(Jugador jugador, int indiceDestino)
    {
        ValidarIndice(indiceDestino);
        int actual = ObtenerPosicion(jugador).Valor.Id;
        int pasos = (indiceDestino - actual + Cantidad) % Cantidad;
        return MoverJugador(jugador, pasos);
    }

    /// <summary>
    /// Envía al jugador directamente a una casilla, sin recorrer el tablero ni pasar por la Salida
    /// (por ejemplo, a la cárcel).
    /// </summary>
    /// <param name="jugador">Jugador a mover; debe estar en el tablero.</param>
    /// <param name="indiceDestino">Posición de destino (0 a 39).</param>
    /// <returns>El resultado, sin casillas recorridas.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el destino no existe.</exception>
    public ResultadoMovimiento EnviarJugadorA(Jugador jugador, int indiceDestino)
    {
        ValidarIndice(indiceDestino);
        NodoCircularDoble<Casilla> inicio = ObtenerPosicion(jugador);
        NodoCircularDoble<Casilla> destino = _casillas.ObtenerNodo(indiceDestino);
        jugador.Posicion = destino;
        return new ResultadoMovimiento(inicio.Valor, destino.Valor, new ListaSimple<Casilla>(), 0);
    }

    private static NodoCircularDoble<Casilla> ObtenerPosicion(Jugador jugador)
    {
        ArgumentNullException.ThrowIfNull(jugador);
        return jugador.Posicion ?? throw new InvalidOperationException($"{jugador.Nombre} no está en el tablero.");
    }

    private void ValidarIndice(int indice)
    {
        if (indice < 0 || indice >= Cantidad)
        {
            throw new ArgumentOutOfRangeException(nameof(indice), $"La casilla debe estar entre 0 y {Cantidad - 1}.");
        }
    }
}
