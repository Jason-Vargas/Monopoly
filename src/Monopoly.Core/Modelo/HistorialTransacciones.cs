using System;
using System.Globalization;
using System.IO;
using System.Text;
using Monopoly.Core.Estructuras;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Historial de transacciones de la partida, almacenado en una <see cref="ListaDobleEnlazada{T}"/>:
/// agregar al final es O(1) y se puede recorrer desde la más antigua (cabeza) o desde la más reciente (cola).
/// </summary>
public class HistorialTransacciones
{
    private const string FormatoFecha = "dd/MM/yyyy HH:mm:ss";

    private static readonly int[] AnchosColumnas = { 5, 6, 8, 28, 16, 16, 10 };

    private readonly ListaDobleEnlazada<Transaccion> _transacciones = new ListaDobleEnlazada<Transaccion>();
    private readonly Func<DateTime> _reloj;

    /// <summary>
    /// Crea un historial vacío.
    /// </summary>
    /// <param name="reloj">Fuente de la fecha y hora de las transacciones; por defecto, la hora local actual.</param>
    public HistorialTransacciones(Func<DateTime>? reloj = null)
    {
        _reloj = reloj ?? (() => DateTime.Now);
    }

    /// <summary>
    /// Cantidad de transacciones registradas.
    /// </summary>
    public int Cantidad => _transacciones.Cantidad;

    /// <summary>
    /// Crea una transacción con el siguiente número y la hora actual, y la agrega al final.
    /// </summary>
    /// <param name="numeroTurno">Turno en que ocurre.</param>
    /// <param name="tipo">Tipo de transacción.</param>
    /// <param name="origen">Quien paga (nombre o <see cref="Transaccion.Banco"/>).</param>
    /// <param name="destino">Quien recibe (nombre o <see cref="Transaccion.Banco"/>).</param>
    /// <param name="monto">Monto (mayor que 0).</param>
    /// <param name="descripcion">Detalle.</param>
    /// <returns>La transacción registrada.</returns>
    public Transaccion Registrar(int numeroTurno, TipoTransaccion tipo, string origen, string destino, int monto, string descripcion)
    {
        Transaccion transaccion = new Transaccion(Cantidad + 1, _reloj(), numeroTurno, tipo, origen, destino, monto, descripcion);
        _transacciones.AgregarAlFinal(transaccion);
        return transaccion;
    }

    /// <summary>
    /// Agrega una transacción ya creada al final del historial.
    /// </summary>
    /// <param name="transaccion">Transacción a agregar.</param>
    public void Agregar(Transaccion transaccion)
    {
        ArgumentNullException.ThrowIfNull(transaccion);
        _transacciones.AgregarAlFinal(transaccion);
    }

    /// <summary>
    /// Recorre las transacciones de la más antigua a la más reciente.
    /// </summary>
    /// <param name="accion">Acción a ejecutar con cada transacción.</param>
    public void RecorrerDesdeMasAntigua(Action<Transaccion> accion)
    {
        _transacciones.RecorrerDesdeInicio(accion);
    }

    /// <summary>
    /// Recorre las transacciones de la más reciente a la más antigua.
    /// </summary>
    /// <param name="accion">Acción a ejecutar con cada transacción.</param>
    public void RecorrerDesdeMasReciente(Action<Transaccion> accion)
    {
        _transacciones.RecorrerDesdeFinal(accion);
    }

    /// <summary>
    /// Busca las transacciones en las que el jugador (o el banco) es origen o destino.
    /// </summary>
    /// <param name="nombre">Nombre del jugador o <see cref="Transaccion.Banco"/>.</param>
    /// <returns>Una lista nueva con las transacciones encontradas, en orden cronológico.</returns>
    public ListaDobleEnlazada<Transaccion> BuscarPorJugador(string nombre)
    {
        return _transacciones.BuscarTodos(t => t.Involucra(nombre));
    }

    /// <summary>
    /// Busca las transacciones de un tipo.
    /// </summary>
    /// <param name="tipo">Tipo buscado.</param>
    /// <returns>Una lista nueva con las transacciones encontradas, en orden cronológico.</returns>
    public ListaDobleEnlazada<Transaccion> BuscarPorTipo(TipoTransaccion tipo)
    {
        return _transacciones.BuscarTodos(t => t.Tipo == tipo);
    }

