# main.py - Módulo electrónico del Monopoly Distribuido (Raspberry Pi Pico W + MicroPython).
#
# Lector RFID RC522, botón y LED de pago, conectados por USB serial a la computadora del
# organizador (que aloja al banco). Por el serial solo salen líneas del protocolo:
#
#   Pico -> PC:  LISTO               al arrancar
#                BOTON               una vez por pulsación del botón
#                RFID:<UID>          al leer una tarjeta (4 bytes en hexadecimal, mayúsculas)
#                PONG                respuesta a PING
#   PC -> Pico:  DADOS:d1,d2         confirma la recepción parpadeando el LED integrado
#                PAGO_OK             enciende el LED de pago durante 2 segundos
#                PAGO_RECHAZADO      el LED de pago parpadea rápido 3 veces
#                LIMPIAR             apaga el LED de pago
#                PING                responde PONG
#
# Los dados los genera el servidor (el estado oficial vive en el banco): la Pico no decide nada.

import sys
import time

import select
from machine import Pin, SPI

from mfrc522 import MFRC522

# ================================================================== CONFIGURACIÓN

# Lector RC522 por SPI0, alimentado desde 3V3(OUT).
PIN_SCK = 18
PIN_MOSI = 19
PIN_MISO = 16
PIN_CS = 17
PIN_RST = 20

PIN_BOTON = 15       # botón entre GP15 y GND (pull-up interno)
PIN_LED_PAGO = 14    # LED externo con resistencia en serie, cátodo a GND (activo en alto)

INTERVALO_BOTON_MS = 10          # cada cuánto se revisa el botón
INTERVALO_RFID_MS = 100          # cada cuánto se busca una tarjeta
ANTIRREBOTE_MS = 50              # tiempo que el botón debe estar estable
ESPERA_MISMA_TARJETA_MS = 2000   # la misma tarjeta se ignora hasta 2 s después de dejar de verla
DURACION_PAGO_OK_MS = 2000       # LED de pago encendido tras PAGO_OK
PARPADEOS_RECHAZO = 3            # parpadeos del LED de pago tras PAGO_RECHAZADO
MEDIO_PARPADEO_RECHAZO_MS = 100  # 100 ms encendido + 100 ms apagado
PARPADEO_INTEGRADO_MS = 60       # destello del LED integrado
LARGO_MAXIMO_LINEA = 64
PAUSA_BUCLE_MS = 2

# True solo para depurar en Thonny: imprime errores como líneas "#..." (el juego las ignora).
DEPURAR = False


def enviar(linea):
    """Envía una línea del protocolo por el USB serial."""
    print(linea)


def depurar(texto):
    if DEPURAR:
        print("# " + texto)


def ya_paso(instante_ms, ahora_ms):
    """Indica si se alcanzó instante_ms (correcto aunque ticks_ms se desborde)."""
    return time.ticks_diff(ahora_ms, instante_ms) >= 0


# ================================================================== COMPONENTES


class Boton:
    """Botón a GND con pull-up interno: avisa una sola vez por pulsación (flanco de bajada) con antirrebote."""

    def __init__(self, numero):
        self._pin = Pin(numero, Pin.IN, Pin.PULL_UP)
        self._estable = self._pin.value()
        self._ultima_lectura = self._estable
        self._cambio_ms = time.ticks_ms()

    def fue_presionado(self, ahora_ms):
        lectura = self._pin.value()
        if lectura != self._ultima_lectura:
            # Hubo un cambio: se espera a que la lectura se mantenga estable.
            self._ultima_lectura = lectura
            self._cambio_ms = ahora_ms
            return False

        if lectura != self._estable and time.ticks_diff(ahora_ms, self._cambio_ms) >= ANTIRREBOTE_MS:
            self._estable = lectura
            return lectura == 0  # solo el paso de suelto (1) a presionado (0)

        return False


class LedPago:
    """LED de pago en GP14: encendido 2 s (pago aceptado) o 3 parpadeos rápidos (rechazado), sin sleep."""

    def __init__(self, numero):
        self._pin = Pin(numero, Pin.OUT, value=0)
        self._cambios_restantes = 0  # transiciones pendientes del parpadeo
        self._proximo_cambio = None  # instante del próximo cambio (apagar o alternar)

    def aceptado(self, ahora_ms):
        self._pin.value(1)
        self._cambios_restantes = 1
        self._proximo_cambio = time.ticks_add(ahora_ms, DURACION_PAGO_OK_MS)

    def rechazado(self, ahora_ms):
        # Encendido, apagado, encendido, apagado, encendido, apagado: 6 estados, 5 cambios más.
        self._pin.value(1)
        self._cambios_restantes = PARPADEOS_RECHAZO * 2 - 1
        self._proximo_cambio = time.ticks_add(ahora_ms, MEDIO_PARPADEO_RECHAZO_MS)

    def apagar(self):
        self._pin.value(0)
        self._cambios_restantes = 0
        self._proximo_cambio = None

    def actualizar(self, ahora_ms):
        if self._proximo_cambio is None or not ya_paso(self._proximo_cambio, ahora_ms):
            return

        self._pin.value(1 - self._pin.value())
        self._cambios_restantes -= 1
        if self._cambios_restantes > 0:
            self._proximo_cambio = time.ticks_add(ahora_ms, MEDIO_PARPADEO_RECHAZO_MS)
        else:
            self.apagar()


