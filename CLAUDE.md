# CLAUDE.md — Monopoly Distribuido con Estructuras Lineales

Proyecto 1 de Algoritmos y Estructuras de Datos 1 (TEC, II Semestre 2026). El enunciado completo está en `docs/enunciado.pdf`; ante cualquier duda, manda el enunciado.

## Decisiones técnicas (obligatorias)

- **C# con .NET 8** (`net8.0` en Core y Tests, `net8.0-windows` en App).
- Solución `Monopoly.sln` con tres proyectos:
  - `src/Monopoly.Core` — biblioteca de clases. Namespaces:
    - `Monopoly.Core.Estructuras` — estructuras de datos propias (único lugar donde se definen colecciones).
    - `Monopoly.Core.Modelo` — entidades: Jugador, Casilla y derivadas, CartaEvento, Transaccion, Dado, Tablero.
    - `Monopoly.Core.Logica` — Juego, Banco, turnos, validaciones, exportación TXT.
    - `Monopoly.Core.Red` — Servidor, Cliente y protocolo TCP.
    - `Monopoly.Core.Hardware` — dado electrónico y lector RFID (Arduino), con implementación simulada.
  - `src/Monopoly.App` — Windows Forms (referencia a Core).
  - `tests/Monopoly.Tests` — xUnit (referencia a Core).
- **Sin paquetes NuGet externos**, salvo xUnit (y su SDK/runner de pruebas) y, más adelante, `System.IO.Ports` para el hardware.
- Configuración común en `Directory.Build.props`: `Nullable` habilitado, `ImplicitUsings` **deshabilitado** (los usings implícitos traen `System.Linq` y `System.Collections.Generic`), documentación XML generada.
- **Tema**: Monopoly clásico (edición Atlantic City) con nombres de casillas en español y estética clásica. **No** copiar el logotipo ni la mascota oficial.
- **Hardware al final**: hasta entonces todo debe funcionar en **modo simulado** (dados aleatorios, identificación de jugador sin tarjeta). El hardware se abstrae detrás de interfaces para poder cambiar simulado ↔ real.
- Los reportes y partidas generados se guardan en `partidas/` (ignorada por git).

## ⛔ Prohibición de colecciones y LINQ

En **cualquier** parte del código (Core, App, red, interfaz y pruebas) está PROHIBIDO usar:

- `List<T>`, `LinkedList<T>`, `Stack<T>`, `Queue<T>`, `PriorityQueue<,>`, `Dictionary<,>`, `SortedList`, `SortedDictionary`, `SortedSet`, `HashSet<T>`, `ArrayList`, `Hashtable`, `Collection<T>`, `ObservableCollection<T>`, colecciones concurrentes o inmutables.
- Cualquier `using System.Collections...` y cualquier `using System.Linq` o método de extensión LINQ.

Permitido: arreglos nativos (`T[]`), `string`, `StringBuilder`, `Random`, `DateTime`, `System.IO`, `System.Net.Sockets`, `System.Threading`, y los delegados `Action<T>`, `Func<T>`, `Predicate<T>`.

Todas las colecciones son estructuras propias de `Monopoly.Core.Estructuras` (ver su `Leeme.md`: qué estructura se usa para qué). Las comparaciones de igualdad dentro de las estructuras usan `object.Equals(a, b)` (no `EqualityComparer<T>`, que vive en `System.Collections.Generic`). Los recorridos se exponen con métodos que reciben delegados (p. ej. `Recorrer(Action<T>)`, `Buscar(Predicate<T>)`) o devolviendo arreglos, **no** implementando `IEnumerable<T>`.

La prueba `PruebasRestricciones` (en Monopoly.Tests) recorre todos los `.cs` y falla si detecta una infracción. **Debe pasar siempre.**

## Convenciones de código

- Todo en **español**: clases, métodos, propiedades, variables, parámetros y comentarios (se usan los nombres del enunciado: `Juego`, `Servidor`, `Cliente`, `Jugador`, `Banco`, `Tablero`, `Casilla`, `Propiedad`, `CasillaEvento`, `CasillaEspecial`, `CartaEvento`, `Dado`, `Transaccion`).
- Sin tildes ni ñ en identificadores (`Transaccion`, `Posicion`, `Anio`); sí en comentarios y textos.
- PascalCase para tipos y miembros públicos; camelCase para locales y parámetros; `_camelCase` para campos privados.
- Comentarios XML (`///`) en todas las clases y miembros públicos.
- Nullable habilitado: no ignorar advertencias de nulabilidad.
- Namespaces con ámbito de archivo (`namespace X;`) y usings explícitos.
- Un tipo por archivo; el nombre del archivo coincide con el del tipo.
- El cliente nunca modifica el estado: toda acción se envía al servidor (banco), que valida y responde.
- Cada estructura de datos nueva lleva sus pruebas unitarias en Monopoly.Tests.
- Compilar con `dotnet build` y probar con `dotnet test` antes de cada commit.
- **Polimorfismo real**: la lógica, la red y la interfaz NO usan `is`, `as`, casts ni `switch` sobre el tipo de casilla. Lo que dependa del tipo se resuelve con miembros virtuales (`AlCaer`, `Categoria`, `CalcularAlquiler`). En las pruebas sí se permiten casts para inspeccionar el tablero.

