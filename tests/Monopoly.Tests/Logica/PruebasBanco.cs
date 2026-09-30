using System;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Tests.Modelo;
using Xunit;

namespace Monopoly.Tests.Logica;

public class PruebasBanco
{
    private readonly HistorialTransacciones _historial = new HistorialTransacciones(() => Fabrica.FechaFija);
    private readonly Banco _banco;
    private readonly Jugador _ana = Fabrica.CrearJugador("Ana", 1);
    private readonly Jugador _beto = Fabrica.CrearJugador("Beto", 2);

    public PruebasBanco()
    {
        _banco = new Banco(_historial);
    }

    [Fact]
    public void Pagar_ElBancoEsOrigen()
    {
        Transaccion t = _banco.Pagar(_ana, 200, TipoTransaccion.PremioInicio, 3, "Salida");

        Assert.Equal(1700, _ana.Saldo);
        Assert.Equal((Transaccion.Banco, "Ana", 200, 3, TipoTransaccion.PremioInicio), (t.Origen, t.Destino, t.Monto, t.NumeroTurno, t.Tipo));
        Assert.Equal(1, _historial.Cantidad);
    }

    [Fact]
    public void Cobrar_ElBancoEsDestino()
    {
        Transaccion t = _banco.Cobrar(_ana, 100, TipoTransaccion.PagoBanco, 1, "Impuesto de Lujo");

        Assert.Equal(1400, _ana.Saldo);
        Assert.Equal(("Ana", Transaccion.Banco), (t.Origen, t.Destino));
    }

    [Fact]
    public void Transferir_EntreJugadores()
    {
        Transaccion t = _banco.Transferir(_beto, _ana, 50, TipoTransaccion.PagoAlquiler, 2, "Alquiler");

        Assert.Equal((1550, 1450), (_ana.Saldo, _beto.Saldo));
        Assert.Equal(("Beto", "Ana"), (t.Origen, t.Destino));
    }

    [Fact]
    public void SaldoInsuficiente_LanzaSinModificarNiRegistrar()
    {
        Assert.Throws<InvalidOperationException>(() => _banco.Cobrar(_ana, 1501, TipoTransaccion.PagoBanco, 1, "x"));
        Assert.Throws<InvalidOperationException>(() => _banco.Transferir(_ana, _beto, 1501, TipoTransaccion.PagoAlquiler, 1, "x"));

        Assert.Equal((1500, 1500), (_ana.Saldo, _beto.Saldo));
        Assert.Equal(0, _historial.Cantidad);
    }

    [Fact]
    public void OperacionesInvalidas_LanzanSinModificar()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _banco.Pagar(_ana, 0, TipoTransaccion.PremioInicio, 1, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _banco.Cobrar(_ana, -5, TipoTransaccion.PagoBanco, 1, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _banco.Pagar(_ana, 10, TipoTransaccion.PremioInicio, -1, "x"));
        Assert.Throws<ArgumentException>(() => _banco.Transferir(_ana, _ana, 10, TipoTransaccion.PagoEntreJugadores, 1, "x"));
        Assert.Throws<ArgumentNullException>(() => _banco.Pagar(null!, 10, TipoTransaccion.PremioInicio, 1, "x"));

        Assert.Equal(1500, _ana.Saldo);
        Assert.Equal(0, _historial.Cantidad);
    }
}
