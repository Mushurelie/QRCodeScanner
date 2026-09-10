<#
    Génère src/QRCodeScanner/app.ico (l'icône de l'exe dans l'Explorateur).

    Le même glyphe est redessiné à l'exécution par AppIcons.cs pour la zone de
    notification ; ce script existe pour que le fichier .ico committé reste
    reproductible. À relancer seulement si le dessin change.

        powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
#>

[CmdletBinding()]
param(
    [string]$OutputPath
)

if (-not $OutputPath) {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
    $OutputPath = Join-Path $root '..\src\QRCodeScanner\app.ico'
}

Add-Type -AssemblyName System.Drawing

$background = [System.Drawing.Color]::FromArgb(79, 70, 229)
$finders = @(@(0, 0), @(5, 0), @(0, 5))
$modules = @(@(3, 2), @(2, 3), @(4, 3), @(3, 4), @(5, 4), @(4, 5), @(6, 6), @(4, 6))

function New-GlyphPng {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $radius = $Size * 0.24
    $diameter = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $path.AddArc($Size - $diameter, 0, $diameter, $diameter, 270, 90)
    $path.AddArc($Size - $diameter, $Size - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc(0, $Size - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    $backBrush = New-Object System.Drawing.SolidBrush($background)
    $graphics.FillPath($backBrush, $path)

    $unit = $Size / 9.0
    $foreBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    foreach ($f in $finders) {
        $graphics.FillRectangle($foreBrush, (1 + $f[0]) * $unit, (1 + $f[1]) * $unit, $unit * 2, $unit * 2)
    }
    foreach ($m in $modules) {
        $graphics.FillRectangle($foreBrush, (1 + $m[0]) * $unit, (1 + $m[1]) * $unit, $unit, $unit)
    }

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)

    $bytes = $stream.ToArray()
    $stream.Dispose(); $foreBrush.Dispose(); $backBrush.Dispose()
    $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()

    return , $bytes
}

<#
    Trame au format DIB (BITMAPINFOHEADER + pixels BGRA de bas en haut + masque AND).
    Windows lit aussi les trames PNG dans un .ico, mais pas System.Drawing.Icon :
    on garde le DIB pour les petites tailles, que tout le monde sait décoder, et
    on réserve le PNG aux 128 et 256 px où il évite un fichier obèse.
#>
function New-GlyphDib {
    param([int]$Size)

    $png = New-GlyphPng -Size $Size
    $stream = New-Object System.IO.MemoryStream(, $png)
    $bitmap = New-Object System.Drawing.Bitmap($stream)

    $out = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($out)

    # BITMAPINFOHEADER : la hauteur est doublée, elle couvre les deux masques.
    $writer.Write([uint32]40)
    $writer.Write([int32]$Size)
    $writer.Write([int32]($Size * 2))
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]0)              # BI_RGB
    $writer.Write([uint32]($Size * $Size * 4))
    $writer.Write([int32]0); $writer.Write([int32]0)
    $writer.Write([uint32]0); $writer.Write([uint32]0)

    # Masque XOR : BGRA, première ligne en bas.
    for ($y = $Size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $Size; $x++) {
            $pixel = $bitmap.GetPixel($x, $y)
            $writer.Write([byte]$pixel.B)
            $writer.Write([byte]$pixel.G)
            $writer.Write([byte]$pixel.R)
            $writer.Write([byte]$pixel.A)
        }
    }

    # Masque AND : inutile en 32 bits (l'alpha suffit), mais le format l'exige.
    $strideBits = [math]::Ceiling($Size / 32) * 4
    for ($y = 0; $y -lt $Size; $y++) {
        $writer.Write((New-Object byte[] $strideBits))
    }

    $writer.Flush()
    $bytes = $out.ToArray()
    $writer.Dispose(); $out.Dispose(); $bitmap.Dispose(); $stream.Dispose()

    return , $bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @{}
foreach ($size in $sizes) {
    $frames[$size] = if ($size -le 64) { New-GlyphDib -Size $size } else { New-GlyphPng -Size $size }
}

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$file = [System.IO.File]::Create($OutputPath)
$writer = New-Object System.IO.BinaryWriter($file)

# ICONDIR
$writer.Write([uint16]0)              # réservé
$writer.Write([uint16]1)              # type : icône
$writer.Write([uint16]$sizes.Count)

# Les données commencent après l'en-tête et le répertoire (16 octets par entrée).
$offset = 6 + 16 * $sizes.Count
foreach ($size in $sizes) {
    $bytes = $frames[$size]
    $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $writer.Write([byte]0)            # palette
    $writer.Write([byte]0)            # réservé
    $writer.Write([uint16]1)          # plans
    $writer.Write([uint16]32)         # bits par pixel
    $writer.Write([uint32]$bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $bytes.Length
}

foreach ($size in $sizes) { $writer.Write($frames[$size]) }

$writer.Flush(); $writer.Close(); $file.Dispose()

Write-Output ("Icône écrite : {0} ({1} octets)" -f $OutputPath, (Get-Item $OutputPath).Length)
