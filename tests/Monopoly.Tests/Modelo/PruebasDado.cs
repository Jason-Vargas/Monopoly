using System;
using Monopoly.Core.Modelo;
using Xunit;

namespace Monopoly.Tests.Modelo;

public class PruebasDado
{
    [Fact]
    public void ValoresFijos_SeUsanEnOrdenYSeRepiten()
    {
        Dado dado = Dado.ConValoresFijos(3, 4, 6, 6);

        TiradaDados primera = dado.Lanzar();
        TiradaDados segunda = dado.Lanzar();
        TiradaDados tercera = dado.Lanzar();

        Assert.Equal((3, 4, 7, false), (primera.Dado1, primera.Dado2, primera.Total, primera.EsDoble));
        Assert.Equal((6, 6, 12, true), (segunda.Dado1, segunda.Dado2, segunda.Total, segunda.EsDoble));
        Assert.Equal(primera, tercera);
        Assert.Equal(tercera, dado.UltimaTirada);
    }

    [Fact]
    public void MismaSemilla_MismaSecuencia()
    {
        Dado a = new Dado(123);
        Dado b = new Dado(123);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(a.Lanzar(), b.Lanzar());
        }
    }

    [Fact]
    public void Aleatorio_ValoresEntreUnoYSeis()
    {
        Dado dado = new Dado(5);
        bool[] vistos = new bool[7];

        for (int i = 0; i < 1000; i++)
        {
            TiradaDados tirada = dado.Lanzar();
            Assert.InRange(tirada.Dado1, 1, 6);
            Assert.InRange(tirada.Dado2, 1, 6);
            vistos[tirada.Dado1] = true;
        }

        for (int valor = 1; valor <= 6; valor++)
        {
            Assert.True(vistos[valor], $"Nunca salió el {valor}.");
        }
    }

    [Fact]
    public void SinLanzar_NoHayUltimaTirada()
    {
        Assert.Null(new Dado(1).UltimaTirada);
    }

    [Fact]
    public void ValoresFijosInvalidos_Lanzan()
    {
        Assert.Throws<ArgumentException>(() => Dado.ConValoresFijos());
        Assert.Throws<ArgumentOutOfRangeException>(() => Dado.ConValoresFijos(0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Dado.ConValoresFijos(3, 7));
    }

    [Fact]
    public void ValoresFijos_SeCopian()
    {
        int[] valores = { 2, 5 };
        Dado dado = Dado.ConValoresFijos(valores);
        valores[0] = 6;

        Assert.Equal(2, dado.Lanzar().Dado1);
    }
}
