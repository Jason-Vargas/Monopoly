# main.py — Módulo electrónico del Monopoly Distribuido (Raspberry Pi Pico W + MicroPython).
#
# Hace de dado electrónico (2 displays de 7 segmentos + botón) y de lector de tarjetas RFID (RC522).
# Se conecta por USB a la computadora del organizador, que aloja al banco (servidor).
#
# IMPORTANTE: los dados NO se generan aquí. El estado oficial vive en el banco: la Pico solo pide la
# tirada (BOTON) y muestra el resultado que le envía el servidor (DADOS:d1,d2).
#
# Protocolo por USB serial (una línea de texto por mensaje):
#   Pico -> PC:  LISTO               al arrancar
#                BOTON               al presionar el botón
#                RFID:<UID en hex>   al leer una tarjeta (p. ej. RFID:A1B2C3D4)
#                PONG                respuesta a PING
#                ERROR:<detalle>     problema del módulo (el programa sigue funcionando)
#   PC -> Pico:  DADOS:d1,d2         muestra los dados (d1 y d2 de 1 a 6) con una animación
#                LIMPIAR             apaga los displays
#                PING                comprobación de conexión

import sys
import time

import select
from machine import Pin, SPI

from mfrc522 import MFRC522

# =============================================================================== CONFIGURACIÓN

# Tipo de displays: True si son de CÁTODO común (el pin común va a GND, un 1 enciende el segmento);
# False si son de ÁNODO común (el pin común va a 3V3(OUT), un 0 enciende el segmento).
CATODO_COMUN = True

# Segmentos a, b, c, d, e, f, g de cada display (conexión directa, una resistencia de 470 Ω por segmento).
PINES_DADO_1 = (2, 3, 4, 5, 6, 7, 8)
PINES_DADO_2 = (9, 10, 11, 12, 13, 14, 15)

# Botón entre GP21 y GND (usa la resistencia de pull-up interna).
PIN_BOTON = 21

# RC522 por SPI0, alimentado desde 3V3(OUT).
SPI_ID = 0
PIN_SCK = 18
PIN_MOSI = 19
PIN_MISO = 16
PIN_CS = 17   # SDA del módulo
PIN_RST = 20

ESPERA_MISMA_TARJETA_MS = 2000   # no se repite una tarjeta hasta 2 s después de dejar de verla
INTERVALO_RFID_MS = 100          # cada cuánto se busca una tarjeta
ANTIRREBOTE_MS = 30              # tiempo que el botón debe estar estable
PARPADEO_TARJETA_MS = 100
PARPADEO_DADOS_MS = 250
CUADROS_ANIMACION = 12           # cuadros de la animación "rodando"
DURACION_CUADRO_MS = 60
PRUEBA_DISPLAYS_MS = 600         # al arrancar se muestran "8 8" para comprobar los segmentos
REINTENTO_RC522_MS = 5000        # si el lector no responde, se reintenta cada 5 s
LARGO_MAXIMO_LINEA = 64
PAUSA_BUCLE_MS = 5

# =============================================================================== SEGMENTOS

# Segmentos (a, b, c, d, e, f, g) encendidos para cada dígito.
DIGITOS = {
    0: (1, 1, 1, 1, 1, 1, 0),
    1: (0, 1, 1, 0, 0, 0, 0),
    2: (1, 1, 0, 1, 1, 0, 1),
    3: (1, 1, 1, 1, 0, 0, 1),
    4: (0, 1, 1, 0, 0, 1, 1),
    5: (1, 0, 1, 1, 0, 1, 1),
    6: (1, 0, 1, 1, 1, 1, 1),
    7: (1, 1, 1, 0, 0, 0, 0),
    8: (1, 1, 1, 1, 1, 1, 1),
    9: (1, 1, 1, 1, 0, 1, 1),
}
APAGADO = (0, 0, 0, 0, 0, 0, 0)
GUION = (0, 0, 0, 0, 0, 0, 1)

# Animación "rodando": un segmento recorre el contorno del display (a, b, c, d, e, f).
GIRO = (
    (1, 0, 0, 0, 0, 0, 0),
    (0, 1, 0, 0, 0, 0, 0),
    (0, 0, 1, 0, 0, 0, 0),
    (0, 0, 0, 1, 0, 0, 0),
    (0, 0, 0, 0, 1, 0, 0),
    (0, 0, 0, 0, 0, 1, 0),
)


def enviar(mensaje):
    """Envía una línea a la computadora por el USB serial."""
    print(mensaje)


def ya_paso(desde_ms, ahora_ms):
    """Indica si ya se alcanzó el instante desde_ms (compatible con el desborde de ticks_ms)."""
    return time.ticks_diff(ahora_ms, desde_ms) >= 0


# =============================================================================== COMPONENTES


