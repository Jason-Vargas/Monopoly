using System;
using System.IO;
using System.Net;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;
using Monopoly.Tests.Modelo;
using Monopoly.Tests.Red;
using Xunit;

namespace Monopoly.Tests.Hardware;

/// <summary>
/// Integración del cajero con el servidor en modo hardware: los dados solo con el botón físico, las compras y
/// los pagos solo con la tarjeta del jugador, y el LED de pago (PAGO_OK / PAGO_RECHAZADO). Se usa un
/// <see cref="CajeroFalso"/> al que se le inyectan líneas BOTON, RFID y basura, con clientes TCP reales.
/// Dados por defecto: Ana 1+2 (Báltica), Beto 1+2 (Báltica), Carla 3+3 (Oriental), Ana 2+2...
/// </summary>
public class PruebasCajeroServidor : IDisposable
{
    private const string TarjetaAna = "AAAAAAAA";
    private const string TarjetaBeto = "BBBBBBBB";
    private const string TarjetaCarla = "CCCCCCCC";

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "monopoly-cajero-" + Guid.NewGuid().ToString("N"));
    private readonly CajeroFalso _cajero = new CajeroFalso();
    private readonly ListaSimple<ClientePrueba> _clientes = new ListaSimple<ClientePrueba>();
    private Servidor _servidor = null!;
    private ClientePrueba _ana = null!;
    private ClientePrueba _beto = null!;
    private ClientePrueba _carla = null!;

    private ClientePrueba[] Todos => new[] { _ana, _beto, _carla };

    /// <summary>
    /// Crea el servidor (modo hardware, con el cajero falso conectado) y une a Ana, Beto y Carla.
    /// </summary>
    private void Preparar(int saldo = Jugador.SaldoInicial, int[]? dados = null, bool conCajero = true)
    {
        CartaEvento[] neutra = { new CartaEvento("Carta sin efecto", TipoCartaEvento.RecibirDinero, 0) };
        Juego juego = new Juego(new OpcionesJuego
        {
            Dado = Dado.ConValoresFijos(dados ?? new[] { 1, 2, 1, 2, 3, 3, 2, 2 }),
            Reloj = () => Fabrica.FechaFija,
            CartasCasualidad = neutra,
            CartasArcaComunal = neutra,
            CarpetaPartidas = _carpeta,
            SaldoInicial = saldo,
        });
        _servidor = new Servidor(juego, 0, IPAddress.Loopback);
        _servidor.Iniciar();
        if (conCajero)
        {
            _cajero.Conectar();
            _servidor.UsarCajero(_cajero);
        }

        _ana = Nuevo("Ana");
        _beto = Nuevo("Beto");
        _carla = Nuevo("Carla");
    }

    private ClientePrueba Nuevo(string nombre)
    {
        ClientePrueba cliente = new ClientePrueba(_servidor.Puerto, nombre);
        cliente.Unirse();
        _clientes.AgregarAlFinal(cliente);
        return cliente;
    }

    private void Vincular(int idJugador, string uid)
    {
        _ana.Cliente.VincularTarjeta(idJugador);
        _ana.Esperar(Protocolo.Evento, m => m.Campo(0).StartsWith("Cajero: Acerque al lector", StringComparison.Ordinal));
        _cajero.Inyectar("RFID:" + uid);
        _ana.EsperarEstado(e => e.BuscarJugador(idJugador)!.TieneTarjetaFisica);
    }

    /// <summary>
    /// Vincula las tres tarjetas e inicia la partida.
    /// </summary>
    private void VincularEIniciar()
    {
        Vincular(1, TarjetaAna);
        Vincular(2, TarjetaBeto);
        Vincular(3, TarjetaCarla);
        _ana.Cliente.IniciarPartida();
        foreach (ClientePrueba cliente in Todos)
        {
            cliente.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);
        }
    }

    private void EsperarAvisoEnTodos(string texto)
    {
        foreach (ClientePrueba cliente in Todos)
        {
            cliente.Esperar(Protocolo.Evento, m => m.Campo(0).Contains(texto, StringComparison.Ordinal));
        }
    }

    private void Boton(Predicate<EstadoRed> estadoEsperado)
    {
        _cajero.Inyectar("BOTON");
        foreach (ClientePrueba cliente in Todos)
        {
            cliente.EsperarEstado(estadoEsperado);
        }
    }

    private int ContarEnviadas(string linea)
    {
        int cantidad = 0;
        foreach (string enviada in _cajero.Enviadas())
        {
            cantidad += enviada == linea ? 1 : 0;
        }

        return cantidad;
    }

    /// <summary>
    /// Ana tira con el botón, elige comprar Báltica y pasa su tarjeta.
    /// </summary>
    private void AnaCompraBaltica()
    {
        Boton(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        _ana.Cliente.ComprarPropiedad();
        _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoTarjetaCompra);
        _cajero.Inyectar("RFID:" + TarjetaAna);
        _ana.EsperarEstado(e => e.PropietarioDe(3) == 1);
    }

    /// <summary>
    /// Ana compra Báltica y Beto (con el botón) cae en ella: pago pendiente de Beto a Ana (₡4).
    /// </summary>
    private void LlevarABetoAPagarAlquiler()
    {
        VincularEIniciar();
        AnaCompraBaltica();
        _ana.Cliente.TerminarTurno();
        _beto.EsperarEstado(e => e.Instantanea.IdJugadorEnTurno == 2);
        _cajero.Inyectar("BOTON");
        EsperarAvisoEnTodos("Beto debe pagar ₡4 a Ana: acerque su tarjeta al lector.");
    }

    // ------------------------------------------------------------------ modo, inicio y validaciones

    [Fact]
    public void Estado_IndicaModoHardwareYCajero()
    {
        Preparar();

        _carla.Cliente.ConsultarEstado();
        EstadoRed estado = _carla.EsperarEstado(e => true);
        Assert.True(estado.CajeroConectado);
        Assert.False(estado.ModoSinHardware);
        Assert.False(estado.EnPausaPorCajero);

        _servidor.EstablecerModoSinHardware(true);

        EsperarAvisoEnTodos("Modo sin hardware (pruebas)");
        _carla.EsperarEstado(e => e.ModoSinHardware && !e.CajeroConectado && !e.EnPausaPorCajero);
    }

    [Fact]
    public void Iniciar_EnModoHardware_ExigeTarjetaParaTodos()
    {
        Preparar();
        Vincular(1, TarjetaAna);

        _ana.Cliente.IniciarPartida();

        _ana.EsperarError("Todos los jugadores deben tener una tarjeta vinculada antes de iniciar. Faltan: Beto, Carla.");
        Vincular(2, TarjetaBeto);
        Vincular(3, TarjetaCarla);
        _ana.Cliente.IniciarPartida();
        _ana.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);
    }

    [Fact]
    public void Red_NoPuedeTirarNiPagarEnModoHardware()
    {
        Preparar();
        VincularEIniciar();

        _ana.Cliente.TirarDados();
        _ana.EsperarError("Los dados se lanzan con el botón físico del cajero.");
        _ana.Cliente.PagarConTarjeta();
        _ana.EsperarError("Acerque su tarjeta al lector del cajero.");
        Assert.Equal(0, _ana.Contar(Protocolo.Dados));
    }

    // ------------------------------------------------------------------ botón

    [Fact]
    public void Boton_EnElMomentoCorrecto_TiraYEnviaDadosALaPico()
    {
        Preparar();
        VincularEIniciar();

        _cajero.Inyectar("BOTON");

        foreach (ClientePrueba cliente in Todos)
        {
            Mensaje dados = cliente.Esperar(Protocolo.Dados);
            Assert.Equal((1, 1, 2), (dados.Entero(0), dados.Entero(1), dados.Entero(2)));
            cliente.EsperarEstado(e => e.IdJugadorUltimoMovimiento == 1 && e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        }

        Assert.Equal(1, ContarEnviadas("DADOS:1,2"));
    }

    [Fact]
    public void Boton_EnMomentoIncorrecto_SeIgnoraSinErrores()
    {
        Preparar();
        _cajero.Inyectar("BOTON");
        EsperarAvisoEnTodos("Se ignoró el botón: la partida no está en curso.");

        VincularEIniciar();
        Boton(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);

        // Doble pulsación en el mismo turno: la segunda se ignora.
        _cajero.Inyectar("BOTON");
        EsperarAvisoEnTodos("Se ignoró el botón: Ana ya lanzó los dados en este turno.");

        foreach (ClientePrueba cliente in Todos)
        {
            Assert.Equal(1, cliente.Contar(Protocolo.Dados));
            Assert.Equal(0, cliente.Contar(Protocolo.Error));
        }

        Assert.Equal(1, ContarEnviadas("DADOS:1,2"));
    }

    // ------------------------------------------------------------------ compras con tarjeta

    [Fact]
    public void Compra_ConfirmadaConLaTarjeta_EnviaPagoOk()
    {
        Preparar();
        VincularEIniciar();
        Boton(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);

        _ana.Cliente.ComprarPropiedad();

        EsperarAvisoEnTodos("Ana quiere comprar Avenida Báltica por ₡60: acerque su tarjeta al lector.");
        EstadoRed esperando = _carla.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoTarjetaCompra);
        Assert.Equal(1500, esperando.BuscarJugador(1)!.Saldo);
        Assert.Null(esperando.PropietarioDe(3));

        _cajero.Inyectar("RFID:" + TarjetaAna);

        EsperarAvisoEnTodos("Ana compró Avenida Báltica por ₡60.");
        foreach (ClientePrueba cliente in Todos)
        {
            EstadoRed estado = cliente.EsperarEstado(e => e.PropietarioDe(3) == 1);
            Assert.Equal(1440, estado.BuscarJugador(1)!.Saldo);
            Assert.Equal(FaseTurno.PuedeTerminar, estado.Instantanea.Fase);
        }

        _ana.Cliente.ConsultarTransacciones(FiltroTransacciones.Tipo, "CompraPropiedad");
        ListaSimple<Transaccion> compras = SerializadorTransacciones.Decodificar(_ana.Esperar(Protocolo.Transacciones));
        Assert.Equal(1, compras.Cantidad);
        Assert.Equal(("Ana", Transaccion.Banco, 60), (compras.Obtener(0).Origen, compras.Obtener(0).Destino, compras.Obtener(0).Monto));
        Assert.Equal((1, 0), (ContarEnviadas("PAGO_OK"), ContarEnviadas("PAGO_RECHAZADO")));
    }

    [Fact]
    public void Compra_Cancelada_NoCobraNiTocaElLed()
    {
        Preparar();
        VincularEIniciar();
        Boton(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        _ana.Cliente.ComprarPropiedad();
        _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoTarjetaCompra);

        _ana.Cliente.NoComprar();

        EsperarAvisoEnTodos("Ana canceló la compra de Avenida Báltica.");
        _carla.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.PuedeTerminar && e.PropietarioDe(3) == null);

        // La tarjeta ya no compra: solo informa de quién es y su saldo.
        _cajero.Inyectar("RFID:" + TarjetaAna);
        EsperarAvisoEnTodos("Cajero: Tarjeta de Ana: saldo ₡1,500.");
        Assert.Equal((0, 0), (ContarEnviadas("PAGO_OK"), ContarEnviadas("PAGO_RECHAZADO")));
    }

    [Fact]
    public void Compra_ConSaldoInsuficiente_SeRechazaYPuedeNoComprar()
    {
        Preparar(saldo: 50);
        VincularEIniciar();
        Boton(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        _ana.Cliente.ComprarPropiedad();
        _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoTarjetaCompra);

        _cajero.Inyectar("RFID:" + TarjetaAna);

        EsperarAvisoEnTodos("Compra rechazada por saldo insuficiente: Avenida Báltica cuesta ₡60 y Ana tiene ₡50.");
        EstadoRed estado = _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        Assert.Equal(50, estado.BuscarJugador(1)!.Saldo);
        Assert.Null(estado.PropietarioDe(3));
        Assert.Equal((0, 1), (ContarEnviadas("PAGO_OK"), ContarEnviadas("PAGO_RECHAZADO")));

        _ana.Cliente.NoComprar();
        _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.PuedeTerminar);
    }

    [Fact]
    public void Compra_ConLaTarjetaDeOtroJugador_SeRechaza()
    {
        Preparar();
        VincularEIniciar();
        Boton(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        _ana.Cliente.ComprarPropiedad();
        _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoTarjetaCompra);

        _cajero.Inyectar("RFID:" + TarjetaBeto);

        EsperarAvisoEnTodos("La tarjeta pertenece a Beto; se espera la tarjeta de Ana para comprar Avenida Báltica.");
        _ana.Cliente.ConsultarEstado();
        EstadoRed estado = _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoTarjetaCompra);
        Assert.Equal((1500, 1500), (estado.BuscarJugador(1)!.Saldo, estado.BuscarJugador(2)!.Saldo));
        Assert.Equal((0, 1), (ContarEnviadas("PAGO_OK"), ContarEnviadas("PAGO_RECHAZADO")));

        // Después, la tarjeta correcta sí completa la compra.
        _cajero.Inyectar("RFID:" + TarjetaAna);
        _ana.EsperarEstado(e => e.PropietarioDe(3) == 1);
        Assert.Equal(1, ContarEnviadas("PAGO_OK"));
    }

    // ------------------------------------------------------------------ cobros obligatorios con tarjeta

    [Fact]
    public void Pago_SoloConLaTarjetaDelDeudor()
    {
        Preparar();
        LlevarABetoAPagarAlquiler();
        foreach (ClientePrueba cliente in Todos)
        {
            Assert.Equal("Beto debe pagar ₡4 a Ana", cliente.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoPago).Instantanea.DescripcionPagoPendiente);
        }

        int okAntes = ContarEnviadas("PAGO_OK");

        _cajero.Inyectar("RFID:" + TarjetaCarla);
        EsperarAvisoEnTodos("La tarjeta pertenece a Carla; se espera la tarjeta de Beto.");
        _cajero.Inyectar("RFID:EEEEEEEE");
        EsperarAvisoEnTodos("La tarjeta EEEEEEEE no está registrada");
        Assert.Equal(2, ContarEnviadas("PAGO_RECHAZADO"));
        _carla.Cliente.ConsultarEstado();
        EstadoRed sinCambios = _carla.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoPago);
        Assert.Equal((1440, 1500), (sinCambios.BuscarJugador(1)!.Saldo, sinCambios.BuscarJugador(2)!.Saldo));

        _cajero.Inyectar("rfid: bb bb bb bb");  // UID con espacios y minúsculas: se normaliza

        EsperarAvisoEnTodos("Beto pagó ₡4 a Ana.");
        foreach (ClientePrueba cliente in Todos)
        {
            EstadoRed estado = cliente.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.PuedeTerminar);
            Assert.Equal((1444, 1496), (estado.BuscarJugador(1)!.Saldo, estado.BuscarJugador(2)!.Saldo));
        }

        Assert.Equal(okAntes + 1, ContarEnviadas("PAGO_OK"));
        _beto.Cliente.ConsultarTransacciones(FiltroTransacciones.Tipo, "PagoAlquiler");
        Assert.Equal(1, SerializadorTransacciones.Decodificar(_beto.Esperar(Protocolo.Transacciones)).Cantidad);

        // Fuera de un pago o una compra, la tarjeta solo informa de quién es y su saldo.
        _cajero.Inyectar("RFID:" + TarjetaBeto);
        EsperarAvisoEnTodos("Tarjeta de Beto: saldo ₡1,496.");
        Assert.Equal(okAntes + 1, ContarEnviadas("PAGO_OK"));
        Assert.Equal(2, ContarEnviadas("PAGO_RECHAZADO"));
    }

    [Fact]
    public void Pago_SinSaldo_AplicaLaEliminacionYEnviaPagoRechazado()
    {
        // Ana 3+3 (Oriental, no compra); Beto 1+3 (Impuesto sobre la Renta, ₡200) con solo ₡100.
        Preparar(saldo: 100, dados: new[] { 3, 3, 1, 3, 2, 2 });
        VincularEIniciar();
        Boton(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        _ana.Cliente.NoComprar();
        _ana.Cliente.TerminarTurno();
        _beto.EsperarEstado(e => e.Instantanea.IdJugadorEnTurno == 2);
        Boton(e => e.Instantanea.Fase == FaseTurno.EsperandoPago && e.Instantanea.IdDeudor == 2);

        _cajero.Inyectar("RFID:" + TarjetaBeto);

        EsperarAvisoEnTodos("Beto no pudo cubrir el pago de ₡200 y queda eliminado.");
        foreach (ClientePrueba cliente in Todos)
        {
            EstadoRed estado = cliente.EsperarEstado(e => !e.BuscarJugador(2)!.Activo);
            Assert.Equal(0, estado.BuscarJugador(2)!.Saldo);
            Assert.Equal(3, estado.Instantanea.IdJugadorEnTurno);
        }

        Assert.Equal((0, 1), (ContarEnviadas("PAGO_OK"), ContarEnviadas("PAGO_RECHAZADO")));
    }

    [Fact]
    public void LineasBasura_NoAfectanLaPartida()
    {
        Preparar();
        VincularEIniciar();

        _cajero.Inyectar("MPY: soft reboot");
        _cajero.Inyectar(">>> ");
        _cajero.Inyectar("RFID:XYZ");
        _cajero.Inyectar("Traceback (most recent call last):");

        Assert.Equal(4, _cajero.LineasIgnoradas);
        _ana.Cliente.ConsultarEstado();
        EstadoRed estado = _ana.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);
        Assert.Equal(FaseTurno.EsperandoDados, estado.Instantanea.Fase);
        Assert.Equal(0, _ana.Contar(Protocolo.Dados));
    }

    // ------------------------------------------------------------------ vinculación

    [Fact]
    public void VincularTarjeta_ValidacionesYUidNoRepetido()
    {
        Preparar();

        _beto.Cliente.VincularTarjeta(2);
        _beto.EsperarError("Solo el organizador puede vincular tarjetas");
        _ana.Cliente.VincularTarjeta(9);
        _ana.EsperarError("No existe un jugador activo con id 9");

        Vincular(2, TarjetaBeto);

        // La misma tarjeta no puede quedar asignada a otro jugador.
        _ana.Cliente.VincularTarjeta(3);
        _ana.Esperar(Protocolo.Evento, m => m.Campo(0).StartsWith("Cajero: Acerque al lector", StringComparison.Ordinal));
        _cajero.Inyectar("RFID:" + TarjetaBeto);
        _ana.EsperarError($"La tarjeta {TarjetaBeto} ya está vinculada a Beto");

        Vincular(3, TarjetaCarla);

        _ana.Cliente.VincularTarjeta(1);
        _ana.Esperar(Protocolo.Evento, m => m.Campo(0).Contains("tarjeta de Ana", StringComparison.Ordinal));
        _ana.Cliente.VincularTarjeta(0);
        EsperarAvisoEnTodos("Se canceló la vinculación");
        _cajero.Inyectar("RFID:DDDDDDDD");
        EsperarAvisoEnTodos("La tarjeta DDDDDDDD no está registrada");
    }

    [Fact]
    public void VincularTarjeta_SinCajeroFisico_SeRechaza()
    {
        Preparar(conCajero: false);

        _ana.Cliente.VincularTarjeta(2);

        _ana.EsperarError("No hay un cajero conectado");
    }

    // ------------------------------------------------------------------ desconexión de la Pico

    [Fact]
    public void Desconexion_PausaLaPartidaYPermiteReconectarSinPerderla()
    {
        Preparar();
        LlevarABetoAPagarAlquiler();

        _cajero.Desenchufar();

        EsperarAvisoEnTodos("Cajero desconectado: la partida queda en pausa hasta que se conecte la Pico W.");
        _beto.EsperarEstado(e => e.EnPausaPorCajero && e.Instantanea.Fase == FaseTurno.EsperandoPago);
        _beto.Cliente.PagarConTarjeta();
        _beto.EsperarError("Acerque su tarjeta al lector del cajero.");

        // Reconexión: la partida sigue donde estaba.
        _cajero.Conectar();
        EsperarAvisoEnTodos("Cajero falso de pruebas conectado");
        _cajero.Inyectar("RFID:" + TarjetaBeto);
        _carla.EsperarEstado(e => !e.EnPausaPorCajero && e.Instantanea.Fase == FaseTurno.PuedeTerminar && e.BuscarJugador(2)!.Saldo == 1496);
    }

    // ------------------------------------------------------------------ modo sin hardware (pruebas)

    [Fact]
    public void ModoSinHardware_SimulaBotonYTarjetaConElMismoFlujo()
    {
        Preparar();
        Assert.False(_servidor.SimularBoton().Exito);

        _servidor.EstablecerModoSinHardware(true);
        int enviadasAntes = _cajero.Enviadas().Length;
        _ana.Cliente.IniciarPartida();  // sin tarjetas físicas: en modo pruebas se permite
        _ana.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);

        Assert.True(_servidor.SimularBoton().Exito);
        foreach (ClientePrueba cliente in Todos)
        {
            cliente.Esperar(Protocolo.Dados);
        }

        _ana.Cliente.ComprarPropiedad();
        _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoTarjetaCompra);
        Assert.True(_servidor.SimularTarjetaDelJugadorEnTurno().Exito);

        _carla.EsperarEstado(e => e.PropietarioDe(3) == 1 && e.BuscarJugador(1)!.Saldo == 1440);
        Assert.Equal(enviadasAntes, _cajero.Enviadas().Length);  // nada se envía a la Pico
    }

    public void Dispose()
    {
        _clientes.Recorrer(cliente => cliente.Dispose());
        _servidor?.Dispose();
        _cajero.Dispose();
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, true);
        }
    }
}
