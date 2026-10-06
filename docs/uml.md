# Diagrama UML

Diagramas de clases del código actual (`src/Monopoly.Core`). GitHub los muestra directamente; en otros visores se pueden pegar en <https://mermaid.live>. Se muestran los miembros principales; los demás están documentados con comentarios XML en el código.

## 1. Casillas (herencia y polimorfismo)

La lógica no consulta el tipo de casilla: llama a `AlCaer`, que devuelve un `ResultadoCasilla` con los efectos como datos, y `Juego` los aplica de forma genérica.

```mermaid
classDiagram
    class Casilla {
        <<abstract>>
        +int Id
        +string Nombre
        +string Categoria*
        +string Detalle
        +AlCaer(Jugador, Juego) ResultadoCasilla*
    }
    class Propiedad {
        +int PrecioCompra
        +int Alquiler
        +GrupoPropiedad Grupo
        +Jugador? Propietario
        +bool EstaDisponible
        +CalcularAlquiler() int
        +AlCaer(Jugador, Juego) ResultadoCasilla
    }
    class Ferrocarril {
        +CalcularAlquiler() int
    }
    class CompaniaServicio {
        +CalcularAlquiler() int
    }
    class CasillaEvento {
        <<abstract>>
        #ObtenerMazo(Juego) MazoCartas*
        +AlCaer(Jugador, Juego) ResultadoCasilla
    }
    class CasillaCasualidad
    class CasillaArcaComunal
    class CasillaEspecial {
        <<abstract>>
    }
    class Salida
    class Impuesto
    class CarcelSoloVisita
    class ParadaLibre
    class VayaALaCarcel
    class ResultadoCasilla {
        +Propiedad? PropiedadEnVenta
        +int MontoAPagar
        +Jugador? Acreedor
        +int MontoARecibir
        +int Movimiento
        +int? DestinoIndice
        +int TurnosAPerder
        +CartaEvento? Carta
    }
    class CartaEvento {
        +string Descripcion
        +TipoCartaEvento Tipo
        +int Valor
        +CrearResultado(Casilla) ResultadoCasilla
    }

    Casilla <|-- Propiedad
    Propiedad <|-- Ferrocarril
    Propiedad <|-- CompaniaServicio
    Casilla <|-- CasillaEvento
    CasillaEvento <|-- CasillaCasualidad
    CasillaEvento <|-- CasillaArcaComunal
    Casilla <|-- CasillaEspecial
    CasillaEspecial <|-- Salida
    CasillaEspecial <|-- Impuesto
    CasillaEspecial <|-- CarcelSoloVisita
    CasillaEspecial <|-- ParadaLibre
    CasillaEspecial <|-- VayaALaCarcel
    Casilla ..> ResultadoCasilla : crea
    CasillaEvento ..> CartaEvento : saca del mazo
```

## 2. Modelo y lógica del juego

