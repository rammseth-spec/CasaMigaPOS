# Prueba local completa en una sola computadora: nube local (con la web) + caja de escritorio, con datos aparte de cualquier
# instalación real. Los datos se guardan en una carpeta y sobreviven a reinicios. Uso: ./scripts/run-local.ps1 [-Datos <carpeta>] [-Puerto 4173] [-SoloNube]
# Requisitos: Windows, .NET SDK 10 y Node.js 24. Sólo para probar; nunca con datos de la sucursal.
param(
    [string]$Datos = (Join-Path $env:USERPROFILE 'CasaMiga-Prueba'),
    [int]$Puerto = 4173,
    [switch]$SoloNube
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
foreach ($tool in 'dotnet', 'node', 'npm') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Falta $tool. Instala .NET SDK 10 (https://dotnet.microsoft.com/download) y Node.js 24 (https://nodejs.org)." }
}
if ($Datos -match 'OneDrive|Dropbox|Google Drive|iCloud') { throw "La carpeta de datos no puede estar en una carpeta sincronizada ($Datos). Usa otra, por ejemplo: -Datos C:\CasaMiga-Prueba" }
$nube = Join-Path $Datos 'nube'; $caja = Join-Path $Datos 'caja'; $programa = Join-Path $Datos 'programa'
New-Item -ItemType Directory -Force $nube, $caja | Out-Null

# Secretos de la nube local: se generan una vez, se guardan junto a los datos de prueba y nunca van al repositorio.
$secretos = Join-Path $nube 'secretos.json'
if (-not (Test-Path $secretos)) {
    $nuevo = { $b = New-Object byte[] 30; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b).Replace('+', '-').Replace('/', '_') }
    @{ enrollment = (& $nuevo); session = (& $nuevo); backup = (& $nuevo); cron = (& $nuevo) } | ConvertTo-Json | Set-Content -Encoding utf8 $secretos
}
$s = Get-Content $secretos -Raw | ConvertFrom-Json

# Dependencias de la web (sólo la primera vez).
Push-Location (Join-Path $repo 'cloud')
try { if (-not (Test-Path node_modules)) { npm ci; if ($LASTEXITCODE -ne 0) { throw 'Falló npm ci.' } } } finally { Pop-Location }

# Nube local en su propia ventana (ciérrala para apagarla).
$env:ENROLLMENT_SECRET = $s.enrollment; $env:SESSION_SECRET = $s.session; $env:BACKUP_SECRET = $s.backup; $env:CRON_SECRET = $s.cron
try {
    Start-Process node -ArgumentList '--import', 'tsx', 'scripts/local-cloud.ts', "`"$nube`"", $Puerto -WorkingDirectory (Join-Path $repo 'cloud')
} finally { Remove-Item Env:ENROLLMENT_SECRET, Env:SESSION_SECRET, Env:BACKUP_SECRET, Env:CRON_SECRET -ErrorAction SilentlyContinue }
$url = "http://localhost:$Puerto"
$lista = $false
for ($i = 0; $i -lt 60 -and -not $lista; $i++) {
    Start-Sleep -Seconds 1
    try { $lista = (Invoke-WebRequest "$url/" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { }
}
if (-not $lista) { throw "La nube local no respondió en $url. Revisa su ventana." }

if (-not $SoloNube) {
    # La caja se compila fuera del repositorio y usa su propia carpeta de datos (CASAMIGA_DATA_DIR), no la de una instalación real.
    dotnet build (Join-Path $repo 'src/CasaMiga.Presentation') -c Release -o $programa --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de la caja.' }
    $env:CASAMIGA_DATA_DIR = $caja
    try { Start-Process (Join-Path $programa 'CasaMiga.Presentation.exe') } finally { Remove-Item Env:CASAMIGA_DATA_DIR -ErrorAction SilentlyContinue }
}

Write-Host ''
Write-Host 'Casa Miga en prueba local' -ForegroundColor Green
Write-Host "  Web:               $url"
Write-Host "  Datos de prueba:   $Datos"
Write-Host "  Secreto de alta:   $($s.enrollment)"
Write-Host ''
Write-Host 'Primera vez:'
Write-Host '  1. En la caja, crea el administrador y entra.'
Write-Host "  2. En la caja, Nube: dirección $url, nombre de la sucursal y el secreto de alta → Registrar terminal."
Write-Host "  3. Abre $url y entra con el mismo usuario y contraseña de la caja."
Write-Host '  4. Para vender en la web: Venta → + Nueva caja web.'
Write-Host 'Para empezar de cero, cierra la caja y la ventana de la nube y borra la carpeta de datos de prueba.'
