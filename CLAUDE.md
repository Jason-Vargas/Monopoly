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
    - `Monopoly.Core.Hardware` — dado electrónico y lector RFID (Raspberry Pi Pico W por USB serial), con implementación simulada.
  - `src/Monopoly.App` — Windows Forms (referencia a Core).
  - `tests/Monopoly.Tests` — xUnit (referencia a Core).
- **Sin paquetes NuGet externos**, salvo xUnit (y su SDK/runner de pruebas) y, más adelante, `System.IO.Ports` para el puerto serie USB de la Pico W.
- Configuración común en `Directory.Build.props`: `Nullable` habilitado, `ImplicitUsings` **deshabilitado** (los usings implícitos traen `System.Linq` y `System.Collections.Generic`), documentación XML generada.
- **Tema**: Monopoly clásico (edición Atlantic City) con nombres de casillas en español y estética clásica. **No** copiar el logotipo ni la mascota oficial.
- **Hardware al final**: hasta entonces todo debe funcionar en **modo simulado** (dados aleatorios, identificación de jugador sin tarjeta). El hardware se abstrae detrás de interfaces para poder cambiar simulado ↔ real.
- **Hardware: Raspberry Pi Pico W con MicroPython (ya NO Arduino)**, conectada por **USB serial** a la computadora del organizador, que aloja al banco. Lleva **lector RFID RC522, un botón y un LED de pago** (más el LED integrado); los **2 displays de 7 segmentos** del dado (2 dígitos) son requisito del enunciado y están **pendientes de implementar**. Detalles en `hardware/README.md`.
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
- **Commits sin coautoría**: el autor es solo la cuenta de `git config`; nada de `Co-Authored-By` ni "Generated with Claude Code" (desactivado con `attribution` en `.claude/settings.json`).
- Estructura de carpetas: ver el árbol en `README.md` (mantenerlo al día si se agregan carpetas).
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
- API pública (todas con `lock` sobre `_candado`; devuelven `ResultadoAccion` con `Exito`/`Mensaje`, nunca lanzan por reglas del juego): `UnirJugador(nombre)` → id + UID virtual `VIRTUAL-{id}`; `VincularTarjeta(id, uid)`; `IniciarPartida(id)` (solo el organizador = primer jugador); `TirarDados(id)`; `SolicitarCompra(id)` (no cobra: pasa a `EsperandoTarjetaCompra`); `NoComprar(id)` (también cancela la espera de la tarjeta); `IdentificarTarjeta(uid)` (completa la compra o el pago pendiente; `ResultadoAccion.PagoAceptado` decide el LED); `ConsultarTarjeta(uid)`; `TerminarTurno(id)`; `ConsultarEstado(id)` → `InstantaneaJuego`; `ConsultarTransacciones(id)`; `ExportarHistorial()`; `ObtenerEventosDesde(n)`; `ObtenerJugador(id)`.
- Estado: `EstadoPartida` (EsperandoJugadores, EnCurso, Finalizada) y `FaseTurno` (EsperandoDados → EsperandoDecisionCompra [→ EsperandoTarjetaCompra] / EsperandoPago → PuedeTerminar). Moneda: colones (`Formato.Dinero` → `₡1,500`).
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
- `ESTADO` se serializa con `SerializadorEstado` (21 campos fijos + 11 por jugador; el último es la forma de la ficha) sobre `EstadoRed` (instantánea + dueños + casillas recorridas). `TRANSACCIONES` con `SerializadorTransacciones` (8 campos por transacción).
- `Cliente`: `TcpClient` + hilo lector; notifica con eventos C# (`BienvenidaRecibida`, `ErrorRecibido`, `EstadoActualizado`, `DadosRecibidos`, `EventoRecibido`, `TransaccionesRecibidas`, `FinRecibido`, `Desconectado`, `MensajeRecibido`). **Se disparan en el hilo lector**: la interfaz debe usar `BeginInvoke`.
- `TIRAR_DADOS` y `PAGAR_CON_TARJETA` solo se aceptan en **modo sin hardware (pruebas)**; en modo hardware los dados salen del botón físico y compras/pagos de la tarjeta RFID. `ESTADO` tiene 21 campos fijos (19 = cajero conectado, 20 = modo sin hardware).
- Robustez (ver `docs/protocolo.md` §5 y `docs/prueba-en-red.md`): `ConfiguracionSocket` aplica keepalive TCP (~20 s para detectar redes caídas) y tiempo máximo de envío de 5 s; `ConexionCliente.LeerLinea` limita las líneas a 8192 caracteres; los errores inesperados al procesar responden `ERROR` sin cortar la conexión.
- `ESTADO` incluye (campo 18) los jugadores **desconectados**; `RETIRAR_JUGADOR|id` (solo el organizador, solo desconectados) llama a `Juego.RetirarJugador`; `Servidor.Detener(motivo)` envía `SERVIDOR_CERRADO|motivo` antes de cerrar y el cliente lo usa como motivo de `Desconectado` (`Cliente.CerradoPorElServidor`).
- `Cliente.Conectar` espera como máximo 5 s y `Cliente.DescribirErrorConexion` traduce los errores (rechazada, sin respuesta, IP inválida) a mensajes claros.

