using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Monopoly.App.Estilo;

/// <summary>
/// Carga las fuentes de licencia libre (SIL OFL 1.1) incrustadas en el ejecutable: Abril Fatface para los
/// títulos y Lato (normal y negrita) para el texto. Se registran en una <see cref="PrivateFontCollection"/>
/// (dibujo con GDI+) y también en GDI con <c>AddFontMemResourceEx</c>, para que los controles estándar
/// (TextBox, Label) las puedan usar. Si algo falla, se usan Georgia y Segoe UI del sistema.
/// </summary>
internal static class Fuentes
{
    private const string Prefijo = "Monopoly.App.Fuentes.";

    // La colección y la memoria de las fuentes deben vivir mientras dure la aplicación.
    private static readonly PrivateFontCollection Coleccion = new PrivateFontCollection();

    static Fuentes()
    {
        try
        {
            Cargar("AbrilFatface-Regular.ttf");
            Cargar("Lato-Regular.ttf");
            Cargar("Lato-Bold.ttf");
            Titulo = Buscar("Abril Fatface") ?? new FontFamily("Georgia");
            Texto = Buscar("Lato") ?? new FontFamily("Segoe UI");
            Incluidas = Titulo.Name == "Abril Fatface" && Texto.Name == "Lato";
        }
        catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is ExternalException)
        {
            Titulo = new FontFamily("Georgia");
            Texto = new FontFamily("Segoe UI");
        }
    }

    /// <summary>Familia de los títulos (gruesa y elegante).</summary>
    public static FontFamily Titulo { get; }

    /// <summary>Familia del texto (sans-serif limpia).</summary>
    public static FontFamily Texto { get; }

    /// <summary>Indica si se cargaron las fuentes incluidas (y no las del sistema).</summary>
    public static bool Incluidas { get; }

    private static void Cargar(string archivo)
    {
        using Stream? recurso = Assembly.GetExecutingAssembly().GetManifestResourceStream(Prefijo + archivo);
        if (recurso == null)
        {
            throw new IOException($"No se encontró la fuente incrustada {archivo}.");
        }

        byte[] datos = new byte[recurso.Length];
        recurso.ReadExactly(datos);
        IntPtr memoria = Marshal.AllocCoTaskMem(datos.Length);
        Marshal.Copy(datos, 0, memoria, datos.Length);
        Coleccion.AddMemoryFont(memoria, datos.Length);
        uint cantidad = 0;
        AddFontMemResourceEx(memoria, (uint)datos.Length, IntPtr.Zero, ref cantidad);
    }

    private static FontFamily? Buscar(string nombre)
    {
        foreach (FontFamily familia in Coleccion.Families)
        {
            if (familia.Name == nombre)
            {
                return familia;
            }
        }

        return null;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr fuente, uint longitud, IntPtr reservado, ref uint cantidad);
}