```mermaid
classDiagram
    class Juego {
        +Tablero Tablero
        +Banco Banco
        +Dado Dado
        +MazoCartas MazoCasualidad
        +MazoCartas MazoArcaComunal
        +HistorialTransacciones Historial
        -ColaCircular~Jugador~ _turnos
        -ListaSimple~Jugador~ _jugadores
        -ListaDobleEnlazada~EventoJuego~ _eventos
        +UnirJugador(nombre, forma, color) ResultadoAccion
        +VincularTarjeta(id, uid) ResultadoAccion
        +IniciarPartida(id) ResultadoAccion
        +TirarDados(id) ResultadoAccion
        +SolicitarCompra(id) ResultadoAccion
        +NoComprar(id) ResultadoAccion
        +IdentificarTarjeta(uid) ResultadoAccion
        +TerminarTurno(id) ResultadoAccion
        +ConsultarEstado(id) ResultadoAccion
        +ConsultarTransacciones(id) ResultadoAccion
        +ExportarHistorial() ResultadoAccion
        +ObtenerInstantanea() InstantaneaJuego
    }
    class Banco {
        +Pagar(destino, monto, tipo, turno, descripcion) Transaccion
        +Cobrar(origen, monto, tipo, turno, descripcion) Transaccion
        +Transferir(origen, destino, monto, tipo, turno, descripcion) Transaccion
    }
    class Tablero {
        -ListaCircularDoble~Casilla~ _casillas
        -ListaSimple~Propiedad~ _propiedades
        +int Cantidad
        +ObtenerCasilla(indice) Casilla
        +BuscarPropiedad(indice) Propiedad?
        +ColocarEnSalida(Jugador)
        +MoverJugador(Jugador, pasos) ResultadoMovimiento
        +MoverJugadorHasta(Jugador, indice) ResultadoMovimiento
        +EnviarJugadorA(Jugador, indice) ResultadoMovimiento
    }
    class Jugador {
        +int Id
        +string Nombre
        +int Saldo
        +NodoCircularDoble~Casilla~? Posicion
        +bool Activo
        +int TurnosPorPerder
        +string? UidTarjeta
        -ListaSimple~Propiedad~ _propiedades
        +PuedePagar(monto) bool
        +CalcularPatrimonio() int
        ~Acreditar(monto)
        ~Debitar(monto)
        ~AgregarPropiedad(Propiedad)
        ~QuitarPropiedad(Propiedad)
    }
    class Dado {
        +TiradaDados? UltimaTirada
        +Lanzar() TiradaDados
        +ConValoresFijos(valores)$ Dado
    }
    class MazoCartas {
        -Cola~CartaEvento~ _cartas
        +Sacar() CartaEvento
        +Barajar(Random)
    }
    class HistorialTransacciones {
        -ListaDobleEnlazada~Transaccion~ _transacciones
        +Registrar(turno, tipo, origen, destino, monto, descripcion) Transaccion
        +RecorrerDesdeMasAntigua(accion)
        +RecorrerDesdeMasReciente(accion)
        +BuscarPorJugador(nombre) ListaDobleEnlazada~Transaccion~
        +BuscarPorTipo(tipo) ListaDobleEnlazada~Transaccion~
        +ImprimirTodas() string
        +ExportarTxt(ruta, fecha, jugadores)
    }
    class Transaccion {
        +int Id
        +DateTime FechaHora
        +int NumeroTurno
        +TipoTransaccion Tipo
        +string Origen
        +string Destino
        +int Monto
        +string Descripcion
    }
    class TipoTransaccion {
        <<enumeration>>
        CompraPropiedad
        PagoAlquiler
        PagoBanco
        PagoEntreJugadores
        GananciaEvento
        PerdidaEvento
        PremioInicio
    }

    Juego *-- Tablero
    Juego *-- Banco
    Juego *-- Dado
    Juego *-- "2" MazoCartas
    Juego *-- HistorialTransacciones
    Juego o-- "2..4" Jugador
    Banco --> HistorialTransacciones : registra
    HistorialTransacciones o-- Transaccion
    Transaccion --> TipoTransaccion
    Tablero o-- "40" Casilla
    Jugador o-- Propiedad : propiedades
    Propiedad --> Jugador : Propietario
    MazoCartas o-- "13" CartaEvento
```

## 3. Red y cajero electrónico

