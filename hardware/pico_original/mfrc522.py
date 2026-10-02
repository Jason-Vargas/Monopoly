from machine import Pin
from time import sleep_ms


class MFRC522:

    OK = 0
    NOTAGERR = 1
    ERR = 2

    REQIDL = 0x26
    REQALL = 0x52

    AUTHENT1A = 0x60
    AUTHENT1B = 0x61

    PCD_IDLE = 0x00
    PCD_AUTHENT = 0x0E
    PCD_TRANSCEIVE = 0x0C
    PCD_RESETPHASE = 0x0F
    PCD_CALCCRC = 0x03

    PICC_ANTICOLL = 0x93

    # Registros
    CommandReg = 0x01
    CommIEnReg = 0x02
    CommIrqReg = 0x04
    ErrorReg = 0x06
    Status2Reg = 0x08
    FIFODataReg = 0x09
    FIFOLevelReg = 0x0A
    ControlReg = 0x0C
    BitFramingReg = 0x0D
    CollReg = 0x0E

    ModeReg = 0x11
    TxModeReg = 0x12
    RxModeReg = 0x13
    TxControlReg = 0x14
    TxASKReg = 0x15

    CRCResultRegH = 0x21
    CRCResultRegL = 0x22

    TModeReg = 0x2A
    TPrescalerReg = 0x2B
    TReloadRegH = 0x2C
    TReloadRegL = 0x2D

    VersionReg = 0x37

    def __init__(self, spi, gpioRst, gpioCs):

        self.spi = spi

        self.rst = Pin(gpioRst, Pin.OUT)
        self.cs = Pin(gpioCs, Pin.OUT)

        # CS desactivado
        self.cs.value(1)

        # Reset
        self.rst.value(0)
        sleep_ms(50)

        self.rst.value(1)
        sleep_ms(50)

        # Configuración del RC522
        self._wreg(self.TModeReg, 0x8D)
        self._wreg(self.TPrescalerReg, 0x3E)
        self._wreg(self.TReloadRegL, 30)
        self._wreg(self.TReloadRegH, 0)

        self._wreg(self.TxASKReg, 0x40)
        self._wreg(self.ModeReg, 0x3D)

        # Encender antena
        self.antenna_on()

    # ------------------------------------------------
    # ESCRIBIR REGISTRO
    # ------------------------------------------------

    def _wreg(self, reg, val):

        self.cs.value(0)

        address = (reg << 1) & 0x7E

        self.spi.write(bytes([address, val]))

        self.cs.value(1)

    # ------------------------------------------------
    # LEER REGISTRO
    # ------------------------------------------------

    def _rreg(self, reg):

        self.cs.value(0)

        address = ((reg << 1) & 0x7E) | 0x80

        self.spi.write(bytes([address]))

        value = self.spi.read(1)[0]

        self.cs.value(1)

        return value

    # ------------------------------------------------
    # ACTIVAR BITS
    # ------------------------------------------------

    def _sflags(self, reg, mask):

        value = self._rreg(reg)

        self._wreg(reg, value | mask)

    # ------------------------------------------------
    # DESACTIVAR BITS
    # ------------------------------------------------

    def _cflags(self, reg, mask):

        value = self._rreg(reg)

        self._wreg(reg, value & (~mask))

    # ------------------------------------------------
    # ENCENDER ANTENA
    # ------------------------------------------------

    def antenna_on(self):

        value = self._rreg(self.TxControlReg)

        if (value & 0x03) != 0x03:

            self._sflags(self.TxControlReg, 0x03)

    # ------------------------------------------------
    # APAGAR ANTENA
    # ------------------------------------------------

    def antenna_off(self):

        self._cflags(self.TxControlReg, 0x03)

    # ------------------------------------------------
    # BUSCAR TARJETA
    # ------------------------------------------------

    def request(self, mode):

        self._wreg(self.BitFramingReg, 0x07)

        # IMPORTANTE:
        # mode debe convertirse en una lista
        status, back_data, back_bits = self._tocard(
            self.PCD_TRANSCEIVE,
            [mode]
        )

        if status != self.OK or back_bits != 0x10:

            status = self.ERR

        return status, back_bits

    # ------------------------------------------------
    # LEER UID
    # ------------------------------------------------

    def anticoll(self):

        serial_number = []

        self._wreg(self.BitFramingReg, 0x00)

        status, back_data, back_bits = self._tocard(
            self.PCD_TRANSCEIVE,
            [self.PICC_ANTICOLL, 0x20]
        )

        if status == self.OK:

            if len(back_data) == 5:

                checksum = 0

                for i in range(4):

                    checksum = checksum ^ back_data[i]

                if checksum != back_data[4]:

                    status = self.ERR

                else:

                    serial_number = back_data

        return status, serial_number

    # ------------------------------------------------
    # COMUNICACIÓN CON RC522
    # ------------------------------------------------

    def _tocard(self, command, send_data):

        back_data = []
        back_len = 0

        irq_en = 0
        wait_irq = 0

        if command == self.PCD_AUTHENT:

            irq_en = 0x12
            wait_irq = 0x10

        elif command == self.PCD_TRANSCEIVE:

            irq_en = 0x77
            wait_irq = 0x30

        # Configurar interrupciones
        self._wreg(
            self.CommIEnReg,
            irq_en | 0x80
        )

        # Limpiar interrupciones
        self._cflags(
            self.CommIrqReg,
            0x80
        )

        # Limpiar FIFO
        self._sflags(
            self.FIFOLevelReg,
            0x80
        )

        # Poner RC522 en estado idle
        self._wreg(
            self.CommandReg,
            self.PCD_IDLE
        )

        # ------------------------------------------------
        # ENVIAR DATOS
        # ------------------------------------------------

        for value in send_data:

            self._wreg(
                self.FIFODataReg,
                value
            )

        # Ejecutar comando

        self._wreg(
            self.CommandReg,
            command
        )

        # Iniciar transmisión

        if command == self.PCD_TRANSCEIVE:

            self._sflags(
                self.BitFramingReg,
                0x80
            )

        # ------------------------------------------------
        # ESPERAR RESPUESTA
        # ------------------------------------------------

        timeout = 2000

        while True:

            irq = self._rreg(
                self.CommIrqReg
            )

            timeout -= 1

            if timeout == 0:

                break

            if irq & 0x01:

                break

            if irq & wait_irq:

                break

        # Detener transmisión

        if command == self.PCD_TRANSCEIVE:

            self._cflags(
                self.BitFramingReg,
                0x80
            )

        # ------------------------------------------------
        # COMPROBAR ERROR
        # ------------------------------------------------

        if timeout == 0:

            return self.ERR, [], 0

        error = self._rreg(
            self.ErrorReg
        )

        if error & 0x1B:

            return self.ERR, [], 0

        status = self.OK

        # ------------------------------------------------
        # LEER RESPUESTA
        # ------------------------------------------------

        if irq & irq_en & 0x01:

            status = self.NOTAGERR

        elif command == self.PCD_TRANSCEIVE:

            number = self._rreg(
                self.FIFOLevelReg
            )

            last_bits = self._rreg(
                self.ControlReg
            ) & 0x07

            if last_bits != 0:

                back_len = (number - 1) * 8 + last_bits

            else:

                back_len = number * 8

            # Máximo 16 bytes
            if number == 0:

                number = 1

            if number > 16:

                number = 16

            # Leer FIFO

            for _ in range(number):

                back_data.append(
                    self._rreg(
                        self.FIFODataReg
                    )
                )

        return status, back_data, back_len