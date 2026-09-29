using System;
using Monopoly.Core.Modelo;
using Monopoly.Tests.Estructuras;
using Xunit;

namespace Monopoly.Tests.Modelo;

public class PruebasTablero
{
    private readonly Tablero _tablero = new Tablero();

    private Jugador JugadorEn(int indice)
    {
        Jugador jugador = Fabrica.CrearJugador();
        _tablero.ColocarEnSalida(jugador);
        _tablero.EnviarJugadorA(jugador, indice);
        return jugador;
    }

    private static string IdsRecorridos(ResultadoMovimiento resultado)
    {
        return AyudaPruebas.Texto<int>(visitar => resultado.CasillasRecorridas.Recorrer(c => visitar(c.Id)));
    }

    [Fact]
    public void TieneCuarentaCasillasConIdIgualAlIndice()
    {
        Assert.Equal(40, _tablero.Cantidad);
        for (int i = 0; i < 40; i++)
        {
            Assert.Equal(i, _tablero.ObtenerCasilla(i).Id);
        }
    }

    [Fact]
    public void CasillasEspecialesEnSuPosicionClasica()
    {
        Assert.Equal("Salida", _tablero.ObtenerCasilla(0).Nombre);
        Assert.Same(_tablero.Salida, _tablero.ObtenerCasilla(Tablero.IndiceSalida));
        Assert.Equal("Cárcel", _tablero.ObtenerCasilla(Tablero.IndiceCarcel).Categoria);
        Assert.Equal("Parada Libre", _tablero.ObtenerCasilla(20).Categoria);
        Assert.Equal("Vaya a la Cárcel", _tablero.ObtenerCasilla(30).Categoria);
        Assert.Equal("Paseo Marítimo", _tablero.ObtenerCasilla(39).Nombre);
        Assert.Equal(200, ((Impuesto)_tablero.ObtenerCasilla(4)).Monto);
        Assert.Equal(100, ((Impuesto)_tablero.ObtenerCasilla(38)).Monto);

        foreach (int i in new[] { 7, 22, 36 })
        {
            Assert.Equal("Casualidad", _tablero.ObtenerCasilla(i).Categoria);
        }

        foreach (int i in new[] { 2, 17, 33 })
        {
            Assert.Equal("Arca Comunal", _tablero.ObtenerCasilla(i).Categoria);
        }
    }

    [Theory]
    [InlineData(GrupoPropiedad.Marron, 2)]
    [InlineData(GrupoPropiedad.Celeste, 3)]
    [InlineData(GrupoPropiedad.Rosa, 3)]
    [InlineData(GrupoPropiedad.Naranja, 3)]
    [InlineData(GrupoPropiedad.Rojo, 3)]
    [InlineData(GrupoPropiedad.Amarillo, 3)]
    [InlineData(GrupoPropiedad.Verde, 3)]
    [InlineData(GrupoPropiedad.AzulOscuro, 2)]
    [InlineData(GrupoPropiedad.Ferrocarril, 4)]
    [InlineData(GrupoPropiedad.Servicio, 2)]
    public void GruposConLaCantidadClasica(GrupoPropiedad grupo, int esperadas)
    {
        int cantidad = _tablero.BuscarCasillas(c => c is Propiedad p && p.Grupo == grupo).Cantidad;

        Assert.Equal(esperadas, cantidad);
    }

    [Fact]
    public void PreciosYAlquileresClasicos()
    {
        Propiedad mediterraneo = Fabrica.PropiedadEn(_tablero, 1);
        Propiedad paseo = Fabrica.PropiedadEn(_tablero, 39);
        Propiedad reading = Fabrica.PropiedadEn(_tablero, 5);

        Assert.Equal((60, 2), (mediterraneo.PrecioCompra, mediterraneo.Alquiler));
        Assert.Equal((400, 50), (paseo.PrecioCompra, paseo.Alquiler));
        Assert.Equal((200, 25), (reading.PrecioCompra, reading.Alquiler));
        Assert.Equal(28, _tablero.BuscarCasillas(c => c is Propiedad).Cantidad);
    }

    [Fact]
    public void MoverJugador_SinPasarPorSalida()
    {
        Jugador jugador = Fabrica.CrearJugador();
        _tablero.ColocarEnSalida(jugador);

        ResultadoMovimiento resultado = _tablero.MoverJugador(jugador, 7);

        Assert.Equal(7, jugador.CasillaActual!.Id);
        Assert.Equal("1,2,3,4,5,6,7", IdsRecorridos(resultado));
        Assert.Equal(0, resultado.CasillaInicial.Id);
        Assert.Same(jugador.CasillaActual, resultado.CasillaFinal);
        Assert.False(resultado.PasoPorSalida);
    }