class LedIntegrado:
    """LED de la placa: destellos cortos sin bloquear el programa."""

    def __init__(self):
        self._led = Pin("LED", Pin.OUT, value=0)
        self._apagar_en = None

    def destello(self, ahora_ms):
        self._led.value(1)
        self._apagar_en = time.ticks_add(ahora_ms, PARPADEO_INTEGRADO_MS)

    def actualizar(self, ahora_ms):
        if self._apagar_en is not None and ya_paso(self._apagar_en, ahora_ms):
            self._led.value(0)
            self._apagar_en = None


class LectorTarjetas:
    """
    Lector RC522. Devuelve el UID (4 bytes en hex) de cada tarjeta nueva. La misma tarjeta no se
    repite mientras siga apoyada ni hasta 2 s después de retirarla; una tarjeta distinta se lee enseguida.
    """

    def __init__(self, rc522):
        self._rc522 = rc522
        self._ultimo_uid = None
        self._ultima_vez_vista = 0

    def leer(self, ahora_ms):
        estado, _ = self._rc522.request(self._rc522.REQIDL)
        if estado != self._rc522.OK:
            return None

        estado, datos = self._rc522.anticoll()
        if estado != self._rc522.OK or len(datos) < 4:
            return None

        # anticoll devuelve 5 bytes: los 4 del UID y el checksum (BCC), que no forma parte del UID.
        uid = "".join("{:02X}".format(byte) for byte in datos[:4])
        repetida = uid == self._ultimo_uid and time.ticks_diff(ahora_ms, self._ultima_vez_vista) < ESPERA_MISMA_TARJETA_MS
        self._ultimo_uid = uid
        self._ultima_vez_vista = ahora_ms
        return None if repetida else uid


class EntradaSerial:
    """Lee líneas del USB serial sin bloquear (sys.stdin + select.poll)."""

    def __init__(self, entrada=sys.stdin):
        self._entrada = entrada
        self._sondeo = select.poll()
        self._sondeo.register(entrada, select.POLLIN)
        self._buffer = ""

    def lineas(self):
        completas = []
        while self._sondeo.poll(0):
            caracter = self._entrada.read(1)
            if not caracter:
                break
            if caracter in "\r\n":
                if self._buffer:
                    completas.append(self._buffer.strip())
                    self._buffer = ""
            elif len(self._buffer) < LARGO_MAXIMO_LINEA:
                self._buffer += caracter
        return completas


# ================================================================== LÓGICA


def dados_validos(texto):
    """Indica si texto tiene la forma "d1,d2" con valores de 1 a 6."""
    partes = texto.split(",")
    if len(partes) != 2:
        return False
    try:
        return all(1 <= int(parte.strip()) <= 6 for parte in partes)
    except ValueError:
        return False


class Modulo:
    """Une los componentes y atiende el protocolo; cada paso del bucle es corto y no bloquea."""

    def __init__(self, boton, lector, led_pago, led_integrado, entrada):
        self.boton = boton
        self.lector = lector
        self.led_pago = led_pago
        self.led_integrado = led_integrado
        self.entrada = entrada
        ahora = time.ticks_ms()
        self._proximo_boton = ahora
        self._proximo_rfid = ahora

    def procesar_linea(self, linea, ahora_ms):
        comando = linea.strip().upper()
        if comando == "PING":
            enviar("PONG")
        elif comando == "PAGO_OK":
            self.led_pago.aceptado(ahora_ms)
        elif comando == "PAGO_RECHAZADO":
            self.led_pago.rechazado(ahora_ms)
        elif comando == "LIMPIAR":
            self.led_pago.apagar()
        elif comando.startswith("DADOS:") and dados_validos(comando[6:]):
            self.led_integrado.destello(ahora_ms)
        else:
            depurar("comando ignorado: " + linea)

    def paso(self, ahora_ms):
        for linea in self.entrada.lineas():
            self.procesar_linea(linea, ahora_ms)

        if ya_paso(self._proximo_boton, ahora_ms):
            self._proximo_boton = time.ticks_add(ahora_ms, INTERVALO_BOTON_MS)
            if self.boton.fue_presionado(ahora_ms):
                enviar("BOTON")
                self.led_integrado.destello(ahora_ms)

        if ya_paso(self._proximo_rfid, ahora_ms):
            self._proximo_rfid = time.ticks_add(ahora_ms, INTERVALO_RFID_MS)
            uid = self.lector.leer(ahora_ms)
            if uid is not None:
                enviar("RFID:" + uid)
                self.led_integrado.destello(ahora_ms)

        self.led_pago.actualizar(ahora_ms)
        self.led_integrado.actualizar(ahora_ms)


def principal():
    led_pago = LedPago(PIN_LED_PAGO)   # apagado desde el arranque
    led_integrado = LedIntegrado()
    spi = SPI(0, baudrate=1000000, polarity=0, phase=0,
              sck=Pin(PIN_SCK), mosi=Pin(PIN_MOSI), miso=Pin(PIN_MISO))
    lector = LectorTarjetas(MFRC522(spi, gpioRst=PIN_RST, gpioCs=PIN_CS))
    modulo = Modulo(Boton(PIN_BOTON), lector, led_pago, led_integrado, EntradaSerial())
    enviar("LISTO")

    while True:
        try:
            modulo.paso(time.ticks_ms())
        except KeyboardInterrupt:
            raise  # Ctrl+C desde Thonny detiene el programa a propósito
        except Exception as error:
            # Un error de lectura (tarjeta retirada a mitad de la lectura, ruido en SPI...) no detiene
            # el programa. No se imprime nada para no mandar al juego líneas fuera del protocolo.
            depurar("error: " + str(error))
        time.sleep_ms(PAUSA_BUCLE_MS)


if __name__ == "__main__":
    principal()
