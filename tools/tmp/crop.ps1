param(
    [string]$In = "treino.png",
    [string]$Out = "treino-crop.png",
    [int]$X = 0,
    [int]$Y = 0,
    [int]$Width = 400,
    [int]$Height = 400,
    [double]$Scale = 2.0
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$shots = Join-Path $PSScriptRoot "shots"
$sourcePath = Join-Path $shots $In
$targetPath = Join-Path $shots $Out

$source = [System.Drawing.Image]::FromFile($sourcePath)

$width = [int]($Width * $Scale)
$height = [int]($Height * $Scale)

$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

$destination = New-Object System.Drawing.Rectangle 0, 0, $width, $height
$origin = New-Object System.Drawing.Rectangle $X, $Y, $Width, $Height

$graphics.DrawImage($source, $destination, $origin, [System.Drawing.GraphicsUnit]::Pixel)

$bitmap.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)

$graphics.Dispose()
$bitmap.Dispose()
$source.Dispose()

"$targetPath (${width}x${height})"
