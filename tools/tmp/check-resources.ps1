$dataDirectory = Join-Path $PSScriptRoot "..\..\src\PedalTrainer.Core\Tracks\Data"
$dll = Join-Path $PSScriptRoot "..\..\src\PedalTrainer.Core\bin\Debug\net8.0\PedalTrainer.Core.dll"

"arquivos em Data: $((Get-ChildItem $dataDirectory -Filter *.json -ErrorAction SilentlyContinue).Count)"

if (-not (Test-Path $dll)) {
    "dll nao encontrada: $dll"
    return
}

$assembly = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $dll))
$names = $assembly.GetManifestResourceNames()

"recursos: $($names.Count)"
$names | Select-Object -First 12
