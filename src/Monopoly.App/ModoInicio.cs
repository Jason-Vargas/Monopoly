namespace Monopoly.App;

/// <summary>
/// Modo en que arranca la aplicación.
/// </summary>
internal enum ModoInicio
{
    /// <summary>Se muestra la ventana de inicio y el usuario elige.</summary>
    Normal,

    /// <summary>Crea la partida automáticamente (organizador).</summary>
    Crear,

    /// <summary>Se une automáticamente a una partida.</summary>
    Unirse,
}
