using System;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;
using Monopoly.Tests.Modelo;
using Xunit;

namespace Monopoly.Tests.Red;

public class PruebasProtocolo
{
    [Fact]
    public void Codificar_SinCampos()
    {
        Assert.Equal("TIRAR_DADOS", Protocolo.Codificar(Protocolo.TirarDados));
    }

    [Fact]
    public void Codificar_EscapaSeparadorBarraYSaltosDeLinea()
    {
        string linea = Protocolo.Codificar("EVENTO", "a|b\\c\nd\re");

        Assert.Equal("EVENTO|a\\|b\\\\c\\nd\\re", linea);
        Assert.DoesNotContain("\n", linea);
    }

    [Theory]
    [InlineData("texto normal")]
    [InlineData("con | separador")]
    [InlineData("barra \\ al medio y al final \\")]
    [InlineData("\\|\\n literal")]
    [InlineData("línea 1\nlínea 2\r\n")]
    [InlineData("")]
    [InlineData("ñandú, Báltica, $1,500")]
    public void CodificarYDecodificar_SonInversos(string texto)
    {
        Mensaje mensaje = Protocolo.Decodificar(Protocolo.Codificar("EVENTO", texto, "segundo"));

        Assert.Equal("EVENTO", mensaje.Comando);
        Assert.Equal(2, mensaje.CantidadCampos);
        Assert.Equal(texto, mensaje.Campo(0));
        Assert.Equal("segundo", mensaje.Campo(1));
    }

    [Fact]
    public void Decodificar_CamposVaciosYComandoEnMinusculas()
    {
        Mensaje mensaje = Protocolo.Decodificar("consultar_transacciones||");

        Assert.Equal(Protocolo.ConsultarTransacciones, mensaje.Comando);
        Assert.Equal(2, mensaje.CantidadCampos);
        Assert.Equal("", mensaje.Campo(0));
        Assert.Null(mensaje.EnteroOpcional(1));
    }

    [Fact]
    public void Decodificar_LineaInvalida_Lanza()
    {
        Assert.Throws<FormatException>(() => Protocolo.Decodificar(""));
        Assert.Throws<FormatException>(() => Protocolo.Decodificar("|campo"));
        Assert.Throws<FormatException>(() => Protocolo.Decodificar("DADOS|1").Campo(3));
        Assert.Throws<FormatException>(() => Protocolo.Decodificar("DADOS|x").Entero(0));
        Assert.Throws<FormatException>(() => Protocolo.Decodificar("ESTADO|Volando").Enumeracion<EstadoPartida>(0));
    }

    [Fact]
    public void Estado_IdaYVuelta()
    {
        EstadoJugador ana = new EstadoJugador(1, "Ana | la del sombrero", ColorFicha.Rojo, 1440, 3, true, 0, 1500, new[] { 3, 5 }, true);
        EstadoJugador beto = new EstadoJugador(2, "Beto", ColorFicha.Azul, 0, 10, false, 1, 0, new int[0], false);
        InstantaneaJuego instantanea = new InstantaneaJuego(EstadoPartida.EnCurso, FaseTurno.EsperandoPago, 7, 100, 1, null, null, 1,
            "Ana debe pagar $200 al banco", 200, new TiradaDados(1, 2), new[] { ana, beto }, 42, 5);
        EstadoRed original = new EstadoRed(instantanea, 1, new[] { 1, 2, 3 });

        string linea = SerializadorEstado.Codificar(original);
        EstadoRed copia = SerializadorEstado.Decodificar(Protocolo.Decodificar(linea));

        InstantaneaJuego i = copia.Instantanea;
        Assert.Equal((EstadoPartida.EnCurso, FaseTurno.EsperandoPago, 7, 100, 1), (i.Estado, i.Fase, i.NumeroTurno, i.MaximoTurnos, i.IdJugadorEnTurno));
        Assert.Null(i.IdGanador);
        Assert.Null(i.IdPropiedadEnVenta);
        Assert.Equal((1, 200, "Ana debe pagar $200 al banco"), (i.IdDeudor, i.MontoPagoPendiente, i.DescripcionPagoPendiente));
        Assert.Equal(new TiradaDados(1, 2), i.UltimaTirada);
        Assert.Equal((42, 5), (i.CantidadEventos, i.CantidadTransacciones));
        Assert.Equal(new[] { 1, 2, 3 }, copia.CasillasRecorridas);
        Assert.Equal(1, copia.IdJugadorUltimoMovimiento);
        Assert.Equal(1, copia.PropietarioDe(3));
        Assert.Equal(1, copia.PropietarioDe(5));
        Assert.Null(copia.PropietarioDe(1));

        Assert.Equal(2, i.Jugadores.Length);
        EstadoJugador a = copia.BuscarJugador(1)!;
        Assert.Equal(("Ana | la del sombrero", ColorFicha.Rojo, 1440, 3, true, 0, 1500, true),
            (a.Nombre, a.ColorFicha, a.Saldo, a.Posicion, a.Activo, a.TurnosPorPerder, a.Patrimonio, a.TieneTarjetaFisica));
        Assert.Equal(new[] { 3, 5 }, a.IdsPropiedades);
        EstadoJugador b = copia.BuscarJugador(2)!;
        Assert.Equal((false, 1, 10), (b.Activo, b.TurnosPorPerder, b.Posicion));
        Assert.Empty(b.IdsPropiedades);
    }

