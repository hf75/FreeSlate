param([string]$Out, [string]$Ids, [string]$Specs = "1,2,3,2x2,3x1")
# Hierarchy layouts with one root and trees given as child counts per level: "3" (three children), "2x2" (two
# children with two each), "1x1x1" (a chain).
$pp = New-Object -ComObject PowerPoint.Application
$pres = $pp.Presentations.Add(-1)
$pres.PageSetup.SlideWidth = 960; $pres.PageSetup.SlideHeight = 540
$all = @{}
for ($i = 1; $i -le $pp.SmartArtLayouts.Count; $i++) { $l = $pp.SmartArtLayouts.Item($i); $all[$l.Id.Split('/')[-1]] = $l }
function Fill($node, [int[]]$counts, [int]$level, [string]$label) {
  while ($node.Nodes.Count -gt 0) { $node.Nodes.Item($node.Nodes.Count).Delete() }
  if ($level -ge $counts.Length) { return }
  for ($k = 1; $k -le $counts[$level]; $k++) {
    $child = $node.Nodes.Add()
    $child.TextFrame2.TextRange.Text = "$label.$k"
    Fill $child $counts ($level + 1) "$label.$k"
  }
}
foreach ($id in $Ids.Split(',')) {
  $layout = $all[$id.Split('/')[-1]]
  if ($null -eq $layout) { "missing $id"; continue }
  foreach ($spec in $Specs.Split(',')) {
    try {
      $slide = $pres.Slides.Add($pres.Slides.Count + 1, 12)
      $d = $slide.Shapes.AddSmartArt($layout, 60, 40, 840, 460)
      $nodes = $d.SmartArt.Nodes
      while ($nodes.Count -gt 1) { $nodes.Item($nodes.Count).Delete() }
      $root = $nodes.Item(1)
      $root.TextFrame2.TextRange.Text = "Root"
      [int[]]$counts = $spec.Split('x') | ForEach-Object { [int]$_ }
      Fill $root $counts 0 "N"
    } catch { "fail $id $spec : $($_.Exception.Message)" }
  }
}
"slides " + $pres.Slides.Count
$pres.SaveAs($Out)
$pres.Close()
