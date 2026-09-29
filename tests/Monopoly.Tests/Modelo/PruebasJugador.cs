using System;
using Monopoly.Core.Modelo;
using Xunit;

namespace Monopoly.Tests.Modelo;

public class PruebasJugador
{
    private readonly Tablero _tablero = new Tablero();
    private readonly Jugador _ana = Fabrica.CrearJugador("Ana", 1);

    [Fact]
    public void JugadorNuevo_EstadoInicial()
    {
        Assert.Equal(Jugador.SaldoInicial, _ana.Saldo);
        Assert.Equal(1500, _ana.Saldo);
        Assert.True(_ana.Activo);
        Assert.Equal(0, _ana.TurnosPorPerder);
        Assert.Equal(0, _ana.CantidadPropiedades);
        Assert.Null(_ana.Posicion);
        Assert.Null(_ana.UidTarjeta);
        Assert.Equal(ColorFicha.Rojo, _ana.ColorFicha);
    }

    [Fact]
    public void Patrimonio_SinPropiedades_EsElSaldo()
    {
        Assert.Equal(1500, _ana.CalcularPatrimonio());
    }

    [Fact]
    public void Patrimonio_SumaSaldoYPrecioDeCompraDePropiedades()
    {
        _ana.Debitar(460);
        _ana.AgregarPropiedad(Fabrica.PropiedadEn(_tablero, 1));
        _ana.AgregarPropiedad(Fabrica.PropiedadEn(_tablero, 39));
        _ana.AgregarPropiedad(Fabrica.PropiedadEn(_tablero, 5));

        Assert.Equal(1040, _ana.Saldo);
        Assert.Equal(1040 + 60 + 400 + 200, _ana.CalcularPatrimonio());
    }

    [Fact]
    public void Patrimonio_ExcluyePropiedadesQuitadas()
    {
        Propiedad paseo = Fabrica.PropiedadEn(_tablero, 39);
        _ana.AgregarPropiedad(paseo);
        _ana.QuitarPropiedad(paseo);

        Assert.Equal(1500, _ana.CalcularPatrimonio());
    }

    [Fact]
    public void AcreditarYDebitar()
    {
        _ana.Acreditar(200);
        _ana.Debitar(700);

        Assert.Equal(1000, _ana.Saldo);
        Assert.True(_ana.PuedePagar(1000));
        Assert.False(_ana.PuedePagar(1001));
    }

    [Fact]
    public void Debitar_SaldoInsuficiente_LanzaYNoModifica()
    {
        Assert.Throws<InvalidOperationException>(() => _ana.Debitar(1501));
        Assert.Equal(1500, _ana.Saldo);
    }

    [Fact]
    public void MontosNegativos_Lanzan()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ana.Acreditar(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ana.Debitar(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ana.PerderTurnos(-1));
    }

    [Fact]
    public void AgregarPropiedad_AsignaPropietario()
    {
        Propiedad propiedad = Fabrica.PropiedadEn(_tablero, 11);

        _ana.AgregarPropiedad(propiedad);

        Assert.Same(_ana, propiedad.Propietario);
        Assert.False(propiedad.EstaDisponible);
        Assert.True(_ana.TienePropiedad(propiedad));
        Assert.Equal(1, _ana.CantidadPropiedades);
    }

    [Fact]
    public void AgregarPropiedad_ConDueno_Lanza()
    {
        Propiedad propiedad = Fabrica.PropiedadEn(_tablero, 11);
        Jugador beto = Fabrica.CrearJugador("Beto", 2);
        _ana.AgregarPropiedad(propiedad);

        Assert.Throws<InvalidOperationException>(() => beto.AgregarPropiedad(propiedad));
        Assert.Throws<InvalidOperationException>(() => _ana.AgregarPropiedad(propiedad));
        Assert.Same(_ana, propiedad.Propietario);
    }

    [Fact]
    public void QuitarPropiedad_LaDevuelveAlBanco()
    {
        Propiedad propiedad = Fabrica.PropiedadEn(_tablero, 11);
        _ana.AgregarPropiedad(propiedad);

        Assert.True(_ana.QuitarPropiedad(propiedad));
        Assert.True(propiedad.EstaDisponible);
        Assert.False(_ana.QuitarPropiedad(propiedad));
    }

    [Fact]
    public void TurnosPerdidos_SeConsumenUnoAUno()
    {
        _ana.PerderTurnos(2);

        Assert.True(_ana.ConsumirTurnoPerdido());
        Assert.Equal(1, _ana.TurnosPorPerder);
        Assert.True(_ana.ConsumirTurnoPerdido());
        Assert.False(_ana.ConsumirTurnoPerdido());
        Assert.Equal(0, _ana.TurnosPorPerder);
    }

    [Fact]
    public void Desactivar_DejaAlJugadorInactivo()
    {
        _ana.Desactivar();

        Assert.False(_ana.Activo);
    }

    [Fact]
    public void RecorrerPropiedades_EnOrdenDeAdquisicion()
    {
        _ana.AgregarPropiedad(Fabrica.PropiedadEn(_tablero, 39));
        _ana.AgregarPropiedad(Fabrica.PropiedadEn(_tablero, 1));
        string nombres = "";

        _ana.RecorrerPropiedades(p => nombres += p.Id + ";");

        Assert.Equal("39;1;", nombres);
    }

    [Fact]
    public void NombreVacio_Lanza()
    {
        Assert.Throws<ArgumentException>(() => new Jugador(1, "  ", ColorFicha.Azul));
    }
}
