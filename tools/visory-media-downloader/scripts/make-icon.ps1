param([Parameter(Mandatory = $true)][string]$OutFile)
# Regenerates the text-encoded application icon (src\MoorkszVisoryMediaDownloader.App\app.ico.b64). Development-time only.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = New-Object System.Collections.ArrayList
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $r = [single]($s * 0.22)
    $w = [single]($s - 1)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc([single]0, [single]0, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($w - 2 * $r, [single]0, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($w - 2 * $r, $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc([single]0, $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $rect = New-Object System.Drawing.RectangleF ([single]0), ([single]0), ([single]$s), ([single]$s)
    $top = [System.Drawing.Color]::FromArgb(255, 59, 123, 255)
    $bottom = [System.Drawing.Color]::FromArgb(255, 28, 72, 190)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $top, $bottom, ([single]90)
    $g.FillPath($brush, $path)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single][Math]::Max(1.5, $s * 0.085))
    $pen.StartCap = 'Round'
    $pen.EndCap = 'Round'
    $pen.LineJoin = 'Round'
    $cx = [single]($s / 2)
    $g.DrawLine($pen, $cx, [single]($s * 0.22), $cx, [single]($s * 0.60))
    $arrow = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF ([single]($s * 0.33)), ([single]($s * 0.45))),
        (New-Object System.Drawing.PointF $cx, ([single]($s * 0.62))),
        (New-Object System.Drawing.PointF ([single]($s * 0.67)), ([single]($s * 0.45))))
    $g.DrawLines($pen, $arrow)
    $tray = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF ([single]($s * 0.24)), ([single]($s * 0.66))),
        (New-Object System.Drawing.PointF ([single]($s * 0.24)), ([single]($s * 0.77))),
        (New-Object System.Drawing.PointF ([single]($s * 0.76)), ([single]($s * 0.77))),
        (New-Object System.Drawing.PointF ([single]($s * 0.76)), ([single]($s * 0.66))))
    $g.DrawLines($pen, $tray)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [void]$images.Add([pscustomobject]@{ Size = $s; Data = $ms.ToArray() })
}
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$img.Data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $img.Data.Length
}
foreach ($img in $images) { $bw.Write([byte[]]$img.Data) }
$bw.Flush()
$b64 = [Convert]::ToBase64String($out.ToArray(), [Base64FormattingOptions]::InsertLineBreaks)
[IO.File]::WriteAllText($OutFile, $b64 + "`r`n", (New-Object System.Text.UTF8Encoding $false))
Write-Output "PASS icon: $($out.Length) bytes -> $OutFile"