```mermaid
classDiagram
    class Servidor {
        -Juego _juego
        -TcpListener _escucha
        -ListaSimple~ConexionCliente~ _clientes
        -IDispositivoCajero _cajero
        +Iniciar()
        +Detener(motivo)
        +UsarCajero(IDispositivoCajero)
        +EstablecerModoSinHardware(bool)
    }
    class ConexionCliente {
        +int? IdJugador
        +LeerLinea() string?
        +Enviar(linea)
    }
    class Cliente {
        +Conectar(host, puerto)
        +Unirse(nombre, forma, color)
        +ComprarPropiedad()
        +NoComprar()
        +TerminarTurno()
        +ConsultarEstado()
        +ConsultarTransacciones(filtro)
        +event EstadoActualizado
        +event DadosRecibidos
        +event EventoRecibido
    }
    class Protocolo {
        <<static>>
        +Codificar(comando, campos) string$
        +Decodificar(linea) Mensaje$
    }
    class Mensaje {
        +string Comando
        +Campo(indice) string
    }
    class EstadoRed {
        +InstantaneaJuego Instantanea
        +int[] Propietarios
        +int[] CasillasRecorridas
    }
    class IDispositivoCajero {
        <<interface>>
        +EstadoCajero Estado
        +bool EsFisico
        +event BotonPresionado
        +event TarjetaLeida
        +MostrarDados(d1, d2)
        +IndicarPago(aceptado)
        +Limpiar()
    }
    class CajeroPorLineas {
        <<abstract>>
        #ProcesarLinea(linea)
    }
    class CajeroPico {
        +string NombrePuerto
        +Conectar()
        +PuertosDisponibles()$ string[]
        +DetectarPuerto()$ string?
    }
    class CajeroSimulado {
        +SimularBoton()
        +SimularTarjeta(uid)
    }

    Servidor --> Juego : único que lo modifica
    Servidor o-- ConexionCliente
    Servidor --> IDispositivoCajero
    Servidor ..> Protocolo
    Cliente ..> Protocolo
    Protocolo ..> Mensaje
    Servidor ..> EstadoRed : difunde ESTADO
    Cliente ..> EstadoRed : recibe
    IDispositivoCajero <|.. CajeroPorLineas
    CajeroPorLineas <|-- CajeroPico
    IDispositivoCajero <|.. CajeroSimulado
```

`Cliente` y `Servidor` se comunican por TCP con el protocolo de `docs/protocolo.md`; la `Pico W` se comunica con `CajeroPico` por USB serial con el protocolo de `hardware/README.md`. La interfaz (`Monopoly.App`) solo usa `Cliente`: nunca llama a `Juego`.

## 4. Estructuras de datos propias

```mermaid
classDiagram
    class ListaSimple~T~ {
        -NodoSimple~T~ _cabeza
        -NodoSimple~T~ _cola
        +int Cantidad
        +AgregarAlFinal(T)
        +AgregarAlInicio(T)
        +Eliminar(T) bool
        +Buscar(Predicate) T?
        +BuscarTodos(Predicate) ListaSimple~T~
        +Recorrer(Action)
    }
    class NodoSimple~T~ {
        +T Valor
        +NodoSimple~T~? Siguiente
    }
    class ListaDobleEnlazada~T~ {
        +int Cantidad
        +AgregarAlFinal(T)
        +RecorrerDesdeInicio(Action)
        +RecorrerDesdeFinal(Action)
        +BuscarTodos(Predicate) ListaDobleEnlazada~T~
    }
    class NodoDoble~T~ {
        +T Valor
        +NodoDoble~T~? Anterior
        +NodoDoble~T~? Siguiente
    }
    class ListaCircularDoble~T~ {
        +NodoCircularDoble~T~? Cabeza
        +int Cantidad
        +Agregar(T) NodoCircularDoble~T~
        +ObtenerNodo(indice) NodoCircularDoble~T~
        +Avanzar(nodo, pasos, alPasar) NodoCircularDoble~T~
        +Retroceder(nodo, pasos, alPasar) NodoCircularDoble~T~
    }
    class NodoCircularDoble~T~ {
        +T Valor
        +NodoCircularDoble~T~ Anterior
        +NodoCircularDoble~T~ Siguiente
    }
    class Cola~T~ {
        +Encolar(T)
        +Desencolar() T
        +Frente() T
    }
    class ColaCircular~T~ {
        -T[] _elementos
        -int _frente
        +Encolar(T)
        +Desencolar() T
        +Frente() T
        +Rotar()
        +Eliminar(T) bool
    }

    ListaSimple~T~ o-- NodoSimple~T~
    Cola~T~ o-- NodoSimple~T~
    ListaDobleEnlazada~T~ o-- NodoDoble~T~
    ListaCircularDoble~T~ o-- NodoCircularDoble~T~
```

Uso en el juego: `Tablero` → `ListaCircularDoble<Casilla>`; turnos → `ColaCircular<Jugador>`; mazos → `Cola<CartaEvento>`; historial → `ListaDobleEnlazada<Transaccion>`; propiedades del jugador y clientes del servidor → `ListaSimple<T>`. Detalle y complejidades en [`estructuras.md`](estructuras.md).
