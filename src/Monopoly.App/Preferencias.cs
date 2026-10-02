using System;
using System.Globalization;
using System.IO;

namespace Monopoly.App;

/// <summary>
/// Preferencias locales de esta computadora (no forman parte de la partida): sonido activado y volumen.
/// Se guardan en <c>%LOCALAPPDATA%\MonopolyTEC\preferencias.txt</c> como líneas <c>clave=valor</c>.
/// </summary>
internal static class Preferencias
{
    private static readonly string Ruta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonopolyTEC", "preferencias.txt");

    private static bool _sonidoActivado = true;
    private static int _volumen = 70;

    static Preferencias()
    {
        Cargar();
    }

    /// <summary>Cambió alguna preferencia.</summary>
    public static event Action? Cambiaron;

    /// <summary>Si se reproducen sonidos (se usará en el siguiente paso del rediseño).</summary>
    public static bool SonidoActivado
    {
        get => _sonidoActivado;
        set
        {
            if (_sonidoActivado != value)
            {
                _sonidoActivado = value;
                Guardar();
            }
        }
    }

    /// <summary>Volumen de 0 a 100.</summary>
    public static int Volumen
    {
        get => _volumen;
        set
        {
            int nuevo = Math.Clamp(value, 0, 100);
            if (_volumen != nuevo)
            {
                _volumen = nuevo;
                Guardar();
            }
        }
    }

    private static void Cargar()
    {
        try
        {
            if (!File.Exists(Ruta))
            {
                return;
            }

            foreach (string linea in File.ReadAllLines(Ruta))
            {
                int igual = linea.IndexOf('=');
                if (igual <= 0)
                {
                    continue;
                }

                string clave = linea.Substring(0, igual).Trim();
                string valor = linea.Substring(igual + 1).Trim();
                if (clave == "sonido")
                {
                    _sonidoActivado = valor == "1";
                }
                else if (clave == "volumen" && int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int volumen))
                {
                    _volumen = Math.Clamp(volumen, 0, 100);
                }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Sin preferencias guardadas: se usan los valores por defecto.
        }
    }

    private static void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
            File.WriteAllText(Ruta, $"sonido={(_sonidoActivado ? 1 : 0)}\nvolumen={_volumen.ToString(CultureInfo.InvariantCulture)}\n");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // No es grave: la preferencia vale para esta sesión.
        }

        Cambiaron?.Invoke();
    }
}
