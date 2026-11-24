<#
Casa Miga POS · Respaldar la caja para pasarla a otra computadora.

Saca el respaldo completo (.cmbackup: ventas, usuarios, recetas, cortes, catálogo, fotos y configuración del ticket) con la
herramienta oficial y lo copia al Escritorio y a la USB que esté conectada. Sólo lee la caja: no cambia ni borra nada.
Hazlo al cerrar el día, con el corte hecho y Casa Miga POS cerrado.

  powershell -NoProfile -ExecutionPolicy Bypass -File .\respaldar-caja.ps1 [-Datos <carpeta de datos de la caja>]
#>
param([string]$Datos)

$ErrorActionPreference = 'Stop'

function Detener([string]$texto) {
    Write-Host ''
    Write-Host $texto -ForegroundColor Red
    Read-Host 'Presiona Enter para cerrar' | Out-Null
    exit 1
}

function Herramienta {
    $opciones = @(
        (Join-Path $PSScriptRoot 'CasaMiga.Maintenance.exe'),
        (Join-Path (Split-Path $PSScriptRoot -Parent) 'CasaMiga.Maintenance.exe'),
        (Join-Path $env:ProgramFiles 'Casa Miga POS\tools\CasaMiga.Maintenance.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Casa Miga POS\tools\CasaMiga.Maintenance.exe')
    )
    return $opciones | Where-Object { Test-Path $_ } | Select-Object -First 1
}

try {
    if (Get-Process -Name 'CasaMiga.Presentation' -ErrorAction SilentlyContinue) {
        Detener 'Casa Miga POS está abierto. Haz el corte, cierra el programa y vuelve a correr esto.'
    }
    $exe = Herramienta
    if (-not $exe) { Detener 'No encontré la herramienta CasaMiga.Maintenance.exe junto a este script.' }

    if (-not $Datos) {
        # La caja de la tienda abre con su propia carpeta (CasaMiga-Ventas\caja); una caja instalada usa %LOCALAPPDATA%\CasaMigaPOS.
        $candidatas = @($env:CASAMIGA_DATA_DIR, (Join-Path $env:USERPROFILE 'CasaMiga-Ventas\caja'), (Join-Path $env:LOCALAPPDATA 'CasaMigaPOS'))
        $Datos = $candidatas | Where-Object { $_ -and (Test-Path (Join-Path $_ 'casamiga.db')) -and (Test-Path (Join-Path $_ 'terminal.json')) } | Select-Object -First 1
    }
    if (-not $Datos -or -not (Test-Path (Join-Path $Datos 'casamiga.db'))) { Detener 'No encontré la carpeta de datos de la caja. Pásala con -Datos <carpeta>.' }

    $base = Get-Item (Join-Path $Datos 'casamiga.db')
    Write-Host "Caja: $Datos"
    Write-Host ("Base modificada por última vez: {0:dd/MM/yyyy HH:mm}" -f $base.LastWriteTime)
    $respuesta = Read-Host '¿Sacar el respaldo de esta caja? (S/N)'
    if ($respuesta -notmatch '^[sS]') { Detener 'No se hizo nada.' }

    Write-Host 'Respaldando (puede tardar un minuto)...'
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $salida = @(& $exe backup $Datos 2>&1 | ForEach-Object { "$_" })
    $codigo = $LASTEXITCODE
    $ErrorActionPreference = $anterior
    $archivo = ($salida | Where-Object { $_ -like '*.cmbackup' } | Select-Object -Last 1)
    if ($codigo -ne 0 -or -not $archivo -or -not (Test-Path $archivo.Trim())) {
        Write-Host ($salida -join "`n")
        Detener 'El respaldo no se pudo hacer. No se cambió nada.'
    }
    $archivo = $archivo.Trim()

    $destinos = @([Environment]::GetFolderPath('Desktop'))
    $destinos += @(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=2' | ForEach-Object { $_.DeviceID + '\' })
    foreach ($destino in $destinos) {
        try {
            Copy-Item $archivo $destino -Force
            $copia = Join-Path $destino (Split-Path $archivo -Leaf)
            if ((Get-FileHash $copia).Hash -ne (Get-FileHash $archivo).Hash) { throw 'la copia no es igual al original' }
            Write-Host "Copiado a $copia" -ForegroundColor Green
        }
        catch { Write-Warning "No se pudo copiar a ${destino}: $($_.Exception.Message)" }
    }
    Write-Host ''
    Write-Host "Respaldo listo: $(Split-Path $archivo -Leaf)" -ForegroundColor Green
    Write-Host 'Llévalo a la computadora nueva y abre ahí «Traer la caja de otra computadora» en el menú Inicio.'
    Write-Host 'A partir de ahora no vendas en esta computadora, para que las dos cajas no se separen.'
    Read-Host 'Presiona Enter para cerrar' | Out-Null
}
catch { Detener ('Algo falló: ' + $_.Exception.Message) }
