using System;
using System.Windows.Forms;

namespace Monopoly.App;

/// <summary>
/// Punto de entrada de la aplicación de escritorio del Monopoly.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Inicia la aplicación y muestra el formulario principal.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new FormularioPrincipal());
    }
}
