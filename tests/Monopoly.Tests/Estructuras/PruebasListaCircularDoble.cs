using System;
using Monopoly.Core.Estructuras;
using Xunit;

namespace Monopoly.Tests.Estructuras;

public class PruebasListaCircularDoble
{
    /// <summary>
    /// Crea una lista con los valores 0..cantidad-1, de modo que valor == índice.
    /// </summary>
    private static ListaCircularDoble<int> Crear(int cantidad)
    {
        ListaCircularDoble<int> lista = new ListaCircularDoble<int>();
        for (int i = 0; i < cantidad; i++)
        {
            lista.Agregar(i);
        }

        return lista;
    }

    [Fact]
    public void ListaNueva_EstaVacia()
    {
        ListaCircularDoble<int> lista = new ListaCircularDoble<int>();

        Assert.True(lista.EstaVacia);
        Assert.Equal(0, lista.Cantidad);
        Assert.Null(lista.Cabeza);
        Assert.Equal("", AyudaPruebas.Texto<int>(lista.Recorrer));
    }

    [Fact]
    public void UnSoloNodo_ApuntaASiMismo()
    {
        ListaCircularDoble<int> lista = new ListaCircularDoble<int>();
        NodoCircularDoble<int> nodo = lista.Agregar(7);

        Assert.Same(nodo, lista.Cabeza);
        Assert.Same(nodo, nodo.Siguiente);
        Assert.Same(nodo, nodo.Anterior);
        Assert.Same(nodo, lista.Avanzar(nodo, 5));
        Assert.Same(nodo, lista.Retroceder(nodo, 3));
        Assert.Equal(0, lista.IndiceDe(nodo));
    }

    [Fact]
    public void Agregar_EnlazaCircularmenteEnAmbosSentidos()
    {
        ListaCircularDoble<int> lista = Crear(4);
        NodoCircularDoble<int> cabeza = lista.Cabeza!;

        Assert.Equal(3, cabeza.Anterior.Valor);
        Assert.Same(cabeza, cabeza.Anterior.Siguiente);

        NodoCircularDoble<int> actual = cabeza;
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(i, actual.Valor);
            Assert.Same(actual, actual.Siguiente.Anterior);
            actual = actual.Siguiente;
        }