## Módulo electrónico (`hardware/`, Raspberry Pi Pico W + MicroPython)

- Archivos: `hardware/pico/main.py` (firmware), `hardware/pico/mfrc522.py` (driver **original que funciona en la placa**, sin cambios; API `MFRC522(spi, gpioRst=20, gpioCs=17)`, `request(REQIDL)`, `anticoll()` → 5 bytes = 4 de UID + checksum), `hardware/pico/prueba_uid.py` (anotar UIDs), `hardware/README.md` (materiales, conexiones, instalación, pruebas, problemas). La prohibición de colecciones aplica al C#; el MicroPython es independiente.
- **Pines en uso** (reemplazan cualquier asignación anterior):
  - RC522 por SPI0: SCK = GP18, MOSI = GP19, MISO = GP16, CS = GP17, RST = GP20; alimentación 3V3(OUT).
  - Botón: GP15 a GND, con pull-up interno (`Pin(15, Pin.IN, Pin.PULL_UP)`), flanco de bajada y antirrebote de 50 ms.
  - LED de pago: GP14, salida activa en alto (resistencia en serie, cátodo a GND), apagado al arrancar.
  - LED integrado `Pin("LED")`: destello al presionar el botón, al leer una tarjeta y al recibir `DADOS`.
- `main.py` nunca bloquea: botón cada ~10 ms, RC522 cada ~100 ms, serial con `select.poll`; el LED de pago (2 s / 3 parpadeos) se temporiza con `time.ticks_ms()`. Por el serial **solo** salen líneas del protocolo (`DEPURAR = True` imprime errores como líneas `#...`, que el juego ignora).
- **Los dados los genera el servidor** (estado oficial en el banco): la Pico envía `BOTON` y solo muestra lo que recibe.
- Protocolo serie (líneas de texto; la Pico termina con `\r\n`):
  - Pico → PC: `LISTO` (al arrancar), `BOTON` (una vez por pulsación), `RFID:<UID>` (4 bytes en hex mayúsculas, sin espacios; la misma tarjeta no se repite mientras está apoyada ni hasta 2 s después de retirarla, una distinta se lee de inmediato), `PONG`.
  - PC → Pico: `DADOS:d1,d2` (solo destello del LED integrado), `PAGO_OK` (LED de pago 2 s), `PAGO_RECHAZADO` (3 parpadeos rápidos), `LIMPIAR` (apaga el LED de pago), `PING`.
  - Al abrir el puerto, la PC debe enviar `PING` y esperar `PONG` (el `LISTO` pudo salir antes). No enviar nunca Ctrl+C (0x03).

## Integración del cajero en C# (`Monopoly.Core.Hardware`, en la computadora del organizador)

