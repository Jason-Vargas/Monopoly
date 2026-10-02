using System;
using System.IO;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Tests.Modelo;
using Xunit;

namespace Monopoly.Tests.Logica;

/// <summary>
/// Base de las pruebas de partida: crea juegos con dados y cartas controlados, jugadores
/// Ana (1), Beto (2), Carla (3) y Dani (4), y una carpeta temporal para los TXT que se borra al final.
/// </summary>
public abstract class PartidaDePrueba : IDisposable
{
    protected const int Ana = 1;
    protected const int Beto = 2;
    protected const int Carla = 3;
    protected const int Dani = 4;

    private static readonly string[] Nombres = { "Ana", "Beto", "Carla", "Dani" };

    protected PartidaDePrueba()
    {
        Carpeta = Path.Combine(Path.GetTempPath(), "monopoly-pruebas-" + Guid.NewGuid().ToString("N"));
    }

    protected string Carpeta { get; }

    protected Juego Juego { get; private set; } = null!;

    /// <summary>
    /// Crea una partida. Si no se indican cartas, ambos mazos tienen una única carta sin efecto.
    /// </summary>
    protected Juego Crear(int jugadores, int[] dados, CartaEvento[]? casualidad = null, CartaEvento[]? arca = null,
        int saldo = Jugador.SaldoInicial, int maximoTurnos = 100, bool iniciar = true)
    {
        Juego = new Juego(new OpcionesJuego
        {
            Dado = Dado.ConValoresFijos(dados),
            Reloj = () => Fabrica.FechaFija,
            CartasCasualidad = casualidad ?? CartaNeutra(),
            CartasArcaComunal = arca ?? CartaNeutra(),
            SaldoInicial = saldo,
            MaximoTurnos = maximoTurnos,
            CarpetaPartidas = Carpeta,
        });

        for (int i = 0; i < jugadores; i++)
        {
            Assert.True(Juego.UnirJugador(Nombres[i]).Exito);
        }

        if (iniciar)
        {
            Assert.True(Juego.IniciarPartida(Ana).Exito);
        }

        return Juego;
    }

    protected static CartaEvento[] Carta(TipoCartaEvento tipo, int valor)
    {
        return new[] { new CartaEvento($"Carta de prueba {tipo} {valor}", tipo, valor) };
    }

    protected static string Uid(int idJugador) => Juego.PrefijoTarjetaVirtual + idJugador;

    /// <summary>
    /// Compra completa: el jugador elige "Comprar" y luego acerca su propia tarjeta.
    /// </summary>
    protected void Comprar(int idJugador)
    {
        Ok(Juego.SolicitarCompra(idJugador));
        Assert.Equal(FaseTurno.EsperandoTarjetaCompra, Juego.Fase);
        ResultadoAccion compra = Ok(Juego.IdentificarTarjeta(J(idJugador).UidTarjeta!));
        Assert.True(compra.PagoAceptado);
    }

    protected InstantaneaJuego Estado() => Juego.ConsultarEstado(Ana).Instantanea!;

    protected Jugador J(int id) => Juego.ObtenerJugador(id)!;

    protected int Saldo(int id) => J(id).Saldo;

    protected int Posicion(int id) => J(id).CasillaActual!.Id;

    protected int EnTurno() => Estado().IdJugadorEnTurno!.Value;

    protected Transaccion UltimaTransaccion()
    {
        Transaccion? ultima = null;
        Juego.Historial.RecorrerDesdeMasReciente(t => ultima ??= t);
        return ultima!;
    }

    protected string Eventos()
    {
        string texto = "";
        foreach (EventoJuego evento in Juego.ObtenerEventosDesde(1))
        {
            texto += evento.Texto + "\n";
        }

        return texto;
    }

    /// <summary>
    /// Ejecuta una acción que debe tener éxito.
    /// </summary>
    protected static ResultadoAccion Ok(ResultadoAccion resultado)
    {
        Assert.True(resultado.Exito, resultado.Mensaje);
        return resultado;
    }

    /// <summary>
    /// Ejecuta una acción que debe ser rechazada con un mensaje que contenga el texto indicado.
    /// </summary>
    protected static void Rechazada(ResultadoAccion resultado, string textoEsperado)
    {
        Assert.False(resultado.Exito, "Se esperaba un rechazo, pero fue aceptada: " + resultado.Mensaje);
        Assert.Contains(textoEsperado, resultado.Mensaje);
    }

    public void Dispose()
    {
        if (Directory.Exists(Carpeta))
        {
            Directory.Delete(Carpeta, true);
        }

        GC.SuppressFinalize(this);
    }

    private static CartaEvento[] CartaNeutra()
    {
        return new[] { new CartaEvento("Carta sin efecto", TipoCartaEvento.RecibirDinero, 0) };
    }
}
