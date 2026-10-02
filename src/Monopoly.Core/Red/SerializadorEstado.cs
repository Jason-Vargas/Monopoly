using System;
using System.Globalization;
using System.Text;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;

namespace Monopoly.Core.Red;

/// <summary>
/// Convierte un <see cref="EstadoRed"/> en el mensaje <c>ESTADO</c> y viceversa.
/// </summary>
/// <remarks>
/// Campos (después de <c>ESTADO</c>): 0 estado · 1 fase · 2 turno · 3 máximo de turnos · 4 id en turno ·
/// 5 id ganador · 6 id propiedad en venta · 7 id deudor · 8 monto pendiente · 9 descripción del pago ·
/// 10 dado 1 · 11 dado 2 · 12 id del último movimiento · 13 casillas recorridas ("4,5,6") ·
/// 14 dueños ("3:1,5:2") · 15 cantidad de eventos · 16 cantidad de transacciones · 17 cantidad de jugadores (N) ·
/// 18 jugadores desconectados ("2,4") · 19 cajero físico conectado (1/0) · 20 modo sin hardware (1/0);
/// luego N bloques de 11 campos: id · nombre · color · saldo · posición · activo (1/0) · turnos por perder ·
/// patrimonio · propiedades ("1,3") · tarjeta física (1/0) · forma de la ficha. Los opcionales vacíos significan "ninguno".
/// </remarks>
public static class SerializadorEstado
{
    /// <summary>
    /// Campos fijos antes de los bloques de jugadores.
    /// </summary>
    public const int CamposFijos = 21;

    /// <summary>
    /// Campos por jugador.
    /// </summary>
    public const int CamposPorJugador = 11;

    /// <summary>
    /// Codifica el estado como línea <c>ESTADO|...</c>.
    /// </summary>
    /// <param name="estado">Estado a enviar.</param>
    /// <returns>La línea del protocolo.</returns>
    public static string Codificar(EstadoRed estado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        InstantaneaJuego i = estado.Instantanea;
        EstadoJugador[] jugadores = i.Jugadores;
        string[] campos = new string[CamposFijos + (jugadores.Length * CamposPorJugador)];

        campos[0] = i.Estado.ToString();
        campos[1] = i.Fase.ToString();
        campos[2] = Texto(i.NumeroTurno);
        campos[3] = Texto(i.MaximoTurnos);
        campos[4] = Texto(i.IdJugadorEnTurno);
        campos[5] = Texto(i.IdGanador);
        campos[6] = Texto(i.IdPropiedadEnVenta);
        campos[7] = Texto(i.IdDeudor);
        campos[8] = Texto(i.MontoPagoPendiente);
        campos[9] = i.DescripcionPagoPendiente ?? string.Empty;
        campos[10] = Texto(i.UltimaTirada?.Dado1);
        campos[11] = Texto(i.UltimaTirada?.Dado2);
        campos[12] = Texto(estado.IdJugadorUltimoMovimiento);
        campos[13] = UnirEnteros(estado.CasillasRecorridas);
        campos[14] = CodificarPropietarios(estado.Propietarios);
        campos[15] = Texto(i.CantidadEventos);
        campos[16] = Texto(i.CantidadTransacciones);
        campos[17] = Texto(jugadores.Length);
        campos[18] = UnirEnteros(estado.IdsDesconectados);
        campos[19] = estado.CajeroConectado ? "1" : "0";
        campos[20] = estado.ModoSinHardware ? "1" : "0";

        for (int j = 0; j < jugadores.Length; j++)
        {
            EstadoJugador jugador = jugadores[j];
            int b = CamposFijos + (j * CamposPorJugador);
            campos[b] = Texto(jugador.Id);
            campos[b + 1] = jugador.Nombre;
            campos[b + 2] = jugador.ColorFicha.ToString();
            campos[b + 3] = Texto(jugador.Saldo);
            campos[b + 4] = Texto(jugador.Posicion);
            campos[b + 5] = jugador.Activo ? "1" : "0";
            campos[b + 6] = Texto(jugador.TurnosPorPerder);
            campos[b + 7] = Texto(jugador.Patrimonio);
            campos[b + 8] = UnirEnteros(jugador.IdsPropiedades);
            campos[b + 9] = jugador.TieneTarjetaFisica ? "1" : "0";
            campos[b + 10] = jugador.FormaFicha.ToString();
        }

        return Protocolo.Codificar(Protocolo.Estado, campos);
    }

    /// <summary>
    /// Decodifica un mensaje <c>ESTADO</c>.
    /// </summary>
    /// <param name="mensaje">Mensaje recibido.</param>
    /// <returns>El estado.</returns>
    /// <exception cref="FormatException">Si el mensaje no es un ESTADO válido.</exception>
    public static EstadoRed Decodificar(Mensaje mensaje)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        if (mensaje.Comando != Protocolo.Estado)
        {
            throw new FormatException($"Se esperaba {Protocolo.Estado} y llegó {mensaje.Comando}.");
        }

        int cantidadJugadores = mensaje.Entero(17);
        if (cantidadJugadores < 0 || mensaje.CantidadCampos != CamposFijos + (cantidadJugadores * CamposPorJugador))
        {
            throw new FormatException("La cantidad de campos del ESTADO no coincide con la de jugadores.");
        }

