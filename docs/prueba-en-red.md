# Prueba con dos computadoras en la misma red

## 1. Generar el ejecutable (una sola vez, en la computadora de desarrollo)

```powershell
powershell -ExecutionPolicy Bypass -File .\publicar.ps1
```

Resultado: **`publicar\win-x64\Monopoly.App.exe`** (≈ 63 MB). Es un único archivo autocontenido: incluye .NET 8, así que la otra computadora **no necesita instalar nada**. Cópielo con una memoria USB, OneDrive, etc.

> La primera vez que se abre un .exe descargado, Windows SmartScreen puede advertir "Windows protegió su PC": pulse **Más información → Ejecutar de todas formas**.

## 2. Computadora del organizador (aloja al banco)

### Ver su IP

La sala de espera de la aplicación ya muestra la IP y el puerto para compartir. Para verla desde PowerShell:

```powershell
Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object InterfaceAlias, IPAddress
```

(o simplemente `ipconfig` y buscar "Dirección IPv4" del adaptador Wi-Fi o Ethernet). Normalmente es algo como `192.168.1.20`.

### Abrir el puerto 5000 en el Firewall (PowerShell **como administrador**)

```powershell
New-NetFirewallRule -DisplayName "Monopoly TCP 5000" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow -Profile Private,Domain
```

La regla solo aplica en redes **privadas** (y de dominio). Si la red está marcada como pública, cámbiela a privada:

```powershell
Get-NetConnectionProfile                                   # ver el nombre de la red y su categoría
Set-NetConnectionProfile -InterfaceAlias "Wi-Fi" -NetworkCategory Private
```

Para **quitar la regla** después de la prueba:

```powershell
Remove-NetFirewallRule -DisplayName "Monopoly TCP 5000"
```

> Si usa otro puerto, cambie `5000` en los comandos y en la aplicación.
> Además de la regla, la primera vez que se crea la partida Windows puede preguntar si permite que `Monopoly.App` se comunique: marque **Redes privadas** y acepte.

## 3. Computadora del otro jugador

1. Verifique que llega al organizador (PowerShell normal):

   ```powershell
   Test-NetConnection -ComputerName 192.168.1.20 -Port 5000
   ```

   Debe decir `TcpTestSucceeded : True` (con la partida ya creada en el organizador).
2. Abra `Monopoly.App.exe`, escriba su nombre, la IP del organizador y el puerto, y pulse **Unirse a partida**.

## 4. Orden de la prueba

1. Organizador: abre la app → nombre → **Crear partida** (puerto 5000).
2. Los demás (en esta u otras computadoras): **Unirse a partida** con la IP del organizador. Para varias ventanas en la misma computadora use `127.0.0.1`.
3. Con 2 a 4 jugadores, el organizador pulsa **Iniciar partida**.

## 5. Mensajes de error y qué hacer

| Mensaje | Causa probable | Solución |
|---|---|---|
| "No hay ninguna partida abierta en IP:puerto (conexión rechazada)" | El organizador aún no creó la partida, o el puerto no coincide | Crear la partida primero; revisar el puerto |
| "No hubo respuesta de IP:puerto..." | IP equivocada, computadoras en redes distintas (o red de invitados con aislamiento), o firewall bloqueando | Revisar la IP con `ipconfig`, conectar ambas a la misma red, abrir el puerto, probar con `Test-NetConnection` |
| "'x' no es una IP válida" | IP mal escrita | Escribirla como `192.168.1.20` |
| "La partida está llena" | Ya hay 4 jugadores | — |
| "La partida ya comenzó..." | Se intenta entrar con un nombre nuevo a una partida iniciada | Si ya estaba en la partida, usar exactamente el mismo nombre para reconectarse |
| "Ya existe un jugador llamado X" / "X ya está conectado" | Nombre repetido | Elegir otro nombre |
| "El puerto 5000 ya está en uso en esta computadora" | Ya hay otra partida abierta en el organizador | Cerrarla o usar otro puerto |

## 6. Robustez (qué pasa si...)

- **Un jugador se desconecta** (cierra la app, se cae la Wi-Fi, se apaga la computadora): el servidor lo detecta (al instante si se cierra la app; en unos 20 s por keepalive TCP si se corta la red), todos ven "DESCONECTADO" en su tarjeta y un aviso en el registro. **Sigue en la partida**: puede volver con **el mismo nombre** (la app ofrece volver a la pantalla de inicio ya rellena). Si era su turno y no vuelve, el **organizador** puede pulsar **Retirar jugadores desconectados**: queda eliminado, sin pagar, y sus propiedades vuelven a estar libres.
- **Dos jugadores actúan a la vez**: el servidor procesa las solicitudes de a una; solo se acepta la del jugador en turno y las demás reciben un error. Todos reciben los mensajes en el mismo orden.
- **El organizador cierra la aplicación**: se le pide confirmación; al salir, todos reciben "El organizador cerró la partida." y la partida termina.
- **Un cliente congelado o que envía basura**: el servidor corta esa conexión (envíos con tiempo máximo de 5 s y líneas de hasta 8192 caracteres) sin afectar a los demás.
