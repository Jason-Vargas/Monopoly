using System;
using System.IO;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Modelo;
using Monopoly.Tests.Estructuras;
using Xunit;

namespace Monopoly.Tests.Modelo;

public class PruebasHistorial
{
    private readonly HistorialTransacciones _historial = new HistorialTransacciones(() => Fabrica.FechaFija);

    private void RegistrarPartidaDeEjemplo()
    {
        _historial.Registrar(1, TipoTransaccion.CompraPropiedad, "Ana", Transaccion.Banco, 60, "Compra de Avenida Mediterráneo");
        _historial.Registrar(2, TipoTransaccion.PagoAlquiler, "Beto", "Ana", 2, "Alquiler de Avenida Mediterráneo");
        _historial.Registrar(3, TipoTransaccion.PremioInicio, Transaccion.Banco, "Carla", 200, "Pasó por la Salida");
        _historial.Registrar(4, TipoTransaccion.PagoBanco, "Beto", Transaccion.Banco, 200, "Impuesto sobre la Renta");
        _historial.Registrar(5, TipoTransaccion.CompraPropiedad, "Carla", Transaccion.Banco, 400, "Compra de Paseo Marítimo");
    }

    private static string Ids(Action<Action<Transaccion>> recorrido)
    {
        return AyudaPruebas.Texto<int>(visitar => recorrido(t => visitar(t.Id)));
    }

    private static string Ids(ListaDobleEnlazada<Transaccion> lista)
    {
        return Ids(lista.RecorrerDesdeInicio);
    }

    [Fact]
    public void HistorialNuevo_EstaVacio()
    {
        Assert.Equal(0, _historial.Cantidad);
        Assert.Equal("", Ids(_historial.RecorrerDesdeMasAntigua));
        Assert.Contains("(sin transacciones)", _historial.ImprimirTodas());
    }

    [Fact]
    public void Registrar_AsignaNumeroConsecutivoYFecha()
    {
        Transaccion primera = _historial.Registrar(1, TipoTransaccion.PremioInicio, Transaccion.Banco, "Ana", 200, "Salida");
        Transaccion segunda = _historial.Registrar(1, TipoTransaccion.PagoBanco, "Ana", Transaccion.Banco, 100, "Impuesto");

        Assert.Equal(1, primera.Id);
        Assert.Equal(2, segunda.Id);
        Assert.Equal(Fabrica.FechaFija, primera.FechaHora);
        Assert.Equal(2, _historial.Cantidad);
    }

    [Fact]
    public void Agregar_TransaccionExistente()
    {
        Transaccion transaccion = new Transaccion(7, Fabrica.FechaFija, 3, TipoTransaccion.GananciaEvento, Transaccion.Banco, "Ana", 50, "Dividendo");

        _historial.Agregar(transaccion);

        Assert.Equal(1, _historial.Cantidad);
        Assert.Equal("7", Ids(_historial.RecorrerDesdeMasAntigua));
        Assert.Throws<ArgumentNullException>(() => _historial.Agregar(null!));
    }

    [Fact]
    public void Recorridos_DesdeLaMasAntiguaYDesdeLaMasReciente()
    {
        RegistrarPartidaDeEjemplo();

        Assert.Equal("1,2,3,4,5", Ids(_historial.RecorrerDesdeMasAntigua));
        Assert.Equal("5,4,3,2,1", Ids(_historial.RecorrerDesdeMasReciente));
    }

    [Fact]
    public void BuscarPorJugador_ComoOrigenODestino()
    {
        RegistrarPartidaDeEjemplo();

        Assert.Equal("1,2", Ids(_historial.BuscarPorJugador("Ana")));
        Assert.Equal("2,4", Ids(_historial.BuscarPorJugador("Beto")));
        Assert.Equal("3,5", Ids(_historial.BuscarPorJugador("Carla")));
        Assert.Equal("1,3,4,5", Ids(_historial.BuscarPorJugador(Transaccion.Banco)));
        Assert.True(_historial.BuscarPorJugador("Nadie").EstaVacia);
    }

