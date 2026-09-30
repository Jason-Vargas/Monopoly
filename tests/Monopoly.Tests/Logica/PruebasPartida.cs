using System.IO;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Xunit;

namespace Monopoly.Tests.Logica;

/// <summary>
/// Flujo normal de la partida: compra, alquiler con tarjeta, Salida, pérdida de turno, eliminación y fin.
/// </summary>
public class PruebasPartida : PartidaDePrueba
{
    [Fact]
    public void Inicio_TodosEnSalidaYTurnoDelOrganizador()
    {
        Crear(4, new[] { 1, 2 });

        InstantaneaJuego estado = Estado();

        Assert.Equal(EstadoPartida.EnCurso, estado.Estado);
        Assert.Equal(FaseTurno.EsperandoDados, estado.Fase);
        Assert.Equal(1, estado.NumeroTurno);
        Assert.Equal(Ana, estado.IdJugadorEnTurno);
        Assert.Equal(4, estado.Jugadores.Length);
        foreach (EstadoJugador jugador in estado.Jugadores)
        {
            Assert.Equal(0, jugador.Posicion);
            Assert.Equal(1500, jugador.Saldo);
            Assert.True(jugador.Activo);
            Assert.False(jugador.TieneTarjetaFisica);
        }

        Assert.Equal(new[] { ColorFicha.Rojo, ColorFicha.Azul, ColorFicha.Verde, ColorFicha.Amarillo },
            new[] { estado.Jugadores[0].ColorFicha, estado.Jugadores[1].ColorFicha, estado.Jugadores[2].ColorFicha, estado.Jugadores[3].ColorFicha });
    }

    [Fact]
    public void Compra_DePropiedadLibre()
    {
        Crear(2, new[] { 1, 2 });

        ResultadoAccion tirada = Ok(Juego.TirarDados(Ana));

        Assert.Equal(3, tirada.Tirada!.Value.Total);
        Assert.Equal(1, tirada.Movimientos.Cantidad);
        Assert.Equal(3, Posicion(Ana));
        Assert.Equal(FaseTurno.EsperandoDecisionCompra, Estado().Fase);
        Assert.Equal(3, Estado().IdPropiedadEnVenta);

        Ok(Juego.ComprarPropiedad(Ana));

        Assert.Equal(1440, Saldo(Ana));
        Assert.Same(J(Ana), Juego.Tablero.BuscarPropiedad(3)!.Propietario);
        Assert.Equal(new[] { 3 }, Estado().Jugadores[0].IdsPropiedades);
        Assert.Equal(FaseTurno.PuedeTerminar, Estado().Fase);
        Transaccion compra = UltimaTransaccion();
        Assert.Equal((TipoTransaccion.CompraPropiedad, "Ana", Transaccion.Banco, 60, 1),
            (compra.Tipo, compra.Origen, compra.Destino, compra.Monto, compra.NumeroTurno));

        Ok(Juego.TerminarTurno(Ana));

        Assert.Equal(Beto, EnTurno());
        Assert.Equal(2, Estado().NumeroTurno);
    }

    [Fact]
    public void Alquiler_SeCobraSoloConLaTarjetaDelDeudor()
    {
        Crear(2, new[] { 1, 2 }, iniciar: false);
        Ok(Juego.VincularTarjeta(Ana, "a1 b2 c3 d4"));
        Ok(Juego.IniciarPartida(Ana));
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.ComprarPropiedad(Ana));
        Ok(Juego.TerminarTurno(Ana));

        Ok(Juego.TirarDados(Beto));

        InstantaneaJuego estado = Estado();
        Assert.Equal(FaseTurno.EsperandoPago, estado.Fase);
        Assert.Equal(Beto, estado.IdDeudor);
        Assert.Equal(4, estado.MontoPagoPendiente);
        Assert.Equal("Beto debe pagar $4 a Ana", estado.DescripcionPagoPendiente);
        Rechazada(Juego.TerminarTurno(Beto), "pago pendiente");

