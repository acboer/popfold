# Gera Popfold.ico (16 a 256 px, frames PNG) — identidade "Bala de Goma".
# Quadrado lilás com quatro balas (chiclete, menta, limonada, branca). Nos tamanhos pequenos
# o desenho é simplificado (balas maiores, sem brilho e sem os risquinhos de "pop").
param([string]$Out = "$PSScriptRoot\..\Popfold.ico", [string]$PngDir = "")
Add-Type -AssemblyName System.Drawing

$Lavanda  = [System.Drawing.Color]::FromArgb(255, 0xB7, 0x9C, 0xFF)
$Chiclete = [System.Drawing.Color]::FromArgb(255, 0xFF, 0x7A, 0xB6)
$Menta    = [System.Drawing.Color]::FromArgb(255, 0x7F, 0xE3, 0xC4)
$Limonada = [System.Drawing.Color]::FromArgb(255, 0xFF, 0xE2, 0x7A)
$Branco   = [System.Drawing.Color]::White
$Uva      = [System.Drawing.Color]::FromArgb(255, 0x2B, 0x1B, 0x4A)

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Fill-Circle($g, $color, [float]$cx, [float]$cy, [float]$r) {
    $b = New-Object System.Drawing.SolidBrush $color
    $g.FillEllipse($b, $cx - $r, $cy - $r, 2 * $r, 2 * $r)
    $b.Dispose()
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 200.0
    $small = $size -le 24
    $big = $size -ge 64

    # Fundo lilás (nos ícones pequenos ocupa mais espaço)
    $m = $(if ($small) { 4 } else { 10 }) * $s
    $bg = New-RoundedPath $m $m ($size - 2 * $m) ($size - 2 * $m) (56 * $s)
    $bb = New-Object System.Drawing.SolidBrush $Lavanda
    $g.FillPath($bb, $bg); $bb.Dispose(); $bg.Dispose()

    # Quatro balas
    $r = $(if ($small) { 32 } else { 27 }) * $s
    Fill-Circle $g $Chiclete (72 * $s) (80 * $s) $r
    Fill-Circle $g $Menta    (130 * $s) (72 * $s) $r
    Fill-Circle $g $Limonada (70 * $s) (134 * $s) $r
    Fill-Circle $g $Branco   (128 * $s) (130 * $s) $r

    if (-not $small) {
        # Brilho das balas
        $hl = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(140, 255, 255, 255))
        foreach ($c in @(@(63, 70), @(121, 62), @(61, 124))) {
            $g.FillEllipse($hl, ($c[0] - 9) * $s, ($c[1] - 6) * $s, 18 * $s, 12 * $s)
        }
        $hl.Dispose()
    }
    if ($big) {
        # Risquinhos de "pop"
        $pen = New-Object System.Drawing.Pen $Uva, (6 * $s)
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        $g.DrawLine($pen, 160 * $s, 28 * $s, 168 * $s, 16 * $s)
        $g.DrawLine($pen, 172 * $s, 44 * $s, 186 * $s, 40 * $s)
        $g.DrawLine($pen, 150 * $s, 22 * $s, 148 * $s, 8 * $s)
        $pen.Dispose()
    }
    $g.Dispose()
    return $bmp
}

function To-Png($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = @()
foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    if ($PngDir) {
        New-Item -ItemType Directory -Force $PngDir | Out-Null
        $bmp.Save((Join-Path $PngDir "popfold-$sz.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $pngs += , (To-Png $bmp)
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $len = $pngs[$i].Length
    $w.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))
    $w.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
"Wrote $Out"
