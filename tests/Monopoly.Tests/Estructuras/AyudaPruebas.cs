using System;
using System.Text;

namespace Monopoly.Tests.Estructuras;

/// <summary>
/// Utilidades compartidas por las pruebas de estructuras.
/// </summary>
internal static class AyudaPruebas
{
    /// <summary>
    /// Ejecuta un recorrido y concatena los elementos visitados separados por comas.
    /// </summary>
    public static string Texto<T>(Action<Action<T>> recorrido)
    {
        StringBuilder texto = new StringBuilder();
        recorrido(valor =>
        {
            if (texto.Length > 0)
            {
                texto.Append(',');
            }

            texto.Append(valor);
        });
        return texto.ToString();
    }
}
