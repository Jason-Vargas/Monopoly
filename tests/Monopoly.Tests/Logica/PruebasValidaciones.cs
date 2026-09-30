using System.Threading;
using Monopoly.Core.Logica;
using Xunit;

namespace Monopoly.Tests.Logica;

/// <summary>
/// Cada validación obligatoria del servidor debe rechazar la acción sin modificar el estado.
/// </summary>
public class PruebasValidaciones : PartidaDePrueba
{
    [Fact]
    public void AntesDeIniciar_NoSePuedeJugar()
    {
        Crear(2, new[] { 1, 2 }, iniciar: false);

        Rechazada(Juego.TirarDados(Ana), "aún no ha comenzado");
        Rechazada(Juego.ComprarPropiedad(Ana), "aún no ha comenzado");
        Rechazada(Juego.TerminarTurno(Ana), "aún no ha comenzado");
        Rechazada(Juego.IdentificarTarjeta(Uid(Ana)), "no está en curso");
        Assert.Equal(EstadoPartida.EsperandoJugadores, Juego.Estado);
    }

    [Fact]
    public void UnirJugador_Validaciones()
    {
        Crear(0, new[] { 1, 2 }, iniciar: false);

        Rechazada(Juego.UnirJugador("  "), "vacío");
        Rechazada(Juego.UnirJugador("banco"), "reservado");
        ResultadoAccion ana = Ok(Juego.UnirJugador(" Ana "));
        Assert.Equal((1, "VIRTUAL-1"), (ana.IdJugador, ana.UidTarjeta));
        Rechazada(Juego.UnirJugador("ANA"), "Ya existe un jugador llamado ANA");
        Ok(Juego.UnirJugador("Beto"));
        Ok(Juego.UnirJugador("Carla"));
        Ok(Juego.UnirJugador("Dani"));
        Rechazada(Juego.UnirJugador("Eva"), "máximo de 4");

        Ok(Juego.IniciarPartida(Ana));
        Rechazada(Juego.UnirJugador("Eva"), "ya comenzó");
    }

    [Fact]
    public void IniciarPartida_Validaciones()
    {
        Crear(1, new[] { 1, 2 }, iniciar: false);
        Rechazada(Juego.IniciarPartida(Ana), "al menos 2 jugadores");
        Ok(Juego.UnirJugador("Beto"));

        Rechazada(Juego.IniciarPartida(Beto), "Solo el organizador (Ana)");
        Rechazada(Juego.IniciarPartida(7), "No existe el jugador 7");
        Ok(Juego.IniciarPartida(Ana));
        Rechazada(Juego.IniciarPartida(Ana), "ya fue iniciada");
    }

    [Fact]
    public void FueraDeTurno_SeRechaza()
    {
        Crear(3, new[] { 1, 2 });

        Rechazada(Juego.TirarDados(Beto), "No es el turno de Beto; le toca a Ana");
        Rechazada(Juego.ComprarPropiedad(Carla), "No es el turno de Carla");
        Rechazada(Juego.NoComprar(Beto), "No es el turno");
        Rechazada(Juego.TerminarTurno(Beto), "No es el turno");
        Rechazada(Juego.TirarDados(9), "No existe el jugador 9");

        Assert.Equal(0, Posicion(Beto));
        Assert.Null(Juego.Dado.UltimaTirada);
    }

    [Fact]
    public void TirarDadosDosVeces_SeRechaza()
    {
        Crear(2, new[] { 3, 3, 1, 1 });
        Ok(Juego.TirarDados(Ana));

        Rechazada(Juego.TirarDados(Ana), "Ana ya lanzó los dados en este turno");

        Assert.Equal(6, Posicion(Ana));
    }

    [Fact]
    public void TerminarSinTirar_SeRechaza()
    {
        Crear(2, new[] { 1, 2 });

        Rechazada(Juego.TerminarTurno(Ana), "Debe lanzar los dados");
        Assert.Equal(Ana, EnTurno());
    }

    [Fact]
    public void TerminarConDecisionDeCompraPendiente_SeRechaza()
    {
        Crear(2, new[] { 1, 2 });
        Ok(Juego.TirarDados(Ana));

        Rechazada(Juego.TerminarTurno(Ana), "Debe decidir si compra Avenida Báltica");

        Ok(Juego.NoComprar(Ana));
        Assert.True(Juego.Tablero.BuscarPropiedad(3)!.EstaDisponible);
        Ok(Juego.TerminarTurno(Ana));
    }

