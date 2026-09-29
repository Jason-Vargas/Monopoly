using System;
using Monopoly.Core.Estructuras;
using Xunit;

namespace Monopoly.Tests.Estructuras;

public class PruebasListaSimple
{
    private static ListaSimple<int> Crear(params int[] valores)
    {
        ListaSimple<int> lista = new ListaSimple<int>();
        foreach (int valor in valores)
        {
            lista.AgregarAlFinal(valor);
        }

        return lista;
    }

    private static string Texto(ListaSimple<int> lista) => AyudaPruebas.Texto<int>(lista.Recorrer);

    [Fact]
    public void ListaNueva_EstaVacia()
    {
        ListaSimple<int> lista = new ListaSimple<int>();

        Assert.True(lista.EstaVacia);
        Assert.Equal(0, lista.Cantidad);
        Assert.Equal("", Texto(lista));
    }

    [Fact]
    public void AgregarAlFinal_ConservaElOrden()
    {
        ListaSimple<int> lista = Crear(1, 2, 3);

        Assert.Equal("1,2,3", Texto(lista));
        Assert.Equal(3, lista.Cantidad);
        Assert.False(lista.EstaVacia);
    }

    [Fact]
    public void AgregarAlInicio_ColocaAdelante()
    {
        ListaSimple<int> lista = new ListaSimple<int>();
        lista.AgregarAlInicio(2);
        lista.AgregarAlInicio(1);
        lista.AgregarAlFinal(3);

        Assert.Equal("1,2,3", Texto(lista));
    }

    [Fact]
    public void AgregarAlInicio_EnListaVacia_TambienEsElUltimo()
    {
        ListaSimple<int> lista = new ListaSimple<int>();
        lista.AgregarAlInicio(5);
        lista.AgregarAlFinal(6);

        Assert.Equal("5,6", Texto(lista));
    }

    [Fact]
    public void Eliminar_Cabeza()
    {
        ListaSimple<int> lista = Crear(1, 2, 3);

        Assert.True(lista.Eliminar(1));
        Assert.Equal("2,3", Texto(lista));
        Assert.Equal(2, lista.Cantidad);
    }

    [Fact]
    public void Eliminar_Cola_ActualizaElFinal()
    {
        ListaSimple<int> lista = Crear(1, 2, 3);

        Assert.True(lista.Eliminar(3));
        lista.AgregarAlFinal(4);

        Assert.Equal("1,2,4", Texto(lista));
    }

    [Fact]
    public void Eliminar_Intermedio()
    {
        ListaSimple<int> lista = Crear(1, 2, 3);

        Assert.True(lista.Eliminar(2));
        Assert.Equal("1,3", Texto(lista));
    }

    [Fact]
    public void Eliminar_UnicoElemento_DejaLaListaUtilizable()
    {
        ListaSimple<int> lista = Crear(7);

        Assert.True(lista.Eliminar(7));
        Assert.True(lista.EstaVacia);

        lista.AgregarAlFinal(8);
        lista.AgregarAlInicio(6);
        Assert.Equal("6,8", Texto(lista));
    }

    [Fact]
    public void Eliminar_SoloLaPrimeraAparicion()
    {
        ListaSimple<int> lista = Crear(1, 2, 1);

        Assert.True(lista.Eliminar(1));
        Assert.Equal("2,1", Texto(lista));
    }

    [Fact]
    public void Eliminar_Inexistente_DevuelveFalso()
    {
        ListaSimple<int> lista = Crear(1, 2);

        Assert.False(lista.Eliminar(9));
        Assert.False(new ListaSimple<int>().Eliminar(1));
        Assert.Equal(2, lista.Cantidad);
    }

    [Fact]
    public void Eliminar_Nulo_FuncionaConReferencias()
    {
        ListaSimple<string?> lista = new ListaSimple<string?>();
        lista.AgregarAlFinal("a");
        lista.AgregarAlFinal(null);

        Assert.True(lista.Contiene(null));
        Assert.True(lista.Eliminar(null));
        Assert.False(lista.Contiene(null));
    }

    [Fact]
    public void Obtener_DevuelveElElementoPorIndice()
    {
        ListaSimple<int> lista = Crear(10, 20, 30);

        Assert.Equal(10, lista.Obtener(0));
        Assert.Equal(20, lista.Obtener(1));
        Assert.Equal(30, lista.Obtener(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Obtener_FueraDeRango_Lanza(int indice)
    {
        ListaSimple<int> lista = Crear(10, 20, 30);

        Assert.Throws<ArgumentOutOfRangeException>(() => lista.Obtener(indice));
    }

    [Fact]
    public void Obtener_EnListaVacia_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ListaSimple<int>().Obtener(0));
    }

    [Fact]
    public void Buscar_DevuelveElPrimeroQueCumple()
    {
        ListaSimple<string> lista = new ListaSimple<string>();
        lista.AgregarAlFinal("Boardwalk");
        lista.AgregarAlFinal("Baltic");
        lista.AgregarAlFinal("Bay");

        Assert.Equal("Baltic", lista.Buscar(s => s.StartsWith("Ba")));
    }

    [Fact]
    public void Buscar_SinCoincidencias_DevuelveDefault()
    {
        ListaSimple<string> lista = new ListaSimple<string>();
        lista.AgregarAlFinal("a");

        Assert.Null(lista.Buscar(s => s == "z"));
        Assert.Null(new ListaSimple<string>().Buscar(s => true));
    }

    [Fact]
    public void BuscarTodos_DevuelveNuevaListaEnOrden()
    {
        ListaSimple<int> lista = Crear(1, 2, 3, 4, 5, 6);

        ListaSimple<int> pares = lista.BuscarTodos(n => n % 2 == 0);

        Assert.Equal("2,4,6", Texto(pares));
        Assert.Equal(6, lista.Cantidad);
    }

    [Fact]
    public void BuscarTodos_SinCoincidencias_DevuelveListaVacia()
    {
        Assert.True(Crear(1, 3).BuscarTodos(n => n > 10).EstaVacia);
        Assert.True(new ListaSimple<int>().BuscarTodos(n => true).EstaVacia);
    }

    [Fact]
    public void Contiene()
    {
        ListaSimple<int> lista = Crear(1, 2);

        Assert.True(lista.Contiene(2));
        Assert.False(lista.Contiene(3));
        Assert.False(new ListaSimple<int>().Contiene(0));
    }

    [Fact]
    public void DelegadosNulos_Lanzan()
    {
        ListaSimple<int> lista = Crear(1);

        Assert.Throws<ArgumentNullException>(() => lista.Recorrer(null!));
        Assert.Throws<ArgumentNullException>(() => lista.Buscar(null!));
        Assert.Throws<ArgumentNullException>(() => lista.BuscarTodos(null!));
    }
}
