Add-Type -AssemblyName System.Drawing

function New-TimerBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))

    $pad = [single]([Math]::Max(1.0, $size * 0.045))
    $edge = [single]($size - (2 * $pad))
    $rect = New-Object System.Drawing.RectangleF -ArgumentList $pad, $pad, $edge, $edge
    $fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 85, 119, 216))
    $graphics.FillEllipse($fill, $rect)

    $penWidth = [single]([Math]::Max(1.5, $size * 0.072))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 247, 249, 252)), $penWidth
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $cx = [single]($size / 2.0)
    $cy = [single]($size / 2.0)
    $minuteLength = [single]($rect.Height * 0.30)
    $hourAngle = 125.0 * [Math]::PI / 180.0
    $hourLength = [single]($rect.Height * 0.20)
    $graphics.DrawLine($pen, $cx, $cy, $cx, ($cy - $minuteLength))
    $hourX = [single]($cx + ([Math]::Sin($hourAngle) * $hourLength))
    $hourY = [single]($cy - ([Math]::Cos($hourAngle) * $hourLength))
    $graphics.DrawLine($pen, $cx, $cy, $hourX, $hourY)

    $dot = [single]([Math]::Max(1.2, $size * 0.048))
    $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 247, 249, 252))
    $graphics.FillEllipse($dotBrush, ($cx - $dot), ($cy - $dot), ($dot * 2), ($dot * 2))

    $pen.Dispose()
    $fill.Dispose()
    $dotBrush.Dispose()
    $graphics.Dispose()
    return $bitmap
}

function Get-PngBytes([System.Drawing.Bitmap]$bitmap) {
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return $bytes
}

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$images = New-Object System.Collections.Generic.List[byte[]]
foreach ($size in $sizes) {
    $bitmap = New-TimerBitmap $size
    $images.Add((Get-PngBytes $bitmap))
    $bitmap.Dispose()
}

$destination = Join-Path $PSScriptRoot "..\src\WindowsTimer\Resources\app.ico"
$directory = Split-Path $destination -Parent
New-Item -ItemType Directory -Force -Path $directory | Out-Null

$stream = [System.IO.File]::Create($destination)
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)
$offset = 6 + (16 * $images.Count)
for ($index = 0; $index -lt $images.Count; $index++) {
    $size = $sizes[$index]
    $dimension = if ($size -ge 256) { [byte]0 } else { [byte]$size }
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}

foreach ($image in $images) {
    $writer.Write($image)
}

$writer.Flush()
$writer.Dispose()
$stream.Dispose()
Write-Output "Wrote $destination"
