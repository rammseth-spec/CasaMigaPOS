param(
    [string]$Version = '0.9.0',
    [string]$Dotnet = 'dotnet',
    [string]$Iscc = (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'La versión debe tener el formato X.Y.Z.' }
if (-not (Test-Path $Iscc)) { $Iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
if (-not (Test-Path $Iscc)) { throw 'No se encontró Inno Setup 6 (ISCC.exe). Instálalo con: winget install JRSoftware.InnoSetup' }
Push-Location (Join-Path $PSScriptRoot '..')
try {
    Remove-Item artifacts/win-x64, artifacts/tools -Recurse -Force -ErrorAction SilentlyContinue
    & $Dotnet publish src/CasaMiga.Presentation -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación de la aplicación.' }
    & $Dotnet publish src/CasaMiga.Maintenance -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o artifacts/tools
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación de la herramienta de mantenimiento.' }
    # Un instalador comercial nunca lleva el modo de prueba ni la entrada automática de una copia de desarrollo.
    $dev = Get-ChildItem artifacts/win-x64, artifacts/tools -Recurse -File -Include development.settings.json, dev-autologin.json
    if ($dev) { throw "La publicación contiene archivos de desarrollo: $($dev.FullName -join ', ')" }
    & $Iscc "/DAppVersion=$Version" installer/CasaMigaPOS.iss
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del instalador.' }
    $setup = Get-Item "artifacts/installer/CasaMigaPOS-$Version-setup.exe"
    $hash = (Get-FileHash $setup.FullName -Algorithm SHA256).Hash
    "$hash  $($setup.Name)" | Set-Content -Encoding ascii "$($setup.FullName).sha256"
    Write-Output "Instalador: $($setup.FullName)"
    Write-Output "SHA-256: $hash"
} finally { Pop-Location }
