# Monopoly.Core.Red
Comunicación TCP entre el banco (servidor) y los jugadores (clientes). El protocolo está documentado en `docs/protocolo.md`.

- `Protocolo`, `Mensaje`: formato de línea `COMANDO|campo|...` con escapes y constantes de comandos.
- `Servidor`: `TcpListener`, un hilo por cliente, difusión de EVENTO/ESTADO/FIN, manejo de desconexiones y reconexión.
- `Cliente`: `TcpClient` con hilo lector y eventos C# para la interfaz.
- `EstadoRed`, `SerializadorEstado`, `SerializadorTransacciones`, `FiltroTransacciones`: contenido de los mensajes ESTADO y TRANSACCIONES.