    /// <summary>
    /// Genera una tabla de texto con todas las transacciones, de la más antigua a la más reciente.
    /// </summary>
    /// <returns>La tabla con encabezado de columnas.</returns>
    public string ImprimirTodas()
    {
        StringBuilder texto = new StringBuilder();
        EscribirTabla(texto);
        return texto.ToString();
    }

    /// <summary>
    /// Exporta el historial a un archivo TXT con encabezado (fecha de la partida y jugadores)
    /// y la tabla de transacciones. Crea la carpeta si no existe.
    /// </summary>
    /// <param name="ruta">Ruta del archivo a crear o reemplazar.</param>
    /// <param name="fechaPartida">Fecha de inicio de la partida.</param>
    /// <param name="jugadores">Jugadores de la partida.</param>
    public void ExportarTxt(string ruta, DateTime fechaPartida, ListaSimple<Jugador> jugadores)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        ArgumentNullException.ThrowIfNull(jugadores);

        StringBuilder texto = new StringBuilder();
        string separador = new string('=', AnchoTotal());
        texto.AppendLine(separador);
        texto.AppendLine("MONOPOLY DISTRIBUIDO - HISTORIAL DE TRANSACCIONES");
        texto.AppendLine(separador);
        texto.AppendLine("Fecha de la partida: " + fechaPartida.ToString(FormatoFecha, CultureInfo.InvariantCulture));
        texto.AppendLine("Fecha de exportación: " + _reloj().ToString(FormatoFecha, CultureInfo.InvariantCulture));
        texto.AppendLine("Jugadores:");
        jugadores.Recorrer(jugador => texto.AppendLine(
            $"  - {jugador.Nombre} (ficha {jugador.ColorFicha}): saldo {Formato.Dinero(jugador.Saldo)}, " +
            $"patrimonio {Formato.Dinero(jugador.CalcularPatrimonio())}, " +
            (jugador.Activo ? "activo" : "eliminado")));
        texto.AppendLine("Total de transacciones: " + Cantidad);
        texto.AppendLine();
        EscribirTabla(texto);

        string? carpeta = Path.GetDirectoryName(Path.GetFullPath(ruta));
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        File.WriteAllText(ruta, texto.ToString(), new UTF8Encoding(true));
    }

    private void EscribirTabla(StringBuilder texto)
    {
        EscribirFila(texto, "N°", "Turno", "Hora", "Tipo", "Origen", "Destino", "Monto", "Descripción");
        EscribirFila(texto, Guiones(0), Guiones(1), Guiones(2), Guiones(3), Guiones(4), Guiones(5), Guiones(6), "-----------");
        _transacciones.RecorrerDesdeInicio(t => EscribirFila(texto,
            t.Id.ToString(CultureInfo.InvariantCulture),
            t.NumeroTurno.ToString(CultureInfo.InvariantCulture),
            t.FechaHora.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Formato.Nombre(t.Tipo),
            t.Origen,
            t.Destino,
            Formato.Dinero(t.Monto),
            t.Descripcion));

        if (Cantidad == 0)
        {
            texto.AppendLine("(sin transacciones)");
        }
    }

    private static void EscribirFila(StringBuilder texto, string numero, string turno, string hora, string tipo,
        string origen, string destino, string monto, string descripcion)
    {
        texto.Append(Celda(numero, AnchosColumnas[0])).Append(' ');
        texto.Append(Celda(turno, AnchosColumnas[1])).Append(' ');
        texto.Append(Celda(hora, AnchosColumnas[2])).Append(' ');
        texto.Append(Celda(tipo, AnchosColumnas[3])).Append(' ');
        texto.Append(Celda(origen, AnchosColumnas[4])).Append(' ');
        texto.Append(Celda(destino, AnchosColumnas[5])).Append(' ');
        texto.Append(Recortar(monto, AnchosColumnas[6]).PadLeft(AnchosColumnas[6])).Append(' ');
        texto.AppendLine(descripcion);
    }

    private static string Celda(string valor, int ancho)
    {
        return Recortar(valor, ancho).PadRight(ancho);
    }

    private static string Recortar(string valor, int ancho)
    {
        return valor.Length <= ancho ? valor : valor.Substring(0, ancho - 1) + "…";
    }

    private static string Guiones(int columna)
    {
        return new string('-', AnchosColumnas[columna]);
    }

    private static int AnchoTotal()
    {
        int total = 0;
        foreach (int ancho in AnchosColumnas)
        {
            total += ancho + 1;
        }

        return total + 30;
    }
}
