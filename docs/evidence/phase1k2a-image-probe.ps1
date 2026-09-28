# Isolated design probe: technical chart only, never a product photograph.
# Run with PowerShell in STA. Outputs stay under ignored work/phase1k2a-probe.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$probeDirectory = Join-Path (Get-Location) 'work/phase1k2a-probe'
[void][IO.Directory]::CreateDirectory($probeDirectory)
$visual = [Windows.Media.DrawingVisual]::new()
$drawing = $visual.RenderOpen()
$drawing.DrawRectangle([Windows.Media.Brushes]::White, $null, [Windows.Rect]::new(0,0,1536,1536))
$gradient = [Windows.Media.LinearGradientBrush]::new([Windows.Media.Colors]::DarkSlateGray,[Windows.Media.Colors]::Wheat,0)
$drawing.DrawRectangle($gradient,$null,[Windows.Rect]::new(0,0,1536,768))
for ($i=0;$i -lt 96;$i++) {
    $pen = [Windows.Media.Pen]::new([Windows.Media.Brushes]::Black,2)
    $drawing.DrawLine($pen,[Windows.Point]::new($i*16,768),[Windows.Point]::new($i*16+180,1152))
}
for ($i=0;$i -lt 12;$i++) {
    $drawing.DrawEllipse([Windows.Media.Brushes]::SaddleBrown,$null,[Windows.Point]::new(64+$i*128,1344),48,48)
}
$drawing.Close()
$master = [Windows.Media.Imaging.RenderTargetBitmap]::new(1536,1536,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$master.Render($visual)
$master.Freeze()
function Load-ProbeImage([string]$Path,[int]$DecodeWidth) {
    $inputStream = [IO.File]::OpenRead($Path)
    try {
        $bitmap = [Windows.Media.Imaging.BitmapImage]::new()
        $bitmap.BeginInit()
        $bitmap.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad
        $bitmap.StreamSource = $inputStream
        $bitmap.DecodePixelWidth = $DecodeWidth
        $bitmap.EndInit()
        $bitmap.Freeze()
        # Force decoding and normalize the measurement to BGRA32.
        $normalized = [Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap,[Windows.Media.PixelFormats]::Bgra32,$null,0)
        $pixels = [byte[]]::new($normalized.PixelWidth*$normalized.PixelHeight*4)
        $normalized.CopyPixels($pixels,$normalized.PixelWidth*4,0)
        return $bitmap
    } finally { $inputStream.Dispose() }
}
$measurements = @()
foreach ($size in @(512,768)) {
    $scaled = [Windows.Media.Imaging.TransformedBitmap]::new($master,[Windows.Media.ScaleTransform]::new($size/1536.0,$size/1536.0))
    foreach ($format in @('jpeg','png')) {
        $path = Join-Path $probeDirectory "chart-$size.$format"
        if ($format -eq 'jpeg') { $encoder = [Windows.Media.Imaging.JpegBitmapEncoder]::new(); $encoder.QualityLevel=85 }
        else { $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new() }
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($scaled))
        $output = [IO.File]::Create($path)
        try { $encoder.Save($output) } finally { $output.Dispose() }
        [void](Load-ProbeImage $path $size)
        $samples = @()
        for ($i=0;$i -lt 30;$i++) {
            $watch = [Diagnostics.Stopwatch]::StartNew()
            [void](Load-ProbeImage $path $size)
            $watch.Stop()
            $samples += $watch.Elapsed.TotalMilliseconds
        }
        $sorted = @($samples | Sort-Object)
        $held = Load-ProbeImage $path $size
        $exclusive = [IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $exclusive.Dispose()
        $measurements += [ordered]@{
            Format=$format; Edge=$size; FileBytes=(Get-Item $path).Length
            Bgra32PayloadBytes=($size*$size*4); MedianMs=[Math]::Round($sorted[15],3)
            P95Ms=[Math]::Round($sorted[28],3); ExclusiveOpenWhileBitmapAlive=$true
        }
    }
}
$batches=@()
foreach ($size in @(512,768)) {
    foreach ($count in @(30,50,100)) {
        $watch=[Diagnostics.Stopwatch]::StartNew()
        for ($i=0;$i -lt $count;$i++) { [void](Load-ProbeImage (Join-Path $probeDirectory "chart-$size.jpeg") $size) }
        $watch.Stop()
        $batches += [ordered]@{Edge=$size;Count=$count;SequentialDecodeMs=[Math]::Round($watch.Elapsed.TotalMilliseconds,2);TheoreticalBgra32MiB=($count*$size*$size*4/1MB)}
    }
}
[ordered]@{
    Kind='Technical chart, not a photograph; warm filesystem; decode+CopyPixels; no POS benchmark'
    Utc=[DateTimeOffset]::UtcNow.ToString('O');OS=[Environment]::OSVersion.VersionString
    Runtime=[Environment]::Version.ToString();Wpf=[Windows.Media.Imaging.BitmapImage].Assembly.GetName().Version.ToString()
    Measurements=$measurements;Batches=$batches
} | ConvertTo-Json -Depth 5
