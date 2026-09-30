# Monopoly Distribuido con Estructuras Lineales

Proyecto 1 del curso **Algoritmos y Estructuras de Datos 1** — Instituto Tecnológico de Costa Rica, II Semestre 2026.

Versión simplificada de Monopoly Electrónico (tema clásico, edición Atlantic City en español) con arquitectura cliente-servidor sobre TCP, estructuras de datos lineales implementadas desde cero y un módulo electrónico (dado de 2 dígitos y lector RFID) basado en Arduino.

> Proyecto académico sin afiliación con Hasbro. No se usan el logotipo ni la mascota oficiales.

## Estructura

| Proyecto | Tipo | Contenido |
|---|---|---|
| `src/Monopoly.Core` | Biblioteca de clases | Estructuras de datos, modelo, lógica del juego, red y hardware |
| `src/Monopoly.App` | Windows Forms | Interfaz gráfica (servidor/organizador y clientes) |
| `src/Monopoly.ClienteConsola` | Consola (temporal) | Servidor y cliente de consola para depurar el protocolo |
| `tests/Monopoly.Tests` | xUnit | Pruebas unitarias y de integración (red) |

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

1. El organizador abre la aplicación, escribe su nombre y pulsa **Crear partida**: su equipo aloja al banco y la sala de espera muestra la IP y el puerto que debe compartir.
2. Los demás abren la aplicación, escriben su nombre, la IP y el puerto, y pulsan **Unirse a partida**.
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

## Probar una partida por consola

Compile una vez y abra una terminal para el servidor y una por jugador (desde la raíz del repositorio):

```bash
dotnet build Monopoly.sln
dotnet run --project src/Monopoly.ClienteConsola --no-build -- servidor 5000
dotnet run --project src/Monopoly.ClienteConsola --no-build -- cliente 127.0.0.1 5000 Ana
```

El servidor muestra las IPv4 de la computadora para que otros equipos de la red se conecten. En el cliente, `?` muestra los comandos.

## Estado

En desarrollo: estructuras de datos, modelo, lógica del juego, comunicación TCP e interfaz gráfica listas; falta el módulo de hardware (Arduino).
