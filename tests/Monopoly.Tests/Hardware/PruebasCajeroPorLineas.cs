using System;
using System.IO;
using Monopoly.Core.Hardware;
using Xunit;

namespace Monopoly.Tests.Hardware;

public class PruebasCajeroPorLineas
{
    private readonly CajeroFalso _cajero = new CajeroFalso();
    private int _botones;
    private string _tarjetas = "";
    private string _errores = "";

    public PruebasCajeroPorLineas()
    {
        _cajero.BotonPresionado += () => _botones++;
        _cajero.TarjetaLeida += uid => _tarjetas += uid + ";";
        _cajero.ErrorDispositivo += detalle => _errores += detalle + ";";
    }

    [Fact]
    public void Boton_DisparaElEvento()
    {
        _cajero.Inyectar("BOTON");
        _cajero.Inyectar("  boton \r");

        Assert.Equal(2, _botones);
    }

    [Theory]
    [InlineData("RFID:A1B2C3D4", "A1B2C3D4")]
    [InlineData("RFID:a1b2c3d4\r", "A1B2C3D4")]
    [InlineData("rfid: 04112233445566 ", "04112233445566")]
    public void Rfid_ValidoSeNormaliza(string linea, string esperado)
    {
        _cajero.Inyectar(linea);

        Assert.Equal(esperado + ";", _tarjetas);
    }

    [Theory]
    [InlineData("RFID:")]
    [InlineData("RFID:ZZZZZZZZ")]
    [InlineData("RFID:A1B2C3")]
    [InlineData("RFID:A1B2C3D4E5")]
    [InlineData("MPY: soft reboot")]
    [InlineData(">>> ")]
    [InlineData("Traceback (most recent call last):")]
    [InlineData("DADOS:3,5")]
    [InlineData("\u0000ÿ###")]
    public void LineasBasura_SeIgnoran(string linea)
    {
        _cajero.Inyectar(linea);

        Assert.Equal(0, _botones);
        Assert.Equal("", _tarjetas);
        Assert.Equal("", _errores);
        Assert.Equal(1, _cajero.LineasIgnoradas);
    }

    [Fact]
    public void LineasVaciasYListo_NoCuentanComoBasura()
    {
        _cajero.Inyectar("");
        _cajero.Inyectar("   \r");
        _cajero.Inyectar("LISTO");
        _cajero.Inyectar(null!);

        Assert.Equal(0, _cajero.LineasIgnoradas);
    }

    [Fact]
    public void Error_SeInforma()
    {
        _cajero.Inyectar("ERROR:RC522 no responde");

        Assert.Equal("RC522 no responde;", _errores);
    }

    [Fact]
    public void MostrarDados_SoloConectado()
    {
        _cajero.MostrarDados(3, 5);
        Assert.Empty(_cajero.Enviadas());

        _cajero.Conectar();
        _cajero.MostrarDados(3, 5);
        _cajero.LimpiarDisplays();

        Assert.Equal(new[] { "DADOS:3,5", "LIMPIAR" }, _cajero.Enviadas());
        Assert.Throws<ArgumentOutOfRangeException>(() => _cajero.MostrarDados(0, 7));
    }

    [Fact]
    public void FalloAlEnviar_PasaADesconectado()
    {
        EstadoCajero? notificado = null;
        _cajero.EstadoCambiado += estado => notificado = estado;
        _cajero.Conectar();
        _cajero.FallarAlEnviar = true;

        _cajero.MostrarDados(1, 1);

        Assert.Equal(EstadoCajero.Desconectado, _cajero.Estado);
        Assert.Equal(EstadoCajero.Desconectado, notificado);
    }

    [Fact]
    public void CajeroSimulado_NoEsFisicoYPuedeSimular()
    {
        CajeroSimulado simulado = new CajeroSimulado();
        int botones = 0;
        string tarjeta = "";
        simulado.BotonPresionado += () => botones++;
        simulado.TarjetaLeida += uid => tarjeta = uid;

        simulado.MostrarDados(3, 4);
        simulado.SimularBoton();
        simulado.SimularTarjeta("A1B2C3D4");

        Assert.False(simulado.EsFisico);
        Assert.Equal(EstadoCajero.Simulado, simulado.Estado);
        Assert.Equal(1, botones);
        Assert.Equal("A1B2C3D4", tarjeta);
    }

    [Fact]
    public void CajeroPico_PuertoInexistente_ErrorClaroYDesconectado()
    {
        using CajeroPico pico = new CajeroPico("COM250");

        IOException error = Assert.Throws<IOException>(() => pico.Conectar(500));

        Assert.Contains("No se pudo abrir COM250", error.Message);
        Assert.Equal(EstadoCajero.Desconectado, pico.Estado);
        Assert.True(pico.EsFisico);
        Assert.Equal("Pico W en COM250", pico.Descripcion);
        pico.MostrarDados(3, 4);
    }

    [Theory]
    [InlineData("A1B2C3D4", true)]
    [InlineData("04112233445566", true)]
    [InlineData("0411223344556677889A", true)]
    [InlineData("a1b2c3d4", false)]
    [InlineData("A1B2C3D", false)]
    [InlineData("VIRTUAL-1", false)]
    public void EsUidValido(string uid, bool esperado)
    {
        Assert.Equal(esperado, ProtocoloCajero.EsUidValido(uid));
    }
}
