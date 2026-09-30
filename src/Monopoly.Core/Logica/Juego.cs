using System;
using System.Globalization;
using System.IO;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Logica;

/// <summary>
/// Estado oficial de la partida y todas sus reglas. Los clientes solo pueden pedir acciones mediante
/// los métodos públicos, que reciben el id del jugador solicitante, validan la solicitud y devuelven
/// un <see cref="ResultadoAccion"/>. Es independiente de la red y de la interfaz.
/// </summary>
/// <remarks>
/// Todos los métodos y propiedades públicos toman un <c>lock</c> sobre un objeto privado, porque los
/// invocan varios hilos del servidor. <see cref="Tablero"/>, <see cref="Historial"/> y los mazos se
/// exponen para el modelo y las pruebas; los demás deben consultar el estado con
/// <see cref="ConsultarEstado"/>, <see cref="ConsultarTransacciones"/> o <see cref="ObtenerEventosDesde"/>.
/// </remarks>
public class Juego
{
    /// <summary>
    /// Mínimo de jugadores para iniciar la partida.
    /// </summary>
    public const int MinimoJugadores = 2;

    /// <summary>
    /// Máximo de jugadores de una partida.
    /// </summary>
    public const int MaximoJugadores = 4;

    /// <summary>
    /// Prefijo de los UID asignados a jugadores sin tarjeta física (modo simulado).
    /// </summary>
    public const string PrefijoTarjetaVirtual = "VIRTUAL-";

    /// <summary>
    /// Límite de encadenamientos casilla → carta → casilla en una misma acción (evita ciclos).
    /// </summary>
    private const int ProfundidadMaximaEfectos = 10;

    private static readonly ColorFicha[] ColoresFicha = { ColorFicha.Rojo, ColorFicha.Azul, ColorFicha.Verde, ColorFicha.Amarillo };

    private readonly object _candado = new object();
    private readonly ColaCircular<Jugador> _turnos = new ColaCircular<Jugador>(MaximoJugadores);
    private readonly ListaSimple<Jugador> _jugadores = new ListaSimple<Jugador>();
    private readonly ListaDobleEnlazada<EventoJuego> _eventos = new ListaDobleEnlazada<EventoJuego>();
    private readonly Func<DateTime> _reloj;
    private readonly string _carpetaPartidas;
    private readonly bool _exportarAlFinalizar;
    private readonly int _saldoInicial;

    private EstadoPartida _estado = EstadoPartida.EsperandoJugadores;
    private FaseTurno _fase = FaseTurno.EsperandoDados;
    private int _numeroTurno;
    private int _siguienteId = 1;
    private bool _dadosLanzados;
    private Propiedad? _propiedadEnVenta;
    private PagoPendiente? _pagoPendiente;
    private Jugador? _ganador;
    private string? _rutaHistorialExportado;
    private ListaSimple<ResultadoMovimiento> _movimientosAccion = new ListaSimple<ResultadoMovimiento>();

