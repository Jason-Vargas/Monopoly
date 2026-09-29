using System;
using System.IO;
using System.Text;
using Xunit;

namespace Monopoly.Tests;

/// <summary>
/// Verifica que ningún archivo fuente de la solución use colecciones de .NET ni LINQ.
/// </summary>
public class PruebasRestricciones
{
    private static readonly string[] EspaciosProhibidos =
    {
        "System.Linq",
        "System.Collections",
    };

    private static readonly string[] TiposGenericosProhibidos =
    {
        "List", "LinkedList", "Stack", "Queue", "PriorityQueue", "Dictionary",
        "SortedList", "SortedDictionary", "SortedSet", "HashSet",
        "ConcurrentQueue", "ConcurrentStack", "ConcurrentDictionary", "ConcurrentBag",
        "BlockingCollection", "ImmutableList", "ImmutableArray", "Collection", "ObservableCollection",
    };

    private static readonly string[] TiposNoGenericosProhibidos =
    {
        "ArrayList", "Hashtable", "BitArray",
    };

    [Fact]
    public void CodigoFuente_NoUsaColeccionesNiLinq()
    {
        string raiz = BuscarRaizSolucion();
        string[] archivos = Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories);
        StringBuilder infracciones = new StringBuilder();

        foreach (string archivo in archivos)
        {
            if (EsArchivoExcluido(archivo))
            {
                continue;
            }

            string[] lineas = File.ReadAllLines(archivo);
            for (int i = 0; i < lineas.Length; i++)
            {
                string linea = lineas[i].Trim();
                if (linea.StartsWith("//"))
                {
                    continue;
                }

                string? motivo = BuscarInfraccion(linea);
                if (motivo != null)
                {
                    infracciones.AppendLine($"{Path.GetRelativePath(raiz, archivo)}:{i + 1} -> {motivo}");
                }
            }
        }

        Assert.True(infracciones.Length == 0, "Uso prohibido de colecciones o LINQ:\n" + infracciones);
    }

    private static string? BuscarInfraccion(string linea)
    {
        foreach (string espacio in EspaciosProhibidos)
        {
            if (linea.Contains(espacio))
            {
                return espacio;
            }
        }

        foreach (string tipo in TiposGenericosProhibidos)
        {
            if (ContieneIdentificador(linea, tipo, true))
            {
                return tipo + "<T>";
            }
        }

        foreach (string tipo in TiposNoGenericosProhibidos)
        {
            if (ContieneIdentificador(linea, tipo, false))
            {
                return tipo;
            }
        }

        return null;
    }

    /// <summary>
    /// Indica si el identificador aparece como palabra completa; si es genérico, debe ir seguido de '&lt;'.
    /// </summary>
    private static bool ContieneIdentificador(string linea, string nombre, bool generico)
    {
        int indice = linea.IndexOf(nombre, StringComparison.Ordinal);
        while (indice >= 0)
        {
            bool inicioValido = indice == 0 || !EsCaracterIdentificador(linea[indice - 1]);
            int fin = indice + nombre.Length;
            while (fin < linea.Length && linea[fin] == ' ')
            {
                fin++;
            }

            bool finValido = generico
                ? fin < linea.Length && linea[fin] == '<'
                : indice + nombre.Length == linea.Length || !EsCaracterIdentificador(linea[indice + nombre.Length]);

            if (inicioValido && finValido)
            {
                return true;
            }

            indice = linea.IndexOf(nombre, indice + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool EsCaracterIdentificador(char caracter)
    {
        return char.IsLetterOrDigit(caracter) || caracter == '_';
    }

    private static bool EsArchivoExcluido(string archivo)
    {
        string separador = Path.DirectorySeparatorChar.ToString();
        return archivo.Contains(separador + "bin" + separador)
            || archivo.Contains(separador + "obj" + separador)
            || Path.GetFileName(archivo) == nameof(PruebasRestricciones) + ".cs";
    }

    private static string BuscarRaizSolucion()
    {
        DirectoryInfo? directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio != null && !File.Exists(Path.Combine(directorio.FullName, "Monopoly.sln")))
        {
            directorio = directorio.Parent;
        }

        if (directorio == null)
        {
            throw new InvalidOperationException("No se encontró Monopoly.sln.");
        }

        return directorio.FullName;
    }
}