        EstadoJugador[] jugadores = new EstadoJugador[cantidadJugadores];
        for (int j = 0; j < cantidadJugadores; j++)
        {
            int b = CamposFijos + (j * CamposPorJugador);
            jugadores[j] = new EstadoJugador(
                mensaje.Entero(b),
                mensaje.Campo(b + 1),
                mensaje.Enumeracion<ColorFicha>(b + 2),
                mensaje.Entero(b + 3),
                mensaje.Entero(b + 4),
                mensaje.Booleano(b + 5),
                mensaje.Entero(b + 6),
                mensaje.Entero(b + 7),
                SepararEnteros(mensaje.Campo(b + 8)),
                mensaje.Booleano(b + 9),
                mensaje.Enumeracion<FormaFicha>(b + 10));
        }

        int? dado1 = mensaje.EnteroOpcional(10);
        int? dado2 = mensaje.EnteroOpcional(11);
        TiradaDados? tirada = dado1.HasValue && dado2.HasValue ? new TiradaDados(dado1.Value, dado2.Value) : null;
        string descripcion = mensaje.Campo(9);

        InstantaneaJuego instantanea = new InstantaneaJuego(
            mensaje.Enumeracion<EstadoPartida>(0),
            mensaje.Enumeracion<FaseTurno>(1),
            mensaje.Entero(2),
            mensaje.Entero(3),
            mensaje.EnteroOpcional(4),
            mensaje.EnteroOpcional(5),
            mensaje.EnteroOpcional(6),
            mensaje.EnteroOpcional(7),
            descripcion.Length == 0 ? null : descripcion,
            mensaje.Entero(8),
            tirada,
            jugadores,
            mensaje.Entero(15),
            mensaje.Entero(16));

        return new EstadoRed(instantanea, mensaje.EnteroOpcional(12), SepararEnteros(mensaje.Campo(13)),
            DecodificarPropietarios(mensaje.Campo(14)), SepararEnteros(mensaje.Campo(18)), mensaje.Booleano(19), mensaje.Booleano(20));
    }

    private static string Texto(int valor)
    {
        return valor.ToString(CultureInfo.InvariantCulture);
    }

    private static string Texto(int? valor)
    {
        return valor.HasValue ? Texto(valor.Value) : string.Empty;
    }

    private static string UnirEnteros(int[] valores)
    {
        StringBuilder texto = new StringBuilder();
        foreach (int valor in valores)
        {
            if (texto.Length > 0)
            {
                texto.Append(',');
            }

            texto.Append(Texto(valor));
        }

        return texto.ToString();
    }

    private static int[] SepararEnteros(string texto)
    {
        if (texto.Length == 0)
        {
            return new int[0];
        }

        string[] partes = texto.Split(',');
        int[] valores = new int[partes.Length];
        for (int i = 0; i < partes.Length; i++)
        {
            valores[i] = int.Parse(partes[i], NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        return valores;
    }

    private static string CodificarPropietarios(int[] propietarios)
    {
        StringBuilder texto = new StringBuilder();
        for (int casilla = 0; casilla < propietarios.Length; casilla++)
        {
            if (propietarios[casilla] != 0)
            {
                if (texto.Length > 0)
                {
                    texto.Append(',');
                }

                texto.Append(Texto(casilla)).Append(':').Append(Texto(propietarios[casilla]));
            }
        }

        return texto.ToString();
    }

    private static int[] DecodificarPropietarios(string texto)
    {
        int[] propietarios = new int[Tablero.CantidadCasillas];
        if (texto.Length == 0)
        {
            return propietarios;
        }

        foreach (string par in texto.Split(','))
        {
            int dosPuntos = par.IndexOf(':');
            if (dosPuntos <= 0)
            {
                throw new FormatException($"Par de dueño inválido: '{par}'.");
            }

            int casilla = int.Parse(par.Substring(0, dosPuntos), NumberStyles.Integer, CultureInfo.InvariantCulture);
            int dueno = int.Parse(par.Substring(dosPuntos + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (casilla < 0 || casilla >= propietarios.Length)
            {
                throw new FormatException($"Casilla fuera de rango: {casilla}.");
            }

            propietarios[casilla] = dueno;
        }

        return propietarios;
    }

    /// <summary>
    /// Convierte una lista de movimientos en el arreglo de casillas recorridas (en orden).
    /// </summary>
    /// <param name="movimientos">Movimientos de una acción.</param>
    /// <returns>Los índices de las casillas visitadas; en los envíos directos (cárcel), la casilla de llegada.</returns>
    public static int[] CasillasRecorridas(ListaSimple<ResultadoMovimiento> movimientos)
    {
        ArgumentNullException.ThrowIfNull(movimientos);
        ListaSimple<int> casillas = new ListaSimple<int>();
        movimientos.Recorrer(movimiento =>
        {
            if (movimiento.CasillasRecorridas.EstaVacia)
            {
                casillas.AgregarAlFinal(movimiento.CasillaFinal.Id);
            }
            else
            {
                movimiento.CasillasRecorridas.Recorrer(casilla => casillas.AgregarAlFinal(casilla.Id));
            }
        });

        int[] resultado = new int[casillas.Cantidad];
        int i = 0;
        casillas.Recorrer(casilla => resultado[i++] = casilla);
        return resultado;
    }
}
