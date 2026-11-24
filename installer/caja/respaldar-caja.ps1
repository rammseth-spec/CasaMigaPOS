<#
Casa Miga POS · Respaldar la caja para pasarla a otra computadora.

Saca el respaldo completo (.cmbackup: ventas, usuarios, recetas, cortes, catálogo, fotos y configuración del ticket) con la
herramienta oficial y lo copia al Escritorio y a la USB que esté conectada. Sólo lee la caja: no cambia ni borra nada.
Hazlo al cerrar el día, con el corte hecho y Casa Miga POS cerrado.

Con -SubirAGitHub (y GitHub CLI con sesión) además lo sube como BORRADOR de release «Traspaso de caja» al repositorio privado,
para bajarlo en la computadora nueva sin USB. Un borrador sólo lo ven quienes tienen permiso de escritura en el repositorio; cada
respaldo nuevo reemplaza al borrador anterior y conviene borrarlo en cuanto la caja nueva quede funcionando (trae ventas y usuarios).

  powershell -NoProfile -ExecutionPolicy Bypass -File .\respaldar-caja.ps1 [-Datos <carpeta de datos de la caja>] [-SubirAGitHub]
#>
param(
    [string]$Datos,
    [switch]$SubirAGitHub,
    [string]$Repositorio = 'rammseth-spec/CasaMigaPOS'
)

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
    $enGitHub = $false
    if ($SubirAGitHub) {
        Write-Host ''
        Write-Host 'Subiendo el respaldo a GitHub como borrador privado...'
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if (-not $gh) { Write-Warning 'No está GitHub CLI (gh) en esta computadora: llévalo en USB.' }
        else {
            $ErrorActionPreference = 'Continue'
            & gh auth status 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Warning 'GitHub CLI no tiene sesión (gh auth login): llévalo en USB.' }
            else {
                # Sólo un traspaso a la vez: los borradores anteriores de este script se quitan antes de subir el nuevo.
                # (Sin --jq: Windows PowerShell 5.1 rompe las comillas dentro de los argumentos de programas externos.)
                $lista = (& gh release list --repo $Repositorio --limit 100 --json tagName,isDraft 2>$null) -join "`n"
                $viejos = @(if ($lista) { $lista | ConvertFrom-Json | Where-Object { $_.isDraft -and $_.tagName -like 'traspaso-caja-*' } | ForEach-Object { $_.tagName } })
                foreach ($viejo in $viejos) { & gh release delete $viejo --repo $Repositorio --yes 2>&1 | Out-Null }
                $tag = 'traspaso-caja-' + (Get-Date -Format 'yyyyMMdd-HHmm')
                $titulo = 'Traspaso de caja ' + (Get-Date -Format 'dd/MM/yyyy HH:mm')
                $notas = "Respaldo completo de la caja ($env:COMPUTERNAME) para pasarla a otra computadora con «Traer la caja de otra computadora». Contiene ventas y usuarios: borrar este borrador cuando la caja nueva quede funcionando."
                & gh release create $tag $archivo --repo $Repositorio --draft --title $titulo --notes $notas 2>&1 | Out-Host
                $vista = (& gh release view $tag --repo $Repositorio --json assets 2>$null) -join "`n"
                $subido = if ($LASTEXITCODE -eq 0 -and $vista) { @(($vista | ConvertFrom-Json).assets)[0].size } else { $null }
                if ($subido -and [long]$subido -eq (Get-Item $archivo).Length) {
                    $enGitHub = $true
                    Write-Host "En GitHub: $Repositorio → Releases → borrador «$titulo»" -ForegroundColor Green
                }
                else { Write-Warning 'No se pudo comprobar la subida a GitHub: llévalo en USB.' }
            }
            $ErrorActionPreference = 'Stop'
        }
    }

    Write-Host ''
    Write-Host "Respaldo listo: $(Split-Path $archivo -Leaf)" -ForegroundColor Green
    if ($enGitHub) { Write-Host "En la computadora nueva: entra a https://github.com/$Repositorio/releases, descarga el .cmbackup del borrador «Traspaso de caja» y abre «Traer la caja de otra computadora» en el menú Inicio." }
    else { Write-Host 'Llévalo a la computadora nueva y abre ahí «Traer la caja de otra computadora» en el menú Inicio.' }
    Write-Host 'A partir de ahora no vendas en esta computadora, para que las dos cajas no se separen.'
    Read-Host 'Presiona Enter para cerrar' | Out-Null
}
catch { Detener ('Algo falló: ' + $_.Exception.Message) }
