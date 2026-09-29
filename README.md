# Monopoly Distribuido con Estructuras Lineales

Proyecto 1 del curso **Algoritmos y Estructuras de Datos 1** — Instituto Tecnológico de Costa Rica, II Semestre 2026.

Versión simplificada de Monopoly Electrónico (tema clásico, edición Atlantic City en español) con arquitectura cliente-servidor sobre TCP, estructuras de datos lineales implementadas desde cero y un módulo electrónico (dado de 2 dígitos y lector RFID) basado en Arduino.

> Proyecto académico sin afiliación con Hasbro. No se usan el logotipo ni la mascota oficiales.

## Estructura

| Proyecto | Tipo | Contenido |
|---|---|---|
| `src/Monopoly.Core` | Biblioteca de clases | Estructuras de datos, modelo, lógica del juego, red y hardware |
| `src/Monopoly.App` | Windows Forms | Interfaz gráfica (servidor/organizador y clientes) |
| `tests/Monopoly.Tests` | xUnit | Pruebas unitarias |

El enunciado está en [`docs/enunciado.pdf`](docs/enunciado.pdf).

## Requisitos

- Windows 10/11
- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) o superior

## Compilar, probar y ejecutar

```bash
dotnet build Monopoly.sln
dotnet test Monopoly.sln
dotnet run --project src/Monopoly.App
```

## Estado

En desarrollo: por ahora solo existe la estructura inicial de la solución.
