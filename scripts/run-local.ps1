# Prueba local completa en una sola computadora: nube local (con la web) + caja de escritorio, con datos aparte de cualquier
# instalación real. Los datos se guardan en una carpeta y sobreviven a reinicios.
# Uso: ./scripts/run-local.ps1 [-Datos <carpeta>] [-Puerto 4173] [-SoloNube] [-ConContrasena] [-SinCarga]
# Por ahora (pedido del usuario, temporal) entra solo: la caja crea su administrador y un usuario fijo por rol, inicia sesión y se
# registra en la nube local, y la web entra sola como administrador (sólo desde esta computadora). Con -ConContrasena todo pide
# usuario y contraseña como en la sucursal.
# Al final, por la web, la caja recibe lo que le falte del catálogo autorizado con sus fotos provisionales y del recetario en
# borrador (cloud/scripts/cargar-caja.ts); sólo manda lo que falta, así que no duplica nada. -SinCarga lo omite.
# Requisitos: Windows, .NET SDK 10 y Node.js 24. Sólo para probar; nunca con datos de la sucursal.
param(
    [string]$Datos = (Join-Path $env:USERPROFILE 'CasaMiga-Prueba'),
    [int]$Puerto = 4173,
    [switch]$SoloNube,
    [switch]$ConContrasena,
    [switch]$SinCarga
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
foreach ($tool in 'dotnet', 'node', 'npm') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Falta $tool. Instala .NET SDK 10 (https://dotnet.microsoft.com/download) y Node.js 24 (https://nodejs.org)." }
}
if ($Datos -match 'OneDrive|Dropbox|Google Drive|iCloud') { throw "La carpeta de datos no puede estar en una carpeta sincronizada ($Datos). Usa otra, por ejemplo: -Datos C:\CasaMiga-Prueba" }
$nube = Join-Path $Datos 'nube'; $caja = Join-Path $Datos 'caja'; $programa = Join-Path $Datos 'programa'
New-Item -ItemType Directory -Force $nube, $caja | Out-Null

# Usuarios de prueba fijos, uno por rol, para entrar sin problema y ver cada vista. Sólo para esta prueba local; nunca en la sucursal.
$usuarios = @(
    [ordered]@{ Username = 'admin'; Password = 'Admin#2026'; Rol = 'Administrador' },
    [ordered]@{ Username = 'encargado'; Password = 'Encargado#2026'; Role = 'Manager'; DisplayName = 'Encargado de prueba'; Rol = 'Encargado' },
    [ordered]@{ Username = 'cajero'; Password = 'Cajero#2026'; Role = 'Cashier'; DisplayName = 'Cajero de prueba'; Rol = 'Cajero' }
)

# Secretos de la nube local: se generan una vez, se guardan junto a los datos de prueba y nunca van al repositorio.
$aleatorio = { $b = New-Object byte[] 30; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b).Replace('+', '-').Replace('/', '_') }
$secretos = Join-Path $nube 'secretos.json'
if (-not (Test-Path $secretos)) {
    @{ enrollment = (& $aleatorio); session = (& $aleatorio); backup = (& $aleatorio); cron = (& $aleatorio) } | ConvertTo-Json | Set-Content -Encoding utf8 $secretos
}
$s = Get-Content $secretos -Raw | ConvertFrom-Json

# Dependencias de la web (sólo la primera vez).
Push-Location (Join-Path $repo 'cloud')
try { if (-not (Test-Path node_modules)) { npm ci; if ($LASTEXITCODE -ne 0) { throw 'Falló npm ci.' } } } finally { Pop-Location }

