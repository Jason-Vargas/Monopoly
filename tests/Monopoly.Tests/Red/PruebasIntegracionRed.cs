using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;
using Monopoly.Tests.Modelo;
using Xunit;

namespace Monopoly.Tests.Red;

/// <summary>
/// Pruebas de integración: servidor real en un puerto libre de loopback y 4 clientes TCP reales.
/// Dados: Ana 1+2 (Báltica), Beto 3+3 (Oriental), Carla 2+2 (Impuesto sobre la Renta), Dani 1+1 (Arca Comunal).
/// </summary>
public class PruebasIntegracionRed : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "monopoly-red-" + Guid.NewGuid().ToString("N"));
    private readonly Servidor _servidor;
    private readonly ListaSimple<ClientePrueba> _clientes = new ListaSimple<ClientePrueba>();

    public PruebasIntegracionRed()
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

    private ClientePrueba[] ConectarCuatro()
    {
        string[] nombres = { "Ana", "Beto", "Carla", "Dani" };
        ClientePrueba[] clientes = new ClientePrueba[4];
        for (int i = 0; i < 4; i++)
        {
            clientes[i] = Nuevo(nombres[i]);
            Assert.Equal(i + 1, clientes[i].Unirse());
        }

        foreach (ClientePrueba cliente in clientes)
        {
            cliente.EsperarEstado(e => e.Instantanea.Jugadores.Length == 4);
        }

        return clientes;
    }

    private static void TodosEsperan(ClientePrueba[] clientes, Predicate<EstadoRed> condicion)
    {
        foreach (ClientePrueba cliente in clientes)
        {
            cliente.EsperarEstado(condicion);
        }
    }

    private static void IniciarYVerificarTurno(ClientePrueba[] c)
    {
        c[0].Cliente.IniciarPartida();
        TodosEsperan(c, e => e.Instantanea.Estado == EstadoPartida.EnCurso && e.Instantanea.IdJugadorEnTurno == 1);
    }

    [Fact]
    public void CuatroClientes_RecibenBienvenidaYEstado()
    {
        ClientePrueba[] c = ConectarCuatro();

        Assert.Equal(4, _servidor.CantidadConexiones);
        c[3].Cliente.ConsultarEstado();
        EstadoRed estado = c[3].EsperarEstado(e => true);
        Assert.Equal(EstadoPartida.EsperandoJugadores, estado.Instantanea.Estado);
        Assert.Equal("Dani", estado.BuscarJugador(4)!.Nombre);
        Assert.Equal(4, c[3].Cliente.IdJugador);
    }

    [Fact]
    public void Turnos_OrdenCompraDadosUnaVezYEstadoParaTodos()
    {
        ClientePrueba[] c = ConectarCuatro();
        IniciarYVerificarTurno(c);

        // Fuera de turno.
        c[1].Cliente.TirarDados();
        c[1].EsperarError("No es el turno de Beto; le toca a Ana");
        c[2].Cliente.ComprarPropiedad();
        c[2].EsperarError("No es el turno de Carla");

        // Ana tira: todos reciben DADOS y el ESTADO con el recorrido.
        c[0].Cliente.TirarDados();
        foreach (ClientePrueba cliente in c)
        {
            Mensaje dados = cliente.Esperar(Protocolo.Dados);
            Assert.Equal((1, 1, 2), (dados.Entero(0), dados.Entero(1), dados.Entero(2)));
        }

        TodosEsperan(c, e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra
            && e.IdJugadorUltimoMovimiento == 1
            && string.Join(",", e.CasillasRecorridas) == "1,2,3"
            && e.Instantanea.IdPropiedadEnVenta == 3);

        // Dados una sola vez por turno.
        c[0].Cliente.TirarDados();
        c[0].EsperarError("ya lanzó los dados");

        // Compra: todos ven el nuevo dueño y el saldo.
        c[0].Cliente.ComprarPropiedad();
        foreach (ClientePrueba cliente in c)
        {
            cliente.Esperar(Protocolo.Evento, m => m.Campo(0) == "Ana compró Avenida Báltica por $60.");
        }

        TodosEsperan(c, e => e.PropietarioDe(3) == 1 && e.BuscarJugador(1)!.Saldo == 1440);

        c[0].Cliente.TerminarTurno();
        TodosEsperan(c, e => e.Instantanea.IdJugadorEnTurno == 2 && e.Instantanea.NumeroTurno == 2);

        c[1].Cliente.TirarDados();
        c[1].EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        c[1].Cliente.TerminarTurno();
        c[1].EsperarError("Debe decidir");
        c[1].Cliente.NoComprar();
        c[1].Cliente.TerminarTurno();
        TodosEsperan(c, e => e.Instantanea.IdJugadorEnTurno == 3);

        // Carla cae en el impuesto: solo su tarjeta paga.
        c[2].Cliente.TirarDados();
        TodosEsperan(c, e => e.Instantanea.Fase == FaseTurno.EsperandoPago && e.Instantanea.IdDeudor == 3);
        c[3].Cliente.PagarConTarjeta();
        c[3].EsperarError("La tarjeta pertenece a Dani; se espera la tarjeta de Carla");
        c[2].Cliente.PagarConTarjeta();
        TodosEsperan(c, e => e.Instantanea.Fase == FaseTurno.PuedeTerminar && e.BuscarJugador(3)!.Saldo == 1300);
        c[2].Cliente.TerminarTurno();
        TodosEsperan(c, e => e.Instantanea.IdJugadorEnTurno == 4);

        c[3].Cliente.TirarDados();
        c[3].EsperarEstado(e => e.Instantanea.Fase == FaseTurno.PuedeTerminar);
        c[3].Cliente.TerminarTurno();
        TodosEsperan(c, e => e.Instantanea.IdJugadorEnTurno == 1 && e.Instantanea.NumeroTurno == 5);
    }

    [Fact]
    public void Solicitudes_SinConectarOInvalidas_ResponderError()
    {
        ClientePrueba anonimo = Nuevo("Anónimo");
        anonimo.Cliente.TirarDados();
        anonimo.EsperarError("Debe enviar CONECTAR|nombre");

        anonimo.Unirse();
        anonimo.Cliente.Unirse("Otro");
        anonimo.EsperarError("ya está asociada a un jugador");
        anonimo.Cliente.EnviarLinea("VOLAR|alto");
        anonimo.EsperarError("Comando desconocido: VOLAR");
        anonimo.Cliente.EnviarLinea("|");
        anonimo.EsperarError("Mensaje mal formado");
        anonimo.Cliente.Enviar(Protocolo.Conectar);
        anonimo.EsperarError("ya está asociada");

        ClientePrueba repetido = Nuevo("anónimo");
        repetido.Cliente.Unirse("ANÓNIMO");
        repetido.EsperarError("Anónimo ya está conectado");
    }

    [Fact]
    public void IniciarPartida_SoloElOrganizador()
    {
        ClientePrueba[] c = ConectarCuatro();

        c[2].Cliente.IniciarPartida();
        c[2].EsperarError("Solo el organizador (Ana)");

        IniciarYVerificarTurno(c);
        ClientePrueba tarde = Nuevo("Eva");
        tarde.Cliente.Unirse("Eva");
        tarde.EsperarError("ya comenzó");
    }

    [Fact]
    public void ConsultarTransacciones_ConFiltros()
    {
        ClientePrueba[] c = ConectarCuatro();
        IniciarYVerificarTurno(c);
        c[0].Cliente.TirarDados();
        c[0].EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        c[0].Cliente.ComprarPropiedad();
        c[0].EsperarEstado(e => e.PropietarioDe(3) == 1);

        c[1].Cliente.ConsultarTransacciones(FiltroTransacciones.Jugador, "Ana");
        Mensaje porJugador = c[1].Esperar(Protocolo.Transacciones);
        ListaSimple<Transaccion> lista = SerializadorTransacciones.Decodificar(porJugador);
        Assert.Equal("JUGADOR Ana", porJugador.Campo(0));
        Assert.Equal(1, lista.Cantidad);
        Assert.Equal((TipoTransaccion.CompraPropiedad, "Ana", Transaccion.Banco, 60),
            (lista.Obtener(0).Tipo, lista.Obtener(0).Origen, lista.Obtener(0).Destino, lista.Obtener(0).Monto));

        c[1].Cliente.ConsultarTransacciones(FiltroTransacciones.Tipo, "PagoAlquiler");
        Assert.Equal(0, SerializadorTransacciones.Decodificar(c[1].Esperar(Protocolo.Transacciones)).Cantidad);

        c[1].Cliente.ConsultarTransacciones(FiltroTransacciones.Recientes);
        Assert.Equal(1, SerializadorTransacciones.Decodificar(c[1].Esperar(Protocolo.Transacciones)).Cantidad);

        c[1].Cliente.ConsultarTransacciones(FiltroTransacciones.Tipo, "Robo");
        c[1].EsperarError("Tipo de transacción desconocido: Robo");
        c[1].Cliente.EnviarLinea("CONSULTAR_TRANSACCIONES|PRIMERAS");
        c[1].EsperarError("Filtro desconocido");
        c[1].Cliente.ConsultarTransacciones(FiltroTransacciones.Jugador);
        c[1].EsperarError("requiere un valor");
    }

    [Fact]
    public void ExportarTransacciones_GeneraElTxtYAvisaATodos()
    {
        ClientePrueba[] c = ConectarCuatro();

        c[2].Cliente.ExportarTransacciones();

        foreach (ClientePrueba cliente in c)
        {
            cliente.Esperar(Protocolo.Evento, m => m.Campo(0).StartsWith("Historial exportado a ", StringComparison.Ordinal));
        }

        Assert.Single(Directory.GetFiles(_carpeta, "partida_*.txt"));
    }

    [Fact]
    public void Desconexion_ElServidorSigueYElJugadorPuedeReconectarse()
    {
        ClientePrueba[] c = ConectarCuatro();
        IniciarYVerificarTurno(c);

        c[1].Cliente.Desconectar();

        c[0].Esperar(Protocolo.Evento, m => m.Campo(0).StartsWith("Beto se desconectó", StringComparison.Ordinal));
        c[2].Esperar(Protocolo.Evento, m => m.Campo(0).StartsWith("Beto se desconectó", StringComparison.Ordinal));

        // La partida sigue: Ana juega su turno normalmente.
        c[0].Cliente.TirarDados();
        c[0].EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);

        ClientePrueba beto = Nuevo("Beto");
        Assert.Equal(2, beto.Unirse());
        beto.EsperarEstado(e => e.BuscarJugador(2)!.Activo && e.Instantanea.Estado == EstadoPartida.EnCurso);
        c[0].Esperar(Protocolo.Evento, m => m.Campo(0) == "Beto se reconectó.");

        ClientePrueba impostor = Nuevo("Ana");
        impostor.Cliente.Unirse("Ana");
        impostor.EsperarError("Ana ya está conectado");
    }

    [Fact]
    public void CaidaAbruptaDelCliente_NoDerribaAlServidor()
    {
        ClientePrueba ana = Nuevo("Ana");
        ClientePrueba beto = Nuevo("Beto");
        ana.Unirse();
        beto.Unirse();

        // Un cliente "crudo" que se une y cierra el socket sin enviar DESCONECTAR.
        using (TcpClient crudo = new TcpClient("127.0.0.1", _servidor.Puerto))
        {
            StreamWriter escritor = new StreamWriter(crudo.GetStream(), Protocolo.Codificacion) { NewLine = "\n", AutoFlush = true };
            escritor.WriteLine("CONECTAR|Dani");
            ana.Esperar(Protocolo.Evento, m => m.Campo(0).StartsWith("Dani se unió", StringComparison.Ordinal));
        }

        ana.Esperar(Protocolo.Evento, m => m.Campo(0).StartsWith("Dani se desconectó", StringComparison.Ordinal));
        beto.Cliente.ConsultarEstado();
        beto.EsperarEstado(e => e.Instantanea.Jugadores.Length == 3);
        Assert.True(_servidor.Activo);
        Assert.Equal(2, _servidor.CantidadConexiones);

        // Dani vuelve con un cliente normal y recupera su id.
        ClientePrueba dani = Nuevo("Dani");
        Assert.Equal(3, dani.Unirse());
    }

    [Fact]
    public void Fin_SeDifundeATodos()
    {
        _servidor.Dispose();
        CartaEvento[] neutra = { new CartaEvento("Carta sin efecto", TipoCartaEvento.RecibirDinero, 0) };
        Juego juego = new Juego(new OpcionesJuego
        {
            Dado = Dado.ConValoresFijos(1, 2, 3, 3),
            Reloj = () => Fabrica.FechaFija,
            CartasCasualidad = neutra,
            CartasArcaComunal = neutra,
            CarpetaPartidas = _carpeta,
            MaximoTurnos = 1,
        });
        using Servidor servidor = new Servidor(juego, 0, IPAddress.Loopback);
        servidor.Iniciar();
        using ClientePrueba ana = new ClientePrueba(servidor.Puerto, "Ana");
        using ClientePrueba beto = new ClientePrueba(servidor.Puerto, "Beto");
        ana.Unirse();
        beto.Unirse();

        ana.Cliente.IniciarPartida();
        ana.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.EnCurso);
        ana.Cliente.TirarDados();
        ana.EsperarEstado(e => e.Instantanea.Fase == FaseTurno.EsperandoDecisionCompra);
        ana.Cliente.ComprarPropiedad();
        ana.EsperarEstado(e => e.PropietarioDe(3) == 1);
        ana.Cliente.TerminarTurno();

        // Empate de patrimonio (Ana 1440 + 60, Beto 1500): gana la primera en la cola de turnos.
        foreach (ClientePrueba cliente in new[] { ana, beto })
        {
            cliente.EsperarEstado(e => e.Instantanea.Estado == EstadoPartida.Finalizada && e.Instantanea.IdGanador == 1);
            Mensaje fin = cliente.Esperar(Protocolo.Fin);
            Assert.Equal("Ana", fin.Campo(0));
            Assert.Contains("Ana: patrimonio $1,500; Beto: patrimonio $1,500", fin.Campo(1));
            Assert.Contains("Historial: ", fin.Campo(1));
        }
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
