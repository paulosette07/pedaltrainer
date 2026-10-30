<#
.SYNOPSIS
    Gera o icone do Pedal Trainer (pedaltrainer.ico) a partir da marca do aplicativo.

.DESCRIPTION
    Desenha o simbolo do pedal em varias resolucoes e empacota tudo em um unico .ico
    compativel com Windows 10/11 (frames BMP 32bpp ate 64px e frames PNG em 128/256px).

    As cores vem da paleta do app (Themes/DarkTheme.xaml):
      fundo   #171D25 -> #0A0C0F
      borda   #2E3742
      pedal   #5CF0A6 -> #28C777

.PARAMETER OutputPath
    Caminho do .ico a ser gerado. Padrao: src/PedalTrainer.App/Assets/pedaltrainer.ico

.PARAMETER PreviewDirectory
    Quando informado, grava tambem PNGs de previa nesta pasta.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/branding/New-AppIcon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputPath = '',
    [string]$PreviewDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

# Resolvido no corpo do script: $PSScriptRoot ainda esta vazio na avaliacao dos defaults.
if (-not $OutputPath) {
    $OutputPath = Join-Path $PSScriptRoot '..\..\src\PedalTrainer.App\Assets\pedaltrainer.ico'
}

# Tamanhos que o Windows usa: barra de tarefas, alt+tab, explorer, jumplist, alt-tab grande.
$Sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$PngFromSize = 128

# Geometria desenhada em um canvas logico de 256x256.
$Canvas = 256.0
$CornerRadius = 56.0
$BorderInset = 1.0
$BorderWidth = 2.0

# O simbolo do pedal e desenhado em coordenadas 16x16 (mesma forma da marca no MainWindow.xaml).
# Escala 10.7 => 150px de altura no canvas de 256 (58%); nos tamanhos minimos sobe para 11.6.

