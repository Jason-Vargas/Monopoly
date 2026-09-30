using System;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.ClienteConsola;

/// <summary>
/// Herramienta temporal de depuración del protocolo. Uso:
/// <c>servidor [puerto]</c> levanta el banco; <c>cliente [host] [puerto] [nombre]</c> abre un cliente interactivo.
/// </summary>
internal static class Program
{
    private static readonly object CandadoConsola = new object();
    private static readonly Tablero TableroReferencia = new Tablero();

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        string modo = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;
        try
        {
            switch (modo)
            {
                case "servidor":
                    return EjecutarServidor(Argumento(args, 1, Servidor.PuertoPredeterminado));
                case "cliente":
                    return EjecutarCliente(
                        args.Length > 1 ? args[1] : "127.0.0.1",
                        Argumento(args, 2, Servidor.PuertoPredeterminado),
                        args.Length > 3 ? args[3] : null);
                default:
                    Console.WriteLine("Uso:");
                    Console.WriteLine("  Monopoly.ClienteConsola servidor [puerto]");
                    Console.WriteLine("  Monopoly.ClienteConsola cliente [host] [puerto] [nombre]");
                    return 1;
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine("Error de red: " + ex.Message);
            return 2;
        }
    }

    private static int EjecutarServidor(int puerto)
    {
        Juego juego = new Juego();
        using Servidor servidor = new Servidor(juego, puerto);
        servidor.Registro += texto => Escribir("[servidor] " + texto, ConsoleColor.DarkGray);
        servidor.Iniciar();

        Escribir($"Banco de Monopoly escuchando en el puerto {servidor.Puerto}.", ConsoleColor.Green);
        Escribir("Los demás jugadores pueden conectarse a:", ConsoleColor.Green);
        Escribir($"  127.0.0.1:{servidor.Puerto} (esta misma computadora)", ConsoleColor.Green);
        Servidor.ObtenerIPv4Locales().Recorrer(ip => Escribir($"  {ip}:{servidor.Puerto}", ConsoleColor.Green));
        Escribir("Escriba 'salir' para detener el servidor.", ConsoleColor.Green);

        string? linea;
        while ((linea = Console.ReadLine()) != null && !linea.Trim().Equals("salir", StringComparison.OrdinalIgnoreCase))
        {
        }

        return 0;
    }

    private static int EjecutarCliente(string host, int puerto, string? nombre)
    {
        using Cliente cliente = new Cliente();
        cliente.BienvenidaRecibida += (id, n) => Escribir($"Bienvenido, {n}. Su id de jugador es {id}.", ConsoleColor.Green);
        cliente.ErrorRecibido += texto => Escribir("ERROR: " + texto, ConsoleColor.Red);
        cliente.EventoRecibido += texto => Escribir("• " + texto, ConsoleColor.Gray);
        cliente.DadosRecibidos += (id, d1, d2) => Escribir($"🎲 Jugador {id} sacó {d1} + {d2} = {d1 + d2}", ConsoleColor.Cyan);
        cliente.EstadoActualizado += estado => MostrarEstado(estado, cliente.IdJugador);
        cliente.TransaccionesRecibidas += MostrarTransacciones;
        cliente.FinRecibido += (ganador, resumen) => Escribir($"*** FIN DE LA PARTIDA. Ganador: {ganador}. {resumen}", ConsoleColor.Yellow);
        cliente.Desconectado += motivo => Escribir("Desconectado: " + motivo, ConsoleColor.Red);

        cliente.Conectar(host, puerto);
        Escribir($"Conectado a {host}:{puerto}.", ConsoleColor.Green);

        while (string.IsNullOrWhiteSpace(nombre))
        {
            Console.Write("Nombre del jugador: ");
            nombre = Console.ReadLine();
            if (nombre == null)
            {
                return 0;
            }
        }

        cliente.Unirse(nombre);
        MostrarAyuda();

        string? linea;
        while (cliente.Conectado && (linea = Console.ReadLine()) != null)
        {
            try
            {
                if (!EjecutarComando(cliente, linea.Trim()))
                {
                    break;
                }
            }
            catch (InvalidOperationException ex)
            {
                Escribir(ex.Message, ConsoleColor.Red);
            }
        }

        return 0;
    }

    /// <summary>
    /// Ejecuta un comando corto de la consola; devuelve <c>false</c> para salir.
    /// </summary>
    private static bool EjecutarComando(Cliente cliente, string linea)
    {
        if (linea.Length == 0)
        {
            return true;
        }

        string[] partes = linea.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        switch (partes[0].ToLowerInvariant())
        {
            case "i": cliente.IniciarPartida(); break;
            case "t": cliente.TirarDados(); break;
            case "c": cliente.ComprarPropiedad(); break;
            case "n": cliente.NoComprar(); break;
            case "p": cliente.PagarConTarjeta(); break;
            case "f": cliente.TerminarTurno(); break;
            case "e": cliente.ConsultarEstado(); break;
            case "x": cliente.ExportarTransacciones(); break;
            case "h":
                ConsultarHistorial(cliente, partes);
                break;
            case "?": MostrarAyuda(); break;
            case "q":
                cliente.Desconectar();
                return false;
            default:
                // Cualquier otra cosa se envía tal cual como línea del protocolo (por ejemplo "CONSULTAR_ESTADO").
                cliente.EnviarLinea(linea);
                break;
        }

        return true;
    }

