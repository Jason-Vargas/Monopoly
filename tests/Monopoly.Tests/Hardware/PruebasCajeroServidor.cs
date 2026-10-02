using System;
using System.IO;
using System.Net;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Hardware;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;
using Monopoly.Tests.Modelo;
using Monopoly.Tests.Red;
using Xunit;

namespace Monopoly.Tests.Hardware;

/// <summary>
/// Integración del cajero con el servidor: líneas BOTON, RFID y basura inyectadas en un cajero falso,
/// con clientes TCP reales. Dados: Ana 1+2 (Báltica), Beto 1+2 (Báltica), Carla 3+3...
/// </summary>
public class PruebasCajeroServidor : IDisposable
{
    private const string TarjetaBeto = "BBBBBBBB";
    private const string TarjetaCarla = "CCCCCCCC";

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "monopoly-cajero-" + Guid.NewGuid().ToString("N"));
    private readonly Servidor _servidor;
    private readonly CajeroFalso _cajero = new CajeroFalso();
    private readonly ListaSimple<ClientePrueba> _clientes = new ListaSimple<ClientePrueba>();
    private ClientePrueba _ana = null!;
    private ClientePrueba _beto = null!;
    private ClientePrueba _carla = null!;

    public PruebasCajeroServidor()
    {
        CartaEvento[] neutra = { new CartaEvento("Carta sin efecto", TipoCartaEvento.RecibirDinero, 0) };
        Juego juego = new Juego(new OpcionesJuego
        {
            Dado = Dado.ConValoresFijos(1, 2, 1, 2, 3, 3, 2, 2),
            Reloj = () => Fabrica.FechaFija,
            CartasCasualidad = neutra,
            CartasArcaComunal = neutra,
            CarpetaPartidas = _carpeta,
        });
        _servidor = new Servidor(juego, 0, IPAddress.Loopback);
        _servidor.Iniciar();
    }

    private ClientePrueba Nuevo(string nombre)
    {
        ClientePrueba cliente = new ClientePrueba(_servidor.Puerto, nombre);
        cliente.Unirse();
        _clientes.AgregarAlFinal(cliente);
        return cliente;
    }

    private void UnirTres(bool conCajero = true)
    {
        if (conCajero)
        {
            _cajero.Conectar();
            _servidor.UsarCajero(_cajero);
        }

        _ana = Nuevo("Ana");
        _beto = Nuevo("Beto");
        _carla = Nuevo("Carla");
    }

    private void Iniciar()
    {
        _ana.Cliente.IniciarPartida();
        foreach (ClientePrueba cliente in new[] { _ana, _beto, _carla })
        {
            cliente.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);
        }
    }

    private void EsperarAvisoEnTodos(string texto)
    {
        foreach (ClientePrueba cliente in new[] { _ana, _beto, _carla })
        {
            cliente.Esperar(Protocolo.Evento, m => m.Campo(0).Contains(texto, StringComparison.Ordinal));
        }
    }

    private void Vincular(ClientePrueba organizador, int idJugador, string uid)
    {
        organizador.Cliente.VincularTarjeta(idJugador);
        organizador.Esperar(Protocolo.Evento, m => m.Campo(0).StartsWith("Cajero: Acerque al lector", StringComparison.Ordinal));
        _cajero.Inyectar("RFID:" + uid);
    }

    /// <summary>
    /// Ana compra Báltica y Beto cae en ella: queda un pago pendiente de Beto a Ana ($4).
    /// </summary>
    private void LlevarABetoAPagarAlquiler()
    {
        Iniciar();
        _ana.Cliente.TirarDados();
        _ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        _ana.Cliente.ComprarPropiedad();
        _ana.EsperarEstado(e => e.PropietarioDe(3) == 1);
        _ana.Cliente.TerminarTurno();
        _beto.EsperarEstado(e => e.Instantanea.IdJugadorEnTurno == 2);
        _beto.Cliente.TirarDados();
        _beto.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoPago && e.Instantanea.IdDeudor == 2);
    }

    [Fact]
    public void Estado_IndicaSiHayCajeroFisico()
    {
        UnirTres();

        _carla.Cliente.ConsultarEstado();
        Assert.True(_carla.EsperarEstado(e => true).CajeroConectado);
        Assert.True(_servidor.CajeroFisicoConectado);

        _servidor.UsarCajero(new CajeroSimulado());

        EsperarAvisoEnTodos("modo simulado");
        _carla.EsperarEstado(e => !e.CajeroConectado);
    }

    [Fact]
    public void Boton_TiraLosDadosDelJugadorEnTurnoYLosMuestraEnLaPico()
    {
        UnirTres();
        _cajero.Inyectar("BOTON");
        EsperarAvisoEnTodos("Botón del dado: la partida no está en curso");

        Iniciar();
        _cajero.Inyectar("BOTON");

        foreach (ClientePrueba cliente in new[] { _ana, _beto, _carla })
        {
            Mensaje dados = cliente.Esperar(Protocolo.Dados);
            Assert.Equal((1, 1, 2), (dados.Entero(0), dados.Entero(1), dados.Entero(2)));
            cliente.EsperarEstado(e => e.IdJugadorUltimoMovimiento == 1 && e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        }

        Assert.Contains("DADOS:1,2", _cajero.Enviadas());

        // Segunda pulsación en el mismo turno: se rechaza con las validaciones de siempre.
        _cajero.Inyectar("BOTON");
        EsperarAvisoEnTodos("Botón del dado: Ana ya lanzó los dados en este turno.");
        Assert.Equal(1, _ana.Contar(Protocolo.Dados));
    }

    [Fact]
    public void TirarDadosDesdeLaInterfaz_TambienSeMuestraEnLaPico()
    {
        UnirTres();
        Iniciar();

        _ana.Cliente.TirarDados();
        _ana.Esperar(Protocolo.Dados);

        Assert.Contains("DADOS:1,2", _cajero.Enviadas());
    }

    [Fact]
    public void VincularTarjeta_ValidacionesYUidNoRepetido()
    {
        UnirTres();

        _beto.Cliente.VincularTarjeta(2);
        _beto.EsperarError("Solo el organizador puede vincular tarjetas");
        _ana.Cliente.VincularTarjeta(9);
        _ana.EsperarError("No existe un jugador activo con id 9");

        Vincular(_ana, 2, TarjetaBeto);
        _carla.EsperarEstado(e => e.BuscarJugador(2)!.TieneTarjetaFisica);

        // La misma tarjeta no puede quedar asignada a otro jugador.
        Vincular(_ana, 3, TarjetaBeto);
        _ana.EsperarError($"La tarjeta {TarjetaBeto} ya está vinculada a Beto");
        EsperarAvisoEnTodos("ya está vinculada a Beto");

        Vincular(_ana, 3, TarjetaCarla);
        _ana.EsperarEstado(e => e.BuscarJugador(3)!.TieneTarjetaFisica && e.BuscarJugador(2)!.TieneTarjetaFisica);

        // Cancelar la espera: la siguiente tarjeta ya no se vincula.
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
        UnirTres(conCajero: false);

        _ana.Cliente.VincularTarjeta(2);

        _ana.EsperarError("No hay un cajero conectado");
    }

    [Fact]
    public void Pago_SoloConLaTarjetaDelDeudor()
    {
        UnirTres();
        Vincular(_ana, 2, TarjetaBeto);
        Vincular(_ana, 3, TarjetaCarla);
        _ana.EsperarEstado(e => e.BuscarJugador(3)!.TieneTarjetaFisica);
        LlevarABetoAPagarAlquiler();

        // Con cajero físico y tarjeta física, el pago simulado está deshabilitado.
        _beto.Cliente.PagarConTarjeta();
        _beto.EsperarError("pásela por el lector del cajero");

        _cajero.Inyectar("RFID:" + TarjetaCarla);
        EsperarAvisoEnTodos("La tarjeta pertenece a Carla; se espera la tarjeta de Beto.");
        _cajero.Inyectar("RFID:EEEEEEEE");
        EsperarAvisoEnTodos("La tarjeta EEEEEEEE no está registrada");
        Assert.Equal(2, ContarEnviadas("PAGO_RECHAZADO"));
        Assert.Equal(0, ContarEnviadas("PAGO_OK"));

        _cajero.Inyectar("RFID:" + TarjetaBeto);

        foreach (ClientePrueba cliente in new[] { _ana, _beto, _carla })
        {
            cliente.Esperar(Protocolo.Evento, m => m.Campo(0) == "Beto pagó $4 a Ana.");
            EstadoRed estado = cliente.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.PuedeTerminar);
            Assert.Equal((1444, 1496), (estado.BuscarJugador(1)!.Saldo, estado.BuscarJugador(2)!.Saldo));
        }

        _beto.Cliente.ConsultarTransacciones(FiltroTransacciones.Tipo, "PagoAlquiler");
        Assert.Equal(1, SerializadorTransacciones.Decodificar(_beto.Esperar(Protocolo.Transacciones)).Cantidad);
        Assert.Equal(1, ContarEnviadas("PAGO_OK"));

        // Fuera de un pago, la tarjeta solo informa de quién es y su saldo (y no toca el LED de pago).
        _cajero.Inyectar("RFID:" + TarjetaBeto);
        EsperarAvisoEnTodos("Tarjeta de Beto: saldo $1,496.");
        Assert.Equal(1, ContarEnviadas("PAGO_OK"));
        Assert.Equal(2, ContarEnviadas("PAGO_RECHAZADO"));
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

    [Fact]
    public void LineasBasura_NoAfectanLaPartida()
    {
        UnirTres();
        Iniciar();

        _cajero.Inyectar("MPY: soft reboot");
        _cajero.Inyectar(">>> ");
        _cajero.Inyectar("RFID:XYZ");
        _cajero.Inyectar("Traceback (most recent call last):");
        _cajero.Inyectar("ERROR:RC522 no responde");

        EsperarAvisoEnTodos("Error del módulo: RC522 no responde");
        Assert.Equal(4, _cajero.LineasIgnoradas);
        _ana.Cliente.ConsultarEstado();
        EstadoRed estado = _ana.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);
        Assert.Equal(FaseTurno.EsperandoDados, estado.Instantanea.Fase);
        Assert.Equal(0, _ana.Contar(Protocolo.Dados));
    }

    [Fact]
    public void Desconexion_VuelveAlModoSimuladoYPermiteReconectar()
    {
        UnirTres();
        Vincular(_ana, 2, TarjetaBeto);
        _ana.EsperarEstado(e => e.BuscarJugador(2)!.TieneTarjetaFisica);
        LlevarABetoAPagarAlquiler();

        _cajero.Desenchufar();

        EsperarAvisoEnTodos("Sin cajero físico: se juega en modo simulado");
        _beto.EsperarEstado(e => !e.CajeroConectado && e.Instantanea.Estado == EstadoPartida.EnCurso);
        Assert.False(_servidor.CajeroFisicoConectado);

        // Sin la Pico, Beto puede pagar con el botón simulado aunque tenga tarjeta física.
        _beto.Cliente.PagarConTarjeta();
        _beto.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.PuedeTerminar && e.BuscarJugador(2)!.Saldo == 1496);

        // Reconexión.
        _cajero.Conectar();
        EsperarAvisoEnTodos("Cajero falso de pruebas conectado");
        _carla.EsperarEstado(e => e.CajeroConectado);
    }

    public void Dispose()
    {
        _clientes.Recorrer(cliente => cliente.Dispose());
        _servidor.Dispose();
        _cajero.Dispose();
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, true);
        }
    }
}