        Assert.Same(cabeza, actual);
    }

    [Fact]
    public void Recorrer_DaUnaSolaVuelta()
    {
        Assert.Equal("0,1,2,3,4", AyudaPruebas.Texto<int>(Crear(5).Recorrer));
    }

    [Fact]
    public void ObtenerNodo_TodosLosIndices()
    {
        ListaCircularDoble<int> lista = Crear(7);

        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(i, lista.ObtenerNodo(i).Valor);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void ObtenerNodo_FueraDeRango_Lanza(int indice)
    {
        ListaCircularDoble<int> lista = Crear(24);

        Assert.Throws<ArgumentOutOfRangeException>(() => lista.ObtenerNodo(indice));
    }

    [Fact]
    public void ObtenerNodo_EnListaVacia_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ListaCircularDoble<int>().ObtenerNodo(0));
    }

    [Fact]
    public void Avanzar_CeroPasos_DevuelveElMismoNodo()
    {
        ListaCircularDoble<int> lista = Crear(24);
        NodoCircularDoble<int> nodo = lista.ObtenerNodo(5);

        Assert.Same(nodo, lista.Avanzar(nodo, 0));
        Assert.Same(nodo, lista.Retroceder(nodo, 0));
    }

    [Fact]
    public void Avanzar_SinDarLaVuelta()
    {
        ListaCircularDoble<int> lista = Crear(24);

        Assert.Equal(12, lista.Avanzar(lista.ObtenerNodo(5), 7).Valor);
    }

    [Fact]
    public void Avanzar_PasandoPorLaCabeza()
    {
        ListaCircularDoble<int> lista = Crear(24);

        Assert.Equal(3, lista.Avanzar(lista.ObtenerNodo(20), 7).Valor);
    }

    [Theory]
    [InlineData(24, 5)]
    [InlineData(48, 5)]
    [InlineData(75, 8)]
    [InlineData(240, 5)]
    public void Avanzar_VariasVueltasCompletas(int pasos, int esperado)
    {
        ListaCircularDoble<int> lista = Crear(24);

        Assert.Equal(esperado, lista.Avanzar(lista.ObtenerNodo(5), pasos).Valor);
    }

    [Fact]
    public void Avanzar_NotificaCadaNodoVisitado()
    {
        ListaCircularDoble<int> lista = Crear(5);
        string visitados = AyudaPruebas.Texto<int>(visitar =>
            lista.Avanzar(lista.ObtenerNodo(3), 4, nodo => visitar(nodo.Valor)));

        Assert.Equal("4,0,1,2", visitados);
    }

    [Fact]
    public void Retroceder_DesdeElNodoCero_LlegaAlUltimo()
    {
        ListaCircularDoble<int> lista = Crear(24);
        NodoCircularDoble<int> cero = lista.ObtenerNodo(0);

        Assert.Equal(23, lista.Retroceder(cero, 1).Valor);
        Assert.Equal(21, lista.Retroceder(cero, 3).Valor);
    }

    [Theory]
    [InlineData(24, 0)]
    [InlineData(27, 21)]
    [InlineData(100, 20)]
    public void Retroceder_DesdeElNodoCero_VariasVueltas(int pasos, int esperado)
    {
        ListaCircularDoble<int> lista = Crear(24);

        Assert.Equal(esperado, lista.Retroceder(lista.ObtenerNodo(0), pasos).Valor);
    }

    [Fact]
    public void Retroceder_NotificaCadaNodoVisitado()
    {
        ListaCircularDoble<int> lista = Crear(5);
        string visitados = AyudaPruebas.Texto<int>(visitar =>
            lista.Retroceder(lista.ObtenerNodo(1), 3, nodo => visitar(nodo.Valor)));

        Assert.Equal("0,4,3", visitados);
    }

    [Fact]
    public void AvanzarYRetroceder_SonInversos()
    {
        ListaCircularDoble<int> lista = Crear(24);
        NodoCircularDoble<int> inicio = lista.ObtenerNodo(17);

        Assert.Same(inicio, lista.Retroceder(lista.Avanzar(inicio, 37), 37));
    }

    [Fact]
    public void Movimiento_PasosNegativos_Lanza()
    {
        ListaCircularDoble<int> lista = Crear(3);
        NodoCircularDoble<int> nodo = lista.Cabeza!;

        Assert.Throws<ArgumentOutOfRangeException>(() => lista.Avanzar(nodo, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => lista.Retroceder(nodo, -1));
    }

    [Fact]
    public void Movimiento_NodoNulo_Lanza()
    {
        ListaCircularDoble<int> lista = Crear(3);

        Assert.Throws<ArgumentNullException>(() => lista.Avanzar(null!, 1));
        Assert.Throws<ArgumentNullException>(() => lista.Retroceder(null!, 1));
        Assert.Throws<ArgumentNullException>(() => lista.IndiceDe(null!));
    }

    [Fact]
    public void Movimiento_NodoDeOtraLista_Lanza()
    {
        ListaCircularDoble<int> lista = Crear(3);
        NodoCircularDoble<int> ajeno = Crear(3).Cabeza!;

        Assert.Throws<ArgumentException>(() => lista.Avanzar(ajeno, 1));
        Assert.Throws<ArgumentException>(() => lista.Retroceder(ajeno, 1));
    }

    [Fact]
    public void IndiceDe_DevuelveLaPosicion()
    {
        ListaCircularDoble<int> lista = Crear(24);

        for (int i = 0; i < 24; i++)
        {
            Assert.Equal(i, lista.IndiceDe(lista.ObtenerNodo(i)));
        }
    }

    [Fact]
    public void IndiceDe_NodoDeOtraLista_DevuelveMenosUno()
    {
        ListaCircularDoble<int> lista = Crear(3);

        Assert.Equal(-1, lista.IndiceDe(Crear(3).Cabeza!));
    }

    [Fact]
    public void Recorrer_DelegadoNulo_Lanza()
    {
        Assert.Throws<ArgumentNullException>(() => Crear(2).Recorrer(null!));
    }
}