## Diseño del modelo (`Monopoly.Core.Modelo`)

- **`Casilla.AlCaer(Jugador, Juego)` no modifica el estado** (la única excepción es rotar el mazo al sacar carta). Devuelve un `ResultadoCasilla` cuyos efectos son datos: `PropiedadEnVenta`, `MontoAPagar` + `Acreedor` (null = banco) + `TipoPago`, `MontoARecibir` + `TipoCobro`, `MontoACadaJugador`, `MontoDeCadaJugador`, `Movimiento` (±casillas), `DestinoIndice` + `MovimientoDirecto`, `TurnosAPerder`, `Carta`. La lógica (Banco/Juego) valida y aplica cada efecto de forma genérica; `Accion` es solo informativa.
- Jerarquía: `Casilla` → `Propiedad` (calles) → `Ferrocarril` (25·2^(n−1)), `CompaniaServicio` (28 con una, 70 con ambas); `Casilla` → `CasillaEvento` → `CasillaCasualidad`, `CasillaArcaComunal`; `Casilla` → `CasillaEspecial` → `Salida`, `Impuesto`, `CarcelSoloVisita`, `ParadaLibre`, `VayaALaCarcel`.
- `Casilla.Id` es a la vez el identificador y la posición en el tablero (0 = Salida, 10 = cárcel, 30 = Vaya a la Cárcel).
- **Premio de Salida**: lo detecta `Tablero.MoverJugador` (`ResultadoMovimiento.VecesPorSalida`) al pasar o caer en ella avanzando; retroceder no cuenta y `EnviarJugadorA` (cárcel) tampoco. `Salida.AlCaer` no vuelve a pagar.
- `Tablero`: `MoverJugador(j, ±pasos)` recorre nodo a nodo y devuelve las casillas recorridas; `MoverJugadorHasta(j, indice)` avanza hasta un destino (cartas "Avance hasta..."); `EnviarJugadorA(j, indice)` teletransporta sin cobrar.
- `Jugador`: saldo solo con `Acreditar`/`Debitar` (Debitar lanza si no alcanza; usar `PuedePagar` antes). `AgregarPropiedad`/`QuitarPropiedad` mantienen sincronizado `Propiedad.Propietario`. La posición la cambia solo `Tablero`.
- Cartas: `TipoCartaEvento` tiene los 6 tipos del enunciado más `PagarACadaJugador` y `CobrarACadaJugador` (generan `PagoEntreJugadores`). `MazoCartas` envuelve una `Cola<CartaEvento>`: `Sacar()` desencola y reencola; `Barajar(Random)` usa un arreglo auxiliar (Fisher-Yates). 13 cartas por mazo en `CartasClasicas`.
- `Dado`: aleatorio, con semilla (`new Dado(semilla)`) o determinista (`Dado.ConValoresFijos(3, 4, ...)`), devuelve `TiradaDados`.
- `HistorialTransacciones` (sobre `ListaDobleEnlazada<Transaccion>`): `Registrar(...)` numera y fecha; `ExportarTxt(ruta, fechaPartida, jugadores)` escribe encabezado + tabla en UTF-8. El reloj es inyectable para pruebas.
- Los métodos que modifican a `Jugador` (`Acreditar`, `Debitar`, `AgregarPropiedad`, `QuitarPropiedad`, `PerderTurnos`, `Desactivar`, `LiberarPropiedades`, setter de `UidTarjeta`) son **`internal`**: fuera de Core (red, interfaz) el modelo es de solo lectura. `InternalsVisibleTo` los abre solo a Monopoly.Tests.
- `Tablero` guarda además las 28 propiedades en una `ListaSimple<Propiedad>` (`BuscarPropiedad(indice)`), para encontrarlas sin chequear el tipo de casilla.

## Diseño de la lógica (`Monopoly.Core.Logica`)