    [Fact]
    public void MoverJugador_DaLaVueltaYPasaPorSalida()
    {
        Jugador jugador = JugadorEn(35);

        ResultadoMovimiento resultado = _tablero.MoverJugador(jugador, 7);

        Assert.Equal(2, jugador.CasillaActual!.Id);
        Assert.Equal("36,37,38,39,0,1,2", IdsRecorridos(resultado));
        Assert.True(resultado.PasoPorSalida);
        Assert.Equal(1, resultado.VecesPorSalida);
        Assert.Equal(200, _tablero.Salida.Premio);
    }

    [Fact]
    public void MoverJugador_CaerExactoEnSalidaCuentaComoPasar()
    {
        Jugador jugador = JugadorEn(35);

        ResultadoMovimiento resultado = _tablero.MoverJugador(jugador, 5);

        Assert.Same(_tablero.Salida, jugador.CasillaActual);
        Assert.True(resultado.PasoPorSalida);
    }

    [Theory]
    [InlineData(40, 0, 1)]
    [InlineData(80, 0, 2)]
    [InlineData(85, 5, 2)]
    public void MoverJugador_VueltasCompletasCobranCadaPaso(int pasos, int llegada, int veces)
    {
        Jugador jugador = Fabrica.CrearJugador();
        _tablero.ColocarEnSalida(jugador);

        ResultadoMovimiento resultado = _tablero.MoverJugador(jugador, pasos);

        Assert.Equal(llegada, jugador.CasillaActual!.Id);
        Assert.Equal(veces, resultado.VecesPorSalida);
        Assert.Equal(pasos, resultado.CasillasRecorridas.Cantidad);
    }

    [Fact]
    public void MoverJugador_CeroPasos_NoSeMueve()
    {
        Jugador jugador = JugadorEn(12);

        ResultadoMovimiento resultado = _tablero.MoverJugador(jugador, 0);

        Assert.Equal(12, jugador.CasillaActual!.Id);
        Assert.True(resultado.CasillasRecorridas.EstaVacia);
        Assert.False(resultado.PasoPorSalida);
    }

    [Fact]
    public void MoverJugador_RetrocedeSobreSalidaSinCobrar()
    {
        Jugador jugador = JugadorEn(2);

        ResultadoMovimiento resultado = _tablero.MoverJugador(jugador, -3);

        Assert.Equal(39, jugador.CasillaActual!.Id);
        Assert.Equal("1,0,39", IdsRecorridos(resultado));
        Assert.False(resultado.PasoPorSalida);
    }

    [Fact]
    public void MoverJugador_RetrocedeDesdeSalida()
    {
        Jugador jugador = Fabrica.CrearJugador();
        _tablero.ColocarEnSalida(jugador);

        ResultadoMovimiento resultado = _tablero.MoverJugador(jugador, -2);

        Assert.Equal(38, jugador.CasillaActual!.Id);
        Assert.Equal("39,38", IdsRecorridos(resultado));
    }

    [Fact]
    public void MoverJugadorHasta_PasandoPorSalida()
    {
        Jugador jugador = JugadorEn(36);

        ResultadoMovimiento resultado = _tablero.MoverJugadorHasta(jugador, 24);

        Assert.Equal(24, jugador.CasillaActual!.Id);
        Assert.Equal(28, resultado.CasillasRecorridas.Cantidad);
        Assert.True(resultado.PasoPorSalida);
    }

    [Fact]
    public void MoverJugadorHasta_SinPasarPorSalida()
    {
        Jugador jugador = JugadorEn(7);

        ResultadoMovimiento resultado = _tablero.MoverJugadorHasta(jugador, 24);

        Assert.Equal(17, resultado.CasillasRecorridas.Cantidad);
        Assert.False(resultado.PasoPorSalida);
    }

    [Fact]
    public void EnviarJugadorA_NoRecorreNiCobra()
    {
        Jugador jugador = JugadorEn(30);

        ResultadoMovimiento resultado = _tablero.EnviarJugadorA(jugador, Tablero.IndiceCarcel);

        Assert.Equal(Tablero.IndiceCarcel, jugador.CasillaActual!.Id);
        Assert.True(resultado.CasillasRecorridas.EstaVacia);
        Assert.False(resultado.PasoPorSalida);
    }

    [Fact]
    public void Movimientos_Invalidos_Lanzan()
    {
        Jugador sinColocar = Fabrica.CrearJugador();
        Jugador colocado = JugadorEn(0);

        Assert.Throws<InvalidOperationException>(() => _tablero.MoverJugador(sinColocar, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => _tablero.MoverJugadorHasta(colocado, 40));
        Assert.Throws<ArgumentOutOfRangeException>(() => _tablero.EnviarJugadorA(colocado, -1));
        Assert.Throws<ArgumentNullException>(() => _tablero.MoverJugador(null!, 1));
    }
}
