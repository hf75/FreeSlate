# Rasterizes every page of a PDF with Windows' own PDF renderer (Windows.Data.Pdf) to <Out>\page-001.png, ... at
# <Scale> pixels per DIP (1 = 96 dpi) and writes a line per page: its file, its pixels and its size in DIPs
# ("page-001.png 1588x2246 793.7x1122.5"). Windows PowerShell 5.1 reaches the WinRT API without any package.
param([Parameter(Mandatory)][string]$Pdf, [Parameter(Mandatory)][string]$Out, [double]$Scale = 1)
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
$invariant = [Globalization.CultureInfo]::InvariantCulture
for ($i = 0; $i -lt $document.PageCount; $i++) {
    $page = $document.GetPage($i)
    $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
    $width = $page.Size.Width
    $height = $page.Size.Height
    $options.DestinationWidth = [uint32][Math]::Round($width * $Scale)
    $options.DestinationHeight = [uint32][Math]::Round($height * $Scale)
    $name = 'page-{0:000}.png' -f ($i + 1)
    $target = Await ($folder.CreateFileAsync($name, [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])
    $stream = Await ($target.OpenAsync([Windows.Storage.FileAccessMode]::ReadWrite)) ([Windows.Storage.Streams.IRandomAccessStream])
    try { AwaitAction ($page.RenderToStreamAsync($stream, $options)) }
    finally { $stream.Dispose(); $page.Dispose() }
    [string]::Format($invariant, '{0} {1}x{2} {3:0.###}x{4:0.###}', $name, $options.DestinationWidth, $options.DestinationHeight, $width, $height)
}
