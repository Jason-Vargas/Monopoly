using System.Windows.Forms;

namespace Monopoly.App.Estilo;

/// <summary>
/// Panel con doble búfer para dibujar contenido propio en su evento <see cref="Control.Paint"/> sin parpadeo.
/// </summary>
internal sealed class Lienzo : Panel
{
    /// <summary>
    /// Crea el lienzo.
    /// </summary>
    public Lienzo()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
}
