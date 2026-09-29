using System;
using Monopoly.Core.Modelo;
using Xunit;

namespace Monopoly.Tests.Modelo;

public class PruebasMazos
{
    public static TheoryData<string> Mazos => new TheoryData<string> { "Casualidad", "Arca Comunal" };

    private static MazoCartas Crear(string nombre)
    {
        CartaEvento[] cartas = nombre == "Casualidad" ? CartasClasicas.CrearCasualidad() : CartasClasicas.CrearArcaComunal();
        return new MazoCartas(nombre, cartas);
    }

    private static CartaEvento[] Orden(MazoCartas mazo)
    {
        CartaEvento[] orden = new CartaEvento[mazo.Cantidad];
        int i = 0;
        mazo.Recorrer(carta => orden[i++] = carta);
        return orden;
    }

    [Theory]
    [MemberData(nameof(Mazos))]
    public void TieneAlMenosOchoCartasYCubreLosSeisTipos(string nombre)
    {
        MazoCartas mazo = Crear(nombre);
        bool[] tipos = new bool[Enum.GetValues<TipoCartaEvento>().Length];

        mazo.Recorrer(carta => tipos[(int)carta.Tipo] = true);

        Assert.True(mazo.Cantidad >= 8);
        Assert.True(tipos[(int)TipoCartaEvento.RecibirDinero]);
        Assert.True(tipos[(int)TipoCartaEvento.PagarDinero]);
        Assert.True(tipos[(int)TipoCartaEvento.Avanzar]);
        Assert.True(tipos[(int)TipoCartaEvento.Retroceder]);
        Assert.True(tipos[(int)TipoCartaEvento.PerderTurno]);
        Assert.True(tipos[(int)TipoCartaEvento.IrACasilla]);
    }

    [Theory]
    [MemberData(nameof(Mazos))]
    public void Sacar_DevuelveElFrenteYLoPasaAlFinal(string nombre)
    {
        MazoCartas mazo = Crear(nombre);
        CartaEvento[] orden = Orden(mazo);

        CartaEvento sacada = mazo.Sacar();

        Assert.Same(orden[0], sacada);
        Assert.Same(orden[1], mazo.VerSiguiente());
        Assert.Equal(orden.Length, mazo.Cantidad);
        CartaEvento[] despues = Orden(mazo);
        Assert.Same(sacada, despues[despues.Length - 1]);
    }

    [Theory]
    [MemberData(nameof(Mazos))]
    public void Sacar_RotaCiclicamenteTodoElMazo(string nombre)
    {
        MazoCartas mazo = Crear(nombre);
        CartaEvento[] orden = Orden(mazo);

        for (int vuelta = 0; vuelta < 2; vuelta++)
        {
            for (int i = 0; i < orden.Length; i++)
            {
                Assert.Same(orden[i], mazo.Sacar());
            }
        }

        Assert.Same(orden[0], mazo.VerSiguiente());
    }

    [Fact]
    public void Recorrer_NoAlteraElMazo()
    {
        MazoCartas mazo = Crear("Casualidad");
        CartaEvento frente = mazo.VerSiguiente();

        Orden(mazo);

        Assert.Same(frente, mazo.VerSiguiente());
    }

    [Theory]
    [MemberData(nameof(Mazos))]
    public void Barajar_ConservaExactamenteLasMismasCartas(string nombre)
    {
        MazoCartas mazo = Crear(nombre);
        CartaEvento[] antes = Orden(mazo);

        mazo.Barajar(new Random(7));
        CartaEvento[] despues = Orden(mazo);

        Assert.Equal(antes.Length, despues.Length);
        foreach (CartaEvento carta in antes)
        {
            int apariciones = 0;
            foreach (CartaEvento otra in despues)
            {
                if (ReferenceEquals(carta, otra))
                {
                    apariciones++;
                }
            }

            Assert.Equal(1, apariciones);
        }
    }

    [Fact]
    public void Barajar_MismaSemillaMismoOrden_DistintaSemillaCambiaElOrden()
    {
        CartaEvento[] cartas = CartasClasicas.CrearCasualidad();
        MazoCartas a = new MazoCartas("A", cartas);
        MazoCartas b = new MazoCartas("B", cartas);
        MazoCartas c = new MazoCartas("C", cartas);

        a.Barajar(new Random(1));
        b.Barajar(new Random(1));
        c.Barajar(new Random(2));

        string ordenA = string.Join("|", (object[])Orden(a));
        Assert.Equal(ordenA, string.Join("|", (object[])Orden(b)));
        Assert.NotEqual(ordenA, string.Join("|", (object[])Orden(c)));
    }

    [Fact]
    public void Juego_BarajaAmbosMazosAlIniciar()
    {
        Assert.Equal(CartasClasicas.CrearCasualidad().Length, Fabrica.CrearJuego().MazoCasualidad.Cantidad);
        Assert.Equal(CartasClasicas.CrearArcaComunal().Length, Fabrica.CrearJuego().MazoArcaComunal.Cantidad);
    }

    [Fact]
    public void MazoVacio_Lanza()
    {
        Assert.Throws<ArgumentException>(() => new MazoCartas("Vacío", new CartaEvento[0]));
    }

    [Fact]
    public void CartaInvalida_Lanza()
    {
        Assert.Throws<ArgumentException>(() => new CartaEvento(" ", TipoCartaEvento.PagarDinero, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CartaEvento("x", TipoCartaEvento.PagarDinero, -1));
    }
}
