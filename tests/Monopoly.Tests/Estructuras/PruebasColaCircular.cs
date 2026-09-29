using System;
using Monopoly.Core.Estructuras;
using Xunit;

namespace Monopoly.Tests.Estructuras;

public class PruebasColaCircular
{
    private static ColaCircular<string> Crear(int capacidad, params string[] valores)
    {
        ColaCircular<string> cola = new ColaCircular<string>(capacidad);
        foreach (string valor in valores)
        {
            cola.Encolar(valor);
        }

        return cola;
    }

    private static string Texto(ColaCircular<string> cola) => AyudaPruebas.Texto<string>(cola.Recorrer);

    [Fact]
    public void ColaNueva_EstaVacia()
    {
        ColaCircular<string> cola = new ColaCircular<string>();

        Assert.True(cola.EstaVacia);
        Assert.Equal(0, cola.Cantidad);
        Assert.Equal("", Texto(cola));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_CapacidadInvalida_Lanza(int capacidad)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColaCircular<string>(capacidad));
    }

    [Fact]
    public void OperacionesEnColaVacia_Lanzan()
    {
        ColaCircular<string> cola = new ColaCircular<string>();

        Assert.Throws<InvalidOperationException>(() => cola.Desencolar());
        Assert.Throws<InvalidOperationException>(() => cola.Frente());
        Assert.Throws<InvalidOperationException>(() => cola.Rotar());
    }

    [Fact]
    public void EncolarDesencolar_OrdenFifo()
    {
        ColaCircular<string> cola = Crear(4, "A", "B", "C");

        Assert.Equal("A", cola.Frente());
        Assert.Equal("A", cola.Desencolar());
        Assert.Equal("B", cola.Desencolar());
        Assert.Equal("C", cola.Desencolar());
        Assert.True(cola.EstaVacia);
    }

    [Fact]
    public void Frente_NoQuitaElElemento()
    {
        ColaCircular<string> cola = Crear(4, "A", "B");

        Assert.Equal("A", cola.Frente());
        Assert.Equal("A", cola.Frente());
        Assert.Equal(2, cola.Cantidad);
    }

    [Fact]
    public void Encolar_DaLaVueltaAlArreglo()
    {
        ColaCircular<string> cola = Crear(3, "A", "B", "C");
        cola.Desencolar();
        cola.Desencolar();
        cola.Encolar("D");
        cola.Encolar("E");

        Assert.Equal(3, cola.Capacidad);
        Assert.Equal("C,D,E", Texto(cola));
    }

    [Fact]
    public void Encolar_ColaLlena_Crece()
    {
        ColaCircular<string> cola = Crear(2, "A", "B");
        cola.Rotar();
        cola.Encolar("C");

        Assert.Equal(4, cola.Capacidad);
        Assert.Equal("B,A,C", Texto(cola));
    }

    [Fact]
    public void Rotar_UnSoloElemento_NoCambiaNada()
    {
        ColaCircular<string> cola = Crear(4, "A");

        cola.Rotar();
        cola.Rotar();

        Assert.Equal("A", cola.Frente());
        Assert.Equal(1, cola.Cantidad);
    }

    [Fact]
    public void Rotar_UnSoloElemento_ConCapacidadUno()
    {
        ColaCircular<string> cola = Crear(1, "A");

        cola.Rotar();

        Assert.Equal("A", cola.Frente());
        Assert.Equal("A", Texto(cola));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    public void Rotar_TurnosCompletos(int capacidad)
    {
        ColaCircular<string> cola = Crear(capacidad, "J1", "J2", "J3", "J4");
        string turnos = "";

        for (int i = 0; i < 9; i++)
        {
            turnos += cola.Frente();
            cola.Rotar();
        }

        Assert.Equal("J1J2J3J4J1J2J3J4J1", turnos);
        Assert.Equal("J2,J3,J4,J1", Texto(cola));
        Assert.Equal(4, cola.Cantidad);
    }

    [Fact]
    public void Eliminar_ElFrente_ElSiguientePasaAlFrente()
    {
        ColaCircular<string> cola = Crear(4, "J1", "J2", "J3", "J4");

        Assert.True(cola.Eliminar("J1"));
        Assert.Equal("J2", cola.Frente());
        Assert.Equal("J2,J3,J4", Texto(cola));
    }

    [Fact]
    public void Eliminar_ElUltimo()
    {
        ColaCircular<string> cola = Crear(4, "J1", "J2", "J3", "J4");

        Assert.True(cola.Eliminar("J4"));
        cola.Encolar("J5");

        Assert.Equal("J1,J2,J3,J5", Texto(cola));
    }

    [Fact]
    public void Eliminar_ConElFrenteDesplazado_ConservaElOrden()
    {
        ColaCircular<string> cola = Crear(4, "J1", "J2", "J3", "J4");
        cola.Rotar();
        cola.Rotar();
        cola.Rotar();

        Assert.True(cola.Eliminar("J2"));
        Assert.Equal("J4,J1,J3", Texto(cola));

        cola.Rotar();
        Assert.Equal("J1,J3,J4", Texto(cola));
    }

    [Fact]
    public void Eliminar_UnicoElemento_DejaLaColaVaciaYUtilizable()
    {
        ColaCircular<string> cola = Crear(4, "J1");

        Assert.True(cola.Eliminar("J1"));
        Assert.True(cola.EstaVacia);
        Assert.Throws<InvalidOperationException>(() => cola.Frente());

        cola.Encolar("J2");
        Assert.Equal("J2", cola.Frente());
    }

    [Fact]
    public void Eliminar_Inexistente_DevuelveFalso()
    {
        ColaCircular<string> cola = Crear(4, "J1", "J2");

        Assert.False(cola.Eliminar("J9"));
        Assert.False(new ColaCircular<string>().Eliminar("J1"));
        Assert.Equal(2, cola.Cantidad);
    }

    [Fact]
    public void Recorrer_DelegadoNulo_Lanza()
    {
        Assert.Throws<ArgumentNullException>(() => Crear(2, "A").Recorrer(null!));
    }
}
