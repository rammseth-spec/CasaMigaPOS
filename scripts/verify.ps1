param([string]$Dotnet = 'dotnet', [string]$PackageSource)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $restoreArgs = @('restore', 'CasaMigaPOS.slnx', '--configfile', 'NuGet.Config', '--locked-mode')
    if ($PackageSource) { $restoreArgs += @('--source', $PackageSource) }
    & $Dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'Falló restore.' }
    & $Dotnet build CasaMigaPOS.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Falló compilación.' }
    & $Dotnet test CasaMigaPOS.slnx -c Release --no-build --no-restore --logger 'trx;LogFilePrefix=foundation' --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron pruebas.' }
} finally { Pop-Location }
