using System;
using System.Globalization;

namespace Monopoly.Core.Red;

/// <summary>
/// Mensaje del protocolo ya decodificado: un comando y sus campos sin escapes.
/// </summary>
public sealed class Mensaje
{
    private readonly string[] _campos;

    /// <summary>
    /// Crea un mensaje.
    /// </summary>
    /// <param name="comando">Comando en mayúsculas.</param>
    /// <param name="campos">Campos del mensaje (sin el comando).</param>
    public Mensaje(string comando, string[] campos)
    {
        Comando = comando;
        _campos = campos ?? new string[0];
    }

    /// <summary>
    /// Comando del mensaje (por ejemplo <c>TIRAR_DADOS</c>).
    /// </summary>
    public string Comando { get; }

    /// <summary>
    /// Cantidad de campos, sin contar el comando.
    /// </summary>
    public int CantidadCampos => _campos.Length;

    /// <summary>
    /// Obtiene un campo de texto.
    /// </summary>
    /// <param name="indice">Posición del campo (0 = el primero después del comando).</param>
    /// <returns>El texto del campo.</returns>
    /// <exception cref="FormatException">Si el campo no existe.</exception>
    public string Campo(int indice)
    {
        if (indice < 0 || indice >= _campos.Length)
        {
            throw new FormatException($"El mensaje {Comando} no tiene el campo {indice}.");
        }

        return _campos[indice];
    }

    /// <summary>
    /// Obtiene un campo entero.
    /// </summary>
    /// <param name="indice">Posición del campo.</param>
    /// <returns>El valor.</returns>
    /// <exception cref="FormatException">Si el campo no existe o no es un entero.</exception>
    public int Entero(int indice)
    {
        return int.Parse(Campo(indice), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Obtiene un campo entero que puede venir vacío.
    /// </summary>
    /// <param name="indice">Posición del campo.</param>
    /// <returns>El valor, o <c>null</c> si el campo está vacío.</returns>
    public int? EnteroOpcional(int indice)
    {
        return Campo(indice).Length == 0 ? null : Entero(indice);
    }

    /// <summary>
    /// Obtiene un campo booleano codificado como <c>1</c> o <c>0</c>.
    /// </summary>
    /// <param name="indice">Posición del campo.</param>
    /// <returns>El valor.</returns>
    public bool Booleano(int indice)
    {
        return Campo(indice) == "1";
    }

    /// <summary>
    /// Obtiene un campo con el nombre de un valor de enumeración.
    /// </summary>
    /// <typeparam name="T">Tipo de la enumeración.</typeparam>
    /// <param name="indice">Posición del campo.</param>
    /// <returns>El valor.</returns>
    /// <exception cref="FormatException">Si el texto no corresponde a ningún valor.</exception>
    public T Enumeracion<T>(int indice) where T : struct, Enum
    {
        string texto = Campo(indice);
        if (!Enum.TryParse(texto, true, out T valor) || !Enum.IsDefined(valor))
        {
            throw new FormatException($"Valor inválido '{texto}' para {typeof(T).Name}.");
        }

        return valor;
    }

    /// <summary>
    /// Devuelve el mensaje codificado como línea del protocolo.
    /// </summary>
    /// <returns>La línea.</returns>
    public override string ToString()
    {
        return Protocolo.Codificar(Comando, _campos);
    }
}
