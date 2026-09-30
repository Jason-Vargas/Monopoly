# mfrc522.py — Driver mínimo del lector RFID MFRC522 (RC522) para MicroPython.
#
# Origen: escrito para el proyecto "Monopoly Distribuido" (TEC, Algoritmos y Estructuras de Datos 1),
# a partir de la hoja de datos de NXP (MFRC522, rev. 3.9) y de la norma ISO/IEC 14443-3 (REQA,
# anticolisión y selección). No copia código de otros drivers.
# Licencia: MIT. Se permite usarlo, copiarlo, modificarlo y distribuirlo libremente, conservando este
# encabezado. Se entrega "tal cual", sin garantías.
#
# Alcance: solo lee el UID de tarjetas ISO 14443-A (MIFARE Classic, Ultralight, NTAG...), de 4 o 7 bytes.
# No lee ni escribe bloques de memoria (no hace falta: la tarjeta solo identifica al jugador).

import time

# Registros del MFRC522 (hoja de datos, sección 9).
_COMMAND = 0x01
_COM_IEN = 0x02
_COM_IRQ = 0x04
_DIV_IRQ = 0x05
_ERROR = 0x06
_FIFO_DATA = 0x09
_FIFO_LEVEL = 0x0A
_CONTROL = 0x0C
_BIT_FRAMING = 0x0D
_MODE = 0x11
_TX_CONTROL = 0x14
_TX_ASK = 0x15
_CRC_RESULT_H = 0x21
_CRC_RESULT_L = 0x22
_T_MODE = 0x2A
_T_PRESCALER = 0x2B
_T_RELOAD_H = 0x2C
_T_RELOAD_L = 0x2D
_VERSION = 0x37

# Comandos del MFRC522.
_CMD_IDLE = 0x00
_CMD_CALC_CRC = 0x03
_CMD_TRANSCEIVE = 0x0C
_CMD_SOFT_RESET = 0x0F

# Comandos ISO 14443-A.
_PICC_REQA = 0x26
_PICC_ANTICOLISION_NIVEL_1 = 0x93
_PICC_ANTICOLISION_NIVEL_2 = 0x95
_ETIQUETA_CASCADA = 0x88