# Nube local en su propia ventana (ciérrala para apagarla). Sin contraseña sólo escucha en esta computadora.
$url = if ($ConContrasena) { "http://localhost:$Puerto" } else { "http://127.0.0.1:$Puerto" }
$argumentos = @('--import', 'tsx', 'scripts/local-cloud.ts', "`"$nube`"", $Puerto)
if (-not $ConContrasena) { $argumentos += '--sin-contrasena' }
$env:ENROLLMENT_SECRET = $s.enrollment; $env:SESSION_SECRET = $s.session; $env:BACKUP_SECRET = $s.backup; $env:CRON_SECRET = $s.cron
try { Start-Process node -ArgumentList $argumentos -WorkingDirectory (Join-Path $repo 'cloud') }
finally { Remove-Item Env:ENROLLMENT_SECRET, Env:SESSION_SECRET, Env:BACKUP_SECRET, Env:CRON_SECRET -ErrorAction SilentlyContinue }
$lista = $false
for ($i = 0; $i -lt 60 -and -not $lista; $i++) {
    Start-Sleep -Seconds 1
    try { $lista = (Invoke-WebRequest "$url/" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { }
}
if (-not $lista) { throw "La nube local no respondió en $url. Revisa su ventana." }

if (-not $SoloNube) {
    # La caja se compila fuera del repositorio y usa su propia carpeta de datos, no la de una instalación real.
    dotnet build (Join-Path $repo 'src/CasaMiga.Presentation') -c Release -o $programa --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de la caja.' }
    $autologin = Join-Path $caja 'dev-autologin.json'
    $desarrollo = Join-Path $programa 'development.settings.json'
    if ($ConContrasena) {
        Remove-Item $autologin, $desarrollo -ErrorAction SilentlyContinue
        $env:CASAMIGA_DATA_DIR = $caja
        try { Start-Process (Join-Path $programa 'CasaMiga.Presentation.exe') } finally { Remove-Item Env:CASAMIGA_DATA_DIR -ErrorAction SilentlyContinue }
    } else {
        # Modo desarrollo de esta copia (el instalador nunca lo incluye): entra con admin y se registra en la nube local.
        @{ IsDevelopment = $true; DataDirectory = $caja } | ConvertTo-Json | Set-Content -Encoding utf8 $desarrollo
        $extra = @($usuarios | Select-Object -Skip 1 | ForEach-Object { @{ Username = $_.Username; Password = $_.Password; Role = $_.Role; DisplayName = $_.DisplayName } })
        @{ Username = 'admin'; Password = $usuarios[0].Password; Users = $extra; Cloud = @{ Url = $url; Secret = $s.enrollment; Branch = 'Sucursal de prueba' } } |
            ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 $autologin
        Remove-Item Env:CASAMIGA_DATA_DIR -ErrorAction SilentlyContinue
        Start-Process (Join-Path $programa 'CasaMiga.Presentation.exe')
    }
}

if (-not $ConContrasena -and -not $SoloNube) {
    # Cuando la caja ya subió sus usuarios, se abre la web en el navegador y entra sola.
    for ($i = 0; $i -lt 90; $i++) {
        Start-Sleep -Seconds 1
        try { if (@((Invoke-WebRequest "$url/api/web/dev-users" -UseBasicParsing -TimeoutSec 2).Content | ConvertFrom-Json).Count -gt 0) { Start-Process "$url/"; break } } catch { }
    }
}

Write-Host ''
Write-Host 'Casa Miga en prueba local' -ForegroundColor Green
Write-Host "  Web:               $url"
Write-Host "  Datos de prueba:   $Datos"
if ($ConContrasena) {
    Write-Host "  Secreto de alta:   $($s.enrollment)"
    Write-Host ''
    Write-Host 'Primera vez:'
    Write-Host '  1. En la caja, crea el administrador y entra.'
    Write-Host "  2. En la caja, Nube: dirección $url, nombre de la sucursal y el secreto de alta → Registrar terminal."
    Write-Host "  3. Abre $url y entra con el mismo usuario y contraseña de la caja."
} else {
    Write-Host '  Entrada automática: la caja y la web entran solas como admin. Al tocar Salir aparece un botón por usuario.'
    Write-Host '  Para pedir usuario y contraseña como en la sucursal: -ConContrasena'
}
Write-Host ''
Write-Host 'Usuarios de prueba (uno por rol):'
foreach ($u in $usuarios) { Write-Host ('  {0,-14} {1,-12} {2}' -f $u.Rol, $u.Username, $u.Password) }
Write-Host ''
Write-Host '  Para vender en la web: Venta → + Nueva caja web.'
Write-Host 'Para empezar de cero, cierra la caja y la ventana de la nube y borra la carpeta de datos de prueba.'

if (-not $ConContrasena -and -not $SoloNube -and -not $SinCarga) {
    # Catálogo con fotos y recetario en borrador por la web; la caja los aplica al sincronizar (cada 5 s). Espera hasta que la caja
    # tenga todo lo que se puede mandar; las recetas de cada pan llegan cuando sus preparaciones estén publicadas en Recetas.
    Write-Host ''
    Write-Host 'Cargando en la caja el catálogo con fotos y el recetario en borrador…' -ForegroundColor Green
    Push-Location (Join-Path $repo 'cloud')
    try {
        $env:CASAMIGA_USUARIO = $usuarios[0].Username; $env:CASAMIGA_CONTRASENA = $usuarios[0].Password
        npm run --silent cargar -- $url --enviar --esperar
        if ($LASTEXITCODE -ne 0) { Write-Warning 'No se completó la carga; vuelve a correr run-local.ps1 o: npm run cargar -- <dirección> --enviar --esperar (desde cloud).' }
    } finally { Remove-Item Env:CASAMIGA_USUARIO, Env:CASAMIGA_CONTRASENA -ErrorAction SilentlyContinue; Pop-Location }
}
