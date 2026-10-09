param([string]$Out, [string]$Id, [string]$Count = "1", [string]$Frames = "840x460,840x300,840x600,600x460,1000x460,500x460")
# One layout at several frame sizes: which dimension limits its size.
$pp = New-Object -ComObject PowerPoint.Application
$pres = $pp.Presentations.Add(-1)
$pres.PageSetup.SlideWidth = 1200; $pres.PageSetup.SlideHeight = 700
$layout = $null
for ($i = 1; $i -le $pp.SmartArtLayouts.Count; $i++) { $l = $pp.SmartArtLayouts.Item($i); if ($l.Id.Split('/')[-1] -eq $Id) { $layout = $l } }
foreach ($c in $Count.Split(',')) {
  foreach ($f in $Frames.Split(',')) {
    $w = [int]$f.Split('x')[0]; $h = [int]$f.Split('x')[1]
    $slide = $pres.Slides.Add($pres.Slides.Count + 1, 12)
    $d = $slide.Shapes.AddSmartArt($layout, 20, 20, $w, $h)
    $nodes = $d.SmartArt.Nodes
    while ($nodes.Count -gt [int]$c) { $nodes.Item($nodes.Count).Delete() }
    while ($nodes.Count -lt [int]$c) { [void]$nodes.Add() }
    for ($k = 1; $k -le $nodes.Count; $k++) {
      $node = $nodes.Item($k)
      while ($node.Nodes.Count -gt 0) { $node.Nodes.Item(1).Delete() }
      $node.TextFrame2.TextRange.Text = "Item $k"
    }
  }
}
"slides " + $pres.Slides.Count
$pres.SaveAs($Out)
$pres.Close()
