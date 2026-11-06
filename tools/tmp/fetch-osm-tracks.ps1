param(
    [switch]$Probe
)

$ErrorActionPreference = "Stop"

function Invoke-Overpass {
    param([string]$Query)

    $encoded = [uri]::EscapeDataString($Query)
    $uri = "https://overpass-api.de/api/interpreter?data=$encoded"
    $headers = @{ "User-Agent" = "PedalTrainer-TrackMapBuilder/1.0 (sim racing pedal trainer)" }

    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 180
            return $response.elements
        }
        catch {
            Write-Host "tentativa $attempt falhou: $($_.Exception.Message)"
            Start-Sleep -Seconds (5 * $attempt)
        }
    }

    throw "Overpass indisponivel."
}

function Get-WayLengthMeters {
    param($Geometry)

    $total = 0.0
    for ($i = 1; $i -lt $Geometry.Count; $i++) {
        $lat1 = [math]::PI * $Geometry[$i - 1].lat / 180.0
        $lat2 = [math]::PI * $Geometry[$i].lat / 180.0
        $dLat = $lat2 - $lat1
        $dLon = [math]::PI * ($Geometry[$i].lon - $Geometry[$i - 1].lon) / 180.0
        $a = [math]::Sin($dLat / 2) * [math]::Sin($dLat / 2) +
             [math]::Cos($lat1) * [math]::Cos($lat2) * [math]::Sin($dLon / 2) * [math]::Sin($dLon / 2)
        $total += 6371000.0 * 2.0 * [math]::Atan2([math]::Sqrt($a), [math]::Sqrt(1 - $a))
    }

    return $total
}

$circuits = @(
    @{ Id = "interlagos"; Name = "Interlagos"; Lat = -23.7036; Lon = -46.6997; Radius = 1300 }
)

$query = ""
foreach ($circuit in $circuits) {
    $query += "[out:json][timeout:120];way[""highway""=""raceway""](around:$($circuit.Radius),$($circuit.Lat),$($circuit.Lon));out tags geom;"
}

$elements = Invoke-Overpass -Query $query

Write-Host "elementos: $($elements.Count)"

foreach ($element in $elements) {
    if ($element.type -ne "way") { continue }
    $geometry = $element.geometry
    if ($null -eq $geometry -or $geometry.Count -lt 2) { continue }

    $length = Get-WayLengthMeters -Geometry $geometry
    $closed = ($geometry[0].lat -eq $geometry[-1].lat) -and ($geometry[0].lon -eq $geometry[-1].lon)
    $name = $element.tags.name
    Write-Host ("{0} | {1} | fechado={2} | pts={3} | {4:N0} m | {5}" -f $element.id, $element.tags.highway, $closed, $geometry.Count, $length, $name)
}
