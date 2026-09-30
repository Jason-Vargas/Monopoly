using System;
using System.Windows.Forms;

namespace Monopoly.App;

/// <summary>
/// Punto de entrada de la aplicación de escritorio del Monopoly.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Inicia la aplicación. Argumentos opcionales para pruebas:
    /// <c>--crear Nombre [puerto] [maxTurnos]</c> o <c>--unirse Nombre [ip] [puerto]</c>.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ContextoAplicacion(ArgumentosInicio.Interpretar(args)));
    }
}
