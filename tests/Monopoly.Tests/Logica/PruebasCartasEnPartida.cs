using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Xunit;

namespace Monopoly.Tests.Logica;

/// <summary>
/// Cada tipo de carta aplicado dentro de una partida. En todas, Ana saca 3 + 4 y cae en Casualidad (7).
/// </summary>
public class PruebasCartasEnPartida : PartidaDePrueba
{
    private ResultadoAccion SacarCarta(TipoCartaEvento tipo, int valor, int jugadores = 2)
    {
        Crear(jugadores, new[] { 3, 4 }, casualidad: Carta(tipo, valor));
        ResultadoAccion resultado = Ok(Juego.TirarDados(Ana));
        Assert.Contains("Carta de prueba", Eventos());
        return resultado;
    }

    [Fact]
    public void RecibirDinero()
    {
        SacarCarta(TipoCartaEvento.RecibirDinero, 50);

        Assert.Equal(1550, Saldo(Ana));
        Transaccion t = UltimaTransaccion();
        Assert.Equal((TipoTransaccion.GananciaEvento, Transaccion.Banco, "Ana", 50), (t.Tipo, t.Origen, t.Destino, t.Monto));
        Assert.Equal(FaseTurno.PuedeTerminar, Estado().Fase);
    }

    [Fact]
    public void PagarDinero_EsperaLaTarjeta()
    {
        SacarCarta(TipoCartaEvento.PagarDinero, 15);
        Assert.Equal(FaseTurno.EsperandoPago, Estado().Fase);
        Assert.Equal(1500, Saldo(Ana));

        Ok(Juego.IdentificarTarjeta(Uid(Ana)));

        Assert.Equal(1485, Saldo(Ana));
        Transaccion t = UltimaTransaccion();
        Assert.Equal((TipoTransaccion.PerdidaEvento, "Ana", Transaccion.Banco, 15), (t.Tipo, t.Origen, t.Destino, t.Monto));
    }

    [Fact]
    public void Avanzar_AplicaLaCasillaDeLlegada()
    {
        ResultadoAccion resultado = SacarCarta(TipoCartaEvento.Avanzar, 4);

        Assert.Equal(11, Posicion(Ana));
        Assert.Equal(2, resultado.Movimientos.Cantidad);
        Assert.Equal(FaseTurno.EsperandoDecisionCompra, Estado().Fase);
        Assert.Equal(11, Estado().IdPropiedadEnVenta);
        Ok(Juego.ComprarPropiedad(Ana));
        Assert.Equal(1360, Saldo(Ana));
    }

    [Fact]
    public void Retroceder_AplicaLaCasillaDeLlegada()
    {
        SacarCarta(TipoCartaEvento.Retroceder, 3);

        Assert.Equal(4, Posicion(Ana));
        Assert.Equal(FaseTurno.EsperandoPago, Estado().Fase);
        Assert.Equal(200, Estado().MontoPagoPendiente);

        Ok(Juego.IdentificarTarjeta(Uid(Ana)));

        Assert.Equal(1300, Saldo(Ana));
        Assert.Equal(TipoTransaccion.PagoBanco, UltimaTransaccion().Tipo);
    }

    [Fact]
    public void IrACasilla_Salida_CobraElPremio()
    {
        SacarCarta(TipoCartaEvento.IrACasilla, 0);

        Assert.Equal(0, Posicion(Ana));
        Assert.Equal(1700, Saldo(Ana));
        Assert.Equal(TipoTransaccion.PremioInicio, UltimaTransaccion().Tipo);
    }

    [Fact]
    public void IrACasilla_Propiedad_OfreceLaCompra()
    {
        SacarCarta(TipoCartaEvento.IrACasilla, 24);

        Assert.Equal(24, Posicion(Ana));
        Assert.Equal(1500, Saldo(Ana));
        Assert.Equal(24, Estado().IdPropiedadEnVenta);
    }

    [Fact]
    public void PerderTurno_MarcaElTurnoPerdido()
    {
        SacarCarta(TipoCartaEvento.PerderTurno, 2);

        Assert.Equal(2, J(Ana).TurnosPorPerder);
        Assert.Equal(2, Estado().Jugadores[0].TurnosPorPerder);
    }

    [Fact]
    public void PagarACadaJugador_ConTarjeta()
    {
        SacarCarta(TipoCartaEvento.PagarACadaJugador, 50, jugadores: 3);
        Assert.Equal(FaseTurno.EsperandoPago, Estado().Fase);
        Assert.Equal(100, Estado().MontoPagoPendiente);

        Ok(Juego.IdentificarTarjeta(Uid(Ana)));

        Assert.Equal((1400, 1550, 1550), (Saldo(Ana), Saldo(Beto), Saldo(Carla)));
        Assert.Equal(2, Juego.Historial.BuscarPorTipo(TipoTransaccion.PagoEntreJugadores).Cantidad);
        Assert.Equal(FaseTurno.PuedeTerminar, Estado().Fase);
    }

    [Fact]
    public void CobrarACadaJugador_Automatico()
    {
        SacarCarta(TipoCartaEvento.CobrarACadaJugador, 10, jugadores: 3);

        Assert.Equal((1520, 1490, 1490), (Saldo(Ana), Saldo(Beto), Saldo(Carla)));
        Assert.Equal(2, Juego.Historial.BuscarPorJugador("Ana").Cantidad);
        Assert.Equal(FaseTurno.PuedeTerminar, Estado().Fase);
    }

    [Fact]
    public void CobrarACadaJugador_EliminaAQuienNoPuedePagar()
    {
        Crear(3, new[] { 3, 4 }, casualidad: Carta(TipoCartaEvento.CobrarACadaJugador, 10), saldo: 5);

        Ok(Juego.TirarDados(Ana));

        Assert.Equal(EstadoPartida.Finalizada, Juego.Estado);
        Assert.Equal(Ana, Estado().IdGanador);
        Assert.Equal(15, Saldo(Ana));
        Assert.False(J(Beto).Activo);
        Assert.False(J(Carla).Activo);
    }
}
