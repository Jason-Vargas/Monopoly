<#
.SYNOPSIS
    Publica Monopoly.App como un único .exe autocontenido (no requiere instalar .NET en la otra computadora).

.DESCRIPTION
    Genera publicar\win-x64\Monopoly.App.exe con el runtime de .NET 8 incluido.
    Copie ese único archivo a cada computadora que vaya a jugar.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\publicar.ps1
#>
param(
    [string]$Configuracion = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$proyecto = Join-Path $raiz 'src\Monopoly.App\Monopoly.App.csproj'
$salida = Join-Path $raiz "publicar\$Runtime"

Write-Host "Publicando Monopoly.App ($Configuracion, $Runtime) en $salida ..." -ForegroundColor Cyan
if (Test-Path $salida) {
    Remove-Item $salida -Recurse -Force
}

dotnet publish $proyecto `
    -c $Configuracion `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o $salida

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falló con código $LASTEXITCODE."
}

$exe = Join-Path $salida 'Monopoly.App.exe'
$tamanoMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Listo: $exe ($tamanoMb MB)" -ForegroundColor Green
Write-Host "Copie ese archivo a la otra computadora y ejecútelo; no necesita instalar .NET."