class Display:
    """Display de 7 segmentos conectado directamente (sin multiplexar) a siete GPIO."""

    def __init__(self, pines):
        # Se crean ya apagados (con ánodo común, "apagado" es un 1) para que no destellen al arrancar.
        self._pines = [Pin(numero, Pin.OUT, value=0 if CATODO_COMUN else 1) for numero in pines]
        self.mostrar(APAGADO)

    def mostrar(self, segmentos):
        for pin, encendido in zip(self._pines, segmentos):
            pin.value(encendido if CATODO_COMUN else 1 - encendido)

    def digito(self, valor):
        self.mostrar(DIGITOS.get(valor, GUION))


class Dados:
    """Los dos displays de los dados, con la animación "rodando" sin bloquear el programa."""

    def __init__(self):
        self._display_1 = Display(PINES_DADO_1)
        self._display_2 = Display(PINES_DADO_2)
        self._valores = None
        self._cuadro = 0
        self._proximo_cuadro = 0
        self._animando = False

    def probar(self):
        """Enciende todos los segmentos ("8 8") para comprobar las conexiones."""
        self._display_1.digito(8)
        self._display_2.digito(8)

    def limpiar(self):
        self._animando = False
        self._valores = None
        self._display_1.mostrar(APAGADO)
        self._display_2.mostrar(APAGADO)

    def mostrar_tirada(self, dado_1, dado_2, ahora_ms):
        """Inicia la animación; al terminar quedan los valores recibidos del servidor."""
        self._valores = (dado_1, dado_2)
        self._cuadro = 0
        self._proximo_cuadro = ahora_ms
        self._animando = True

    def actualizar(self, ahora_ms):
        if not self._animando or not ya_paso(self._proximo_cuadro, ahora_ms):
            return

        if self._cuadro < CUADROS_ANIMACION:
            # Los dos displays giran desfasados para que parezcan dados distintos.
            self._display_1.mostrar(GIRO[self._cuadro % len(GIRO)])
            self._display_2.mostrar(GIRO[(self._cuadro + 3) % len(GIRO)])
            self._cuadro += 1
            self._proximo_cuadro = time.ticks_add(ahora_ms, DURACION_CUADRO_MS)
            return

        self._display_1.digito(self._valores[0])
        self._display_2.digito(self._valores[1])
        self._animando = False


class Boton:
    """Botón a GND con pull-up interno. Informa cada pulsación una sola vez, con antirrebote."""

    def __init__(self, numero):
        self._pin = Pin(numero, Pin.IN, Pin.PULL_UP)
        self._estable = 1
        self._ultima_lectura = 1
        self._cambio_ms = time.ticks_ms()

    def fue_presionado(self, ahora_ms):
        lectura = self._pin.value()
        if lectura != self._ultima_lectura:
            self._ultima_lectura = lectura
            self._cambio_ms = ahora_ms
            return False

        if lectura != self._estable and time.ticks_diff(ahora_ms, self._cambio_ms) >= ANTIRREBOTE_MS:
            self._estable = lectura
            return lectura == 0  # 0 = presionado (el botón une el pin con GND)

        return False


class Indicador:
    """LED integrado de la Pico W: parpadeos cortos sin bloquear el programa."""

    def __init__(self):
        self._led = Pin("LED", Pin.OUT)
        self._led.off()
        self._apagar_en = None

    def parpadear(self, duracion_ms, ahora_ms):
        self._led.on()
        self._apagar_en = time.ticks_add(ahora_ms, duracion_ms)

    def actualizar(self, ahora_ms):
        if self._apagar_en is not None and ya_paso(self._apagar_en, ahora_ms):
            self._led.off()
            self._apagar_en = None


class LectorTarjetas:
    """
    Lector RC522. Devuelve el UID de cada tarjeta nueva. Una misma tarjeta no se vuelve a informar
    mientras siga apoyada, ni hasta 2 s después de retirarla (evita lecturas en ráfaga).
    """

    def __init__(self):
        self._lector = None
        self._ultimo_uid = None
        self._ultima_vez_vista = 0
        self._proxima_lectura = 0
        self._proximo_intento = 0

    def iniciar(self, ahora_ms):
        try:
            spi = SPI(SPI_ID, baudrate=1000000, polarity=0, phase=0,
                      sck=Pin(PIN_SCK), mosi=Pin(PIN_MOSI), miso=Pin(PIN_MISO))
            lector = MFRC522(spi, Pin(PIN_CS, Pin.OUT), Pin(PIN_RST, Pin.OUT))
            if not lector.detectado():
                raise OSError("RC522 no responde; revise las conexiones SPI y la alimentación de 3,3 V")
            self._lector = lector
        except Exception as error:
            self._lector = None
            self._proximo_intento = time.ticks_add(ahora_ms, REINTENTO_RC522_MS)
            enviar("ERROR:" + str(error))

    def leer(self, ahora_ms):
        """Devuelve el UID en hexadecimal (mayúsculas) de una tarjeta nueva, o None."""
        if self._lector is None:
            if ya_paso(self._proximo_intento, ahora_ms):
                self.iniciar(ahora_ms)
            return None

        if not ya_paso(self._proxima_lectura, ahora_ms):
            return None

        self._proxima_lectura = time.ticks_add(ahora_ms, INTERVALO_RFID_MS)
        uid = self._lector.leer_uid()
        if uid is None:
            return None

        texto = "".join("{:02X}".format(byte) for byte in uid)
        repetida = texto == self._ultimo_uid and time.ticks_diff(ahora_ms, self._ultima_vez_vista) < ESPERA_MISMA_TARJETA_MS
        self._ultimo_uid = texto
        self._ultima_vez_vista = ahora_ms
        return None if repetida else texto


