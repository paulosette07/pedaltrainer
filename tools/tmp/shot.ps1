param(
    [string]$Out = "treino.png",
    [int]$WaitSeconds = 6
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class NativeWindow
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr handle, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr handle, int command);
}
"@

$exe = Join-Path $PSScriptRoot "..\..\src\PedalTrainer.App\bin\Debug\net8.0-windows\PedalTrainer.exe"

if (-not (Test-Path $exe)) {
    throw "Executavel nao encontrado: $exe"
}

Get-Process PedalTrainer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

$process = Start-Process -FilePath $exe -PassThru
$deadline = (Get-Date).AddSeconds($WaitSeconds)

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 400
    $process.Refresh()

    if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
}

$handle = $process.MainWindowHandle

if ($handle -eq [IntPtr]::Zero) {
    throw "A janela do PedalTrainer nao apareceu."
}

[NativeWindow]::ShowWindow($handle, 9) | Out-Null
[NativeWindow]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Milliseconds 1500

$rect = New-Object NativeWindow+RECT
[NativeWindow]::GetWindowRect($handle, [ref]$rect) | Out-Null

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top

$shots = Join-Path $PSScriptRoot "shots"
New-Item -ItemType Directory -Force -Path $shots | Out-Null

$path = Join-Path $shots $Out
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)

$bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

"pid=$($process.Id) size=${width}x${height} -> $path"
