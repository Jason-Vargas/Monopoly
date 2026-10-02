from machine import Pin, SPI
from mfrc522 import MFRC522
import time

# SPI0
spi = SPI(
    0,
    baudrate=1000000,
    polarity=0,
    phase=0,
    sck=Pin(18),
    mosi=Pin(19),
    miso=Pin(16)
)

# RC522
rfid = MFRC522(
    spi,
    gpioRst=20,
    gpioCs=17
)

print("================================")
print("      LECTOR RFID RC522")
print("================================")
print("Acerque una tarjeta...")
print()

while True:

    # Buscar una tarjeta
    (stat, tag_type) = rfid.request(rfid.REQIDL)

    if stat == rfid.OK:

        # Leer UID
        (stat, uid) = rfid.anticoll()

        if stat == rfid.OK:

            uid_string = ""

            for i in uid:
                uid_string += "{:02X} ".format(i)

            print("Tarjeta detectada!")
            print("UID:", uid_string)
            print("----------------------------")

            # Evita leer la misma tarjeta cientos de veces
            time.sleep(1)

    time.sleep(0.1)