- **`Banco`** es la única clase que mueve dinero: `Pagar` (banco → jugador), `Cobrar` (jugador → banco), `Transferir` (jugador → jugador). Valida monto > 0 y saldo (lanza sin modificar nada) y registra cada `Transaccion`.
- **`Juego`** es el estado oficial. Configuración con `OpcionesJuego` (dado, reloj, cartas sin barajar, saldo inicial, `MaximoTurnos` = 100, `CarpetaPartidas` = "partidas", `ExportarAlFinalizar`).
- API pública (todas con `lock` sobre `_candado`; devuelven `ResultadoAccion` con `Exito`/`Mensaje`, nunca lanzan por reglas del juego): `UnirJugador(nombre)` → id + UID virtual `VIRTUAL-{id}`; `VincularTarjeta(id, uid)`; `IniciarPartida(id)` (solo el organizador = primer jugador); `TirarDados(id)`; `ComprarPropiedad(id)`; `NoComprar(id)`; `IdentificarTarjeta(uid)`; `TerminarTurno(id)`; `ConsultarEstado(id)` → `InstantaneaJuego`; `ConsultarTransacciones(id)`; `ExportarHistorial()`; `ObtenerEventosDesde(n)`; `ObtenerJugador(id)`.
- Estado: `EstadoPartida` (EsperandoJugadores, EnCurso, Finalizada) y `FaseTurno` (EsperandoDados → EsperandoDecisionCompra / EsperandoPago → PuedeTerminar).
- Turnos: `ColaCircular<Jugador>` solo con jugadores activos; el frente es el jugador en turno. `TerminarTurno` rota; los jugadores con `TurnosPorPerder` se saltan consumiendo un turno (el turno saltado cuenta en `NumeroTurno`). Sin regla de dobles.
- Efectos: `TirarDados` mueve y resuelve la casilla; `AplicarEfectos` aplica el `ResultadoCasilla` de forma genérica y encadena movimientos de cartas (máx. 10 niveles). Los UID se normalizan (sin espacios, en mayúsculas).
- **Pagos obligatorios** (alquiler, impuesto, carta de pagar, pagar a cada jugador) → `EsperandoPago` hasta `IdentificarTarjeta(uid del deudor)`. `CobrarACadaJugador` (cumpleaños) se cobra automáticamente a los demás.
- **Eliminación**: si no cubre un pago obligatorio, paga lo que tiene ("pago parcial"), queda inactivo, sus propiedades se liberan y sale de la cola; si era su turno, pasa al siguiente.
- **Fin**: queda un único activo, o se completa `MaximoTurnos` (gana el mayor patrimonio; en empate, el primero en la cola). Exporta automáticamente `partidas/partida_AAAA-MM-DD_HH-mm-ss.txt`.
- Registro de eventos legible: `ListaDobleEnlazada<EventoJuego>`; la red envía los nuevos con `ObtenerEventosDesde`.
- Para el servidor: `ObtenerInstantanea()` (sin validar solicitante) y `LeerHistorial(func)` (lee el historial con el candado tomado).

## Diseño de la red (`Monopoly.Core.Red`) — ver `docs/protocolo.md`

- Protocolo de texto: una línea UTF-8 por mensaje, `COMANDO|campo|...`, con escapes `\\`, `\|`, `\n`, `\r` (`Protocolo.Codificar/Decodificar`, `Mensaje`). **Todo cambio al protocolo se refleja en `docs/protocolo.md`.**
- `Servidor`: `TcpListener` (puerto 5000 por defecto, `IPAddress.Any`; las pruebas usan puerto 0 y loopback), un hilo por cliente, conexiones en `ListaSimple<ConexionCliente>` con `lock`. El jugador se identifica por la conexión. Las solicitudes se procesan de a una (`_candadoProcesamiento`) y tras cada acción aceptada se difunden `EVENTO`s nuevos + `ESTADO` (+ `FIN` una vez). Los rechazos van solo al solicitante como `ERROR`.
- Desconexión: el jugador sigue en la partida (no se elimina ni se salta su turno); `CONECTAR` con el mismo nombre lo reconecta con su id.
- `ESTADO` se serializa con `SerializadorEstado` (18 campos fijos + 10 por jugador) sobre `EstadoRed` (instantánea + dueños + casillas recorridas). `TRANSACCIONES` con `SerializadorTransacciones` (8 campos por transacción).
- `Cliente`: `TcpClient` + hilo lector; notifica con eventos C# (`BienvenidaRecibida`, `ErrorRecibido`, `EstadoActualizado`, `DadosRecibidos`, `EventoRecibido`, `TransaccionesRecibidas`, `FinRecibido`, `Desconectado`, `MensajeRecibido`). **Se disparan en el hilo lector**: la interfaz debe usar `BeginInvoke`.
- `PAGAR_CON_TARJETA` = modo simulado (UID del propio jugador). El lector RFID real se integrará en la etapa de hardware.
- `src/Monopoly.ClienteConsola` es una herramienta **temporal** de depuración (`servidor [puerto]` / `cliente [host] [puerto] [nombre]`).

