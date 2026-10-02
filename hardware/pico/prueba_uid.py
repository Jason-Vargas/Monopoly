# prueba_uid.py - Imprime el UID (4 bytes en hex) de cada tarjeta que se acerca al RC522, para registrarlas.
#
# Uso: ábralo en Thonny (con mfrc522.py ya copiado en la Pico) y pulse Run (F5); no hace falta
# guardarlo en la Pico. Acerque cada tarjeta y anote su UID. Ctrl+C para terminar.

import time

from machine import Pin, SPI

from mfrc522 import MFRC522

spi = SPI(0, baudrate=1000000, polarity=0, phase=0, sck=Pin(18), mosi=Pin(19), miso=Pin(16))
rfid = MFRC522(spi, gpioRst=20, gpioCs=17)
print("VersionReg del RC522: 0x{:02X} (0x00 o 0xFF = revisar cableado)".format(rfid._rreg(rfid.VersionReg)))
print("Acerque una tarjeta... (Ctrl+C para terminar)")

ultimo_uid = None
ultima_vez = 0
while True:
    try:
        estado, _ = rfid.request(rfid.REQIDL)
        if estado == rfid.OK:
            estado, datos = rfid.anticoll()
            if estado == rfid.OK and len(datos) >= 4:
                uid = "".join("{:02X}".format(byte) for byte in datos[:4])
                ahora = time.ticks_ms()
                if uid != ultimo_uid or time.ticks_diff(ahora, ultima_vez) >= 2000:
                    print("UID:", uid)
                ultimo_uid = uid
                ultima_vez = ahora
    except Exception as error:
        print("Error de lectura (se ignora):", error)
    time.sleep_ms(100)