    private static void ConsultarHistorial(Cliente cliente, string[] partes)
    {
        if (partes.Length < 2)
        {
            cliente.ConsultarTransacciones();
            return;
        }

        if (!Enum.TryParse(partes[1], true, out FiltroTransacciones filtro))
        {
            Escribir("Filtro desconocido. Use: todas, antiguas, recientes, jugador <nombre>, tipo <tipo>.", ConsoleColor.Red);
            return;
        }

        cliente.ConsultarTransacciones(filtro, partes.Length > 2 ? partes[2] : null);
    }

    private static void MostrarAyuda()
    {
        Escribir("Comandos: i=iniciar partida · t=tirar dados · c=comprar · n=no comprar · p=pagar con tarjeta · " +
                 "f=terminar turno · e=estado · h [todas|antiguas|recientes|jugador X|tipo T]=historial · " +
                 "x=exportar TXT · q=salir · ?=ayuda. Otra línea se envía tal cual al servidor.", ConsoleColor.DarkYellow);
    }

    private static void MostrarEstado(EstadoRed estado, int? miId)
    {
        InstantaneaJuego i = estado.Instantanea;
        StringBuilder texto = new StringBuilder();
        texto.AppendLine($"── ESTADO: {i.Estado} · turno {i.NumeroTurno}/{i.MaximoTurnos} · fase {i.Fase}" +
                         (i.IdJugadorEnTurno.HasValue ? $" · en turno: jugador {i.IdJugadorEnTurno}" : string.Empty));
        foreach (EstadoJugador j in i.Jugadores)
        {
            string marca = j.Id == miId ? "*" : " ";
            string propiedades = j.IdsPropiedades.Length == 0 ? "-" : string.Join(", ", NombresCasillas(j.IdsPropiedades));
            texto.AppendLine($"  {marca}{j.Id} {j.Nombre,-10} {Formato.Dinero(j.Saldo),8}  en {TableroReferencia.ObtenerCasilla(j.Posicion).Nombre,-26} " +
                             $"{(j.Activo ? "activo" : "ELIMINADO")}{(j.TurnosPorPerder > 0 ? $" (pierde {j.TurnosPorPerder})" : string.Empty)}  props: {propiedades}");
        }

        if (estado.CasillasRecorridas.Length > 0)
        {
            texto.AppendLine($"  Último movimiento (jugador {estado.IdJugadorUltimoMovimiento}): {string.Join(" → ", NombresCasillas(estado.CasillasRecorridas))}");
        }

        if (i.IdPropiedadEnVenta.HasValue)
        {
            Propiedad? propiedad = TableroReferencia.BuscarPropiedad(i.IdPropiedadEnVenta.Value);
            texto.AppendLine($"  En venta: {propiedad?.Nombre} por {Formato.Dinero(propiedad?.PrecioCompra ?? 0)} (c = comprar, n = no comprar)");
        }

        if (i.DescripcionPagoPendiente != null)
        {
            texto.AppendLine($"  Pago pendiente: {i.DescripcionPagoPendiente} (p = pagar con tarjeta)");
        }

        Escribir(texto.ToString().TrimEnd(), ConsoleColor.White);
    }

    private static void MostrarTransacciones(string filtro, Monopoly.Core.Estructuras.ListaSimple<Transaccion> transacciones)
    {
        StringBuilder texto = new StringBuilder();
        texto.AppendLine($"── TRANSACCIONES ({filtro}): {transacciones.Cantidad}");
        transacciones.Recorrer(t => texto.AppendLine("  " + t));
        Escribir(texto.ToString().TrimEnd(), ConsoleColor.Magenta);
    }

    private static string[] NombresCasillas(int[] indices)
    {
        string[] nombres = new string[indices.Length];
        for (int k = 0; k < indices.Length; k++)
        {
            nombres[k] = TableroReferencia.ObtenerCasilla(indices[k]).Nombre;
        }

        return nombres;
    }

    private static int Argumento(string[] args, int indice, int predeterminado)
    {
        return args.Length > indice && int.TryParse(args[indice], NumberStyles.Integer, CultureInfo.InvariantCulture, out int valor)
            ? valor
            : predeterminado;
    }

    private static void Escribir(string texto, ConsoleColor color)
    {
        lock (CandadoConsola)
        {
            ConsoleColor anterior = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(texto);
            Console.ForegroundColor = anterior;
        }
    }
}
