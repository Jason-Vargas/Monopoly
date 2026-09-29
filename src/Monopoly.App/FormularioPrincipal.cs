using System.Drawing;
using System.Windows.Forms;

namespace Monopoly.App;

/// <summary>
/// Ventana principal de la aplicación. Por ahora solo es un marcador de posición.
/// </summary>
public class FormularioPrincipal : Form
{
    /// <summary>
    /// Crea el formulario principal con su configuración básica.
    /// </summary>
    public FormularioPrincipal()
    {
        Text = "Monopoly Distribuido";
        ClientSize = new Size(1024, 768);
        StartPosition = FormStartPosition.CenterScreen;
    }
}
