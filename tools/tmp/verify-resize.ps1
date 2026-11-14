param(
    [int]$WaitSeconds = 6,
    [int]$Width = 640,
    [int]$Height = 760,
    [string]$Out = "overlay-resize"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class ResizeWin
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    public delegate bool EnumProc(IntPtr handle, IntPtr param);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr handle, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    public static bool ShowWindowRestore(IntPtr handle)
    {
        return ShowWindow(handle, 9);
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc callback, IntPtr param);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int max);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);

    public static IntPtr FindByTitle(uint processId, string needle, IntPtr exclude)
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((handle, param) =>
        {
            if (handle == exclude) { return true; }

            uint owner;
            GetWindowThreadProcessId(handle, out owner);

            if (owner != processId) { return true; }

            int length = GetWindowTextLength(handle);
            if (length == 0) { return true; }

            var text = new StringBuilder(length + 1);
            GetWindowText(handle, text, text.Capacity);

            if (text.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                found = handle;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}
"@

[ResizeWin]::SetProcessDPIAware() | Out-Null

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $root "src\PedalTrainer.App\bin\Debug\net8.0-windows\PedalTrainer.exe"
$shots = Join-Path $root "tools\tmp\shots"

if (-not (Test-Path $exe)) { throw "Executavel nao encontrado: $exe" }
if (-not (Test-Path $shots)) { New-Item -ItemType Directory -Path $shots | Out-Null }

Get-Process PedalTrainer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$process = Start-Process -FilePath $exe -PassThru
$deadline = (Get-Date).AddSeconds($WaitSeconds)

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 300
    $process.Refresh()
    if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
}

if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw "A janela principal nao apareceu." }

$mainHandle = $process.MainWindowHandle
$processId = [uint32]$process.Id

$automation = [System.Windows.Automation.AutomationElement]
$root = $automation::RootElement

function Get-WindowByHandle([IntPtr]$handle) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        $automation::NativeWindowHandleProperty, $handle.ToInt32())

    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
}

function Find-ByName($window, [string]$name) {
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition($automation::NameProperty, $name)

    return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
}

[ResizeWin]::ShowWindowRestore($mainHandle) | Out-Null
[ResizeWin]::SetForegroundWindow($mainHandle) | Out-Null
Start-Sleep -Milliseconds 1800

$shell = Get-WindowByHandle $mainHandle
if ($null -eq $shell) { throw "Automacao nao encontrou a janela principal." }

$telemetryTab = Find-ByName $shell "Telemetria"
if ($null -eq $telemetryTab) { throw "Aba Telemetria nao encontrada." }

$selection = $telemetryTab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
$selection.Select()
Start-Sleep -Milliseconds 1200

$shell = Get-WindowByHandle $mainHandle
$highlight = Find-ByName $shell "Destacar janela"
if ($null -eq $highlight) { throw "Botao 'Destacar janela' nao encontrado." }

$invoke = $highlight.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
$invoke.Invoke()
Start-Sleep -Milliseconds 1400

$overlay = [ResizeWin]::FindByTitle($processId, "telemetria", $mainHandle)
if ($overlay -eq [IntPtr]::Zero) { throw "Janela destacada nao encontrada." }

$before = New-Object ResizeWin+RECT
[ResizeWin]::GetWindowRect($overlay, [ref]$before) | Out-Null
$beforeWidth = $before.Right - $before.Left
$beforeHeight = $before.Bottom - $before.Top

# Reposiciona no canto superior esquerdo: crescendo para a direita, a janela sairia da tela
# e a captura pegaria area vazia no lugar do painel.
[ResizeWin]::MoveWindow($overlay, 40, 40, $Width, $Height, $true) | Out-Null
Start-Sleep -Milliseconds 900

$after = New-Object ResizeWin+RECT
[ResizeWin]::GetWindowRect($overlay, [ref]$after) | Out-Null
$afterWidth = $after.Right - $after.Left
$afterHeight = $after.Bottom - $after.Top

[ResizeWin]::SetForegroundWindow($overlay) | Out-Null
Start-Sleep -Milliseconds 400

$bitmap = New-Object System.Drawing.Bitmap $afterWidth, $afterHeight
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($after.Left, $after.Top, 0, 0, $bitmap.Size)
$path = Join-Path $shots "$Out.png"
$bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

"antes: ${beforeWidth}x${beforeHeight}"
"pedido: ${Width}x${Height}"
"depois: ${afterWidth}x${afterHeight}"
"captura: $path"

$process.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 1200

$settings = Join-Path $env:APPDATA "PedalTrainer\settings.json"

if (Test-Path $settings) {
    $saved = Get-Content $settings -Raw | ConvertFrom-Json
    "salvo: {0}x{1}" -f $saved.TelemetryOverlayWidth, $saved.TelemetryOverlayHeight
}

Get-Process PedalTrainer -ErrorAction SilentlyContinue | Stop-Process -Force

"ok"