- `IDispositivoCajero` (eventos `BotonPresionado`, `TarjetaLeida`, `EstadoCambiado`, `ErrorDispositivo`; `MostrarDados`, `IndicarPago(aceptado)` → `PAGO_OK`/`PAGO_RECHAZADO`, `Limpiar`; `Estado`, `EsFisico`) con dos implementaciones: `CajeroPico` (físico) y `CajeroSimulado` (sin placa: se juega con los botones de la interfaz; `SimularBoton`/`SimularTarjeta` para depurar). `CajeroPorLineas` es la base que interpreta el protocolo de líneas (ignora lo que no es del protocolo y cuenta `LineasIgnoradas`); las pruebas usan un `CajeroFalso` que hereda de ella.
- `CajeroPico`: `SerialPort` a 115200, `NewLine "\n"`, **`DtrEnable` y `RtsEnable` = true**, hilo lector propio, `Conectar()` insiste con PING hasta recibir PONG (2,5 s), PING cada 2 s y desconexión si no hay respuesta en ~7 s o si falla la lectura/escritura (cable retirado). `PuertosDisponibles()` y `DetectarPuerto()` (PING/PONG en cada puerto).
- `Servidor.UsarCajero(cajero)` (por defecto un `CajeroSimulado`); el servidor no libera el dispositivo. Los eventos del cajero se procesan con el mismo `_candadoProcesamiento` que las solicitudes:
  - **Modo hardware** (por defecto): `BOTON` → `ProcesarTirarDados(null)` para el jugador en turno con todas las validaciones; una pulsación fuera de momento se ignora con `EVENTO|Cajero: Se ignoró el botón: ...`. `TIRAR_DADOS` y `PAGAR_CON_TARJETA` por red se rechazan. `INICIAR_PARTIDA` exige que todos tengan tarjeta vinculada.
  - Toda tirada aceptada → `cajero.MostrarDados(d1, d2)` + `DADOS` a todos.
  - `RFID:uid` (normalizado: mayúsculas sin espacios): vinculación pendiente → `Juego.VincularTarjeta`; fase `EsperandoTarjetaCompra` o `EsperandoPago` → `Juego.IdentificarTarjeta` + `cajero.IndicarPago(PagoAceptado == true)` (`PAGO_OK` compra/pago correcto; `PAGO_RECHAZADO` tarjeta ajena, no registrada, saldo insuficiente para comprar o eliminación por no poder pagar); si no → `Juego.ConsultarTarjeta` (dueño y saldo, sin LED).
  - Sin cajero conectado y sin modo de pruebas la partida queda **en pausa** (`EstadoRed.EnPausaPorCajero`); al reconectar sigue donde estaba.
  - **Modo sin hardware (pruebas)**: `Servidor.EstablecerModoSinHardware(true)` cambia a un `CajeroSimulado`; `SimularBoton()` y `SimularTarjetaDelJugadorEnTurno()` recorren el mismo flujo; no se envía nada a la Pico.
  - Cambios de estado del cajero → `EVENTO|Cajero: ...` + `ESTADO`.
- Protocolo TCP: `VINCULAR_TARJETA|idJugador` (solo el organizador y con cajero físico; `0` cancela). Los avisos del cajero son `EVENTO` con el prefijo `Cajero: `.
- Interfaz del organizador: todo lo de la Pico está en `FormularioOpciones` (engranaje, en la sala y en el juego): estado, puerto COM, ↻, "Detectar automáticamente", "Conectar", "Desconectar", "Probar LED" (envía `PAGO_OK`), monitor de líneas (`MonitorCajero`: BOTON, RFID, errores y conexión; vive en `SesionJuego.MonitorCajero` y avisa con `PicoPerdida` si la Pico se pierde sin pedirlo), tarjetas vinculadas y la casilla "Modo sin hardware (pruebas)" con "Simular botón" / "Simular tarjeta del jugador en turno". `SesionJuego.CambiarCajero`/`EstablecerModoSinHardware`; `SesionJuego.CajeroCambiado` avisa en el hilo de la interfaz. En la sala: "Vincular tarjeta" / "Cancelar vinculación" e "Iniciar" bloqueado hasta que todos tengan tarjeta (modo hardware). En el juego no hay botones de tirar ni de pagar: el aviso para todos ("Turno de X: presione el botón físico...", "X quiere comprar P por ₡N: acerque su tarjeta al lector", "X debe pagar ₡N a Y: acerque su tarjeta al lector") se ve en el panel y en el centro del tablero (`PanelTablero.Aviso`).

## Publicación y prueba en red

- `publicar.ps1` genera `publicar\win-x64\Monopoly.App.exe`: un único .exe autocontenido (win-x64, .NET 8 incluido, sin recorte porque WinForms no lo admite). La carpeta `publicar/` está en `.gitignore`.
- Firewall, IP, orden de la prueba y solución de problemas: `docs/prueba-en-red.md`.

