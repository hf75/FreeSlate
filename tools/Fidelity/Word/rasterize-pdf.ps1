# Rasterizes every page of a PDF with Windows' own PDF renderer (Windows.Data.Pdf) to <Out>\page-001.png, ... at
# <Scale> pixels per DIP (1 = 96 dpi). With -Sizes "w1xh1,w2xh2,..." (points) page i is cut to the rectangle of
# that size at its top left, as if it had that size. Windows PowerShell 5.1 reaches the WinRT API without any package.
param([Parameter(Mandatory)][string]$Pdf, [Parameter(Mandatory)][string]$Out, [double]$Scale = 1, [string]$Sizes = "")
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } |
    Select-Object -First 1
$asTaskAction = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' } |
    Select-Object -First 1
function Await($operation, [Type]$type) {
    $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation))
    $task.Wait(-1) | Out-Null
    $task.Result
}
function AwaitAction($action) {
    $task = $asTaskAction.Invoke($null, @($action))
    $task.Wait(-1) | Out-Null
}

[void][Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
[void][Windows.Storage.StorageFolder, Windows.Storage, ContentType = WindowsRuntime]
[void][Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType = WindowsRuntime]
[void][Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]

$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path $Pdf).Path)) ([Windows.Storage.StorageFile])
$document = Await ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
$folder = Await ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync((Resolve-Path $Out).Path)) ([Windows.Storage.StorageFolder])
$cuts = @($Sizes -split ',' | Where-Object { $_ -ne '' })
for ($i = 0; $i -lt $document.PageCount; $i++) {
    $page = $document.GetPage($i)
    $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
    $width = $page.Size.Width
    $height = $page.Size.Height
    if ($i -lt $cuts.Count) {
        $size = $cuts[$i] -split 'x'
        $width = [double]::Parse($size[0], [Globalization.CultureInfo]::InvariantCulture) * 96 / 72
        $height = [double]::Parse($size[1], [Globalization.CultureInfo]::InvariantCulture) * 96 / 72
        $options.SourceRect = New-Object Windows.Foundation.Rect(0, 0, $width, $height)
    }
    $options.DestinationWidth = [uint32][Math]::Round($width * $Scale)
    $options.DestinationHeight = [uint32][Math]::Round($height * $Scale)
    $name = 'page-{0:000}.png' -f ($i + 1)
    $target = Await ($folder.CreateFileAsync($name, [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])
    $stream = Await ($target.OpenAsync([Windows.Storage.FileAccessMode]::ReadWrite)) ([Windows.Storage.Streams.IRandomAccessStream])
    try { AwaitAction ($page.RenderToStreamAsync($stream, $options)) }
    finally { $stream.Dispose(); $page.Dispose() }
    '{0} {1}x{2}' -f $name, $options.DestinationWidth, $options.DestinationHeight
}
