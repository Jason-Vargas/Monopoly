# Estructuras de datos utilizadas

Todas las colecciones del proyecto son estructuras lineales propias, en `src/Monopoly.Core/Estructuras` (namespace `Monopoly.Core.Estructuras`). En ninguna parte del código (Core, App ni pruebas) se usan `List<T>`, `LinkedList<T>`, `Stack<T>`, `Queue<T>`, `Dictionary<,>` u otras colecciones de .NET, ni LINQ; la prueba `PruebasRestricciones` recorre todos los `.cs` y falla si encuentra alguna. Los recorridos se hacen con delegados (`Recorrer(Action<T>)`, `Buscar(Predicate<T>)`) y la igualdad se compara con `object.Equals`.

| Estructura | Implementación | Dónde se usa | Requisito del enunciado |
|---|---|---|---|
| `ListaCircularDoble<T>` + `NodoCircularDoble<T>` | Nodos con `Anterior` y `Siguiente`; el último apunta a la cabeza y viceversa | Tablero de 40 casillas (`Tablero`); la posición de cada jugador es un nodo | §5 tablero, §9 movimiento por nodos |
| `ColaCircular<T>` | Arreglo con índice de frente y aritmética modular; crece al doble si se llena | Turnos (`Juego._turnos`); monitor del cajero (`MonitorCajero`); registro de actividad (`FormularioJuego`) | §8 turnos |
| `Cola<T>` | Nodos simples con frente y final (FIFO) | Mazos de Casualidad y Arca Comunal (`MazoCartas`) | §10 cartas |
| `ListaDobleEnlazada<T>` + `NodoDoble<T>` | Nodos con `Anterior` y `Siguiente`, cabeza y cola | Historial de transacciones (`HistorialTransacciones`); registro de eventos (`Juego._eventos`) | §12 historial |
| `ListaSimple<T>` + `NodoSimple<T>` | Nodos con `Siguiente`, cabeza y cola | Propiedades de cada jugador (`Jugador`); propiedades del tablero; jugadores de la partida; clientes conectados (`Servidor`); casillas recorridas; mensajes y transacciones de la red; notificaciones y eventos de la interfaz | §4 jugadores |

## ListaCircularDoble&lt;T&gt; — tablero

Cada casilla es un nodo con referencia a la anterior y a la siguiente; la Salida (casilla 0) es la cabeza.

| Operación | Complejidad | Uso |
|---|---|---|
| `Agregar(valor)` | O(1) | Construir el tablero en orden |
| `Avanzar(nodo, pasos, alPasar)` | O(pasos) | `Tablero.MoverJugador`: recorre nodo a nodo y avisa cada casilla (para el premio de Salida y la animación) |
| `Retroceder(nodo, pasos, alPasar)` | O(pasos) | Cartas "retroceda N casillas" |
| `ObtenerNodo(indice)` | O(n/2) | Ir directo a una casilla (cárcel); elige el sentido más corto |
| `IndiceDe(nodo)`, `Recorrer(accion)` | O(n) | Consultas y dibujo del tablero |

El jugador guarda su posición como `NodoCircularDoble<Casilla>` (`Jugador.Posicion`), así que moverse es seguir referencias, no sumar índices. Al pasar por la cabeza avanzando se cuenta el premio de Salida; retroceder no lo cuenta.

## ColaCircular&lt;T&gt; — turnos

Arreglo circular: el frente es el jugador en turno.

| Operación | Complejidad | Uso |
|---|---|---|
| `Encolar(valor)` | O(1) amortizado | Agregar jugadores al iniciar la partida |
| `Frente()` | O(1) | Saber quién tiene el turno |
| `Rotar()` | O(1) | `TerminarTurno`: el jugador actual pasa al final y el siguiente queda al frente |
| `Desencolar()` | O(1) | Descartar la línea más antigua del monitor y del registro |
| `Eliminar(valor)` | O(n) | Sacar a un jugador eliminado o retirado; los demás conservan su orden |
| `Recorrer(accion)` | O(n) | Listar jugadores activos, mostrar el monitor |

Solo contiene jugadores activos: el eliminado sale de la cola y, si era su turno, el frente pasa al siguiente. Los jugadores con turnos por perder se saltan rotando la cola.

## Cola&lt;T&gt; — mazos de cartas

| Operación | Complejidad | Uso |
|---|---|---|
| `Encolar(valor)` | O(1) | Cargar las 13 cartas de cada mazo |
| `Desencolar()` | O(1) | Sacar la carta de arriba |
| `Frente()` | O(1) | Ver la siguiente carta (pruebas) |

`MazoCartas.Sacar()` desencola la carta y la vuelve a encolar: la carta usada pasa al final y se reutiliza (§10). `Barajar(Random)` vacía la cola en un arreglo auxiliar, aplica Fisher-Yates y vuelve a encolar.

## ListaDobleEnlazada&lt;T&gt; — historial de transacciones

| Operación | Complejidad | Uso en `HistorialTransacciones` |
|---|---|---|
| `AgregarAlFinal(valor)` | O(1) | `Registrar` / `Agregar` una transacción |
| `RecorrerDesdeInicio(accion)` | O(n) | `RecorrerDesdeMasAntigua` |
| `RecorrerDesdeFinal(accion)` | O(n) | `RecorrerDesdeMasReciente` (gracias al enlace `Anterior`) |
| `BuscarTodos(condicion)` | O(n) | `BuscarPorJugador` y `BuscarPorTipo` |
| `Obtener(indice)` | O(n) | Consultas puntuales |

`ImprimirTodas()` arma la tabla completa y `ExportarTxt(...)` la escribe en `partidas/partida_AAAA-MM-DD_HH-mm-ss.txt` (§13). Se eligió una lista doblemente enlazada porque el enunciado pide recorrer en ambos sentidos sin copiar ni invertir los datos.

## ListaSimple&lt;T&gt; — propiedades y colecciones generales

| Operación | Complejidad | Uso |
|---|---|---|
| `AgregarAlFinal(valor)` / `AgregarAlInicio(valor)` | O(1) | Comprar una propiedad, conectar un cliente |
| `Eliminar(valor)` | O(n) | Liberar propiedades de un eliminado, desconectar un cliente |
| `Buscar(condicion)`, `BuscarTodos(condicion)`, `Contiene(valor)` | O(n) | Buscar la propiedad de una casilla, filtrar transacciones |
| `Obtener(indice)` | O(n) | Acceso por posición (pruebas, serialización) |
| `Recorrer(accion)` | O(n) | Calcular patrimonio, difundir mensajes a todos los clientes |

`Jugador.AgregarPropiedad` / `QuitarPropiedad` mantienen sincronizado `Propiedad.Propietario`; el patrimonio (saldo + precio de las propiedades) se calcula recorriendo esta lista.

## Pruebas

Cada estructura tiene sus pruebas unitarias en `tests/Monopoly.Tests/Estructuras` (casos vacíos, un elemento, varios elementos, eliminación al inicio, en medio y al final, recorridos en ambos sentidos, crecimiento de la cola circular y rotación). Se ejecutan con `dotnet test Monopoly.sln`.
