# Generates src/app.ico (multi-size, 16..256 px) without any external tools.
#
#   powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
#
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
$target = Join-Path $root 'src\app.ico'

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [single]($size * 0.03)
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))
    $bg = New-RoundedPath $rect.X $rect.Y $rect.Width $rect.Height ([single]($size * 0.22))

    $c1 = [System.Drawing.Color]::FromArgb(255, 59, 130, 246)
    $c2 = [System.Drawing.Color]::FromArgb(255, 29, 78, 216)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, [single]45)
    $g.FillPath($brush, $bg)

    # shield
    $pts = @(
        (New-Object System.Drawing.PointF([single]($size * 0.50), [single]($size * 0.16)))
        (New-Object System.Drawing.PointF([single]($size * 0.79), [single]($size * 0.28)))
        (New-Object System.Drawing.PointF([single]($size * 0.79), [single]($size * 0.53)))
        (New-Object System.Drawing.PointF([single]($size * 0.50), [single]($size * 0.85)))
        (New-Object System.Drawing.PointF([single]($size * 0.21), [single]($size * 0.53)))
        (New-Object System.Drawing.PointF([single]($size * 0.21), [single]($size * 0.28)))
    )
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillPolygon($white, $pts)

    # check mark
    $penWidth = [single][Math]::Max(1.5, $size * 0.085)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 22, 101, 52), $penWidth)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $check = @(
        (New-Object System.Drawing.PointF([single]($size * 0.35), [single]($size * 0.50)))
        (New-Object System.Drawing.PointF([single]($size * 0.46), [single]($size * 0.62)))
        (New-Object System.Drawing.PointF([single]($size * 0.66), [single]($size * 0.39)))
    )
    $g.DrawLines($pen, $check)

    $pen.Dispose()
    $white.Dispose()
    $brush.Dispose()
    $bg.Dispose()
    $g.Dispose()
    return $bmp
}

function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $size = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $raw = New-Object byte[] ($stride * $size)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $raw, 0, $raw.Length)
    }
    finally {
        $bmp.UnlockBits($data)
    }

    $maskStride = [int]([Math]::Floor(($size + 31) / 32) * 4)
    $mask = New-Object byte[] ($maskStride * $size)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([int]40)                 # biSize
    $bw.Write([int]$size)              # biWidth
    $bw.Write([int]($size * 2))        # biHeight (image + mask)
    $bw.Write([int16]1)                # biPlanes
    $bw.Write([int16]32)               # biBitCount
    $bw.Write([int]0)                  # biCompression
    $bw.Write([int]($stride * $size))  # biSizeImage
    $bw.Write([int]0); $bw.Write([int]0)
    $bw.Write([int]0); $bw.Write([int]0)

    for ($row = $size - 1; $row -ge 0; $row--) {
        $bw.Write($raw, $row * $stride, $size * 4)
    }
    $bw.Write($mask)
    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose()
    $ms.Dispose()
    return , $bytes
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return , $bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    if ($size -ge 128) { $bytes = [byte[]](Get-PngBytes $bmp) } else { $bytes = [byte[]](Get-DibBytes $bmp) }
    $bmp.Dispose()
    $images += (New-Object psobject -Property @{ Size = $size; Bytes = $bytes })
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
$fs = [System.IO.File]::Create($target)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([int16]0)
$bw.Write([int16]1)
$bw.Write([int16]$images.Count)

$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $size = $image.Size
    $bytes = $image.Bytes
    $dim = if ($size -ge 256) { 0 } else { $size }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([int16]1)
    $bw.Write([int16]32)
    $bw.Write([int]$bytes.Length)
    $bw.Write([int]$offset)
    $offset += $bytes.Length
}
foreach ($image in $images) {
    $bw.Write($image.Bytes)
}
$bw.Flush()
$bw.Dispose()
$fs.Dispose()

Write-Host ("Wrote {0} ({1:N0} bytes)" -f $target, (Get-Item -LiteralPath $target).Length)