    /// <summary>
    /// Crea una partida con la configuración indicada (o la predeterminada).
    /// </summary>
    /// <param name="opciones">Configuración; <c>null</c> para usar los valores predeterminados.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si el máximo de turnos o el saldo inicial no son válidos.</exception>
    public Juego(OpcionesJuego? opciones = null)
    {
        opciones ??= new OpcionesJuego();
        if (opciones.MaximoTurnos < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opciones), "El máximo de turnos debe ser al menos 1.");
        }

        if (opciones.SaldoInicial < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(opciones), "El saldo inicial no puede ser negativo.");
        }

        _reloj = opciones.Reloj ?? (() => DateTime.Now);
        _carpetaPartidas = opciones.CarpetaPartidas;
        _exportarAlFinalizar = opciones.ExportarAlFinalizar;
        _saldoInicial = opciones.SaldoInicial;
        MaximoTurnos = opciones.MaximoTurnos;

        Dado = opciones.Dado ?? new Dado();
        Tablero = new Tablero();
        Historial = new HistorialTransacciones(_reloj);
        Banco = new Banco(Historial);
        FechaInicio = _reloj();

        Random aleatorio = opciones.Aleatorio ?? new Random();
        MazoCasualidad = new MazoCartas("Casualidad", opciones.CartasCasualidad ?? CartasClasicas.CrearCasualidad());
        MazoArcaComunal = new MazoCartas("Arca Comunal", opciones.CartasArcaComunal ?? CartasClasicas.CrearArcaComunal());
        if (opciones.CartasCasualidad == null)
        {
            MazoCasualidad.Barajar(aleatorio);
        }

        if (opciones.CartasArcaComunal == null)
        {
            MazoArcaComunal.Barajar(aleatorio);
        }
    }

    /// <summary>
    /// Crea una partida con los dados y el generador indicados (útil para pruebas reproducibles).
    /// </summary>
    /// <param name="dado">Dados de la partida.</param>
    /// <param name="aleatorio">Generador usado para barajar los mazos.</param>
    /// <param name="reloj">Fuente de fecha y hora; por defecto, la hora local actual.</param>
    public Juego(Dado dado, Random aleatorio, Func<DateTime>? reloj = null)
        : this(new OpcionesJuego
        {
            Dado = dado ?? throw new ArgumentNullException(nameof(dado)),
            Aleatorio = aleatorio ?? throw new ArgumentNullException(nameof(aleatorio)),
            Reloj = reloj,
        })
    {
    }

    /// <summary>
    /// Tablero de la partida.
    /// </summary>
    public Tablero Tablero { get; }

    /// <summary>
    /// Mazo de cartas de Casualidad.
    /// </summary>
    public MazoCartas MazoCasualidad { get; }

    /// <summary>
    /// Mazo de cartas de Arca Comunal.
    /// </summary>
    public MazoCartas MazoArcaComunal { get; }

    /// <summary>
    /// Dados de la partida.
    /// </summary>
    public Dado Dado { get; }

    /// <summary>
    /// Historial de transacciones.
    /// </summary>
    public HistorialTransacciones Historial { get; }

    /// <summary>
    /// Banco de la partida (única entidad que mueve dinero).
    /// </summary>
    public Banco Banco { get; }

    /// <summary>
    /// Fecha y hora de creación de la partida.
    /// </summary>
    public DateTime FechaInicio { get; }

    /// <summary>
    /// Máximo de turnos configurado.
    /// </summary>
    public int MaximoTurnos { get; }

    /// <summary>
    /// Estado de la partida.
    /// </summary>
    public EstadoPartida Estado
    {
        get
        {
            lock (_candado)
            {
                return _estado;
            }
        }
    }

    /// <summary>
    /// Fase del turno actual.
    /// </summary>
    public FaseTurno Fase
    {
        get
        {
            lock (_candado)
            {
                return _fase;
            }
        }
    }

    /// <summary>
    /// Número de turno global (0 antes de iniciar).
    /// </summary>
    public int NumeroTurno
    {
        get
        {
            lock (_candado)
            {
                return _numeroTurno;
            }
        }
    }

    /// <summary>
    /// Ruta completa del TXT exportado al finalizar, o <c>null</c> si aún no se exportó.
    /// </summary>
    public string? RutaHistorialExportado
    {
        get
        {
            lock (_candado)
            {
                return _rutaHistorialExportado;
            }
        }
    }

    /// <summary>
    /// Registra un jugador nuevo mientras la partida espera jugadores. Se le asigna un id,
    /// un color de ficha y un UID de tarjeta virtual (reemplazable con <see cref="VincularTarjeta"/>).
    /// </summary>
    /// <param name="nombre">Nombre del jugador (único, sin distinguir mayúsculas).</param>
    /// <returns>El resultado con <see cref="ResultadoAccion.IdJugador"/> y <see cref="ResultadoAccion.UidTarjeta"/>.</returns>
    public ResultadoAccion UnirJugador(string nombre)
    {
        lock (_candado)
        {
            if (_estado != EstadoPartida.EsperandoJugadores)
            {
                return ResultadoAccion.Fallido("La partida ya comenzó; no se pueden unir jugadores nuevos. " +
                    "Si usted ya estaba en la partida, escriba exactamente el mismo nombre para reconectarse.");
            }

            if (string.IsNullOrWhiteSpace(nombre))
            {
                return ResultadoAccion.Fallido("El nombre del jugador no puede estar vacío.");
            }

            string nombreLimpio = nombre.Trim();
            if (_jugadores.Cantidad >= MaximoJugadores)
            {
                return ResultadoAccion.Fallido($"La partida está llena: ya tiene el máximo de {MaximoJugadores} jugadores.");
            }

            if (string.Equals(nombreLimpio, Transaccion.Banco, StringComparison.OrdinalIgnoreCase))
            {
                return ResultadoAccion.Fallido($"El nombre {Transaccion.Banco} está reservado.");
            }

            if (_jugadores.Buscar(j => string.Equals(j.Nombre, nombreLimpio, StringComparison.OrdinalIgnoreCase)) != null)
            {
                return ResultadoAccion.Fallido($"Ya existe un jugador llamado {nombreLimpio}; elija otro nombre.");
            }

            int id = _siguienteId++;
            Jugador jugador = new Jugador(id, nombreLimpio, ColoresFicha[_jugadores.Cantidad], _saldoInicial)
            {
                UidTarjeta = PrefijoTarjetaVirtual + id,
            };
            _jugadores.AgregarAlFinal(jugador);
            RegistrarEvento($"{jugador.Nombre} se unió a la partida con la ficha {jugador.ColorFicha}.");

            return ResultadoAccion.Correcto($"{jugador.Nombre} se unió a la partida.") with
            {
                IdJugador = id,
                UidTarjeta = jugador.UidTarjeta,
            };
        }
    }

    /// <summary>
    /// Vincula una tarjeta RFID a un jugador, reemplazando su UID virtual.
    /// </summary>
    /// <param name="idJugador">Jugador que vincula la tarjeta.</param>
    /// <param name="uid">UID leído de la tarjeta (se ignoran espacios y mayúsculas).</param>
    /// <returns>El resultado; falla si el UID ya pertenece a otro jugador.</returns>
    public ResultadoAccion VincularTarjeta(int idJugador, string uid)
    {
        lock (_candado)
        {
            if (_estado == EstadoPartida.Finalizada)
            {
                return ResultadoAccion.Fallido("La partida ya terminó.");
            }

            Jugador? jugador = BuscarJugador(idJugador);
            if (jugador == null)
            {
                return ResultadoAccion.Fallido($"No existe el jugador {idJugador}.");
            }

            if (!jugador.Activo)
            {
                return ResultadoAccion.Fallido($"{jugador.Nombre} fue eliminado y no puede vincular una tarjeta.");
            }

            string uidNormalizado = NormalizarUid(uid);
            if (uidNormalizado.Length == 0)
            {
                return ResultadoAccion.Fallido("El UID de la tarjeta no puede estar vacío.");
            }

            Jugador? dueno = BuscarPorUid(uidNormalizado);
            if (dueno != null && dueno != jugador)
            {
                return ResultadoAccion.Fallido($"La tarjeta {uidNormalizado} ya está vinculada a {dueno.Nombre}.");
            }

            jugador.UidTarjeta = uidNormalizado;
            RegistrarEvento($"{jugador.Nombre} vinculó una tarjeta RFID.");
            return ResultadoAccion.Correcto($"Tarjeta vinculada a {jugador.Nombre}.") with { UidTarjeta = uidNormalizado };
        }
    }

    /// <summary>
    /// Inicia la partida: coloca a todos en la Salida, arma la cola de turnos y da el primer turno.
    /// Solo puede hacerlo el organizador (el primer jugador que se unió).
    /// </summary>
    /// <param name="idSolicitante">Jugador que lo solicita.</param>
    /// <returns>El resultado.</returns>
    public ResultadoAccion IniciarPartida(int idSolicitante)
    {
        lock (_candado)
        {
            if (_estado != EstadoPartida.EsperandoJugadores)
            {
                return ResultadoAccion.Fallido("La partida ya fue iniciada.");
            }

            Jugador? solicitante = BuscarJugador(idSolicitante);
            if (solicitante == null)
            {
                return ResultadoAccion.Fallido($"No existe el jugador {idSolicitante}.");
            }

            Jugador organizador = _jugadores.Obtener(0);
            if (solicitante != organizador)
            {
                return ResultadoAccion.Fallido($"Solo el organizador ({organizador.Nombre}) puede iniciar la partida.");
            }

            if (_jugadores.Cantidad < MinimoJugadores)
            {
                return ResultadoAccion.Fallido($"Se necesitan al menos {MinimoJugadores} jugadores para iniciar.");
            }

            _jugadores.Recorrer(jugador =>
            {
                Tablero.ColocarEnSalida(jugador);
                _turnos.Encolar(jugador);
            });
            _estado = EstadoPartida.EnCurso;
            RegistrarEvento($"La partida comenzó con {_jugadores.Cantidad} jugadores (máximo {MaximoTurnos} turnos).");
            PasarAlSiguienteTurno(false);
            return ResultadoAccion.Correcto($"La partida comenzó. Turno de {_turnos.Frente().Nombre}.");
        }
    }

    /// <summary>
    /// Lanza los dados del jugador en turno, lo mueve nodo a nodo, cobra la Salida si pasa por ella
    /// y aplica el efecto de la casilla (y de las casillas a las que lo lleven las cartas).
    /// </summary>
    /// <param name="idJugador">Jugador que lo solicita.</param>
    /// <returns>El resultado con la tirada y los movimientos.</returns>
    public ResultadoAccion TirarDados(int idJugador)
    {
        lock (_candado)
        {
            ResultadoAccion? error = ValidarJugadorEnTurno(idJugador, out Jugador jugador);
            if (error != null)
            {
                return error;
            }

            if (_dadosLanzados)
            {
                return ResultadoAccion.Fallido($"{jugador.Nombre} ya lanzó los dados en este turno.");
            }

            _movimientosAccion = new ListaSimple<ResultadoMovimiento>();
            TiradaDados tirada = Dado.Lanzar();
            _dadosLanzados = true;
            _fase = FaseTurno.PuedeTerminar;
            RegistrarEvento($"{jugador.Nombre} lanzó los dados: {tirada}.");
            Mover(jugador, tirada.Total, 0);

            string mensaje = $"{jugador.Nombre} sacó {tirada} y llegó a {jugador.CasillaActual}.";
            return ResultadoAccion.Correcto(mensaje) with { Tirada = tirada, Movimientos = _movimientosAccion };
        }
    }

    /// <summary>
    /// Compra la propiedad libre en la que cayó el jugador en turno.
    /// </summary>
    /// <param name="idJugador">Jugador que lo solicita.</param>
    /// <returns>El resultado; falla sin saldo suficiente o si la propiedad tiene dueño.</returns>
    public ResultadoAccion ComprarPropiedad(int idJugador)
    {
        lock (_candado)
        {
            ResultadoAccion? error = ValidarJugadorEnTurno(idJugador, out Jugador jugador);
            if (error != null)
            {
                return error;
            }

            if (!_dadosLanzados)
            {
                return ResultadoAccion.Fallido("Debe lanzar los dados antes de comprar.");
            }

            Propiedad? propiedad = Tablero.BuscarPropiedad(jugador.CasillaActual!.Id);
            if (propiedad == null)
            {
                return ResultadoAccion.Fallido($"{jugador.CasillaActual} no es una propiedad; no se puede comprar.");
            }

            if (propiedad.Propietario != null)
            {
                return ResultadoAccion.Fallido($"{propiedad.Nombre} ya tiene propietario ({propiedad.Propietario.Nombre}).");
            }

            if (_fase != FaseTurno.EsperandoDecisionCompra || _propiedadEnVenta != propiedad)
            {
                return ResultadoAccion.Fallido("No hay ninguna compra pendiente.");
            }

            if (!jugador.PuedePagar(propiedad.PrecioCompra))
            {
                return ResultadoAccion.Fallido(
                    $"Saldo insuficiente: {propiedad.Nombre} cuesta {Formato.Dinero(propiedad.PrecioCompra)} y {jugador.Nombre} tiene {Formato.Dinero(jugador.Saldo)}.");
            }

            Banco.Cobrar(jugador, propiedad.PrecioCompra, TipoTransaccion.CompraPropiedad, _numeroTurno, $"Compra de {propiedad.Nombre}");
            jugador.AgregarPropiedad(propiedad);
            _propiedadEnVenta = null;
            _fase = FaseTurno.PuedeTerminar;
            string mensaje = $"{jugador.Nombre} compró {propiedad.Nombre} por {Formato.Dinero(propiedad.PrecioCompra)}.";
            RegistrarEvento(mensaje);
            return ResultadoAccion.Correcto(mensaje);
        }
    }

    /// <summary>
    /// Rechaza la compra de la propiedad libre en la que cayó el jugador en turno.
    /// </summary>
    /// <param name="idJugador">Jugador que lo solicita.</param>
    /// <returns>El resultado.</returns>
    public ResultadoAccion NoComprar(int idJugador)
    {
        lock (_candado)
        {
            ResultadoAccion? error = ValidarJugadorEnTurno(idJugador, out Jugador jugador);
            if (error != null)
            {
                return error;
            }

            if (_fase != FaseTurno.EsperandoDecisionCompra || _propiedadEnVenta == null)
            {
                return ResultadoAccion.Fallido("No hay ninguna compra pendiente.");
            }

            string mensaje = $"{jugador.Nombre} decidió no comprar {_propiedadEnVenta.Nombre}.";
            _propiedadEnVenta = null;
            _fase = FaseTurno.PuedeTerminar;
            RegistrarEvento(mensaje);
            return ResultadoAccion.Correcto(mensaje);
        }
    }

    /// <summary>
    /// Procesa la lectura de una tarjeta RFID (o su UID virtual). Si hay un pago pendiente y la tarjeta
    /// es del deudor, se valida y ejecuta el pago; si el deudor no puede cubrirlo, paga lo que tiene
    /// y queda eliminado.
    /// </summary>
    /// <param name="uid">UID leído.</param>
    /// <returns>El resultado; falla si no hay pago pendiente o la tarjeta no es del deudor.</returns>
    public ResultadoAccion IdentificarTarjeta(string uid)
    {
        lock (_candado)
        {
            if (_estado != EstadoPartida.EnCurso)
            {
                return ResultadoAccion.Fallido("La partida no está en curso.");
            }

            string uidNormalizado = NormalizarUid(uid);
            Jugador? jugador = BuscarPorUid(uidNormalizado);
            if (jugador == null)
            {
                return ResultadoAccion.Fallido($"La tarjeta {uidNormalizado} no está registrada.");
            }

            if (_pagoPendiente == null)
            {
                return ResultadoAccion.Fallido($"Tarjeta de {jugador.Nombre} leída, pero no hay ningún pago pendiente.");
            }

            Jugador deudor = _pagoPendiente.Deudor;
            if (jugador != deudor)
            {
                return ResultadoAccion.Fallido($"La tarjeta pertenece a {jugador.Nombre}; se espera la tarjeta de {deudor.Nombre}.");
            }

            RegistrarEvento($"{deudor.Nombre} se identificó con su tarjeta.");
            string mensaje = EjecutarPagoPendiente();
            return ResultadoAccion.Correcto(mensaje);
        }
    }

    /// <summary>
    /// Termina el turno del jugador y pasa al siguiente de la cola (saltando a quien deba perder turnos).
    /// Si se completa el máximo de turnos, la partida finaliza.
    /// </summary>
    /// <param name="idJugador">Jugador que lo solicita.</param>
    /// <returns>El resultado; falla si quedan dados, una decisión o un pago pendientes.</returns>
    public ResultadoAccion TerminarTurno(int idJugador)
    {
        lock (_candado)
        {
            ResultadoAccion? error = ValidarJugadorEnTurno(idJugador, out Jugador jugador);
            if (error != null)
            {
                return error;
            }

            switch (_fase)
            {
                case FaseTurno.EsperandoDados:
                    return ResultadoAccion.Fallido("Debe lanzar los dados antes de terminar el turno.");
                case FaseTurno.EsperandoDecisionCompra:
                    return ResultadoAccion.Fallido($"Debe decidir si compra {_propiedadEnVenta?.Nombre} antes de terminar el turno.");
                case FaseTurno.EsperandoPago:
                    return ResultadoAccion.Fallido($"Tiene un pago pendiente: {DescribirPago(_pagoPendiente!)}. Acerque su tarjeta.");
            }

            RegistrarEvento($"{jugador.Nombre} terminó su turno.");
            PasarAlSiguienteTurno(true);
            string mensaje = _estado == EstadoPartida.Finalizada
                ? $"La partida terminó. Ganador: {_ganador!.Nombre}."
                : $"Turno {_numeroTurno}: le toca a {_turnos.Frente().Nombre}.";
            return ResultadoAccion.Correcto(mensaje);
        }
    }

    /// <summary>
    /// Retira de la partida a un jugador (por ejemplo, porque se desconectó y no vuelve): queda eliminado
    /// sin pagar nada, sus propiedades vuelven a estar libres y sale de la cola de turnos; si era su turno,
    /// pasa al siguiente. Solo puede hacerlo el organizador, y no puede retirarse a sí mismo.
    /// </summary>
    /// <param name="idSolicitante">Jugador que lo solicita (debe ser el organizador).</param>
    /// <param name="idRetirado">Jugador a retirar.</param>
    /// <returns>El resultado.</returns>
    public ResultadoAccion RetirarJugador(int idSolicitante, int idRetirado)
    {
        lock (_candado)
        {
            if (_estado != EstadoPartida.EnCurso)
            {
                return ResultadoAccion.Fallido("La partida no está en curso.");
            }

            Jugador? solicitante = BuscarJugador(idSolicitante);
            Jugador organizador = _jugadores.Obtener(0);
            if (solicitante != organizador)
            {
                return ResultadoAccion.Fallido($"Solo el organizador ({organizador.Nombre}) puede retirar jugadores.");
            }

            Jugador? retirado = BuscarJugador(idRetirado);
            if (retirado == null)
            {
                return ResultadoAccion.Fallido($"No existe el jugador {idRetirado}.");
            }

            if (retirado == organizador)
            {
                return ResultadoAccion.Fallido("El organizador no puede retirarse a sí mismo.");
            }

            if (!retirado.Activo)
            {
                return ResultadoAccion.Fallido($"{retirado.Nombre} ya no está en la partida.");
            }

            string mensaje = $"{organizador.Nombre} retiró a {retirado.Nombre} de la partida.";
            RegistrarEvento(mensaje);
            EliminarJugador(retirado);
            return ResultadoAccion.Correcto(mensaje);
        }
    }

    /// <summary>
    /// Devuelve una copia del estado de la partida.
    /// </summary>
    /// <param name="idJugador">Jugador que lo solicita (puede estar eliminado).</param>
    /// <returns>El resultado con <see cref="ResultadoAccion.Instantanea"/>.</returns>
    public ResultadoAccion ConsultarEstado(int idJugador)
    {
        lock (_candado)
        {
            if (BuscarJugador(idJugador) == null)
            {
                return ResultadoAccion.Fallido($"No existe el jugador {idJugador}.");
            }

            return ResultadoAccion.Correcto(DescribirEstado()) with { Instantanea = CrearInstantanea() };
        }
    }

    /// <summary>
    /// Devuelve la tabla con todas las transacciones de la partida.
    /// </summary>
    /// <param name="idJugador">Jugador que lo solicita.</param>
    /// <returns>El resultado con la tabla en <see cref="ResultadoAccion.Mensaje"/>.</returns>
    public ResultadoAccion ConsultarTransacciones(int idJugador)
    {
        lock (_candado)
        {
            if (BuscarJugador(idJugador) == null)
            {
                return ResultadoAccion.Fallido($"No existe el jugador {idJugador}.");
            }

            return ResultadoAccion.Correcto(Historial.ImprimirTodas());
        }
    }

    /// <summary>
    /// Exporta en cualquier momento el historial a un TXT en la carpeta de partidas.
    /// </summary>
    /// <returns>El resultado con la ruta del archivo en el mensaje.</returns>
    public ResultadoAccion ExportarHistorial()
    {
        lock (_candado)
        {
            string? ruta = ExportarHistorialInterno();
            return ruta == null
                ? ResultadoAccion.Fallido("No se pudo exportar el historial.")
                : ResultadoAccion.Correcto(ruta);
        }
    }

    /// <summary>
    /// Busca un jugador por id (incluidos los eliminados).
    /// </summary>
    /// <param name="idJugador">Id del jugador.</param>
    /// <returns>El jugador, o <c>null</c> si no existe.</returns>
    public Jugador? ObtenerJugador(int idJugador)
    {
        lock (_candado)
        {
            return BuscarJugador(idJugador);
        }
    }

    /// <summary>
    /// Devuelve los eventos del registro a partir del número indicado (útil para enviar solo los nuevos).
    /// </summary>
    /// <param name="numero">Primer número de evento a incluir (1 para todos).</param>
    /// <returns>Los eventos en orden cronológico.</returns>
    public EventoJuego[] ObtenerEventosDesde(int numero)
    {
        lock (_candado)
        {
            ListaDobleEnlazada<EventoJuego> nuevos = _eventos.BuscarTodos(evento => evento.Numero >= numero);
            EventoJuego[] resultado = new EventoJuego[nuevos.Cantidad];
            int i = 0;
            nuevos.RecorrerDesdeInicio(evento => resultado[i++] = evento);
            return resultado;
        }
    }

    /// <summary>
    /// Devuelve una copia del estado de la partida sin validar a un solicitante
    /// (la usa el servidor para difundir el estado a todos los clientes).
    /// </summary>
    /// <returns>La instantánea actual.</returns>
    public InstantaneaJuego ObtenerInstantanea()
    {
        lock (_candado)
        {
            return CrearInstantanea();
        }
    }

    /// <summary>
    /// Ejecuta una lectura del historial con el candado tomado, para que ningún otro hilo
    /// registre transacciones mientras se recorre.
    /// </summary>
    /// <typeparam name="T">Tipo del resultado de la lectura.</typeparam>
    /// <param name="lectura">Función que lee el historial (no debe modificarlo).</param>
    /// <returns>El resultado de la lectura.</returns>
    public T LeerHistorial<T>(Func<HistorialTransacciones, T> lectura)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        lock (_candado)
        {
            return lectura(Historial);
        }
    }

    // ----------------------------------------------------------------------------------------
    // Métodos privados: se llaman siempre con el candado tomado.
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Comprueba que la partida esté en curso, que el jugador exista, esté activo y tenga el turno.
    /// </summary>
    private ResultadoAccion? ValidarJugadorEnTurno(int idJugador, out Jugador jugador)
    {
        jugador = null!;
        if (_estado == EstadoPartida.EsperandoJugadores)
        {
            return ResultadoAccion.Fallido("La partida no está en curso: aún no ha comenzado.");
        }

        if (_estado == EstadoPartida.Finalizada)
        {
            return ResultadoAccion.Fallido("La partida no está en curso: ya terminó.");
        }

        Jugador? encontrado = BuscarJugador(idJugador);
        if (encontrado == null)
        {
            return ResultadoAccion.Fallido($"No existe el jugador {idJugador}.");
        }

        jugador = encontrado;
        if (!jugador.Activo)
        {
            return ResultadoAccion.Fallido($"{jugador.Nombre} fue eliminado y no puede realizar acciones.");
        }

        Jugador enTurno = _turnos.Frente();
        if (enTurno != jugador)
        {
            return ResultadoAccion.Fallido($"No es el turno de {jugador.Nombre}; le toca a {enTurno.Nombre}.");
        }

        return null;
    }

    /// <summary>
    /// Mueve al jugador por el tablero y procesa el movimiento.
    /// </summary>
    private void Mover(Jugador jugador, int pasos, int profundidad)
    {
        ProcesarMovimiento(jugador, Tablero.MoverJugador(jugador, pasos), profundidad);
    }

    /// <summary>
    /// Registra el movimiento, paga el premio de Salida por cada paso por ella y resuelve la casilla de llegada.
    /// </summary>
    private void ProcesarMovimiento(Jugador jugador, ResultadoMovimiento movimiento, int profundidad)
    {
        _movimientosAccion.AgregarAlFinal(movimiento);
        RegistrarEvento($"{jugador.Nombre} se mueve de {movimiento.CasillaInicial} a {movimiento.CasillaFinal}.");

        for (int i = 0; i < movimiento.VecesPorSalida; i++)
        {
            int premio = Tablero.Salida.Premio;
            Banco.Pagar(jugador, premio, TipoTransaccion.PremioInicio, _numeroTurno, "Premio por pasar por la Salida");
            RegistrarEvento($"{jugador.Nombre} pasó por la Salida y cobra {Formato.Dinero(premio)}.");
        }

        ResolverCasilla(jugador, profundidad);
    }

    /// <summary>
    /// Pide a la casilla actual su efecto (polimorfismo) y lo aplica.
    /// </summary>
    private void ResolverCasilla(Jugador jugador, int profundidad)
    {
        if (profundidad > ProfundidadMaximaEfectos)
        {
            RegistrarEvento("Se alcanzó el límite de efectos encadenados; no se aplican más movimientos.");
            return;
        }

        ResultadoCasilla resultado = jugador.CasillaActual!.AlCaer(jugador, this);
        RegistrarEvento(resultado.Descripcion);
        AplicarEfectos(jugador, resultado, profundidad);
    }

    /// <summary>
    /// Aplica de forma genérica los efectos descritos por un <see cref="ResultadoCasilla"/>,
    /// sin depender del tipo de casilla que lo produjo.
    /// </summary>
    private void AplicarEfectos(Jugador jugador, ResultadoCasilla resultado, int profundidad)
    {
        if (resultado.MontoARecibir > 0)
        {
            Banco.Pagar(jugador, resultado.MontoARecibir, resultado.TipoCobro, _numeroTurno, resultado.Descripcion);
            RegistrarEvento($"{jugador.Nombre} recibe {Formato.Dinero(resultado.MontoARecibir)} del banco.");
        }

        if (resultado.MontoDeCadaJugador > 0)
        {
            CobrarDeCadaJugador(jugador, resultado.MontoDeCadaJugador, resultado.Descripcion);
            if (_estado != EstadoPartida.EnCurso)
            {
                return;
            }
        }

        if (resultado.TurnosAPerder > 0)
        {
            jugador.PerderTurnos(resultado.TurnosAPerder);
            RegistrarEvento($"{jugador.Nombre} perderá {resultado.TurnosAPerder} turno(s).");
        }

        if (resultado.MontoAPagar > 0)
        {
            EstablecerPagoPendiente(new PagoPendiente(jugador, resultado.Acreedor, resultado.MontoAPagar,
                resultado.TipoPago, resultado.Descripcion, false));
        }
        else if (resultado.MontoACadaJugador > 0 && _turnos.Cantidad > 1)
        {
            EstablecerPagoPendiente(new PagoPendiente(jugador, null, resultado.MontoACadaJugador,
                TipoTransaccion.PagoEntreJugadores, resultado.Descripcion, true));
        }

        if (resultado.PropiedadEnVenta != null)
        {
            _propiedadEnVenta = resultado.PropiedadEnVenta;
            _fase = FaseTurno.EsperandoDecisionCompra;
        }

        if (resultado.Movimiento != 0)
        {
            Mover(jugador, resultado.Movimiento, profundidad + 1);
        }
        else if (resultado.DestinoIndice.HasValue)
        {
            int destino = resultado.DestinoIndice.Value;
            ResultadoMovimiento movimiento = resultado.MovimientoDirecto
                ? Tablero.EnviarJugadorA(jugador, destino)
                : Tablero.MoverJugadorHasta(jugador, destino);
            ProcesarMovimiento(jugador, movimiento, profundidad + 1);
        }
    }

    private void EstablecerPagoPendiente(PagoPendiente pago)
    {
        _pagoPendiente = pago;
        _fase = FaseTurno.EsperandoPago;
        RegistrarEvento($"{DescribirPago(pago)}. {pago.Deudor.Nombre} debe acercar su tarjeta.");
    }

    /// <summary>
    /// Ejecuta el pago pendiente del deudor ya identificado; si no le alcanza, paga lo que tiene y queda eliminado.
    /// </summary>
    private string EjecutarPagoPendiente()
    {
        PagoPendiente pago = _pagoPendiente!;
        _pagoPendiente = null;
        Jugador deudor = pago.Deudor;

        if (pago.ACadaJugador)
        {
            Jugador[] otros = OtrosActivos(deudor);
            int total = pago.Monto * otros.Length;
            if (deudor.PuedePagar(total))
            {
                foreach (Jugador otro in otros)
                {
                    Banco.Transferir(deudor, otro, pago.Monto, pago.Tipo, _numeroTurno, pago.Descripcion);
                }

                return FinalizarPagoExitoso($"{deudor.Nombre} pagó {Formato.Dinero(pago.Monto)} a cada jugador.");
            }

            foreach (Jugador otro in otros)
            {
                PagarLoQueTiene(deudor, otro, pago.Monto, pago.Tipo, pago.Descripcion);
            }
        }
        else
        {
            if (deudor.PuedePagar(pago.Monto))
            {
                PagarA(deudor, pago.Acreedor, pago.Monto, pago.Tipo, pago.Descripcion);
                return FinalizarPagoExitoso($"{deudor.Nombre} pagó {Formato.Dinero(pago.Monto)} {DestinoPago(pago.Acreedor)}.");
            }

            PagarLoQueTiene(deudor, pago.Acreedor, pago.Monto, pago.Tipo, pago.Descripcion);
        }

        string mensaje = $"{deudor.Nombre} no pudo cubrir el pago de {Formato.Dinero(pago.Monto)} y queda eliminado.";
        RegistrarEvento(mensaje);
        EliminarJugador(deudor);
        return mensaje;
    }

    private string FinalizarPagoExitoso(string mensaje)
    {
        _fase = FaseTurno.PuedeTerminar;
        RegistrarEvento(mensaje);
        return mensaje;
    }

    /// <summary>
    /// Cada uno de los demás jugadores activos paga al beneficiario; quien no pueda, paga lo que tiene y queda eliminado.
    /// </summary>
    private void CobrarDeCadaJugador(Jugador beneficiario, int monto, string descripcion)
    {
        foreach (Jugador otro in OtrosActivos(beneficiario))
        {
            if (otro.PuedePagar(monto))
            {
                Banco.Transferir(otro, beneficiario, monto, TipoTransaccion.PagoEntreJugadores, _numeroTurno, descripcion);
                RegistrarEvento($"{otro.Nombre} paga {Formato.Dinero(monto)} a {beneficiario.Nombre}.");
            }
            else
            {
                PagarLoQueTiene(otro, beneficiario, monto, TipoTransaccion.PagoEntreJugadores, descripcion);
                RegistrarEvento($"{otro.Nombre} no pudo pagar {Formato.Dinero(monto)} a {beneficiario.Nombre} y queda eliminado.");
                EliminarJugador(otro);
                if (_estado != EstadoPartida.EnCurso)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Paga al acreedor lo que el deudor pueda del monto indicado (todo su saldo como máximo).
    /// </summary>
    private void PagarLoQueTiene(Jugador deudor, Jugador? acreedor, int monto, TipoTransaccion tipo, string descripcion)
    {
        int parcial = Math.Min(monto, deudor.Saldo);
        if (parcial > 0)
        {
            PagarA(deudor, acreedor, parcial, tipo, $"{descripcion} (pago parcial de {Formato.Dinero(monto)})");
        }
    }

    private void PagarA(Jugador deudor, Jugador? acreedor, int monto, TipoTransaccion tipo, string descripcion)
    {
        if (acreedor == null)
        {
            Banco.Cobrar(deudor, monto, tipo, _numeroTurno, descripcion);
        }
        else
        {
            Banco.Transferir(deudor, acreedor, monto, tipo, _numeroTurno, descripcion);
        }
    }

    /// <summary>
    /// Deja inactivo al jugador, libera sus propiedades y lo quita de la cola de turnos. Si queda un
    /// único jugador activo, la partida termina; si era su turno, el turno pasa al siguiente.
    /// </summary>
    private void EliminarJugador(Jugador jugador)
    {
        bool eraSuTurno = _turnos.Frente() == jugador;
        jugador.Desactivar();
        int liberadas = jugador.LiberarPropiedades();
        _turnos.Eliminar(jugador);
        RegistrarEvento($"{jugador.Nombre} quedó eliminado; {liberadas} propiedad(es) vuelven a estar libres.");

        if (_turnos.Cantidad == 1)
        {
            Finalizar(_turnos.Frente(), "es el único jugador activo");
            return;
        }

        if (eraSuTurno)
        {
            PasarAlSiguienteTurno(false);
        }
    }

    /// <summary>
    /// Avanza la cola de turnos. Salta (consumiéndolos) a los jugadores con turnos por perder y
    /// finaliza la partida si ya se jugó el máximo de turnos.
    /// </summary>
    /// <param name="rotar"><c>false</c> si el siguiente jugador ya está al frente (al iniciar o tras una eliminación).</param>
    private void PasarAlSiguienteTurno(bool rotar)
    {
        _dadosLanzados = false;
        _propiedadEnVenta = null;
        _pagoPendiente = null;

        while (true)
        {
            if (_numeroTurno >= MaximoTurnos)
            {
                FinalizarPorLimiteDeTurnos();
                return;
            }

            if (rotar)
            {
                _turnos.Rotar();
            }

            rotar = true;
            _numeroTurno++;
            Jugador jugador = _turnos.Frente();
            if (jugador.ConsumirTurnoPerdido())
            {
                RegistrarEvento($"Turno {_numeroTurno}: {jugador.Nombre} pierde este turno.");
                continue;
            }

            _fase = FaseTurno.EsperandoDados;
            RegistrarEvento($"Turno {_numeroTurno}: le toca a {jugador.Nombre}.");
            return;
        }
    }

    private void FinalizarPorLimiteDeTurnos()
    {
        Jugador? mejor = null;
        _turnos.Recorrer(jugador =>
        {
            RegistrarEvento($"{jugador.Nombre}: patrimonio {Formato.Dinero(jugador.CalcularPatrimonio())}.");
            if (mejor == null || jugador.CalcularPatrimonio() > mejor.CalcularPatrimonio())
            {
                mejor = jugador;
            }
        });

        Finalizar(mejor!, $"se alcanzó el máximo de {MaximoTurnos} turnos y gana el mayor patrimonio");
    }

    private void Finalizar(Jugador ganador, string motivo)
    {
        _estado = EstadoPartida.Finalizada;
        _ganador = ganador;
        _pagoPendiente = null;
        _propiedadEnVenta = null;
        RegistrarEvento($"La partida terminó: {motivo}. Ganador: {ganador.Nombre} con patrimonio {Formato.Dinero(ganador.CalcularPatrimonio())}.");

        if (_exportarAlFinalizar)
        {
            ExportarHistorialInterno();
        }
    }

    /// <summary>
    /// Exporta el historial a partidas/partida_AAAA-MM-DD_HH-mm-ss.txt (con sufijo si ya existe).
    /// </summary>
    /// <returns>La ruta completa, o <c>null</c> si falló (el error queda en el registro de eventos).</returns>
    private string? ExportarHistorialInterno()
    {
        string marca = _reloj().ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        string ruta = Path.Combine(_carpetaPartidas, $"partida_{marca}.txt");
        for (int sufijo = 2; File.Exists(ruta); sufijo++)
        {
            ruta = Path.Combine(_carpetaPartidas, $"partida_{marca}_{sufijo}.txt");
        }

        try
        {
            Historial.ExportarTxt(ruta, FechaInicio, _jugadores);
            _rutaHistorialExportado = Path.GetFullPath(ruta);
            RegistrarEvento($"Historial exportado a {_rutaHistorialExportado}.");
            return _rutaHistorialExportado;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            RegistrarEvento($"No se pudo exportar el historial: {ex.Message}");
            return null;
        }
    }

    private Jugador[] OtrosActivos(Jugador jugador)
    {
        Jugador[] otros = new Jugador[_turnos.Cantidad - 1];
        int i = 0;
        _turnos.Recorrer(otro =>
        {
            if (otro != jugador)
            {
                otros[i++] = otro;
            }
        });
        return otros;
    }

    private Jugador? BuscarJugador(int idJugador)
    {
        return _jugadores.Buscar(jugador => jugador.Id == idJugador);
    }

    private Jugador? BuscarPorUid(string uid)
    {
        return _jugadores.Buscar(jugador => jugador.UidTarjeta == uid);
    }

    private static string NormalizarUid(string? uid)
    {
        return (uid ?? string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Complemento "a Ana" / "al banco" para los mensajes de pago.
    /// </summary>
    private static string DestinoPago(Jugador? acreedor)
    {
        return acreedor == null ? "al banco" : "a " + acreedor.Nombre;
    }

    private static string DescribirPago(PagoPendiente pago)
    {
        string destino = pago.ACadaJugador ? "a cada jugador" : DestinoPago(pago.Acreedor);
        return $"{pago.Deudor.Nombre} debe pagar {Formato.Dinero(pago.Monto)} {destino}";
    }

    private void RegistrarEvento(string texto)
    {
        _eventos.AgregarAlFinal(new EventoJuego(_eventos.Cantidad + 1, _reloj(), _numeroTurno, texto));
    }

    private string DescribirEstado()
    {
        return _estado switch
        {
            EstadoPartida.EsperandoJugadores => $"Esperando jugadores ({_jugadores.Cantidad}/{MaximoJugadores}).",
            EstadoPartida.EnCurso => $"Turno {_numeroTurno}: le toca a {_turnos.Frente().Nombre} ({_fase}).",
            _ => $"Partida finalizada. Ganador: {_ganador?.Nombre}.",
        };
    }

    private InstantaneaJuego CrearInstantanea()
    {
        EstadoJugador[] jugadores = new EstadoJugador[_jugadores.Cantidad];
        int i = 0;
        _jugadores.Recorrer(jugador =>
        {
            int[] propiedades = new int[jugador.CantidadPropiedades];
            int k = 0;
            jugador.RecorrerPropiedades(propiedad => propiedades[k++] = propiedad.Id);
            bool fisica = jugador.UidTarjeta != null && !jugador.UidTarjeta.StartsWith(PrefijoTarjetaVirtual, StringComparison.Ordinal);
            jugadores[i++] = new EstadoJugador(jugador.Id, jugador.Nombre, jugador.ColorFicha, jugador.Saldo,
                jugador.CasillaActual?.Id ?? Tablero.IndiceSalida, jugador.Activo, jugador.TurnosPorPerder,
                jugador.CalcularPatrimonio(), propiedades, fisica);
        });

        bool enCurso = _estado == EstadoPartida.EnCurso;
        int montoPendiente = 0;
        if (_pagoPendiente != null)
        {
            montoPendiente = _pagoPendiente.ACadaJugador
                ? _pagoPendiente.Monto * (_turnos.Cantidad - 1)
                : _pagoPendiente.Monto;
        }

        return new InstantaneaJuego(
            _estado,
            _fase,
            _numeroTurno,
            MaximoTurnos,
            enCurso ? _turnos.Frente().Id : null,
            _ganador?.Id,
            _propiedadEnVenta?.Id,
            _pagoPendiente?.Deudor.Id,
            _pagoPendiente == null ? null : DescribirPago(_pagoPendiente),
            montoPendiente,
            Dado.UltimaTirada,
            jugadores,
            _eventos.Cantidad,
            Historial.Cantidad);
    }
}
