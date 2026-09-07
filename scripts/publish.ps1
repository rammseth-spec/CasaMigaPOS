param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $Dotnet publish src/CasaMiga.Presentation -c Release -r win-x64 --self-contained true -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Falló publicación.' }
} finally { Pop-Location }
