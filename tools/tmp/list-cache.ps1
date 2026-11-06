$cache = Join-Path $PSScriptRoot "osm-cache"
$files = Get-ChildItem $cache -Filter *.json -ErrorAction SilentlyContinue

"arquivos: $($files.Count)"

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $ways = ([regex]::Matches($content, '"type"\s*:\s*"way"')).Count
    $kilobytes = [math]::Round($file.Length / 1kb, 1)
    "{0,-18} {1,4} ways {2,7} KB" -f $file.BaseName, $ways, $kilobytes
}