class LectorSerial:
    """Lee líneas del USB serial sin bloquear (sys.stdin + select.poll)."""

    def __init__(self, entrada=sys.stdin):
        self._entrada = entrada
        self._sondeo = select.poll()
        self._sondeo.register(entrada, select.POLLIN)
        self._buffer = ""

    def lineas(self):
        """Devuelve las líneas completas recibidas hasta ahora."""
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


# =============================================================================== LÓGICA


class ModuloElectronico:
    """Une los componentes y atiende el protocolo con la computadora."""

    def __init__(self, dados, boton, indicador, tarjetas, serial):
        self.dados = dados
        self.boton = boton
        self.indicador = indicador
        self.tarjetas = tarjetas
        self.serial = serial

    def procesar_linea(self, linea, ahora_ms):
        """Atiende un mensaje recibido de la computadora."""
        comando = linea.strip().upper()
        if comando == "PING":
            enviar("PONG")
        elif comando == "LIMPIAR":
            self.dados.limpiar()
        elif comando.startswith("DADOS:"):
            valores = interpretar_dados(comando[6:])
            if valores is None:
                enviar("ERROR:formato de DADOS invalido (use DADOS:d1,d2 con valores de 1 a 6): " + linea)
                return
            self.dados.mostrar_tirada(valores[0], valores[1], ahora_ms)
            self.indicador.parpadear(PARPADEO_DADOS_MS, ahora_ms)
        elif comando:
            enviar("ERROR:comando desconocido: " + linea)

    def paso(self, ahora_ms):
        """Una vuelta del bucle principal: nada de lo que hace bloquea."""
        for linea in self.serial.lineas():
            self.procesar_linea(linea, ahora_ms)

        if self.boton.fue_presionado(ahora_ms):
            enviar("BOTON")

        uid = self.tarjetas.leer(ahora_ms)
        if uid is not None:
            enviar("RFID:" + uid)
            self.indicador.parpadear(PARPADEO_TARJETA_MS, ahora_ms)

        self.dados.actualizar(ahora_ms)
        self.indicador.actualizar(ahora_ms)


def interpretar_dados(texto):
    """Convierte "3,5" en (3, 5). Devuelve None si el formato o los valores no son válidos."""
    partes = texto.split(",")
    if len(partes) != 2:
        return None
    try:
        dado_1 = int(partes[0].strip())
        dado_2 = int(partes[1].strip())
    except ValueError:
        return None
    if not (1 <= dado_1 <= 6 and 1 <= dado_2 <= 6):
        return None
    return dado_1, dado_2


def principal():
    dados = Dados()
    dados.probar()
    indicador = Indicador()
    indicador.parpadear(PRUEBA_DISPLAYS_MS, time.ticks_ms())
    time.sleep_ms(PRUEBA_DISPLAYS_MS)
    dados.limpiar()

    tarjetas = LectorTarjetas()
    tarjetas.iniciar(time.ticks_ms())
    modulo = ModuloElectronico(dados, Boton(PIN_BOTON), indicador, tarjetas, LectorSerial())
    enviar("LISTO")

    ultimo_error_ms = None
    while True:
        try:
            modulo.paso(time.ticks_ms())
        except KeyboardInterrupt:
            raise  # Ctrl+C desde Thonny detiene el programa a propósito.
        except Exception as error:
            # Un error de lectura (tarjeta retirada a mitad de la lectura, ruido en SPI...) no detiene
            # el programa. Se informa como máximo una vez por segundo para no saturar el puerto.
            ahora = time.ticks_ms()
            if ultimo_error_ms is None or time.ticks_diff(ahora, ultimo_error_ms) >= 1000:
                enviar("ERROR:" + str(error))
                ultimo_error_ms = ahora
        time.sleep_ms(PAUSA_BUCLE_MS)


if __name__ == "__main__":
    principal()