function New-RoundedRectanglePath {
    param([double]$Size, [double]$Radius)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $path.AddArc($Size - $diameter, 0, $diameter, $diameter, 270, 90)
    $path.AddArc($Size - $diameter, $Size - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc(0, $Size - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-GlyphScale {
    param([int]$Size)

    # Em tamanhos minusculos o simbolo precisa de mais area para nao sumir no antialias.
    if ($Size -le 24) { return 11.6 }
    return 10.7
}

function New-IconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $scale = $Size / $Canvas
        $graphics.ScaleTransform($scale, $scale)

        $tilePath = New-RoundedRectanglePath -Size $Canvas -Radius $CornerRadius
        try {
            $tileBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
                (New-Object System.Drawing.PointF(0, 0)),
                (New-Object System.Drawing.PointF(0, [float]$Canvas)),
                [System.Drawing.ColorTranslator]::FromHtml('#171D25'),
                [System.Drawing.ColorTranslator]::FromHtml('#0A0C0F'))
            $graphics.FillPath($tileBrush, $tilePath)
            $tileBrush.Dispose()

            $inset = $BorderInset
            $innerSize = $Canvas - (2 * $inset)
            $borderPath = New-RoundedRectanglePath -Size $innerSize -Radius ($CornerRadius - $inset)
            try {
                $borderPen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#2E3742'), [float]$BorderWidth)
                $graphics.TranslateTransform([float]$inset, [float]$inset)
                $graphics.DrawPath($borderPen, $borderPath)
                $graphics.TranslateTransform([float](-$inset), [float](-$inset))
                $borderPen.Dispose()
            }
            finally {
                $borderPath.Dispose()
            }
        }
        finally {
            $tilePath.Dispose()
        }

        $glyphScale = New-GlyphScale -Size $Size
        $glyphOffset = ($Canvas / 2.0) - (8.0 * $glyphScale)

        $glyphGraphicsPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        try {
            $glyphGraphicsPath.AddLines([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF(2, 1)),
                (New-Object System.Drawing.PointF(5, 1)),
                (New-Object System.Drawing.PointF(5, 9))
            ))
            $glyphGraphicsPath.AddBezier(
                (New-Object System.Drawing.PointF(5, 9)),
                (New-Object System.Drawing.PointF(5, 11)),
                (New-Object System.Drawing.PointF(6, 12)),
                (New-Object System.Drawing.PointF(8, 12)))
            $glyphGraphicsPath.AddBezier(
                (New-Object System.Drawing.PointF(8, 12)),
                (New-Object System.Drawing.PointF(10, 12)),
                (New-Object System.Drawing.PointF(11, 11)),
                (New-Object System.Drawing.PointF(11, 9)))
            $glyphGraphicsPath.AddLines([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF(11, 9)),
                (New-Object System.Drawing.PointF(11, 1)),
                (New-Object System.Drawing.PointF(14, 1)),
                (New-Object System.Drawing.PointF(14, 9))
            ))
            $glyphGraphicsPath.AddBezier(
                (New-Object System.Drawing.PointF(14, 9)),
                (New-Object System.Drawing.PointF(14, 13)),
                (New-Object System.Drawing.PointF(11.5, 15)),
                (New-Object System.Drawing.PointF(8, 15)))
            $glyphGraphicsPath.AddBezier(
                (New-Object System.Drawing.PointF(8, 15)),
                (New-Object System.Drawing.PointF(4.5, 15)),
                (New-Object System.Drawing.PointF(2, 13)),
                (New-Object System.Drawing.PointF(2, 9)))
            $glyphGraphicsPath.CloseFigure()

            $glyphBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
                (New-Object System.Drawing.PointF(2, 1)),
                (New-Object System.Drawing.PointF(14, 15)),
                [System.Drawing.ColorTranslator]::FromHtml('#5CF0A6'),
                [System.Drawing.ColorTranslator]::FromHtml('#28C777'))
            $graphics.TranslateTransform([float]$glyphOffset, [float]$glyphOffset)
            $graphics.ScaleTransform([float]$glyphScale, [float]$glyphScale)
            $graphics.FillPath($glyphBrush, $glyphGraphicsPath)
            $glyphBrush.Dispose()
        }
        finally {
            $glyphGraphicsPath.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function ConvertTo-BmpFrame {
    param([System.Drawing.Bitmap]$Bitmap)

    $width = $Bitmap.Width
    $height = $Bitmap.Height

    $pixels = New-Object byte[] ($width * $height * 4)
    for ($y = 0; $y -lt $height; $y++) {
        $targetRow = ($height - 1 - $y) * $width * 4   # BMP guarda as linhas de baixo para cima.
        for ($x = 0; $x -lt $width; $x++) {
            $color = $Bitmap.GetPixel($x, $y)
            $offset = $targetRow + ($x * 4)
            $pixels[$offset + 0] = $color.B   # BGRA
            $pixels[$offset + 1] = $color.G
            $pixels[$offset + 2] = $color.R
            $pixels[$offset + 3] = $color.A
        }
    }

    $maskStride = [int]([Math]::Floor(($width + 31) / 32) * 4)
    $mask = New-Object byte[] ($maskStride * $height)   # mascara zerada: o canal alfa manda.

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        $writer.Write([int]40)                       # biSize
        $writer.Write([int]$width)                   # biWidth
        $writer.Write([int]($height * 2))            # biHeight (imagem + mascara)
        $writer.Write([int16]1)                      # biPlanes
        $writer.Write([int16]32)                     # biBitCount
        $writer.Write([int]0)                        # biCompression = BI_RGB
        $writer.Write([int]($pixels.Length + $mask.Length))
        $writer.Write([int]0)                        # biXPelsPerMeter
        $writer.Write([int]0)                        # biYPelsPerMeter
        $writer.Write([int]0)                        # biClrUsed
        $writer.Write([int]0)                        # biClrImportant
        $writer.Write($pixels)
        $writer.Write($mask)
        $writer.Flush()
        return $stream.ToArray()
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function ConvertTo-PngFrame {
    param([System.Drawing.Bitmap]$Bitmap)

    $stream = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

$frames = New-Object System.Collections.Generic.List[object]
foreach ($size in $Sizes) {
    $bitmap = New-IconBitmap -Size $size
    try {
        if ($size -ge $PngFromSize) {
            [byte[]]$data = ConvertTo-PngFrame -Bitmap $bitmap
        }
        else {
            [byte[]]$data = ConvertTo-BmpFrame -Bitmap $bitmap
        }

        if ($PreviewDirectory) {
            if (-not (Test-Path -LiteralPath $PreviewDirectory)) {
                New-Item -ItemType Directory -Path $PreviewDirectory -Force | Out-Null
            }
            $previewPath = Join-Path $PreviewDirectory ("pedaltrainer-{0}.png" -f $size)
            $bitmap.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }

        if ($null -eq $data -or $data.Length -lt 50) {
            throw ("Frame de {0}px saiu vazio ({1} bytes)." -f $size, $data.Length)
        }

        Write-Verbose ("Frame {0}px: {1:N0} bytes" -f $size, $data.Length)
        $frames.Add([pscustomobject]@{ Size = $size; Data = $data })
    }
    finally {
        $bitmap.Dispose()
    }
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outputFullPath)
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$fileStream = [System.IO.File]::Create($outputFullPath)
$fileWriter = New-Object System.IO.BinaryWriter($fileStream)
try {
    $fileWriter.Write([int16]0)                  # idReserved
    $fileWriter.Write([int16]1)                  # idType = 1 (icone)
    $fileWriter.Write([int16]$frames.Count)      # idCount

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $fileWriter.Write([byte]$dimension)      # bWidth
        $fileWriter.Write([byte]$dimension)      # bHeight
        $fileWriter.Write([byte]0)               # bColorCount
        $fileWriter.Write([byte]0)               # bReserved
        $fileWriter.Write([int16]1)              # wPlanes
        $fileWriter.Write([int16]32)             # wBitCount
        $frameBytes = [byte[]]$frame.Data
        $fileWriter.Write([int]$frameBytes.Length)
        $fileWriter.Write([int]$offset)
        $offset += $frameBytes.Length
    }

    foreach ($frame in $frames) {
        # Overload de 3 argumentos: Write(byte[]) sozinho nao e confiavel via PowerShell.
        $frameBytes = [byte[]]$frame.Data
        $fileWriter.Write($frameBytes, 0, $frameBytes.Length)
    }

    $fileWriter.Flush()
}
finally {
    $fileWriter.Dispose()
    $fileStream.Dispose()
}

$fileInfo = Get-Item -LiteralPath $outputFullPath
Write-Host ("Icone gerado: {0} ({1:N0} bytes, {2} resolucoes: {3})" -f `
    $fileInfo.FullName, $fileInfo.Length, $frames.Count, ($Sizes -join ', '))
