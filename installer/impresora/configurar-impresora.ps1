<#
Casa Miga POS · Configurar la impresora de tickets (térmica de 80 mm por USB).

La caja manda cada ticket en crudo (ESC/POS, tipo RAW), así que basta el driver «Generic / Text Only» que ya trae Windows:
no se descarga ni se instala nada de fuera. El script nunca cambia una impresora POS-80 que ya exista ni un printing.json que ya esté.

  -Parte cola     Crea la impresora POS-80 en Windows y, si la acaba de crear, imprime un ticket de prueba. Necesita administrador.
  -Parte ticket   Deja printing.json (los datos del ticket de Casa Miga) en la carpeta de datos del usuario si todavía no tiene uno.
  (sin -Parte)    Las dos cosas y un ticket de prueba. Es lo que hace «Configurar impresora de tickets» en el menú Inicio.

El instalador corre «cola» como administrador y «ticket» como el usuario que instaló. También sirve suelto (desde una USB,
junto a printing.casamiga.json) en una caja que ya tenga Casa Miga POS instalado:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\configurar-impresora.ps1
#>
param(
    [ValidateSet('todo', 'cola', 'ticket')][string]$Parte = 'todo',
    [switch]$SinPrueba
)

$ErrorActionPreference = 'Stop'
$Cola = 'POS-80'
$DriverWindows = 'Generic / Text Only'
$Plantilla = Join-Path $PSScriptRoot 'printing.casamiga.json'

function Avisar([string]$texto) { Write-Host $texto -ForegroundColor Green }

function Detener([string]$texto) {
    Write-Host ''
    Write-Host $texto -ForegroundColor Red
    Read-Host 'Presiona Enter para cerrar' | Out-Null
    exit 1
}

