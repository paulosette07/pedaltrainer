$exe = Join-Path $PSScriptRoot "..\..\src\PedalTrainer.App\bin\Debug\net8.0-windows\PedalTrainer.exe"

Get-Process PedalTrainer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$process = Start-Process -FilePath $exe -PassThru

for ($attempt = 0; $attempt -lt 12; $attempt++) {
    Start-Sleep -Milliseconds 700
    $process.Refresh()

    if ($process.HasExited) {
        "saiu com codigo $($process.ExitCode)"
        break
    }

    "vivo: handle=$($process.MainWindowHandle) titulo='$($process.MainWindowTitle)'"
}

if (-not $process.HasExited) {
    $process | Select-Object Id, MainWindowHandle, MainWindowTitle | Format-List
}