    [Fact]
    public void Estado_SinTiradaNiJugadores()
    {
        InstantaneaJuego instantanea = new InstantaneaJuego(EstadoPartida.EsperandoJugadores, FaseTurno.EsperandoDados, 0, 100,
            null, null, null, null, null, 0, null, new EstadoJugador[0], 0, 0);

        EstadoRed copia = SerializadorEstado.Decodificar(Protocolo.Decodificar(SerializadorEstado.Codificar(new EstadoRed(instantanea, null, new int[0]))));

        Assert.Null(copia.Instantanea.UltimaTirada);
        Assert.Null(copia.IdJugadorUltimoMovimiento);
        Assert.Empty(copia.CasillasRecorridas);
        Assert.Empty(copia.Instantanea.Jugadores);
    }

    [Fact]
    public void Estado_CantidadDeCamposIncorrecta_Lanza()
    {
        Assert.Throws<FormatException>(() => SerializadorEstado.Decodificar(Protocolo.Decodificar("ESTADO|EnCurso")));
        Assert.Throws<FormatException>(() => SerializadorEstado.Decodificar(Protocolo.Decodificar("EVENTO|hola")));
    }

    [Fact]
    public void Transacciones_IdaYVuelta()
    {
        HistorialTransacciones historial = new HistorialTransacciones(() => Fabrica.FechaFija);
        historial.Registrar(1, TipoTransaccion.CompraPropiedad, "Ana", Transaccion.Banco, 60, "Compra de Avenida Báltica | lote 3");
        historial.Registrar(2, TipoTransaccion.PagoAlquiler, "Beto", "Ana", 4, "Alquiler");
        ListaSimple<Transaccion> lista = new ListaSimple<Transaccion>();
        historial.RecorrerDesdeMasReciente(lista.AgregarAlFinal);

        Mensaje mensaje = Protocolo.Decodificar(SerializadorTransacciones.Codificar("RECIENTES", lista));
        ListaSimple<Transaccion> copia = SerializadorTransacciones.Decodificar(mensaje);

        Assert.Equal("RECIENTES", mensaje.Campo(0));
        Assert.Equal(2, copia.Cantidad);
        Transaccion primera = copia.Obtener(0);
        Assert.Equal((2, TipoTransaccion.PagoAlquiler, "Beto", "Ana", 4, 2), (primera.Id, primera.Tipo, primera.Origen, primera.Destino, primera.Monto, primera.NumeroTurno));
        Assert.Equal(Fabrica.FechaFija, primera.FechaHora);
        Assert.Equal("Compra de Avenida Báltica | lote 3", copia.Obtener(1).Descripcion);
    }
}
