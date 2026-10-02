namespace Monopoly.Core.Hardware;

/// <summary>
/// Mensajes del protocolo serie con la Pico W (ver hardware/README.md). Una línea por mensaje.
/// </summary>
public static class ProtocoloCajero
{
    /// <summary>Pico → PC: el programa arrancó.</summary>
    public const string Listo = "LISTO";

    /// <summary>Pico → PC: se presionó el botón.</summary>
    public const string Boton = "BOTON";

    /// <summary>Pico → PC: <c>RFID:&lt;UID en hex&gt;</c>.</summary>
    public const string PrefijoRfid = "RFID:";

    /// <summary>Pico → PC: respuesta a <see cref="Ping"/>.</summary>
    public const string Pong = "PONG";

    /// <summary>Pico → PC: <c>ERROR:&lt;detalle&gt;</c>.</summary>
    public const string PrefijoError = "ERROR:";

    /// <summary>PC → Pico: <c>DADOS:d1,d2</c> (la Pico confirma la recepción con un destello del LED integrado).</summary>
    public const string PrefijoDados = "DADOS:";

    /// <summary>PC → Pico: pago aceptado (LED de pago encendido 2 s).</summary>
    public const string PagoOk = "PAGO_OK";

    /// <summary>PC → Pico: pago rechazado (LED de pago, 3 parpadeos rápidos).</summary>
    public const string PagoRechazado = "PAGO_RECHAZADO";

    /// <summary>PC → Pico: apagar el LED de pago.</summary>
    public const string Limpiar = "LIMPIAR";

    /// <summary>PC → Pico: comprobar la conexión.</summary>
    public const string Ping = "PING";

    /// <summary>
    /// Indica si un texto es un UID válido: 4, 7 o 10 bytes en hexadecimal (8, 14 o 20 caracteres).
    /// </summary>
    /// <param name="uid">UID en hexadecimal.</param>
    /// <returns><c>true</c> si es válido.</returns>
    public static bool EsUidValido(string uid)
    {
        if (uid.Length != 8 && uid.Length != 14 && uid.Length != 20)
        {
            return false;
        }

        foreach (char caracter in uid)
        {
            bool hexadecimal = (caracter >= '0' && caracter <= '9') || (caracter >= 'A' && caracter <= 'F');
            if (!hexadecimal)
            {
                return false;
            }
        }

        return true;
    }
}
