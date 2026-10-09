param([string]$Out, [string]$Ids, [string]$Counts = "2,3,4,5,7", [string]$Mode = "top")
# Layouts (comma-separated uniqueIds or short names) at several node counts: Mode top sets the top-level nodes
# (without children), Mode children one top-level node with that many children.
$pp = New-Object -ComObject PowerPoint.Application
$pres = $pp.Presentations.Add(-1)
$pres.PageSetup.SlideWidth = 960; $pres.PageSetup.SlideHeight = 540
$all = @{}
for ($i = 1; $i -le $pp.SmartArtLayouts.Count; $i++) { $l = $pp.SmartArtLayouts.Item($i); $all[$l.Id.Split('/')[-1]] = $l }
foreach ($id in $Ids.Split(',')) {
  $layout = $all[$id.Split('/')[-1]]
  if ($null -eq $layout) { "missing $id"; continue }
  foreach ($c in $Counts.Split(',')) {
    try {
      $slide = $pres.Slides.Add($pres.Slides.Count + 1, 12)
      $d = $slide.Shapes.AddSmartArt($layout, 60, 40, 840, 460)
      $nodes = $d.SmartArt.Nodes
      $top = if ($Mode -eq "children") { 1 } else { [int]$c }
      while ($nodes.Count -gt $top) { $nodes.Item($nodes.Count).Delete() }
      while ($nodes.Count -lt $top) { [void]$nodes.Add() }
      for ($k = 1; $k -le $nodes.Count; $k++) {
        $node = $nodes.Item($k)
        $want = if ($Mode -eq "children") { [int]$c } else { 0 }
        while ($node.Nodes.Count -gt $want) { $node.Nodes.Item($node.Nodes.Count).Delete() }
        while ($node.Nodes.Count -lt $want) { [void]$node.Nodes.Add() }
        $node.TextFrame2.TextRange.Text = "Item $k"
        for ($j = 1; $j -le $node.Nodes.Count; $j++) { $node.Nodes.Item($j).TextFrame2.TextRange.Text = "Sub $k.$j" }
      }
    } catch { "fail $id $c : $($_.Exception.Message)" }
  }
}
"slides " + $pres.Slides.Count
$pres.SaveAs($Out)
$pres.Close()
