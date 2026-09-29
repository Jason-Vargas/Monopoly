using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Xunit;

namespace Monopoly.Tests.Modelo;

public class PruebasCasillas
{
    private readonly Juego _juego = Fabrica.CrearJuego();
    private readonly Jugador _ana = Fabrica.CrearJugador("Ana", 1);
    private readonly Jugador _beto = Fabrica.CrearJugador("Beto", 2);

    private Casilla Casilla(int indice) => _juego.Tablero.ObtenerCasilla(indice);

    private Propiedad Propiedad(int indice) => Fabrica.PropiedadEn(_juego.Tablero, indice);

    [Fact]
    public void Propiedad_Disponible_OfreceCompra()
    {
        ResultadoCasilla resultado = Casilla(1).AlCaer(_ana, _juego);

        Assert.Equal(AccionCasilla.OfrecerCompra, resultado.Accion);
        Assert.Same(Propiedad(1), resultado.PropiedadEnVenta);
        Assert.Equal(0, resultado.MontoAPagar);
    }

    [Fact]
    public void Propiedad_DeOtroJugador_CobraAlquiler()
    {
        _beto.AgregarPropiedad(Propiedad(39));

        ResultadoCasilla resultado = Casilla(39).AlCaer(_ana, _juego);

        Assert.Equal(AccionCasilla.PagarAlquiler, resultado.Accion);
        Assert.Equal(50, resultado.MontoAPagar);
        Assert.Same(_beto, resultado.Acreedor);
        Assert.Equal(TipoTransaccion.PagoAlquiler, resultado.TipoPago);
        Assert.Null(resultado.PropiedadEnVenta);
    }

    [Fact]
    public void Propiedad_Propia_NoPagaNada()
    {
        _ana.AgregarPropiedad(Propiedad(39));

        ResultadoCasilla resultado = Casilla(39).AlCaer(_ana, _juego);

        Assert.Equal(AccionCasilla.Ninguna, resultado.Accion);
        Assert.Equal(0, resultado.MontoAPagar);
        Assert.Null(resultado.PropiedadEnVenta);
    }

    [Fact]
    public void Ferrocarril_AlquilerSeDuplicaPorCadaFerrocarrilDelDueno()
    {
        int[] ferrocarriles = { 5, 15, 25, 35 };
        int[] esperados = { 25, 50, 100, 200 };

        for (int i = 0; i < ferrocarriles.Length; i++)
        {
            _beto.AgregarPropiedad(Propiedad(ferrocarriles[i]));
            Assert.Equal(esperados[i], Casilla(5).AlCaer(_ana, _juego).MontoAPagar);
        }
    }

    [Fact]
    public void CompaniaServicio_AlquilerSegunCuantasTieneElDueno()
    {
        _beto.AgregarPropiedad(Propiedad(12));
        Assert.Equal(CompaniaServicio.AlquilerConUna, Casilla(12).AlCaer(_ana, _juego).MontoAPagar);

        _beto.AgregarPropiedad(Propiedad(28));
        Assert.Equal(CompaniaServicio.AlquilerConAmbas, Casilla(12).AlCaer(_ana, _juego).MontoAPagar);
    }

    [Theory]
    [InlineData(4, 200)]
    [InlineData(38, 100)]
    public void Impuesto_SePagaAlBanco(int indice, int monto)
    {
        ResultadoCasilla resultado = Casilla(indice).AlCaer(_ana, _juego);

        Assert.Equal(AccionCasilla.PagarImpuesto, resultado.Accion);
        Assert.Equal(monto, resultado.MontoAPagar);
        Assert.Null(resultado.Acreedor);
        Assert.Equal(TipoTransaccion.PagoBanco, resultado.TipoPago);
    }