    [Fact]
    public void BuscarPorTipo()
    {
        RegistrarPartidaDeEjemplo();

        Assert.Equal("1,5", Ids(_historial.BuscarPorTipo(TipoTransaccion.CompraPropiedad)));
        Assert.Equal("2", Ids(_historial.BuscarPorTipo(TipoTransaccion.PagoAlquiler)));
        Assert.True(_historial.BuscarPorTipo(TipoTransaccion.PagoEntreJugadores).EstaVacia);
    }

    [Fact]
    public void Busquedas_ResultadoRecorribleEnAmbosSentidos()
    {
        RegistrarPartidaDeEjemplo();

        ListaDobleEnlazada<Transaccion> beto = _historial.BuscarPorJugador("Beto");

        Assert.Equal("4,2", Ids(beto.RecorrerDesdeFinal));
    }

    [Fact]
    public void ImprimirTodas_MuestraTablaConTodasLasColumnas()
    {
        RegistrarPartidaDeEjemplo();

        string tabla = _historial.ImprimirTodas();
        string[] lineas = tabla.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(7, lineas.Length);
        foreach (string columna in new[] { "N°", "Turno", "Tipo", "Origen", "Destino", "Monto", "Descripción" })
        {
            Assert.Contains(columna, lineas[0]);
        }

        Assert.Contains("Compra de propiedad", lineas[2]);
        Assert.Contains("Premio por pasar por inicio", lineas[4]);
        Assert.Contains("$400", lineas[6]);
        Assert.Contains("Compra de Paseo Marítimo", lineas[6]);
    }

    [Fact]
    public void ExportarTxt_CreaArchivoConEncabezadoYTabla()
    {
        RegistrarPartidaDeEjemplo();
        Tablero tablero = new Tablero();
        ListaSimple<Jugador> jugadores = new ListaSimple<Jugador>();
        Jugador ana = new Jugador(1, "Ana", ColorFicha.Rojo);
        ana.AgregarPropiedad(Fabrica.PropiedadEn(tablero, 1));
        Jugador beto = new Jugador(2, "Beto", ColorFicha.Azul);
        beto.Desactivar();
        jugadores.AgregarAlFinal(ana);
        jugadores.AgregarAlFinal(beto);

        string carpeta = Path.Combine(Path.GetTempPath(), "monopoly-pruebas-" + Guid.NewGuid().ToString("N"));
        string ruta = Path.Combine(carpeta, "partidas", "historial.txt");
        try
        {
            _historial.ExportarTxt(ruta, new DateTime(2026, 9, 29, 14, 0, 0), jugadores);

            Assert.True(File.Exists(ruta));
            string contenido = File.ReadAllText(ruta);
            Assert.Contains("HISTORIAL DE TRANSACCIONES", contenido);
            Assert.Contains("Fecha de la partida: 29/09/2026 14:00:00", contenido);
            Assert.Contains("Ana (ficha Rojo): saldo $1,500, patrimonio $1,560, activo", contenido);
            Assert.Contains("Beto (ficha Azul)", contenido);
            Assert.Contains("eliminado", contenido);
            Assert.Contains("Total de transacciones: 5", contenido);
            Assert.Contains("Descripción", contenido);
            Assert.Contains("Impuesto sobre la Renta", contenido);
            Assert.Contains("Pago al banco", contenido);
        }
        finally
        {
            if (Directory.Exists(carpeta))
            {
                Directory.Delete(carpeta, true);
            }
        }
    }

    [Fact]
    public void Transaccion_DatosInvalidos_Lanzan()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _historial.Registrar(1, TipoTransaccion.PagoBanco, "Ana", Transaccion.Banco, 0, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _historial.Registrar(-1, TipoTransaccion.PagoBanco, "Ana", Transaccion.Banco, 10, "x"));
        Assert.Throws<ArgumentException>(() => _historial.Registrar(1, TipoTransaccion.PagoBanco, "", Transaccion.Banco, 10, "x"));
        Assert.Equal(0, _historial.Cantidad);
    }

    [Fact]
    public void Transaccion_ToStringLegible()
    {
        Transaccion t = _historial.Registrar(3, TipoTransaccion.PagoAlquiler, "Beto", "Ana", 50, "Alquiler");

        Assert.Equal("#1 T3 Pago de alquiler: Beto -> Ana $50 (Alquiler)", t.ToString());
    }
}
