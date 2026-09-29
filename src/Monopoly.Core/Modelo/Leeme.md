# Monopoly.Core.Modelo
Entidades del juego: Jugador, Casilla y derivadas, CartaEvento y mazos, Transaccion e historial, Dado, Tablero.

Jerarquía de casillas (polimorfismo mediante `AlCaer`, `Categoria` y `CalcularAlquiler`):

```
Casilla (abstracta)
├── Propiedad (calles)
│   ├── Ferrocarril
│   └── CompaniaServicio
├── CasillaEvento (abstracta)
│   ├── CasillaCasualidad
│   └── CasillaArcaComunal
└── CasillaEspecial (abstracta)
    ├── Salida
    ├── Impuesto
    ├── CarcelSoloVisita
    ├── ParadaLibre
    └── VayaALaCarcel
```