    [Fact]
    public void VayaALaCarcel_EnviaDirectoYPierdeUnTurno()
    {
        ResultadoCasilla resultado = Casilla(30).AlCaer(_ana, _juego);

        Assert.Equal(AccionCasilla.IrALaCarcel, resultado.Accion);
        Assert.Equal(Tablero.IndiceCarcel, resultado.DestinoIndice);
        Assert.True(resultado.MovimientoDirecto);
        Assert.Equal(1, resultado.TurnosAPerder);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    public void CasillasSinEfecto(int indice)
    {
        ResultadoCasilla resultado = Casilla(indice).AlCaer(_ana, _juego);

        Assert.Equal(AccionCasilla.Ninguna, resultado.Accion);
        Assert.Equal(0, resultado.MontoAPagar);
        Assert.Equal(0, resultado.MontoARecibir);
        Assert.Null(resultado.DestinoIndice);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(22)]
    [InlineData(36)]
    public void Casualidad_SacaDeSuMazoYLaCartaVuelveAlFinal(int indice)
    {
        CartaEvento esperada = _juego.MazoCasualidad.VerSiguiente();
        CartaEvento arcaAntes = _juego.MazoArcaComunal.VerSiguiente();

        ResultadoCasilla resultado = Casilla(indice).AlCaer(_ana, _juego);

        Assert.Equal(AccionCasilla.SacarCarta, resultado.Accion);
        Assert.Same(esperada, resultado.Carta);
        Assert.NotSame(esperada, _juego.MazoCasualidad.VerSiguiente());
        Assert.Same(arcaAntes, _juego.MazoArcaComunal.VerSiguiente());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(17)]
    [InlineData(33)]
    public void ArcaComunal_SacaDeSuMazo(int indice)
    {
        CartaEvento esperada = _juego.MazoArcaComunal.VerSiguiente();
        CartaEvento casualidadAntes = _juego.MazoCasualidad.VerSiguiente();

        ResultadoCasilla resultado = Casilla(indice).AlCaer(_ana, _juego);

        Assert.Same(esperada, resultado.Carta);
        Assert.Same(casualidadAntes, _juego.MazoCasualidad.VerSiguiente());
    }

    [Fact]
    public void TodasLasCasillas_RespondenPolimorficamente()
    {
        for (int i = 0; i < _juego.Tablero.Cantidad; i++)
        {
            Casilla casilla = Casilla(i);

            ResultadoCasilla resultado = casilla.AlCaer(_ana, _juego);

            Assert.Same(casilla, resultado.Casilla);
            Assert.False(string.IsNullOrWhiteSpace(resultado.Descripcion));
            Assert.False(string.IsNullOrWhiteSpace(casilla.Categoria));
        }
    }

    [Fact]
    public void CartaEvento_TraduceCadaTipoASuEfecto()
    {
        Casilla casilla = Casilla(7);

        Assert.Equal(50, new CartaEvento("x", TipoCartaEvento.RecibirDinero, 50).CrearResultado(casilla).MontoARecibir);
        Assert.Equal(TipoTransaccion.GananciaEvento, new CartaEvento("x", TipoCartaEvento.RecibirDinero, 50).CrearResultado(casilla).TipoCobro);

        ResultadoCasilla pagar = new CartaEvento("x", TipoCartaEvento.PagarDinero, 15).CrearResultado(casilla);
        Assert.Equal(15, pagar.MontoAPagar);
        Assert.Equal(TipoTransaccion.PerdidaEvento, pagar.TipoPago);
        Assert.Null(pagar.Acreedor);

        Assert.Equal(4, new CartaEvento("x", TipoCartaEvento.Avanzar, 4).CrearResultado(casilla).Movimiento);
        Assert.Equal(-3, new CartaEvento("x", TipoCartaEvento.Retroceder, 3).CrearResultado(casilla).Movimiento);
        Assert.Equal(1, new CartaEvento("x", TipoCartaEvento.PerderTurno, 1).CrearResultado(casilla).TurnosAPerder);

        ResultadoCasilla ir = new CartaEvento("x", TipoCartaEvento.IrACasilla, 24).CrearResultado(casilla);
        Assert.Equal(24, ir.DestinoIndice);
        Assert.False(ir.MovimientoDirecto);

        Assert.Equal(50, new CartaEvento("x", TipoCartaEvento.PagarACadaJugador, 50).CrearResultado(casilla).MontoACadaJugador);
        Assert.Equal(10, new CartaEvento("x", TipoCartaEvento.CobrarACadaJugador, 10).CrearResultado(casilla).MontoDeCadaJugador);
    }
}