## Diseño de la interfaz (`Monopoly.App`, Windows Forms)

- Flujo de ventanas (`ContextoAplicacion`): `FormularioInicio` → `FormularioSalaEspera` → `FormularioJuego` (+ `FormularioHistorial`, `FormularioFin`). Cerrar la ventana activa cierra la sesión y, si es el organizador, el servidor.
- **Crear partida**: la app crea `Juego` + `Servidor` en segundo plano y conecta al organizador a `127.0.0.1` como un cliente más. **La interfaz nunca llama a `Juego`**: todo pasa por el protocolo (`SesionJuego.Cliente`).
- `SesionJuego` reenvía los eventos del `Cliente` (hilo de red) al hilo de la interfaz con `Control.BeginInvoke`; los formularios se suscriben a los eventos de la sesión y se desuscriben en `OnFormClosed`. Guarda `UltimoEstado`, el registro de eventos y el FIN.
- `PanelTablero` (GDI+, `OptimizedDoubleBuffer`, `ResizeRedraw`): lado de 13 unidades (esquinas 2×2, casillas 1×2); 0 abajo a la derecha y sentido horario. Laterales rotados 90°/270°; la fila de arriba se dibuja sin girar con la franja abajo para que el texto se lea. Los textos salen de miembros polimórficos (`Nombre`, `Categoria`, `Detalle`) y los colores de `Tablero.BuscarPropiedad(i)?.Grupo`: **sin chequear el tipo de casilla**.
- Animación: al recibir `DADOS` se marca que el siguiente `ESTADO` se anima; un `Timer` de WinForms mueve la ficha por `CasillasRecorridas` (hacia adelante o atrás) y al final se ajusta a las posiciones del estado.
- Botones habilitados según el último `ESTADO` ("Comprar" en mi turno con decisión pendiente, "No comprar" también mientras se espera la tarjeta, "Terminar turno"). El servidor valida igual.
- Argumentos de prueba: `--crear Nombre [puerto] [maxTurnos]` y `--unirse Nombre [ip] [puerto]`.
- Errores al conectar: se muestran en la etiqueta y en un diálogo (`FormularioInicio.MostrarError`); se valida la IP antes de conectar y se espera la BIENVENIDA como máximo 8 s.
- Desconexiones: las tarjetas muestran "DESCONECTADO"; el organizador ve el botón "Retirar jugadores desconectados..."; un jugador que pierde la conexión puede volver al inicio ya relleno (`VolverParaReconectar` + `ArgumentosInicio.ParaReconectar`), salvo que el servidor haya cerrado.
- `Paleta`: datos del tablero (colores de grupo y de ficha delegan en `Tema`). `Casilla.Detalle` (virtual) da el texto secundario de cada casilla.

## Rediseño de la interfaz (en curso: pasos 1 y 2 de 3 hechos)

