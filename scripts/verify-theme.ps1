param([string]$OutputPath = 'work/theme-preview.png')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$theme = [Windows.Markup.XamlReader]::Load([System.Xml.XmlReader]::Create((Join-Path $PSScriptRoot '../src/CasaMiga.Presentation/Resources/Theme.xaml')))
$hostPanel = [Windows.Controls.StackPanel]::new()
$hostPanel.Resources = $theme
$hostPanel.Background = $theme['BackgroundBrush']
$hostPanel.Width = 500
function Find-Text($node) {
    if ($node -is [Windows.Controls.TextBlock]) { return $node }
    for ($i=0; $i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($node); $i++) {
        $found = Find-Text ([Windows.Media.VisualTreeHelper]::GetChild($node,$i))
        if ($null -ne $found) { return $found }
    }
}
foreach ($styleName in @('Primary','SecondaryButton','SidebarNavButton','DestructiveButton','SectionTabButton')) {
    $button = [Windows.Controls.Button]::new()
    $button.Content = "Entrar - $styleName"
    $button.Margin = [Windows.Thickness]::new(20,10,20,10)
    if ($styleName -eq 'Primary') { $button.Style = $theme[[Windows.Controls.Button]] }
    else { $button.Style = $theme[$styleName] }
    if ($styleName -eq 'SectionTabButton') { $button.Tag = $true }
    if ($styleName -eq 'SidebarNavButton') {
        $button.Background = $theme['AvocadoLeafBrush']
        $button.Tag = 'M2,3 L18,3 18,14 2,14 Z M6,18 L14,18 M10,14 L10,18'
    }
    [void]$hostPanel.Children.Add($button)
}
$hostPanel.Measure([Windows.Size]::new(500,1000))
$hostPanel.Arrange([Windows.Rect]::new(0,0,500,$hostPanel.DesiredSize.Height))
$hostPanel.UpdateLayout()
foreach ($button in $hostPanel.Children) {
    $label = Find-Text $button
    if ($null -eq $label) { throw 'No rendered label found' }
    $actual = $label.Foreground.ToString()
    $expected = $button.Foreground.ToString()
    if ($actual -ne $expected) { throw "Foreground mismatch: $actual != $expected" }
    "$($button.Content): rendered=$actual expected=$expected"
    if ($button.Content -like '*SectionTabButton') {
        if ($actual -ne '#FFFFFFFF' -or $button.Background.ToString() -ne $theme['AvocadoLeafBrush'].ToString()) {
            throw 'Selected section tab must have white text on avocado green'
        }
    }
    if ($button.Content -like '*SidebarNavButton') {
        $before = $label.Foreground.ToString()
        $button.IsEnabled = $false
        $hostPanel.UpdateLayout()
        if ($label.Foreground.ToString() -ne $before) { throw 'Navigation flashes when disabled' }
        $button.IsEnabled = $true
    }
}
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(500,[int]$hostPanel.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($hostPanel)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create((Join-Path (Get-Location) $OutputPath))
try { $encoder.Save($stream) } finally { $stream.Dispose() }
