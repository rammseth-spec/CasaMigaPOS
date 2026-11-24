<#
Casa Miga POS · Traer la caja de otra computadora (menú Inicio → «Traer la caja de otra computadora»).

Busca el respaldo más reciente (.cmbackup) en la USB, el Escritorio o Descargas, lo restaura con la herramienta oficial en la
carpeta de datos de este usuario y después configura la impresora de tickets. Si esta computadora ya tenía datos de caja, no los
borra: los aparta en una carpeta «CasaMigaPOS-antes-<fecha>». Correr con Casa Miga POS cerrado y como el usuario de la caja.

  powershell -NoProfile -ExecutionPolicy Bypass -File .\traer-caja.ps1 [-Respaldo <archivo .cmbackup>]
#>
param([string]$Respaldo)

$ErrorActionPreference = 'Stop'

function Detener([string]$texto) {
    Write-Host ''
    Write-Host $texto -ForegroundColor Red
    Read-Host 'Presiona Enter para cerrar' | Out-Null
    exit 1
}

function Herramienta {
    $opciones = @(
        (Join-Path (Split-Path $PSScriptRoot -Parent) 'CasaMiga.Maintenance.exe'),
        (Join-Path $env:ProgramFiles 'Casa Miga POS\tools\CasaMiga.Maintenance.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Casa Miga POS\tools\CasaMiga.Maintenance.exe')
    )
    return $opciones | Where-Object { Test-Path $_ } | Select-Object -First 1
}

try {
    if (Get-Process -Name 'CasaMiga.Presentation' -ErrorAction SilentlyContinue) { Detener 'Cierra Casa Miga POS y vuelve a correr esto.' }
    $exe = Herramienta
    if (-not $exe) { Detener 'No encontré Casa Miga POS instalado (falta tools\CasaMiga.Maintenance.exe).' }

    if (-not $Respaldo) {
        $lugares = @(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=2' | ForEach-Object { $_.DeviceID + '\' })
        $lugares += [Environment]::GetFolderPath('Desktop')
        $lugares += (Join-Path $env:USERPROFILE 'Downloads')
        $encontrado = $lugares | Where-Object { Test-Path $_ } |
            ForEach-Object { Get-ChildItem -Path $_ -Filter '*.cmbackup' -File -ErrorAction SilentlyContinue } |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $encontrado) { Detener 'No encontré ningún respaldo (.cmbackup). Ponlo en la USB, en el Escritorio o en Descargas.' }
        $Respaldo = $encontrado.FullName
    }
    if (-not (Test-Path $Respaldo)) { Detener "No existe $Respaldo." }
    $info = Get-Item $Respaldo
    Write-Host "Respaldo: $($info.FullName)"
    Write-Host ("Hecho el {0:dd/MM/yyyy HH:mm} · {1:N1} MB" -f $info.LastWriteTime, ($info.Length / 1MB))
    if ((Read-Host '¿Traer esta caja a esta computadora? (S/N)') -notmatch '^[sS]') { Detener 'No se hizo nada.' }

    $datos = if ($env:CASAMIGA_DATA_DIR) { $env:CASAMIGA_DATA_DIR } else { Join-Path $env:LOCALAPPDATA 'CasaMigaPOS' }
    $apartada = $null
    if (Test-Path $datos) {
        if (Test-Path (Join-Path $datos 'casamiga.db')) {
            Write-Host "Esta computadora ya tiene una caja con datos en $datos." -ForegroundColor Yellow
            if ((Read-Host 'Escribe APARTAR para guardarla a un lado (no se borra) y traer la otra') -ne 'APARTAR') { Detener 'No se hizo nada.' }
        }
        # La restauración exige una carpeta nueva: lo que hubiera (por ejemplo, sólo printing.json del instalador) se aparta, no se borra.
        $apartada = "$datos-antes-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Rename-Item -Path $datos -NewName (Split-Path $apartada -Leaf)
        Write-Host "Lo que había quedó en $apartada"
    }

    Write-Host 'Restaurando y validando (puede tardar un minuto)...'
    $anterior = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $salida = & $exe restore-installation $info.FullName $datos 2>&1 | Out-String
    $codigo = $LASTEXITCODE
    $ErrorActionPreference = $anterior
    if ($codigo -ne 0 -or -not (Test-Path (Join-Path $datos 'casamiga.db'))) {
        Write-Host $salida
        if ($apartada -and -not (Test-Path $datos)) { Rename-Item -Path $apartada -NewName (Split-Path $datos -Leaf) }
        Detener 'No se pudo traer la caja. Esta computadora quedó como estaba.'
    }
    Write-Host 'Caja restaurada y validada.' -ForegroundColor Green

    # El respaldo ya trae printing.json; falta la impresora POS-80 de Windows en esta computadora.
    $impresora = Join-Path (Split-Path $PSScriptRoot -Parent) 'impresora\configurar-impresora.ps1'
    if (Test-Path $impresora) {
        Write-Host ''
        Write-Host 'Ahora la impresora de tickets (debe estar conectada por USB y encendida).'
        & $impresora
    }
    Write-Host ''
    Write-Host 'Listo. Abre Casa Miga POS y entra con tu usuario y contraseña de siempre.' -ForegroundColor Green
    Write-Host 'Marca «Recuérdame» si quieres que en esta computadora entre sola.'
    Read-Host 'Presiona Enter para cerrar' | Out-Null
}
catch { Detener ('Algo falló: ' + $_.Exception.Message) }
