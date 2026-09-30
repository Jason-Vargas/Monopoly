# prueba_uid.py — Imprime el UID de cada tarjeta RFID que se acerca al RC522, para registrarlas.
#
# Uso: ábralo en Thonny (con mfrc522.py ya copiado en la Pico) y pulse "Run" (F5). No hace falta
# guardarlo en la Pico. Acerque cada tarjeta o llavero y anote su UID. Ctrl+C para terminar.

import time

from machine import Pin, SPI

from mfrc522 import MFRC522

ESPERA_MISMA_TARJETA_MS = 2000

spi = SPI(0, baudrate=1000000, polarity=0, phase=0, sck=Pin(18), mosi=Pin(19), miso=Pin(16))
lector = MFRC522(spi, Pin(17, Pin.OUT), Pin(20, Pin.OUT))

version = lector.version()
print("VersionReg del RC522: 0x{:02X}".format(version))
if not lector.detectado():
    print("El RC522 no responde: revise SCK=GP18, MOSI=GP19, MISO=GP16, SDA=GP17, RST=GP20, 3V3 y GND.")
else:
    print("Acerque una tarjeta... (Ctrl+C para terminar)")

ultimo_uid = None
ultima_vez = 0
contador = 0
while True:
    try:
        uid = lector.leer_uid()
        ahora = time.ticks_ms()
        if uid is not None:
            texto = "".join("{:02X}".format(byte) for byte in uid)
            if texto != ultimo_uid or time.ticks_diff(ahora, ultima_vez) >= ESPERA_MISMA_TARJETA_MS:
                contador += 1
                print("Tarjeta {}: UID = {}  ({} bytes)".format(contador, texto, len(uid)))
            ultimo_uid = texto
            ultima_vez = ahora
    except Exception as error:
        print("Error de lectura (se ignora):", error)
    time.sleep_ms(100)
