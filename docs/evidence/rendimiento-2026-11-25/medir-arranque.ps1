param([Parameter(Mandatory)][string]$Exe, [Parameter(Mandatory)][string]$Datos, [int]$Veces = 5, [switch]$UnNucleo)
# -UnNucleo: limita la caja a un solo núcleo del procesador para simular una computadora modesta.
# Mide el arranque de una copia aislada: tiempo hasta ventana, tiempo hasta quedar quieta (CPU estable 1.5 s) y CPU usada.
$ErrorActionPreference = 'Stop'
if ($Datos -like "*$env:LOCALAPPDATA\CasaMigaPOS*") { throw 'Nunca con datos reales.' }
$res = @()
for ($i = 1; $i -le $Veces; $i++) {
    $env:CASAMIGA_DATA_DIR = $Datos
    $inicio = [DateTimeOffset]::UtcNow; $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $Exe -PassThru
    if ($UnNucleo) { $p.ProcessorAffinity = [IntPtr]1 }
    Remove-Item Env:CASAMIGA_DATA_DIR
    while ($p.MainWindowHandle -eq 0 -and $sw.Elapsed.TotalSeconds -lt 60) { Start-Sleep -Milliseconds 20; $p.Refresh() }
    $ventana = $sw.Elapsed.TotalMilliseconds
    $last = $p.TotalProcessorTime.TotalMilliseconds; $quieto = 0; $t = $sw.Elapsed.TotalMilliseconds
    while ($sw.Elapsed.TotalSeconds -lt 60) {
        Start-Sleep -Milliseconds 250; $p.Refresh()
        $cpu = $p.TotalProcessorTime.TotalMilliseconds
        if ($cpu - $last -lt 20) { $quieto += 250; if ($quieto -ge 1500) { break } } else { $quieto = 0; $t = $sw.Elapsed.TotalMilliseconds }
        $last = $cpu
    }
    $p.Refresh()
    $sesion = Get-ChildItem $Datos -Filter 'events-*.jsonl' | Get-Content | ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object { $_.eventName -eq 'store.autologin' -and [DateTimeOffset]$_.timestamp -gt $inicio } | Select-Object -First 1
    $sesionMs = if ($sesion) { [int]([DateTimeOffset]$sesion.timestamp - $inicio).TotalMilliseconds } else { -1 }
    $res += [pscustomobject]@{ Vez = $i; VentanaMs = [int]$ventana; SesionMs = $sesionMs; ListaMs = [int]$t; CpuMs = [int]$p.TotalProcessorTime.TotalMilliseconds; PrivadaMB = [math]::Round($p.PrivateMemorySize64 / 1MB, 1); TrabajoMB = [math]::Round($p.WorkingSet64 / 1MB, 1) }
    Stop-Process -Id $p.Id -Force; $p.WaitForExit(); Start-Sleep -Seconds 2
}
$res | Format-Table -AutoSize | Out-String | Write-Output
$m = { param($k) ($res | Sort-Object $k)[[int][math]::Floor($res.Count / 2)].$k }
Write-Output ("Mediana: ventana {0} ms · sesión {4} ms · lista {1} ms · CPU {2} ms · privada {3} MB · trabajo {5} MB" -f (& $m VentanaMs), (& $m ListaMs), (& $m CpuMs), (& $m PrivadaMB), (& $m SesionMs), (& $m TrabajoMB))
