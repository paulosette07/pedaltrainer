param([string]$Id = "mid-ohio")

$ProgressPreference = "SilentlyContinue"
$circuits = (Import-PowerShellDataFile (Join-Path $PSScriptRoot "circuits.psd1")).Circuits
$circuit = $circuits | Where-Object { $_.Id -eq $Id }
if ($null -eq $circuit) { Write-Host "id desconhecido"; exit 1 }

$radius = [double]$circuit.Radius * 1.6
$deltaLat = $radius / 111320.0
$deltaLon = $radius / (111320.0 * [math]::Cos([math]::PI * [double]$circuit.Lat / 180.0))

$bbox = [string]::Format(
    [System.Globalization.CultureInfo]::InvariantCulture,
    "{0:F6},{1:F6},{2:F6},{3:F6}",
    ([double]$circuit.Lon - $deltaLon), ([double]$circuit.Lat - $deltaLat),
    ([double]$circuit.Lon + $deltaLon), ([double]$circuit.Lat + $deltaLat))

$uri = "https://api.openstreetmap.org/api/0.6/map?bbox=$bbox"
$response = Invoke-WebRequest -UseBasicParsing -Uri $uri -TimeoutSec 90 -Headers @{ "User-Agent" = "PedalTrainer-TrackMapBuilder/1.0" }
$document = [xml]$response.Content

"nos: {0}  vias: {1}  relacoes: {2}" -f $document.osm.node.Count, $document.osm.way.Count, $document.osm.relation.Count

$stats = @{}

foreach ($way in $document.osm.way) {
    $values = @()
    foreach ($tag in $way.tag) {
        if ($tag.k -in @("highway", "leisure", "sport", "area", "name", "type", "landuse")) {
            $values += ("{0}={1}" -f $tag.k, $tag.v)
        }
    }

    if ($values.Count -eq 0) { continue }

    $key = ($values | Sort-Object) -join ";"
    if (-not $stats.ContainsKey($key)) { $stats[$key] = 0 }
    $stats[$key]++
}

"`ncombinacoes mais comuns:"
$stats.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 15 | ForEach-Object {
    "{0,4}  {1}" -f $_.Value, $_.Key
}
