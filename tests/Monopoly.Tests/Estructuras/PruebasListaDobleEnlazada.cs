using System;
using Monopoly.Core.Estructuras;
using Xunit;

namespace Monopoly.Tests.Estructuras;

public class PruebasListaDobleEnlazada
{
    private static ListaDobleEnlazada<int> Crear(params int[] valores)
    {
        ListaDobleEnlazada<int> lista = new ListaDobleEnlazada<int>();
        foreach (int valor in valores)
        {
            lista.AgregarAlFinal(valor);
        }

        return lista;
    }

    private static string DesdeInicio(ListaDobleEnlazada<int> lista) => AyudaPruebas.Texto<int>(lista.RecorrerDesdeInicio);

    private static string DesdeFinal(ListaDobleEnlazada<int> lista) => AyudaPruebas.Texto<int>(lista.RecorrerDesdeFinal);

    [Fact]
    public void ListaNueva_EstaVacia()
    {
        ListaDobleEnlazada<int> lista = new ListaDobleEnlazada<int>();

        Assert.True(lista.EstaVacia);
        Assert.Equal(0, lista.Cantidad);
        Assert.Equal("", DesdeInicio(lista));
        Assert.Equal("", DesdeFinal(lista));
    }

    [Fact]
    public void UnSoloElemento_AmbosRecorridosIguales()
    {
        ListaDobleEnlazada<int> lista = Crear(42);

        Assert.Equal("42", DesdeInicio(lista));
        Assert.Equal("42", DesdeFinal(lista));
        Assert.Equal(42, lista.Obtener(0));
        Assert.Equal(1, lista.Cantidad);
    }

    [Fact]
    public void Recorridos_EnAmbosSentidos()
    {
        ListaDobleEnlazada<int> lista = Crear(1, 2, 3, 4);

        Assert.Equal("1,2,3,4", DesdeInicio(lista));
        Assert.Equal("4,3,2,1", DesdeFinal(lista));
        Assert.Equal(4, lista.Cantidad);
        Assert.False(lista.EstaVacia);
    }

    [Fact]
    public void Obtener_TodosLosIndices_DesdeAmbosExtremos()
    {
        ListaDobleEnlazada<int> lista = Crear(0, 10, 20, 30, 40);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i * 10, lista.Obtener(i));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Obtener_FueraDeRango_Lanza(int indice)
    {
        ListaDobleEnlazada<int> lista = Crear(1, 2, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => lista.Obtener(indice));
    }

    [Fact]
    public void Obtener_EnListaVacia_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ListaDobleEnlazada<int>().Obtener(0));
    }

    [Fact]
    public void BuscarTodos_DevuelveListaDobleRecorribleEnAmbosSentidos()
    {
        ListaDobleEnlazada<int> lista = Crear(1, 2, 3, 4, 5, 6);

        ListaDobleEnlazada<int> pares = lista.BuscarTodos(n => n % 2 == 0);

        Assert.Equal("2,4,6", DesdeInicio(pares));
        Assert.Equal("6,4,2", DesdeFinal(pares));
        Assert.Equal(3, pares.Cantidad);
        Assert.Equal(6, lista.Cantidad);
    }

    [Fact]
    public void BuscarTodos_SinCoincidencias_DevuelveListaVacia()
    {
        Assert.True(Crear(1, 3).BuscarTodos(n => n > 10).EstaVacia);
        Assert.True(new ListaDobleEnlazada<int>().BuscarTodos(n => true).EstaVacia);
    }

    [Fact]
    public void DelegadosNulos_Lanzan()
    {
        ListaDobleEnlazada<int> lista = Crear(1);

        Assert.Throws<ArgumentNullException>(() => lista.RecorrerDesdeInicio(null!));
        Assert.Throws<ArgumentNullException>(() => lista.RecorrerDesdeFinal(null!));
        Assert.Throws<ArgumentNullException>(() => lista.BuscarTodos(null!));
    }
}