## Resumen de requisitos del enunciado

1. **Descripción general**: Monopoly Electrónico simplificado, cliente-servidor, POO, estructuras lineales y lector RFID. Partida de 4 jugadores; demo con al menos 2 computadoras en red. Temática visual libre.
2. **Objetivos**: POO, estructuras lineales propias usadas en una aplicación real, cliente-servidor, múltiples jugadores, estado centralizado, transacciones, integración de hardware.
3. **Arquitectura**: el servidor/banco mantiene el estado oficial y corre en la máquina del organizador, de forma automática. Todos los jugadores (incluido el organizador) piden acciones al servidor. El cliente no modifica directamente saldo, posición, propiedades, turno, dados ni transacciones. El banco valida antes de modificar.
4. **Jugadores**: identificador, nombre, saldo, posición actual, estado activo/inactivo, propiedades adquiridas (en una estructura lineal propia).
5. **Tablero**: **lista circular doblemente enlazada**; cada nodo es una casilla con referencia a anterior y siguiente. Mínimo **24 casillas**. Todos ven el tablero y los movimientos.
6. **Tipos de casillas**: clase base `Casilla`; derivadas `Propiedad`, `CasillaEvento`, `CasillaEspecial`, con comportamiento propio (herencia y polimorfismo).
7. **Propiedades**: identificador, nombre, precio de compra, alquiler, propietario. Al caer: disponible → puede comprar; de otro → paga alquiler; propia → nada.
8. **Turnos**: **cola circular**. Se sabe quién tiene el turno y se avanza automáticamente al terminar. Nadie actúa fuera de su turno.
9. **Dados**: dos dados electrónicos por turno; el resultado se transmite a todos; el jugador recorre los nodos del tablero y el movimiento se muestra a los demás.
10. **Cartas de eventos**: al usar una carta, pasa al final (reutilizable → estructura tipo cola). Eventos: recibir dinero, pagar dinero, avanzar, retroceder, perder un turno, ir a una casilla.
11. **Transacciones**: toda operación económica genera una. Campos: identificador, fecha y hora, número de turno, tipo, jugador origen, jugador destino, monto, descripción. El banco puede ser origen o destino. Tipos: compra de propiedad, pago de alquiler, pago al banco, pago entre jugadores, ganancia por evento, pérdida por evento, premio por pasar por inicio.
12. **Historial**: estructura elegida por el grupo (p. ej. lista doblemente enlazada) que permita agregar, recorrer desde la más antigua, desde la más reciente, buscar por jugador, buscar por tipo e imprimir todas.
13. **Exportación**: historial a **TXT** con número de transacción, turno, tipo, origen, destino, monto y descripción.
14. **Módulo electrónico (cajero)**: dado electrónico de 2 dígitos (2 displays de 7 segmentos + botón) y lector RFID RC522 (≥ 2 tarjetas). La tarjeta solo identifica al jugador; el saldo oficial vive en el servidor. Flujo: el juego exige pago → el jugador acerca la tarjeta → el dispositivo envía el ID → el servidor identifica, valida, modifica el saldo, genera la transacción y lo muestra. No sustituye las estructuras de datos.
15. **Clases mínimas**: Juego, Servidor, Cliente, Jugador, Banco, Tablero, Casilla, Propiedad, CasillaEvento, CasillaEspecial, CartaEvento, Dado, Transaccion, más las de las estructuras. No usar List, LinkedList, Stack, Queue, PriorityQueue o equivalentes.
16. **Comunicación**: **sockets TCP** con protocolo propio. Acciones de ejemplo: CONECTAR, TIRAR_DADOS, COMPRAR_PROPIEDAD, NO_COMPRAR, TERMINAR_TURNO, CONSULTAR_ESTADO, CONSULTAR_TRANSACCIONES. El servidor actualiza a los clientes tras cada acción importante.
17. **Validaciones** del servidor: jugar fuera de turno, comprar sin saldo, comprar propiedad con dueño, lanzar dados más de una vez por turno, pagar con saldo insuficiente sin aplicar eliminación, modificar información desde el cliente.
18. **Fin de partida**: se elimina a quien no pueda cubrir un pago obligatorio. Termina cuando queda un solo jugador activo o se alcanza un máximo configurable de turnos (gana el mayor patrimonio = saldo + valor de propiedades).
19. **Entregables**: código (GitHub), diagrama UML, documentación de estructuras, descripción del protocolo, archivo de transacciones de una partida, manual breve de ejecución, código y conexión del hardware.
20. **Defensa**: 4 jugadores conectados, ≥ 2 computadoras, turnos, dados, movimiento, compra, alquiler, carta, transacciones, uso de estructuras, consulta del historial, archivo de transacciones. El profesor puede pedir modificaciones sobre las estructuras en vivo.

