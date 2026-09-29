namespace Monopoly.Core.Modelo;

/// <summary>
/// Acción principal que resulta de caer en una casilla. Es informativa (para mensajes e interfaz):
/// los efectos concretos están en las propiedades de <see cref="ResultadoCasilla"/>.
/// </summary>
public enum AccionCasilla
{
    /// <summary>No ocurre nada.</summary>
    Ninguna,

    /// <summary>La propiedad está disponible y se ofrece su compra.</summary>
    OfrecerCompra,

    /// <summary>Se debe pagar alquiler al dueño.</summary>
    PagarAlquiler,

    /// <summary>Se debe pagar un impuesto al banco.</summary>
    PagarImpuesto,

    /// <summary>Se sacó una carta de evento.</summary>
    SacarCarta,

    /// <summary>El jugador es enviado a la cárcel.</summary>
    IrALaCarcel,
}
