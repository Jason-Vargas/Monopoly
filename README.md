# Monopoly Distribuido con Estructuras Lineales

Proyecto 1 del curso **Algoritmos y Estructuras de Datos 1** — Instituto Tecnológico de Costa Rica, II Semestre 2026.

Versión simplificada de Monopoly Electrónico (tema clásico, edición Atlantic City en español) con arquitectura cliente-servidor sobre TCP, estructuras de datos lineales implementadas desde cero y un módulo electrónico (lector RFID, botón para los dados y LED de pago) basado en una Raspberry Pi Pico W con MicroPython, conectada por USB a la computadora del organizador (ver [`hardware/README.md`](hardware/README.md)).

> Proyecto académico sin afiliación con Hasbro. No se usan el logotipo ni la mascota oficiales.

## Estructura

| Proyecto | Tipo | Contenido |
|---|---|---|
| `src/Monopoly.Core` | Biblioteca de clases | Estructuras de datos, modelo, lógica del juego, red y hardware |
| `src/Monopoly.App` | Windows Forms | Interfaz gráfica (servidor/organizador y clientes) |
| `tests/Monopoly.Tests` | xUnit | Pruebas unitarias y de integración (red y cajero) |

```
Monopoly/
├── Monopoly.sln
├── Directory.Build.props        configuración común (nullable, sin usings implícitos, documentación XML)
├── publicar.ps1                 genera el .exe autocontenido en publicar/win-x64/
├── README.md                    este manual
├── CLAUDE.md                    decisiones técnicas y checklist de requisitos
├── docs/
│   ├── enunciado.pdf            enunciado del proyecto
│   ├── protocolo.md             protocolo TCP cliente-servidor
│   └── prueba-en-red.md         firewall, IP y prueba con varias computadoras
├── hardware/
│   ├── README.md                materiales, conexiones, instalación y pruebas de la Pico W
│   └── pico/                    main.py (firmware), mfrc522.py (driver) y prueba_uid.py
├── src/
│   ├── Monopoly.Core/
│   │   ├── Estructuras/         ListaSimple, ListaDobleEnlazada, ListaCircularDoble, ColaCircular, Cola
│   │   ├── Modelo/              Jugador, Casilla y derivadas, cartas, Transaccion, Dado, Tablero
│   │   ├── Logica/              Juego, Banco, estado del turno y exportación TXT
│   │   ├── Red/                 Servidor, Cliente y protocolo
│   │   └── Hardware/            cajero Pico W (serie) y cajero simulado
│   └── Monopoly.App/
│       ├── Estilo/              tema, fuentes, controles dibujados e ilustraciones
│       └── *.cs                 ventanas, tablero y paneles
└── tests/Monopoly.Tests/        Estructuras/, Modelo/, Logica/, Red/, Hardware/ y PruebasRestricciones
```

Cada carpeta de `src/Monopoly.Core` tiene un `Leeme.md` con lo que contiene (el de `Estructuras/` explica qué estructura se usa para qué). Las partidas exportadas se guardan en `partidas/` (no se sube a git).

El enunciado está en [`docs/enunciado.pdf`](docs/enunciado.pdf) y el protocolo cliente-servidor en [`docs/protocolo.md`](docs/protocolo.md).

## Requisitos

- Windows 10/11
- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) o superior

## Compilar, probar y ejecutar

```bash
dotnet build Monopoly.sln
dotnet test Monopoly.sln
dotnet run --project src/Monopoly.App
```

## Ejecutable para otras computadoras

```powershell
powershell -ExecutionPolicy Bypass -File .\publicar.ps1
```

Genera `publicar\win-x64\Monopoly.App.exe`, un único archivo que incluye .NET (no hay que instalar nada en la otra computadora). Para jugar en red (firewall, IP, solución de problemas) vea [`docs/prueba-en-red.md`](docs/prueba-en-red.md).

## Jugar con la interfaz gráfica

1. El organizador abre la aplicación, escribe su nombre, elige su ficha y su color, y pulsa **Crear partida**: su equipo aloja al banco y la sala de espera muestra la dirección (IP y puerto) que debe compartir, con un botón para copiarla.
2. Los demás abren la aplicación, escriben su nombre, eligen ficha y color, escriben la IP y el puerto, y pulsan **Unirse a partida**. Dos jugadores no pueden tener la misma ficha ni el mismo color.
3. Con 2 a 4 jugadores, el organizador pulsa **Iniciar partida**.

Para probar con 4 ventanas en una sola computadora (compilar primero):

```powershell
dotnet build Monopoly.sln
$exe = "src\Monopoly.App\bin\Debug\net8.0-windows\Monopoly.App.exe"
Start-Process $exe "--crear Ana 5000 100"
Start-Process $exe "--unirse Beto 127.0.0.1 5000"
Start-Process $exe "--unirse Carla 127.0.0.1 5000"
Start-Process $exe "--unirse Dani 127.0.0.1 5000"
```

Con estos argumentos de prueba el servidor asigna a cada ventana la primera ficha libre.

Tipografías: [Abril Fatface](https://fonts.google.com/specimen/Abril+Fatface) y [Lato](https://fonts.google.com/specimen/Lato), con licencia SIL Open Font License 1.1 (ver `src/Monopoly.App/Estilo/Fuentes/OFL-*.txt`). El logotipo "MONOPOLY TEC" y las fichas son diseño propio.

## Probar sin el cajero físico

En **Opciones** (engranaje), el organizador puede marcar **Modo sin hardware (pruebas)**: los dados y los pagos se simulan con "Simular botón" y "Simular tarjeta del jugador en turno", sin la Pico W.

## Estado

En desarrollo: estructuras de datos, modelo, lógica del juego, comunicación TCP, interfaz gráfica y cajero Pico W (botón, RFID y LED de pago por USB serial) integrados y con pruebas. Pendiente: ventana de fin y sonidos, una partida completa con el cajero físico y en 2 computadoras, y los entregables de documentación (UML, estructuras, archivo de transacciones).