**Rúbrica**: POO 10 %, estructuras y su uso 20 %, cliente-servidor 10 %, lógica del juego 15 %, cajero electrónico 10 %, ≥ 10 commits significativos y distribuidos 10 %, documentación/UML/calidad 10 %, defensa 15 %.

## Checklist de requisitos

Marcar con `[x]` al completar cada punto en su etapa.

- [x] 0. Estructura inicial de la solución (Core, App, Tests), CLAUDE.md, .gitignore, README
- [x] 0.1. Estructuras genéricas en `Monopoly.Core.Estructuras` con pruebas: `ListaSimple<T>`, `ListaDobleEnlazada<T>`, `ListaCircularDoble<T>`/`NodoCircularDoble<T>`, `ColaCircular<T>` (sobre arreglo), `Cola<T>` (enlazada). Base de los puntos 4, 5, 8, 10 y 12, que se marcarán cuando el modelo las use.
- [ ] 1. Descripción general: partida de 4 jugadores en al menos 2 computadoras; temática definida (Atlantic City en español)
- [ ] 2. Objetivos cubiertos (POO, estructuras propias, cliente-servidor, estado centralizado, transacciones, hardware)
- [ ] 3. Arquitectura: servidor/banco con estado oficial en la máquina del organizador; clientes solo solicitan acciones — *lógica y servidor TCP listos; falta que la interfaz levante el servidor en la máquina del organizador*
- [x] 4. Jugador con id, nombre, saldo, posición, estado activo y propiedades en estructura lineal propia (`ListaSimple<Propiedad>`)
- [ ] 5. Tablero como lista circular doblemente enlazada con ≥ 24 casillas, visible para todos — *modelo listo (40 casillas clásicas en `ListaCircularDoble`); falta mostrarlo a todos (interfaz/red)*
- [x] 6. Casilla base + Propiedad, CasillaEvento, CasillaEspecial con polimorfismo
- [x] 7. Propiedades: datos mínimos y comportamiento comprar / alquiler / propia
- [x] 8. Turnos con cola circular, avance automático, bloqueo fuera de turno
- [ ] 9. Dos dados por turno, resultado transmitido, movimiento nodo a nodo mostrado a todos — *lógica y red listas (`DADOS` + casillas recorridas en `ESTADO`); falta animarlo en la interfaz*
- [x] 10. Mazo de cartas de evento que devuelve la carta al final; los 6 tipos de evento
- [x] 11. Transacciones con todos los campos y los 7 tipos mínimos; banco como origen/destino
- [x] 12. Historial: agregar, recorrer ambos sentidos, buscar por jugador y por tipo, imprimir todo
- [x] 13. Exportación del historial a TXT con los campos mínimos
- [ ] 14. Módulo electrónico: dado de 2 dígitos + RFID (primero simulado, luego Arduino) — *flujo de pago con tarjeta listo en modo simulado (UID virtual, `VincularTarjeta`, `IdentificarTarjeta`); falta el Arduino*
- [x] 15. Todas las clases mínimas presentes; ninguna colección de .NET
- [x] 16. Comunicación por sockets TCP con protocolo documentado (`docs/protocolo.md`) y difusión de estado
- [x] 17. Todas las validaciones del servidor (en `Juego`; el cliente no puede modificar el modelo porque sus mutadores son `internal`)
- [x] 18. Eliminación de jugadores y fin de partida (último activo o límite de turnos con patrimonio)
- [ ] 19. Entregables: UML, doc. de estructuras, protocolo, archivo de transacciones, manual, hardware
- [ ] 20. Preparación de la defensa (guion de demostración cubriendo todos los puntos)

## Comandos

```bash
dotnet build Monopoly.sln
dotnet test Monopoly.sln
dotnet run --project src/Monopoly.App
# Depuración del protocolo (compilar antes; --no-build permite varias instancias a la vez):
dotnet run --project src/Monopoly.ClienteConsola --no-build -- servidor 5000
dotnet run --project src/Monopoly.ClienteConsola --no-build -- cliente 127.0.0.1 5000 Ana
```