class MFRC522:
    """Lector RC522 conectado por SPI. Uso: lector.leer_uid() -> bytes del UID, o None si no hay tarjeta."""

    OK = 0
    SIN_TARJETA = 1
    ERROR = 2

    def __init__(self, spi, cs, rst):
        self._spi = spi
        self._cs = cs
        self._rst = rst
        self._cs.value(1)
        # Reinicio por hardware (RST en bajo y luego en alto).
        self._rst.value(0)
        time.sleep_ms(50)
        self._rst.value(1)
        time.sleep_ms(50)
        self.iniciar()

    # ------------------------------------------------------------------ acceso a registros

    def _escribir(self, registro, valor):
        self._cs.value(0)
        self._spi.write(bytes(((registro << 1) & 0x7E, valor)))
        self._cs.value(1)

    def _leer(self, registro):
        self._cs.value(0)
        self._spi.write(bytes((((registro << 1) & 0x7E) | 0x80,)))
        valor = self._spi.read(1)
        self._cs.value(1)
        return valor[0]

    def _activar_bits(self, registro, mascara):
        self._escribir(registro, self._leer(registro) | mascara)

    def _limpiar_bits(self, registro, mascara):
        self._escribir(registro, self._leer(registro) & (~mascara & 0xFF))

    # ------------------------------------------------------------------ configuración

    def iniciar(self):
        """Reinicio por software y configuración del temporizador, la modulación y la antena."""
        self._escribir(_COMMAND, _CMD_SOFT_RESET)
        time.sleep_ms(50)
        self._escribir(_T_MODE, 0x8D)        # temporizador automático, preescalador alto
        self._escribir(_T_PRESCALER, 0x3E)   # ~ 25 ms de tiempo de espera por comando
        self._escribir(_T_RELOAD_L, 30)
        self._escribir(_T_RELOAD_H, 0)
        self._escribir(_TX_ASK, 0x40)        # modulación ASK 100 %
        self._escribir(_MODE, 0x3D)          # CRC con valor inicial 0x6363 (ISO 14443-A)
        self._activar_bits(_TX_CONTROL, 0x03)  # enciende la antena (TX1 y TX2)

    def version(self):
        """Valor del registro VersionReg: 0x91 o 0x92 en chips originales; algunos clones dan 0x12, 0x88 o 0xB2."""
        return self._leer(_VERSION)

    def detectado(self):
        """Indica si el chip responde por SPI (0x00 o 0xFF suelen significar un cable suelto)."""
        return self.version() not in (0x00, 0xFF)

    # ------------------------------------------------------------------ comunicación con la tarjeta

    def _comunicar(self, datos):
        """Envía bytes a la tarjeta y devuelve (estado, bytes recibidos, cantidad de bits recibidos)."""
        self._escribir(_COM_IEN, 0x77 | 0x80)
        self._limpiar_bits(_COM_IRQ, 0x80)
        self._activar_bits(_FIFO_LEVEL, 0x80)  # vacía el FIFO
        self._escribir(_COMMAND, _CMD_IDLE)
        for byte in datos:
            self._escribir(_FIFO_DATA, byte)
        self._escribir(_COMMAND, _CMD_TRANSCEIVE)
        self._activar_bits(_BIT_FRAMING, 0x80)  # StartSend

        intentos = 2000
        while True:
            interrupciones = self._leer(_COM_IRQ)
            intentos -= 1
            # 0x30: RxIRq o IdleIRq (terminó); 0x01: TimerIRq (nadie respondió).
            if (interrupciones & 0x30) or (interrupciones & 0x01) or intentos == 0:
                break

        self._limpiar_bits(_BIT_FRAMING, 0x80)
        if intentos == 0 or (self._leer(_ERROR) & 0x1B):  # BufferOvfl, Coll, Parity, Protocol
            return MFRC522.ERROR, [], 0

        if interrupciones & 0x01 and not (interrupciones & 0x30):
            return MFRC522.SIN_TARJETA, [], 0

        cantidad = self._leer(_FIFO_LEVEL)
        ultimos_bits = self._leer(_CONTROL) & 0x07
        bits = (cantidad - 1) * 8 + ultimos_bits if ultimos_bits else cantidad * 8
        cantidad = max(1, min(cantidad, 16))
        recibidos = [self._leer(_FIFO_DATA) for _ in range(cantidad)]
        return MFRC522.OK, recibidos, bits

    def _solicitar(self):
        """REQA: pregunta si hay una tarjeta en el campo. Responde ATQA (16 bits)."""
        self._escribir(_BIT_FRAMING, 0x07)  # trama corta de 7 bits
        estado, _, bits = self._comunicar([_PICC_REQA])
        return estado == MFRC522.OK and bits == 0x10

    def _anticolision(self, nivel):
        """Obtiene 4 bytes del UID más el byte de verificación (BCC) en el nivel de cascada indicado."""
        self._escribir(_BIT_FRAMING, 0x00)
        estado, datos, _ = self._comunicar([nivel, 0x20])
        if estado != MFRC522.OK or len(datos) != 5:
            return None

        verificacion = 0
        for byte in datos[:4]:
            verificacion ^= byte
        return datos if verificacion == datos[4] else None

    def _calcular_crc(self, datos):
        """CRC_A calculado por el coprocesador del MFRC522 (byte bajo, byte alto)."""
        self._limpiar_bits(_DIV_IRQ, 0x04)
        self._activar_bits(_FIFO_LEVEL, 0x80)
        for byte in datos:
            self._escribir(_FIFO_DATA, byte)
        self._escribir(_COMMAND, _CMD_CALC_CRC)
        for _ in range(255):
            if self._leer(_DIV_IRQ) & 0x04:
                break
        return [self._leer(_CRC_RESULT_L), self._leer(_CRC_RESULT_H)]

    def _seleccionar(self, nivel, uid_con_bcc):
        """SELECT: selecciona la tarjeta en un nivel de cascada. Devuelve el SAK o None."""
        trama = [nivel, 0x70] + list(uid_con_bcc)
        trama += self._calcular_crc(trama)
        self._escribir(_BIT_FRAMING, 0x00)
        estado, datos, bits = self._comunicar(trama)
        if estado != MFRC522.OK or bits != 0x18:
            return None
        return datos[0]

    def leer_uid(self):
        """
        Devuelve el UID de la tarjeta presente (bytes de 4 o 7 de largo) o None si no hay ninguna.
        Mientras la tarjeta sigue en el campo, las lecturas alternan entre éxito y None (la tarjeta vuelve a
        reposo tras cada lectura); el programa principal filtra las repeticiones.
        """
        if not self._solicitar():
            return None

        nivel_1 = self._anticolision(_PICC_ANTICOLISION_NIVEL_1)
        if nivel_1 is None:
            return None

        if nivel_1[0] != _ETIQUETA_CASCADA:
            return bytes(nivel_1[:4])  # UID de 4 bytes

        # UID de 7 bytes: 0x88 + 3 bytes en el nivel 1, y 4 bytes más en el nivel 2.
        if self._seleccionar(_PICC_ANTICOLISION_NIVEL_1, nivel_1) is None:
            return None

        nivel_2 = self._anticolision(_PICC_ANTICOLISION_NIVEL_2)
        if nivel_2 is None:
            return None

        return bytes(nivel_1[1:4] + nivel_2[:4])
