using System;
using System.Globalization;

namespace Monopoly.App;

/// <summary>
/// Argumentos opcionales de la línea de comandos, útiles para abrir varias ventanas de prueba:
/// <c>--crear Nombre [puerto] [maxTurnos]</c> o <c>--unirse Nombre [ip] [puerto]</c>.
/// </summary>
internal sealed class ArgumentosInicio
{
    /// <summary>Modo de arranque.</summary>
    public ModoInicio Modo { get; private set; }

    /// <summary>Nombre del jugador.</summary>
    public string? Nombre { get; private set; }

    /// <summary>IP del servidor (modo unirse).</summary>
    public string? Host { get; private set; }

    /// <summary>Puerto.</summary>
    public int? Puerto { get; private set; }

    /// <summary>Máximo de turnos (modo crear).</summary>
    public int? MaximoTurnos { get; private set; }

    /// <summary>
    /// Valores para volver a la ventana de inicio tras perder la conexión: el formulario queda relleno
    /// (mismo nombre, IP y puerto) y el usuario pulsa "Unirse" cuando quiera.
    /// </summary>
    public static ArgumentosInicio ParaReconectar(string? nombre, string host, int puerto)
    {
        return new ArgumentosInicio { Modo = ModoInicio.Normal, Nombre = nombre, Host = host, Puerto = puerto };
    }

    /// <summary>
    /// Interpreta los argumentos; si no se reconocen, arranca en modo normal.
    /// </summary>
    public static ArgumentosInicio Interpretar(string[] args)
    {
        ArgumentosInicio resultado = new ArgumentosInicio();
        if (args.Length < 2)
        {
            return resultado;
        }

        string modo = args[0].ToLowerInvariant();
        if (modo == "--crear")
        {
            resultado.Modo = ModoInicio.Crear;
            resultado.Nombre = args[1];
            resultado.Puerto = Entero(args, 2);
            resultado.MaximoTurnos = Entero(args, 3);
        }
        else if (modo == "--unirse")
        {
            resultado.Modo = ModoInicio.Unirse;
            resultado.Nombre = args[1];
            resultado.Host = args.Length > 2 ? args[2] : "127.0.0.1";
            resultado.Puerto = Entero(args, 3);
        }

        return resultado;
    }

    private static int? Entero(string[] args, int indice)
    {
        return args.Length > indice && int.TryParse(args[indice], NumberStyles.Integer, CultureInfo.InvariantCulture, out int valor)
            ? valor
            : null;
    }
}