- Nombre visible del juego: **MONOPOLY TEC** (`Tema.NombreJuego`), con logotipo propio (marquesina roja con luces, `Estilo/Logotipo.cs`). Nada del logotipo, la mascota ni las ilustraciones oficiales.
- **Sistema de estilo** en `src/Monopoly.App/Estilo` (namespace `Monopoly.App.Estilo`): `Tema` (paleta: verde menta, rojo clásico, crema/marfil, colores de grupo menos saturados, éxito/error/información/advertencia; fuentes `Titulo(t)`, `Texto(t, estilo)` y `Monoespaciada(t)`), `Fuentes` (Abril Fatface para títulos y Lato normal/negrita para texto, SIL OFL 1.1, incrustadas como recursos y cargadas con `PrivateFontCollection` + `AddFontMemResourceEx`; licencias en `Estilo/Fuentes/OFL-*.txt`, copiadas a `Licencias/` en la salida; si fallan, Georgia y Segoe UI), `Dibujo` (antialiasing, redondeados, sombras, fondo con patrón de tablero, indicador, ✓ dibujada), `BotonRedondeado` (hereda de `Button` para conservar teclado/accesibilidad; estilos Principal/Secundario/Exito), `TarjetaPanel`, `CampoTexto` (etiqueta + caja + error en rojo), `Etiqueta` (dibujada con GDI+), `Notificaciones`/`AvisoNotificacion` (toasts), `SelectorFicha`, `DibujoFicha` (6 fichas: sombrero, carro, barco, perro, dedal, bota), `DibujoDado`, `Iconos`.
- Lato no trae cursiva: no usar `FontStyle.Italic` con `Tema.Texto`. Todo control dibujado usa `OptimizedDoubleBuffer` y `Dibujo.Calidad`.
- **Escala fija en píxeles**: todas las ventanas usan `AutoScaleMode.None` y las fuentes del tema se crean en píxeles (`Tema.PixelesPorPunto` = 2: tamaño 10 → 20 px). Así texto y diseño mantienen la misma proporción con cualquier escala de Windows (la interfaz se diseñó y revisó a 150 %). No mezclar fuentes en puntos en controles nuevos.
- Otros componentes: `BotonIcono` (engranaje, historial), `Lienzo` (panel con doble búfer), `IlustracionesTablero` (flecha de Salida, rejas, auto, silbato, tren, bombilla, gota, anillo, billete, ?, cofre), `IconoTransaccion`, íconos `Engranaje`, `Libro`, `Altavoz`.
- **Ficha elegida**: `FormaFicha` (Modelo) + `ColorFicha` (6 colores). `CONECTAR|nombre|forma|color` (opcionales); `Juego.UnirJugador(nombre, forma?, color?)` rechaza forma o color repetidos y, si no se indican, asigna la primera libre (así lo hacen los argumentos de prueba `--crear/--unirse`). La forma viaja en `EstadoJugador.FormaFicha` y se dibuja en el tablero y en los paneles.
- Ventana de inicio: fondo de tablero, logotipo y dados animados, tarjeta "Su jugador" (nombre + `SelectorFicha`), tarjetas "Crear partida" y "Unirse a partida" con validación visual; errores como notificaciones.
- Sala de espera: `TarjetaJugadorSala` (ficha, color, insignias, estado RFID; aparece animada), dirección del banco con "Copiar", estado de la Pico W, vinculación de tarjetas (clic en el jugador) y "Iniciar partida" con la explicación de lo que falta.
- **Tablero** (`PanelTablero`): casillas con franja arriba, nombre (se achica si la palabra más larga no cabe) y precio abajo, giradas 0°/90°/180°/270° para leerse desde afuera; esquinas en diagonal con ilustración; ícono según `Casilla.Categoria` (dato polimórfico, no se chequea el tipo); centro con logotipo en diagonal, mazos apilados en sus espacios, dados que giran al llegar una tirada y el aviso del turno; fichas repartidas (1 a 4 por casilla) con sombra; banda del color del dueño en el borde exterior; tarjeta de título al pasar el mouse; se escala manteniendo el cuadrado.
- **Rendimiento del tablero**: rectángulos de las 40 casillas precalculados al redimensionar; dos imágenes en caché (base: mesa, sombra, esquinas, logotipo y mazos, solo cambia con el tamaño; fondo: casillas con dueños, turno y aviso, se regenera con cada `ESTADO`); encima se dibujan dados, resaltado del cursor, fichas y la tarjeta. El mouse solo invalida al cambiar de casilla (casilla anterior, nueva y tarjeta); la tarjeta es una única `TarjetaCasilla` que se dibuja en su propia imagen y aparece tras 150 ms. Dados y fichas animadas invalidan solo su zona. `Dibujo.Sombra` recorta el interior de la forma (la sombra del tablero costaba ~770 ms).
- **Ventana de juego**: tablero + columna derecha con "Historial" y engranaje, `PanelJugadores` (ficha, saldo grande, propiedades con sus colores, RFID, borde dorado e indicador para el turno, "En bancarrota" en gris; clic → `ListaPropiedadesJugador` desplegada, agrupada por color), tarjeta de situación (alto según el texto), solo los botones de la fase (Comprar, No comprar/Cancelar compra, Terminar turno; retirar desconectados para el organizador) y "Actividad reciente" plegable. Errores y avisos del cajero como notificaciones.
- **Opciones** (`FormularioOpciones`): Pico W (solo organizador), Sonido (`Preferencias`: activado y volumen, guardados en `%LOCALAPPDATA%\MonopolyTEC\preferencias.txt`; se usarán en el paso 3) y Partida (servidor, usted, turno, salir con confirmación).
- **Historial** (`FormularioHistorial` + `TablaTransacciones`): ventana aparte, filtros como botones (todas, más antiguas, más recientes, por jugador, por tipo) consultados con CONSULTAR_TRANSACCIONES, filas alternadas, ícono por tipo, monto verde (ingreso) o rojo (pago) desde el jugador de referencia, exportar TXT y actualización automática cuando cambia la cantidad de transacciones del ESTADO.
- El monitor de la Pico muestra BOTON/RFID/errores reconstruidos de los eventos de `IDispositivoCajero`; **PONG no se muestra** porque la capa de hardware no lo expone (habría que agregar un evento en `CajeroPorLineas`).
- Pendiente (paso 3): ventana de fin y sonidos.

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
- [ ] 1. Descripción general: partida de 4 jugadores en al menos 2 computadoras; temática definida (Atlantic City en español) — *probado con 4 ventanas en una computadora; falta probar en 2 computadoras*
- [ ] 2. Objetivos cubiertos (POO, estructuras propias, cliente-servidor, estado centralizado, transacciones, hardware)
- [x] 3. Arquitectura: servidor/banco con estado oficial en la máquina del organizador; clientes solo solicitan acciones
- [x] 4. Jugador con id, nombre, saldo, posición, estado activo y propiedades en estructura lineal propia (`ListaSimple<Propiedad>`)
- [x] 5. Tablero como lista circular doblemente enlazada con ≥ 24 casillas, visible para todos
- [x] 6. Casilla base + Propiedad, CasillaEvento, CasillaEspecial con polimorfismo
- [x] 7. Propiedades: datos mínimos y comportamiento comprar / alquiler / propia
- [x] 8. Turnos con cola circular, avance automático, bloqueo fuera de turno
- [x] 9. Dos dados por turno, resultado transmitido, movimiento nodo a nodo mostrado a todos (animado en la interfaz)
- [x] 10. Mazo de cartas de evento que devuelve la carta al final; los 6 tipos de evento
- [x] 11. Transacciones con todos los campos y los 7 tipos mínimos; banco como origen/destino
- [x] 12. Historial: agregar, recorrer ambos sentidos, buscar por jugador y por tipo, imprimir todo
- [x] 13. Exportación del historial a TXT con los campos mínimos
- [ ] 14. Módulo electrónico (Raspberry Pi Pico W): RFID + botón + LED de pago — *firmware nuevo probado en la placa real (LISTO, PING/PONG, solo líneas del protocolo) y con el `CajeroPico` del juego; integración en C# (dados solo con el botón, compras y cobros solo con tarjeta, `PAGO_OK`/`PAGO_RECHAZADO`, pausa si se desconecta la Pico, modo sin hardware para pruebas) con pruebas. Falta una partida completa con tarjetas y botón físicos. Los 2 displays de 7 segmentos (dado de 2 dígitos): pendiente de implementar.*
- [x] 15. Todas las clases mínimas presentes; ninguna colección de .NET
- [x] 16. Comunicación por sockets TCP con protocolo documentado (`docs/protocolo.md`) y difusión de estado
- [x] 17. Todas las validaciones del servidor (en `Juego`; el cliente no puede modificar el modelo porque sus mutadores son `internal`)
- [x] 18. Eliminación de jugadores y fin de partida (último activo o límite de turnos con patrimonio)
- [ ] 19. Entregables: UML (`docs/uml.md`), estructuras (`docs/estructuras.md`), protocolo TCP (`docs/protocolo.md`) y serie (`hardware/README.md`), manual (`README.md`) y hardware listos — *el archivo de transacciones (`docs/ejemplo-transacciones.txt`) es un respaldo de una partida simulada; falta reemplazarlo por el de una partida real con el cajero físico*
- [ ] 20. Preparación de la defensa (guion de demostración cubriendo todos los puntos)

## Comandos

```bash
dotnet build Monopoly.sln
dotnet test Monopoly.sln
dotnet run --project src/Monopoly.App
# Varias ventanas de prueba (compilar antes): Monopoly.App.exe --crear Ana 5000 100 / --unirse Beto 127.0.0.1 5000
```
