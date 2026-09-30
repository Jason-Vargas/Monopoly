namespace Monopoly.Core.Red;

/// <summary>
/// Filtros de <c>CONSULTAR_TRANSACCIONES</c>. En la red se envían en mayúsculas (<c>TODAS</c>, <c>RECIENTES</c>...).
/// </summary>
public enum FiltroTransacciones
{
    /// <summary>Todas, de la más antigua a la más reciente.</summary>
    Todas,

    /// <summary>Todas, de la más antigua a la más reciente (igual que <see cref="Todas"/>).</summary>
    Antiguas,

    /// <summary>Todas, de la más reciente a la más antigua.</summary>
    Recientes,

    /// <summary>Las del jugador indicado (como origen o destino); requiere el nombre.</summary>
    Jugador,

    /// <summary>Las de un tipo; requiere el nombre del <see cref="Modelo.TipoTransaccion"/>.</summary>
    Tipo,
}
