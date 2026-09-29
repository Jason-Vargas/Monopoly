namespace Monopoly.Core.Modelo;

/// <summary>
/// Cartas de Casualidad y Arca Comunal inspiradas en el Monopoly clásico. Cada mazo cubre
/// los seis tipos básicos de evento, además de pagos entre jugadores.
/// </summary>
public static class CartasClasicas
{
    /// <summary>
    /// Crea las cartas del mazo de Casualidad.
    /// </summary>
    /// <returns>Un arreglo nuevo con las cartas, sin barajar.</returns>
    public static CartaEvento[] CrearCasualidad()
    {
        return new CartaEvento[]
        {
            new CartaEvento("Avance hasta la Salida y cobre $200.", TipoCartaEvento.IrACasilla, Tablero.IndiceSalida),
            new CartaEvento("Avance hasta la Avenida Illinois. Si pasa por la Salida, cobre $200.", TipoCartaEvento.IrACasilla, 24),
            new CartaEvento("Avance hasta la Plaza San Carlos. Si pasa por la Salida, cobre $200.", TipoCartaEvento.IrACasilla, 11),
            new CartaEvento("Dé un paseo hasta el Paseo Marítimo.", TipoCartaEvento.IrACasilla, 39),
            new CartaEvento("Tome el Ferrocarril Reading. Si pasa por la Salida, cobre $200.", TipoCartaEvento.IrACasilla, 5),
            new CartaEvento("Retroceda tres casillas.", TipoCartaEvento.Retroceder, 3),
            new CartaEvento("Avance cuatro casillas.", TipoCartaEvento.Avanzar, 4),
            new CartaEvento("El banco le paga un dividendo de $50.", TipoCartaEvento.RecibirDinero, 50),
            new CartaEvento("Su préstamo de construcción vence. Cobre $150.", TipoCartaEvento.RecibirDinero, 150),
            new CartaEvento("Multa por exceso de velocidad: pague $15.", TipoCartaEvento.PagarDinero, 15),
            new CartaEvento("Reparaciones generales en sus propiedades: pague $100.", TipoCartaEvento.PagarDinero, 100),
            new CartaEvento("Queda atrapado en el tráfico: pierda un turno.", TipoCartaEvento.PerderTurno, 1),
            new CartaEvento("Ha sido elegido presidente de la junta: pague $50 a cada jugador.", TipoCartaEvento.PagarACadaJugador, 50),
        };
    }

    /// <summary>
    /// Crea las cartas del mazo de Arca Comunal.
    /// </summary>
    /// <returns>Un arreglo nuevo con las cartas, sin barajar.</returns>
    public static CartaEvento[] CrearArcaComunal()
    {
        return new CartaEvento[]
        {
            new CartaEvento("Avance hasta la Salida y cobre $200.", TipoCartaEvento.IrACasilla, Tablero.IndiceSalida),
            new CartaEvento("Error del banco a su favor: cobre $200.", TipoCartaEvento.RecibirDinero, 200),
            new CartaEvento("Venta de acciones: cobre $50.", TipoCartaEvento.RecibirDinero, 50),
            new CartaEvento("Devolución de impuestos: cobre $20.", TipoCartaEvento.RecibirDinero, 20),
            new CartaEvento("Hereda $100.", TipoCartaEvento.RecibirDinero, 100),
            new CartaEvento("Segundo premio en un concurso de belleza: cobre $10.", TipoCartaEvento.RecibirDinero, 10),
            new CartaEvento("Honorarios del médico: pague $50.", TipoCartaEvento.PagarDinero, 50),
            new CartaEvento("Cuenta del hospital: pague $100.", TipoCartaEvento.PagarDinero, 100),
            new CartaEvento("Cuota escolar: pague $50.", TipoCartaEvento.PagarDinero, 50),
            new CartaEvento("Participa en una carrera solidaria: avance dos casillas.", TipoCartaEvento.Avanzar, 2),
            new CartaEvento("Olvidó la billetera: regrese dos casillas.", TipoCartaEvento.Retroceder, 2),
            new CartaEvento("Le da gripe: pierda un turno.", TipoCartaEvento.PerderTurno, 1),
            new CartaEvento("Es su cumpleaños: cobre $10 de cada jugador.", TipoCartaEvento.CobrarACadaJugador, 10),
        };
    }
}