    [Fact]
    public void ComprarSinSaldoSuficiente_SeRechaza()
    {
        Crear(2, new[] { 5, 6 }, saldo: 100);
        Ok(Juego.TirarDados(Ana));

        Rechazada(Juego.ComprarPropiedad(Ana), "Saldo insuficiente: Plaza San Carlos cuesta $140 y Ana tiene $100");

        Assert.Equal(100, Saldo(Ana));
        Assert.Equal(0, Juego.Historial.Cantidad);
        Assert.Equal(FaseTurno.EsperandoDecisionCompra, Estado().Fase);
        Ok(Juego.NoComprar(Ana));
    }

    [Fact]
    public void ComprarPropiedadConDueno_SeRechaza()
    {
        Crear(2, new[] { 1, 2 });
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.ComprarPropiedad(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));

        Rechazada(Juego.ComprarPropiedad(Beto), "Avenida Báltica ya tiene propietario (Ana)");

        Assert.Same(J(Ana), Juego.Tablero.BuscarPropiedad(3)!.Propietario);
        Assert.Equal(1500, Saldo(Beto));
    }

    [Fact]
    public void ComprarAntesDeTirarOEnCasillaNoComprable_SeRechaza()
    {
        Crear(2, new[] { 2, 2 });
        Rechazada(Juego.ComprarPropiedad(Ana), "Debe lanzar los dados");
        Ok(Juego.TirarDados(Ana));

        Rechazada(Juego.ComprarPropiedad(Ana), "no es una propiedad");
        Rechazada(Juego.NoComprar(Ana), "No hay ninguna compra pendiente");
    }

    [Fact]
    public void ComprarDosVeces_SeRechaza()
    {
        Crear(2, new[] { 1, 2 });
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.ComprarPropiedad(Ana));

        Rechazada(Juego.ComprarPropiedad(Ana), "ya tiene propietario");
        Assert.Equal(1440, Saldo(Ana));
    }

    [Fact]
    public void IdentificarTarjetaSinPagoPendiente_SeRechaza()
    {
        Crear(2, new[] { 1, 2 });

        Rechazada(Juego.IdentificarTarjeta(Uid(Ana)), "no hay ningún pago pendiente");
    }

    [Fact]
    public void VincularTarjeta_NoPermiteUidRepetido()
    {
        Crear(2, new[] { 1, 2 }, iniciar: false);
        Ok(Juego.VincularTarjeta(Ana, "AB CD EF 01"));

        Rechazada(Juego.VincularTarjeta(Beto, "abcdef01"), "ya está vinculada a Ana");
        Rechazada(Juego.VincularTarjeta(Beto, " "), "vacío");
        Rechazada(Juego.VincularTarjeta(5, "1234"), "No existe el jugador 5");
        Ok(Juego.VincularTarjeta(Ana, "ABCDEF01"));
        Ok(Juego.VincularTarjeta(Beto, "99887766"));
    }

    [Fact]
    public void ConsultarEstado_JugadorInexistente_SeRechaza()
    {
        Crear(2, new[] { 1, 2 });

        Rechazada(Juego.ConsultarEstado(42), "No existe el jugador 42");
    }

    [Fact]
    public void UnirJugador_DesdeVariosHilos_SoloEntranCuatro()
    {
        Crear(0, new[] { 1, 2 }, iniciar: false);
        ResultadoAccion[] resultados = new ResultadoAccion[12];
        Thread[] hilos = new Thread[resultados.Length];
        using ManualResetEventSlim salida = new ManualResetEventSlim(false);

        for (int i = 0; i < hilos.Length; i++)
        {
            int indice = i;
            hilos[i] = new Thread(() =>
            {
                salida.Wait();
                resultados[indice] = Juego.UnirJugador("Jugador" + indice);
            });
            hilos[i].Start();
        }

        salida.Set();
        foreach (Thread hilo in hilos)
        {
            hilo.Join();
        }

        bool[] idsVistos = new bool[5];
        int exitos = 0;
        foreach (ResultadoAccion resultado in resultados)
        {
            if (resultado.Exito)
            {
                exitos++;
                Assert.False(idsVistos[resultado.IdJugador!.Value]);
                idsVistos[resultado.IdJugador.Value] = true;
            }
        }

        Assert.Equal(4, exitos);
        Assert.Equal(4, Juego.ConsultarEstado(1).Instantanea!.Jugadores.Length);
    }
}
