# Monopoly.Core.Hardware
Integración con el cajero electrónico: Raspberry Pi Pico W con lector RFID RC522, botón y LED de pago, conectada por USB serial a la computadora del organizador (firmware y conexiones en `hardware/README.md`).

- `IDispositivoCajero`: eventos `BotonPresionado`, `TarjetaLeida`, `EstadoCambiado`, `ErrorDispositivo`; órdenes `MostrarDados`, `IndicarPago`, `Limpiar`.
- `CajeroPorLineas`: base que interpreta el protocolo serie de líneas (`LISTO`, `BOTON`, `RFID:<UID>`, `PONG`).
- `CajeroPico`: implementación física sobre `SerialPort` (PING/PONG, detección del puerto y de la desconexión).
- `CajeroSimulado`: sin placa, para el modo sin hardware (pruebas).
