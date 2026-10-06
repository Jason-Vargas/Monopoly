using Monopoly.Core.Hardware;
using Xunit;

namespace Monopoly.Tests.Hardware;

public class PruebasOrdenPuertos
{
    [Fact]
    public void OrdenarPorInsercion_OrdenaSinDistinguirMayusculas()
    {
        string[] puertos = { "COM7", "com3", "COM10", "COM1", "COM3" };

        CajeroPico.OrdenarPorInsercion(puertos);

        Assert.Equal(new[] { "COM1", "COM10", "com3", "COM3", "COM7" }, puertos);
    }

    [Fact]
    public void OrdenarPorInsercion_AceptaArreglosVaciosYDeUnElemento()
    {
        string[] vacio = new string[0];
        string[] uno = { "COM4" };

        CajeroPico.OrdenarPorInsercion(vacio);
        CajeroPico.OrdenarPorInsercion(uno);

        Assert.Empty(vacio);
        Assert.Equal("COM4", uno[0]);
    }
}