        Rechazada(Juego.IdentificarTarjeta("A1B2C3D4"), "pertenece a Ana; se espera la tarjeta de Beto");
        Rechazada(Juego.IdentificarTarjeta("FFFFFFFF"), "no está registrada");
        Assert.Equal((1440, 1500), (Saldo(Ana), Saldo(Beto)));
        Assert.Equal(FaseTurno.EsperandoPago, Estado().Fase);

        Ok(Juego.IdentificarTarjeta(Uid(Beto)));

        Assert.Equal((1444, 1496), (Saldo(Ana), Saldo(Beto)));
        Transaccion alquiler = UltimaTransaccion();
        Assert.Equal((TipoTransaccion.PagoAlquiler, "Beto", "Ana", 4),
            (alquiler.Tipo, alquiler.Origen, alquiler.Destino, alquiler.Monto));
        Assert.Equal(FaseTurno.PuedeTerminar, Estado().Fase);
        Assert.True(Estado().Jugadores[0].TieneTarjetaFisica);
        Ok(Juego.TerminarTurno(Beto));
    }

    [Fact]
    public void Alquiler_ConTarjetaFisicaDelDeudor()
    {
        Crear(2, new[] { 1, 2 }, iniciar: false);
        Ok(Juego.VincularTarjeta(Beto, "0A1B2C3D"));
        Ok(Juego.IniciarPartida(Ana));
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.ComprarPropiedad(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));

        Rechazada(Juego.IdentificarTarjeta(Uid(Beto)), "no está registrada");
        Ok(Juego.IdentificarTarjeta("0a 1b 2c 3d"));

        Assert.Equal(1496, Saldo(Beto));
    }

    [Fact]
    public void PropiedadPropia_NoPagaNada()
    {
        Crear(2, new[] { 1, 2, 3, 3, 2, 2 }, casualidad: Carta(TipoCartaEvento.IrACasilla, 3));
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.ComprarPropiedad(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));
        Ok(Juego.NoComprar(Beto));
        Ok(Juego.TerminarTurno(Beto));
        int transacciones = Juego.Historial.Cantidad;

        Ok(Juego.TirarDados(Ana));

        Assert.Equal(3, Posicion(Ana));
        Assert.Equal(FaseTurno.PuedeTerminar, Estado().Fase);
        Assert.Equal(1440 + 200, Saldo(Ana));
        Assert.Equal(transacciones + 1, Juego.Historial.Cantidad);
        Assert.Equal(TipoTransaccion.PremioInicio, UltimaTransaccion().Tipo);
    }

    [Fact]
    public void PasarPorSalida_ConLosDados_CobraPremio()
    {
        Crear(2, new[] { 3, 4, 1, 2, 2, 2 }, casualidad: Carta(TipoCartaEvento.IrACasilla, 39));
        Ok(Juego.TirarDados(Ana));
        Assert.Equal(39, Posicion(Ana));
        Ok(Juego.NoComprar(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));
        Ok(Juego.NoComprar(Beto));
        Ok(Juego.TerminarTurno(Beto));

        ResultadoAccion resultado = Ok(Juego.TirarDados(Ana));

        Assert.Equal(3, Posicion(Ana));
        Assert.Equal(1700, Saldo(Ana));
        Assert.True(resultado.Movimientos.Obtener(0).PasoPorSalida);
        Transaccion premio = UltimaTransaccion();
        Assert.Equal((TipoTransaccion.PremioInicio, Transaccion.Banco, "Ana", 200),
            (premio.Tipo, premio.Origen, premio.Destino, premio.Monto));
    }

    [Fact]
    public void PerderTurno_LaColaSaltaAlJugador()
    {
        Crear(2, new[] { 3, 4, 1, 1 }, casualidad: Carta(TipoCartaEvento.PerderTurno, 1));
        Ok(Juego.TirarDados(Ana));
        Assert.Equal(1, J(Ana).TurnosPorPerder);
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));
        Ok(Juego.TerminarTurno(Beto));

        Assert.Equal(Beto, EnTurno());
        Assert.Equal(4, Estado().NumeroTurno);
        Assert.Equal(0, J(Ana).TurnosPorPerder);
        Assert.Contains("Turno 3: Ana pierde este turno.", Eventos());
        Rechazada(Juego.TirarDados(Ana), "No es el turno de Ana");
    }

    [Fact]
    public void VayaALaCarcel_MueveALaCarcelSinCobrarSalidaYPierdeTurno()
    {
        Crear(2, new[] { 3, 4 }, casualidad: Carta(TipoCartaEvento.IrACasilla, 30));

        ResultadoAccion resultado = Ok(Juego.TirarDados(Ana));

        Assert.Equal(Tablero.IndiceCarcel, Posicion(Ana));
        Assert.Equal(1, J(Ana).TurnosPorPerder);
        Assert.Equal(1500, Saldo(Ana));
        Assert.Equal(3, resultado.Movimientos.Cantidad);
        Assert.True(resultado.Movimientos.Obtener(2).CasillasRecorridas.EstaVacia);
        Assert.Equal(FaseTurno.PuedeTerminar, Estado().Fase);
    }

    [Fact]
    public void Eliminacion_PagaLoQueTieneLiberaPropiedadesYSaleDeLaCola()
    {
        Crear(3, new[] { 3, 3, 1, 2, 3, 3, 2, 2, 1, 3 }, casualidad: Carta(TipoCartaEvento.PagarDinero, 100), saldo: 100);
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.NoComprar(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));
        Ok(Juego.ComprarPropiedad(Beto));
        Ok(Juego.TerminarTurno(Beto));
        Ok(Juego.TirarDados(Carla));
        Ok(Juego.NoComprar(Carla));
        Ok(Juego.TerminarTurno(Carla));
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));
        Assert.Equal(FaseTurno.EsperandoPago, Estado().Fase);
        Assert.Equal(40, Saldo(Beto));

        Ok(Juego.IdentificarTarjeta(Uid(Beto)));

        Assert.False(J(Beto).Activo);
        Assert.Equal(0, Saldo(Beto));
        Assert.Equal(0, J(Beto).CantidadPropiedades);
        Assert.True(Juego.Tablero.BuscarPropiedad(3)!.EstaDisponible);
        Transaccion parcial = UltimaTransaccion();
        Assert.Equal((TipoTransaccion.PerdidaEvento, "Beto", Transaccion.Banco, 40),
            (parcial.Tipo, parcial.Origen, parcial.Destino, parcial.Monto));
        Assert.Contains("pago parcial", parcial.Descripcion);

        InstantaneaJuego estado = Estado();
        Assert.Equal(EstadoPartida.EnCurso, estado.Estado);
        Assert.Equal(Carla, estado.IdJugadorEnTurno);
        Assert.Equal(6, estado.NumeroTurno);
        Assert.Equal(FaseTurno.EsperandoDados, estado.Fase);
        Rechazada(Juego.TirarDados(Beto), "Beto fue eliminado");
        Rechazada(Juego.VincularTarjeta(Beto, "1234"), "eliminado");

        Ok(Juego.TirarDados(Carla));
        Assert.Equal(12, Posicion(Carla));
        Ok(Juego.NoComprar(Carla));
        Ok(Juego.TerminarTurno(Carla));
        Assert.Equal(Ana, EnTurno());
        Assert.Equal(7, Estado().NumeroTurno);
    }

    [Fact]
    public void Fin_CuandoQuedaUnSoloJugador_ExportaElTxt()
    {
        Crear(2, new[] { 3, 3, 1, 2, 2, 2, 1, 3 }, casualidad: Carta(TipoCartaEvento.PagarDinero, 100), saldo: 100);
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.NoComprar(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));
        Ok(Juego.ComprarPropiedad(Beto));
        Ok(Juego.TerminarTurno(Beto));
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));

        Ok(Juego.IdentificarTarjeta(Uid(Beto)));

        InstantaneaJuego estado = Estado();
        Assert.Equal(EstadoPartida.Finalizada, estado.Estado);
        Assert.Equal(Ana, estado.IdGanador);
        Assert.Null(estado.IdJugadorEnTurno);
        Assert.Contains("único jugador activo", Eventos());

        string ruta = Juego.RutaHistorialExportado!;
        Assert.True(File.Exists(ruta));
        Assert.Equal(Path.GetFullPath(Carpeta), Path.GetDirectoryName(ruta));
        string contenido = File.ReadAllText(ruta);
        Assert.Contains("HISTORIAL DE TRANSACCIONES", contenido);
        Assert.Contains("Beto (ficha Azul): saldo $0", contenido);
        Assert.Contains("eliminado", contenido);
        Assert.Contains("Pérdida por evento", contenido);

        Rechazada(Juego.TirarDados(Ana), "ya terminó");
        Rechazada(Juego.IdentificarTarjeta(Uid(Ana)), "no está en curso");
    }

    [Fact]
    public void Fin_PorLimiteDeTurnos_GanaElMayorPatrimonio()
    {
        Crear(2, new[] { 1, 2, 1, 3 }, maximoTurnos: 2);
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.ComprarPropiedad(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));
        Ok(Juego.IdentificarTarjeta(Uid(Beto)));

        ResultadoAccion fin = Ok(Juego.TerminarTurno(Beto));

        Assert.Contains("Ganador: Ana", fin.Mensaje);
        InstantaneaJuego estado = Estado();
        Assert.Equal(EstadoPartida.Finalizada, estado.Estado);
        Assert.Equal(Ana, estado.IdGanador);
        Assert.Equal(1500, estado.Jugadores[0].Patrimonio);
        Assert.Equal(1300, estado.Jugadores[1].Patrimonio);
        Assert.Contains("máximo de 2 turnos", Eventos());

        string ruta = Juego.RutaHistorialExportado!;
        Assert.Equal("partida_2026-09-29_15-30-00.txt", Path.GetFileName(ruta));
        string contenido = File.ReadAllText(ruta);
        Assert.Contains("Compra de propiedad", contenido);
        Assert.Contains("Pago al banco", contenido);
        Assert.Contains("Total de transacciones: 2", contenido);
    }

    [Fact]
    public void Fin_PorLimite_ContandoTurnosPerdidos()
    {
        Crear(2, new[] { 3, 4, 1, 1 }, casualidad: Carta(TipoCartaEvento.PerderTurno, 1), maximoTurnos: 3);
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.TerminarTurno(Ana));
        Ok(Juego.TirarDados(Beto));

        Ok(Juego.TerminarTurno(Beto));

        Assert.Equal(EstadoPartida.Finalizada, Juego.Estado);
        Assert.Equal(3, Juego.NumeroTurno);
    }

    [Fact]
    public void ExportarHistorial_EnCualquierMomento()
    {
        Crear(2, new[] { 1, 2 });
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.ComprarPropiedad(Ana));

        ResultadoAccion primera = Ok(Juego.ExportarHistorial());
        ResultadoAccion segunda = Ok(Juego.ExportarHistorial());

        Assert.True(File.Exists(primera.Mensaje));
        Assert.EndsWith("_2.txt", segunda.Mensaje);
        Assert.Contains("Compra de Avenida Báltica", File.ReadAllText(primera.Mensaje));
    }

    [Fact]
    public void RegistroDeEventos_LegibleEIncremental()
    {
        Crear(2, new[] { 1, 2 });
        int antes = Juego.ObtenerEventosDesde(1).Length;

        Ok(Juego.TirarDados(Ana));
        EventoJuego[] nuevos = Juego.ObtenerEventosDesde(antes + 1);

        Assert.NotEmpty(nuevos);
        Assert.Equal(antes + 1, nuevos[0].Numero);
        Assert.Contains("Ana lanzó los dados: 1 + 2 = 3.", nuevos[0].Texto);
        Assert.Contains("Avenida Báltica", Eventos());
        Assert.StartsWith("[15:30:00] T1 ", nuevos[0].ToString());
        Assert.Contains("Ana se unió a la partida", Eventos());
    }

    [Fact]
    public void ConsultarTransacciones_DevuelveLaTabla()
    {
        Crear(2, new[] { 1, 2 });
        Ok(Juego.TirarDados(Ana));
        Ok(Juego.ComprarPropiedad(Ana));

        ResultadoAccion tabla = Ok(Juego.ConsultarTransacciones(Beto));

        Assert.Contains("Compra de propiedad", tabla.Mensaje);
        Rechazada(Juego.ConsultarTransacciones(9), "No existe el jugador 9");
    }
}