function EsAdministrador {
    $yo = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    return $yo.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Herramienta {
    $opciones = @(
        (Join-Path (Split-Path $PSScriptRoot -Parent) 'CasaMiga.Maintenance.exe'),
        (Join-Path $env:ProgramFiles 'Casa Miga POS\tools\CasaMiga.Maintenance.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Casa Miga POS\tools\CasaMiga.Maintenance.exe')
    )
    return $opciones | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function ElegirPuertoUsb {
    $puertos = @(Get-PrinterPort | Where-Object { $_.Name -like 'USB*' } | Sort-Object Name | ForEach-Object { $_.Name })
    if ($puertos.Count -eq 0) { return $null }
    if ($puertos.Count -eq 1) { return $puertos[0] }
    # Windows conserva el puerto de cada impresora USB que se haya conectado alguna vez: si sólo uno está libre, es el de la térmica.
    $ocupados = @(Get-Printer | ForEach-Object { $_.PortName })
    $libres = @($puertos | Where-Object { $ocupados -notcontains $_ })
    if ($libres.Count -eq 1) { return $libres[0] }
    Write-Host 'Hay varios puertos USB de impresora:'
    for ($i = 0; $i -lt $puertos.Count; $i++) {
        $quien = @(Get-Printer | Where-Object { $_.PortName -eq $puertos[$i] } | ForEach-Object { $_.Name }) -join ', '
        if (-not $quien) { $quien = 'sin impresora' }
        Write-Host ("  {0}) {1}  ({2})" -f ($i + 1), $puertos[$i], $quien)
    }
    $respuesta = Read-Host 'Escribe el número del puerto de la impresora de tickets'
    $numero = 0
    if ([int]::TryParse($respuesta, [ref]$numero) -and $numero -ge 1 -and $numero -le $puertos.Count) { return $puertos[$numero - 1] }
    return $null
}

# Devuelve $true sólo si la impresora se creó en esta corrida.
function CrearCola {
    $existente = Get-Printer -Name $Cola -ErrorAction SilentlyContinue
    if ($existente) {
        Avisar "La impresora $Cola ya existe en Windows (driver: $($existente.DriverName), puerto: $($existente.PortName)). No se cambia."
        return $false
    }
    if (-not (EsAdministrador)) { Detener 'Para crear la impresora hace falta permiso de administrador.' }
    $puerto = ElegirPuertoUsb
    if (-not $puerto) {
        Detener ("No encontré la impresora de tickets en ningún puerto USB.`n" +
            'Conéctala por USB, enciéndela, espera unos segundos y abre «Configurar impresora de tickets» en el menú Inicio.')
    }
    if (-not (Get-PrinterDriver -Name $DriverWindows -ErrorAction SilentlyContinue)) { Add-PrinterDriver -Name $DriverWindows | Out-Null }
    Add-Printer -Name $Cola -DriverName $DriverWindows -PortName $puerto | Out-Null
    Avisar "Impresora $Cola creada en el puerto $puerto con el driver «$DriverWindows» de Windows."
    return $true
}

function DejarConfiguracion {
    if (-not (Test-Path $Plantilla)) { Detener "Falta $Plantilla junto al script." }
    $datos = if ($env:CASAMIGA_DATA_DIR) { $env:CASAMIGA_DATA_DIR } else { Join-Path $env:LOCALAPPDATA 'CasaMigaPOS' }
    $destino = Join-Path $datos 'printing.json'
    if (Test-Path $destino) {
        Avisar "Esta caja ya tiene su configuración de ticket ($destino). No se cambia."
        return
    }
    New-Item -ItemType Directory -Force -Path $datos | Out-Null
    Copy-Item $Plantilla $destino
    Avisar "Datos del ticket de Casa Miga guardados en $destino."
}

# La prueba de la herramienta exige el perfil «PRUEBA CASA MIGA» y una carpeta nueva en cada intento: nunca toca la base de la caja.
function ImprimirPrueba {
    $exe = Herramienta
    if (-not $exe) { Write-Warning 'No encontré Casa Miga POS instalado: no se imprimió el ticket de prueba.'; return }
    $carpeta = Join-Path $env:TEMP ('casamiga-prueba-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $carpeta | Out-Null
    try {
        $texto = [IO.File]::ReadAllText($Plantilla)
        $texto = [regex]::Replace($texto, '"displayName"\s*:\s*"[^"]*"', '"displayName": "PRUEBA CASA MIGA"')
        [IO.File]::WriteAllText((Join-Path $carpeta 'printing.json'), $texto)
        Write-Host 'Mandando un ticket de prueba a la impresora...'
        $anterior = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $salida = & $exe print-test $carpeta 2>&1 | Out-String
        $codigo = $LASTEXITCODE
        $ErrorActionPreference = $anterior
        if ($codigo -eq 0) { Avisar 'Ticket de prueba enviado: debe salir uno que dice «PRUEBA CASA MIGA».' }
        else {
            Write-Host $salida
            Write-Warning 'La impresora no aceptó el ticket de prueba. Revisa que esté encendida, con papel y conectada por USB.'
        }
    }
    finally { Remove-Item $carpeta -Recurse -Force -ErrorAction SilentlyContinue }
}

try {
    switch ($Parte) {
        'cola' {
            if ((CrearCola) -and -not $SinPrueba) { ImprimirPrueba }
            Start-Sleep -Seconds 4
        }
        'ticket' {
            DejarConfiguracion
            Start-Sleep -Seconds 2
        }
        'todo' {
            if (-not (Get-Printer -Name $Cola -ErrorAction SilentlyContinue) -and -not (EsAdministrador)) {
                Write-Host 'Pidiendo permiso de administrador para crear la impresora...'
                $argumentos = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Parte', 'cola', '-SinPrueba')
                $proceso = Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList $argumentos
                if ($proceso.ExitCode -ne 0) { Detener 'No se pudo crear la impresora.' }
            }
            else { CrearCola | Out-Null }
            DejarConfiguracion
            if (-not $SinPrueba) { ImprimirPrueba }
            Read-Host 'Listo. Presiona Enter para cerrar' | Out-Null
        }
    }
}
catch { Detener ('Algo falló: ' + $_.Exception.Message) }
