using System;
using Monopoly.Core.Estructuras;
using Xunit;

namespace Monopoly.Tests.Estructuras;

public class PruebasCola
{
    [Fact]
    public void ColaNueva_EstaVacia()
    {
        Cola<int> cola = new Cola<int>();

        Assert.True(cola.EstaVacia);
        Assert.Equal(0, cola.Cantidad);
    }

    [Fact]
    public void OperacionesEnColaVacia_Lanzan()
    {
        Cola<int> cola = new Cola<int>();

        Assert.Throws<InvalidOperationException>(() => cola.Desencolar());
        Assert.Throws<InvalidOperationException>(() => cola.Frente());
    }

    [Fact]
    public void EncolarDesencolar_OrdenFifo()
    {
        Cola<int> cola = new Cola<int>();
        cola.Encolar(1);
        cola.Encolar(2);
        cola.Encolar(3);

        Assert.Equal(3, cola.Cantidad);
        Assert.Equal(1, cola.Desencolar());
        Assert.Equal(2, cola.Desencolar());
        Assert.Equal(3, cola.Desencolar());
        Assert.True(cola.EstaVacia);
    }

    [Fact]
    public void Frente_NoQuitaElElemento()
    {
        Cola<int> cola = new Cola<int>();
        cola.Encolar(5);
        cola.Encolar(6);

        Assert.Equal(5, cola.Frente());
        Assert.Equal(5, cola.Frente());
        Assert.Equal(2, cola.Cantidad);
    }

    [Fact]
    public void UnSoloElemento_AlVaciarseQuedaUtilizable()
    {
        Cola<int> cola = new Cola<int>();
        cola.Encolar(1);

        Assert.Equal(1, cola.Desencolar());
        Assert.True(cola.EstaVacia);
        Assert.Throws<InvalidOperationException>(() => cola.Frente());

        cola.Encolar(2);
        cola.Encolar(3);
        Assert.Equal(2, cola.Desencolar());
        Assert.Equal(3, cola.Desencolar());
    }

    [Fact]
    public void Mazo_LaCartaUsadaVuelveAlFinal()
    {
        Cola<string> mazo = new Cola<string>();
        mazo.Encolar("Cobrar");
        mazo.Encolar("Pagar");
        mazo.Encolar("Avanzar");
        string orden = "";

        for (int i = 0; i < 7; i++)
        {
            string carta = mazo.Desencolar();
            orden += carta[0];
            mazo.Encolar(carta);
        }

        Assert.Equal("CPACPAC", orden);
        Assert.Equal(3, mazo.Cantidad);
        Assert.Equal("Pagar", mazo.Frente());
    }
}
