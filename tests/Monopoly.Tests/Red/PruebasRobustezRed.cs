using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;
using Monopoly.Tests.Modelo;
using Xunit;

namespace Monopoly.Tests.Red;

/// <summary>
/// Robustez de la red: acciones simultáneas, desconexión a mitad de partida, cierre del organizador,
/// errores de conexión y clientes defectuosos.
/// </summary>
public class PruebasRobustezRed : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "monopoly-robustez-" + Guid.NewGuid().ToString("N"));
    private readonly Servidor _servidor;
    private readonly ListaSimple<ClientePrueba> _clientes = new ListaSimple<ClientePrueba>();

    public PruebasRobustezRed()
    {
        CartaEvento[] neutra = { new CartaEvento("Carta sin efecto", TipoCartaEvento.RecibirDinero, 0) };
        Juego juego = new Juego(new OpcionesJuego
        {
            Dado = Dado.ConValoresFijos(1, 2, 3, 3, 2, 2, 1, 1),
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
        _clientes.AgregarAlFinal(cliente);
        return cliente;
    }

    private ClientePrueba[] UnirEIniciar(params string[] nombres)
    {
        ClientePrueba[] clientes = new ClientePrueba[nombres.Length];
        for (int i = 0; i < nombres.Length; i++)
        {
            clientes[i] = Nuevo(nombres[i]);
            clientes[i].Unirse();
        }

        clientes[0].Cliente.IniciarPartida();
        foreach (ClientePrueba cliente in clientes)
        {
            cliente.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);
        }

        return clientes;
    }

    [Fact]
    public void AccionesSimultaneas_SoloSeAceptaUnaTiradaYElServidorSigueCoherente()
    {
        ClientePrueba[] c = UnirEIniciar("Ana", "Beto", "Carla", "Dani");
        const int intentosPorCliente = 5;
        Thread[] hilos = new Thread[c.Length];
        using ManualResetEventSlim salida = new ManualResetEventSlim(false);

        for (int i = 0; i < c.Length; i++)
        {
            ClientePrueba cliente = c[i];
            hilos[i] = new Thread(() =>
            {
                salida.Wait();
                for (int k = 0; k < intentosPorCliente; k++)
                {
                    cliente.Cliente.TirarDados();
                    cliente.Cliente.ComprarPropiedad();
                }
            });
            hilos[i].Start();
        }

        salida.Set();
        foreach (Thread hilo in hilos)
        {
            hilo.Join();
        }

        // Cada cliente recibe una respuesta por solicitud: Ana, 1 tirada aceptada + 1 compra aceptada
        // y 8 rechazos; los demás, 10 rechazos por estar fuera de turno.
        for (int k = 0; k < (2 * intentosPorCliente) - 2; k++)
        {
            c[0].Esperar(Protocolo.Error);
        }

        for (int i = 1; i < c.Length; i++)
        {
            for (int k = 0; k < 2 * intentosPorCliente; k++)
            {
                c[i].EsperarError("No es el turno");
            }
        }

        c[0].Cliente.ConsultarEstado();
        EstadoRed estado = c[0].EsperarEstado(e => e.PropietarioDe(3) == 1);
        Assert.Equal(1440, estado.BuscarJugador(1)!.Saldo);
        Assert.Equal(3, estado.BuscarJugador(1)!.Posicion);
        foreach (ClientePrueba cliente in c)
        {
            Assert.Equal(1, cliente.Contar(Protocolo.Dados));
        }

        Assert.True(_servidor.Activo);
        Assert.Equal(4, _servidor.CantidadConexiones);
    }

    [Fact]
    public void DesconexionEnSuTurno_TodosLoVenYElOrganizadorPuedeRetirarlo()
    {
        ClientePrueba[] c = UnirEIniciar("Ana", "Beto", "Carla");
        c[0].Cliente.TirarDados();
        c[0].EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        c[0].Cliente.NoComprar();
        c[0].Cliente.TerminarTurno();
        c[2].EsperarEstado(e => e.Instantanea.IdJugadorEnTurno == 2);

        // Beto se cae en su turno.
        c[1].Cliente.Desconectar();

        c[2].Esperar(Protocolo.Evento, m => m.Campo(0).Contains("Beto se desconectó", StringComparison.Ordinal)
                                          && m.Campo(0).Contains("Es su turno", StringComparison.Ordinal));
        EstadoRed conBetoDesconectado = c[2].EsperarEstado(e => !e.EstaConectado(2));
        Assert.Equal(new[] { 2 }, conBetoDesconectado.IdsDesconectados);
        Assert.Equal(2, conBetoDesconectado.Instantanea.IdJugadorEnTurno);

        // Validaciones del retiro.
        c[2].Cliente.RetirarJugador(2);
        c[2].EsperarError("Solo el organizador (Ana)");
        c[0].Cliente.RetirarJugador(3);
        c[0].EsperarError("Carla está conectado");
        c[0].Cliente.RetirarJugador(9);
        c[0].EsperarError("No existe el jugador 9");

        c[0].Cliente.RetirarJugador(2);

        foreach (ClientePrueba cliente in new[] { c[0], c[2] })
        {
            cliente.Esperar(Protocolo.Evento, m => m.Campo(0) == "Ana retiró a Beto de la partida.");
            cliente.EsperarEstado(e => !e.BuscarJugador(2)!.Activo && e.Instantanea.IdJugadorEnTurno == 3);
        }

        // La partida sigue con Carla.
        c[2].Cliente.TirarDados();
        c[2].EsperarEstado(e => e.IdJugadorUltimoMovimiento == 3);
    }

    [Fact]
    public void CierreDelOrganizador_TodosLosClientesRecibenElMotivo()
    {
        ClientePrueba[] c = UnirEIniciar("Ana", "Beto");

        _servidor.Detener("El organizador cerró la partida.");

        foreach (ClientePrueba cliente in c)
        {
            cliente.Esperar(Protocolo.ServidorCerrado);
            Assert.Equal("El organizador cerró la partida.", cliente.EsperarDesconexion());
            Assert.False(cliente.Cliente.Conectado);
        }

        Assert.False(_servidor.Activo);
        Assert.Throws<SocketException>(() => new Cliente().Conectar("127.0.0.1", _servidor.Puerto, 10000));
    }

    [Fact]
    public void ErrorDeConexion_PuertoCerrado_MensajeClaro()
    {
        TcpListener temporal = new TcpListener(IPAddress.Loopback, 0);
        temporal.Start();
        int puertoLibre = ((IPEndPoint)temporal.LocalEndpoint).Port;
        temporal.Stop();

        SocketException error = Assert.Throws<SocketException>(() => new Cliente().Conectar("127.0.0.1", puertoLibre, 10000));

        Assert.Equal(SocketError.ConnectionRefused, error.SocketErrorCode);
        Assert.Contains("No hay ninguna partida abierta", Cliente.DescribirErrorConexion(error, "127.0.0.1", puertoLibre));
    }

    [Fact]
    public void ErrorDeConexion_MensajesClarosPorTipo()
    {
        Assert.Contains("No hubo respuesta de 10.0.0.9:5000",
            Cliente.DescribirErrorConexion(new TimeoutException(), "10.0.0.9", 5000));
        Assert.Contains("Firewall", Cliente.DescribirErrorConexion(new SocketException((int)SocketError.HostUnreachable), "10.0.0.9", 5000));
        Assert.Contains("No se encontró la computadora 'servidr'",
            Cliente.DescribirErrorConexion(new SocketException((int)SocketError.HostNotFound), "servidr", 5000));
        Assert.Contains("no es válida", Cliente.DescribirErrorConexion(new ArgumentException(), "1.2.3", 5000));
    }

    [Fact]
    public void RechazosAlUnirse_MensajesClaros()
    {
        ClientePrueba[] c = new ClientePrueba[4];
        string[] nombres = { "Ana", "Beto", "Carla", "Dani" };
        for (int i = 0; i < 4; i++)
        {
            c[i] = Nuevo(nombres[i]);
            c[i].Unirse();
        }

        ClientePrueba quinto = Nuevo("Eva");
        quinto.Cliente.Unirse("Eva");
        quinto.EsperarError("La partida está llena");

        ClientePrueba repetido = Nuevo("x");
        repetido.Cliente.Unirse("Ana");
        repetido.EsperarError("Ana ya está conectado");

        c[0].Cliente.IniciarPartida();
        c[0].EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);
        ClientePrueba tarde = Nuevo("Fer");
        tarde.Cliente.Unirse("Fer");
        tarde.EsperarError("escriba exactamente el mismo nombre para reconectarse");
    }

    [Fact]
    public void LineaDemasiadoLarga_CierraSoloEsaConexion()
    {
        ClientePrueba ana = Nuevo("Ana");
        ana.Unirse();

        using TcpClient crudo = new TcpClient("127.0.0.1", _servidor.Puerto);
        NetworkStream flujo = crudo.GetStream();
        byte[] basura = new byte[ConexionCliente.LongitudMaximaLinea + 100];
        for (int i = 0; i < basura.Length; i++)
        {
            basura[i] = (byte)'A';
        }

        flujo.Write(basura, 0, basura.Length);
        flujo.ReadTimeout = 5000;
        int leidos;
        try
        {
            leidos = flujo.Read(new byte[16], 0, 16);
        }
        catch (IOException)
        {
            leidos = 0;
        }

        Assert.Equal(0, leidos);
        ana.Cliente.ConsultarEstado();
        ana.EsperarEstado(e => e.Instantanea.Jugadores.Length == 1);
        Assert.True(_servidor.Activo);
    }

    public void Dispose()
    {
        _clientes.Recorrer(cliente => cliente.Dispose());
        _servidor.Dispose();
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, true);
        }
    }
}
