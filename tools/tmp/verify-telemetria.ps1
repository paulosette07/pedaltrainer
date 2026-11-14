param(
    [int]$WaitSeconds = 8,
    [string]$Out = "telemetria"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class NativeWin
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr handle, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();
}
"@

# Sem isso o GetWindowRect devolve coordenadas virtuais (escala do Windows) e a captura
# pega uma area maior do que a janela, encolhendo tudo na imagem.
[NativeWin]::SetProcessDPIAware() | Out-Null

$exe = Join-Path $PSScriptRoot "..\..\src\PedalTrainer.App\bin\Debug\net8.0-windows\PedalTrainer.exe"
$exe = [System.IO.Path]::GetFullPath($exe)

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

if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
    throw "A janela do PedalTrainer nao apareceu."
}

function Get-Window([IntPtr]$handle) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NativeWindowHandleProperty, $handle.ToInt32())

    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
}

function Find-ByName($scope, [string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)

    return $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-Element($element) {
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Capture-Window([IntPtr]$handle, [string]$file) {
    $rect = New-Object NativeWin+RECT
    [NativeWin]::GetWindowRect($handle, [ref]$rect) | Out-Null

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)

    $shots = Join-Path $PSScriptRoot "shots"
    New-Item -ItemType Directory -Force -Path $shots | Out-Null

    $path = Join-Path $shots $file
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()

    return "$path (${width}x${height})"
}

[NativeWin]::ShowWindow($process.MainWindowHandle, 9) | Out-Null
[NativeWin]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 1800

$window = Get-Window $process.MainWindowHandle
if ($null -eq $window) { throw "Automacao nao encontrou a janela principal." }

$navItem = Find-ByName $window "Telemetria"
if ($null -eq $navItem) { throw "Item de navegacao 'Telemetria' nao encontrado." }

$select = $navItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
$select.Select()
Start-Sleep -Milliseconds 1200

"tela: " + (Capture-Window $process.MainWindowHandle "$Out-tela.png")

$recordButton = Find-ByName $window "Gravar telemetria"
if ($null -eq $recordButton) { throw "Botao 'Gravar telemetria' nao encontrado." }

Invoke-Element $recordButton
Start-Sleep -Milliseconds 2500

$folder = Join-Path (Split-Path $exe) "telemetria"
$files = @(Get-ChildItem -Path $folder -Filter *.csv -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending)

if ($files.Count -eq 0) {
    throw "Nenhum arquivo foi criado em $folder"
}

"arquivo: $($files[0].FullName) ($($files[0].Length) bytes)"
"linhas: " + (Get-Content $files[0].FullName | Measure-Object -Line).Lines
"cabecalho:"
Get-Content $files[0].FullName -TotalCount 10 | ForEach-Object { "  $_" }

$stopButton = Find-ByName $window "Parar gravação"
if ($null -ne $stopButton) { Invoke-Element $stopButton }
Start-Sleep -Milliseconds 800

$detachButton = Find-ByName $window "Destacar janela"
if ($null -eq $detachButton) { throw "Botao 'Destacar janela' nao encontrado." }

Invoke-Element $detachButton
Start-Sleep -Milliseconds 1500

$overlay = $null
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    Start-Sleep -Milliseconds 300
    $process.Refresh()

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)

    $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($item in $windows) {
        if ($item.Current.Name -like "*telemetria*") { $overlay = $item; break }
    }

    if ($null -ne $overlay) { break }
}

if ($null -eq $overlay) {
    throw "A janela destacada nao abriu."
}

$handle = [IntPtr]$overlay.Current.NativeWindowHandle
[NativeWin]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Milliseconds 600

"janela destacada: topo=$($overlay.Current.IsOffscreen) nome='$($overlay.Current.Name)'"
"destaque: " + (Capture-Window $handle "$Out-destaque.png")

$closeButton = Find-ByName $overlay "X"
if ($null -ne $closeButton) { Invoke-Element $closeButton }
Start-Sleep -Milliseconds 600

"ok"
