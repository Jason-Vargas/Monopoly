# Monopoly.Core.Logica
Reglas y estado oficial de la partida, independientes de la red y de la interfaz.

- `Juego`: estado oficial, turnos (`ColaCircular<Jugador>`), fases, validaciones, efectos de casillas y cartas, eliminación, fin de partida, registro de eventos y exportación del historial. Seguro para varios hilos.
- `Banco`: única clase que mueve dinero; cada operación registra su transacción.
- `ResultadoAccion`, `InstantaneaJuego`, `EstadoJugador`, `EventoJuego`: respuestas y copias de solo lectura para la red y la interfaz.
- `OpcionesJuego`: configuración (máximo de turnos, saldo inicial, dados y cartas controlados para pruebas, carpeta de partidas).
