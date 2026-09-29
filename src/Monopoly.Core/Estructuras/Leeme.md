# Monopoly.Core.Estructuras
Estructuras de datos lineales propias. Es el único lugar donde se definen colecciones.

| Estructura | Implementación | Uso en el juego |
|---|---|---|
| `ListaSimple<T>` | Nodos simples con cabeza y cola | Propiedades de cada jugador y colecciones internas |
| `ListaDobleEnlazada<T>` | Nodos dobles con cabeza y cola | Historial de transacciones (recorrido en ambos sentidos) |
| `ListaCircularDoble<T>` / `NodoCircularDoble<T>` | Nodos dobles circulares | Tablero (movimiento nodo a nodo) |
| `ColaCircular<T>` | Arreglo con índices modulares | Turnos (rotar al siguiente jugador, eliminar jugadores) |
| `Cola<T>` | Nodos simples FIFO | Mazos de cartas de evento (la carta usada vuelve al final) |
