param(
    [string[]]$Only = @(),
    [double]$RadiusBoost = 1.6
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Alternativa ao Overpass: a API do OpenStreetMap (/api/0.6/map) devolve os dados
# brutos de uma caixa, com nós e vias. É outra infraestrutura — quando o Overpass
# está fora do ar, esta continua respondendo. O resultado é gravado no mesmo
# formato de cache que o build-tracks.ps1 lê.
$cacheDirectory = Join-Path $PSScriptRoot "osm-cache"
New-Item -ItemType Directory -Force -Path $cacheDirectory | Out-Null

$circuits = (Import-PowerShellDataFile (Join-Path $PSScriptRoot "circuits.psd1")).Circuits
$endpoint = "https://api.openstreetmap.org/api/0.6/map"
$headers = @{ "User-Agent" = "PedalTrainer-TrackMapBuilder/1.0 (pedal trainer for sim racing)" }

function Get-BoundingBox {
    param([double]$Lat, [double]$Lon, [double]$RadiusMeters)

    $deltaLat = $RadiusMeters / 111320.0
    $deltaLon = $RadiusMeters / (111320.0 * [math]::Cos([math]::PI * $Lat / 180.0))

    return @{
        MinLat = $Lat - $deltaLat
        MaxLat = $Lat + $deltaLat
        MinLon = $Lon - $deltaLon
        MaxLon = $Lon + $deltaLon
    }
}

function Invoke-OsmMap {
    param([hashtable]$Box)

    # A cultura do Windows é pt-BR: formatar com ela produziria "-0,3344,41,1241" e a
    # API recusa a caixa. O separador da URL tem de ser sempre o ponto.
    $bbox = [string]::Format(
        [System.Globalization.CultureInfo]::InvariantCulture,
        "{0:F6},{1:F6},{2:F6},{3:F6}",
        $Box.MinLon, $Box.MinLat, $Box.MaxLon, $Box.MaxLat)

    $response = Invoke-WebRequest -UseBasicParsing -Uri "$endpoint`?bbox=$bbox" -Headers $headers -TimeoutSec 90
    return [xml]$response.Content
}

function Convert-OsmToCache {
    param([xml]$Document)

    $nodes = @{}
    foreach ($node in $Document.osm.node) {
        $nodes[$node.id] = @{ Lat = [double]$node.lat; Lon = [double]$node.lon }
    }

    # Em vários circuitos (Bathurst, Mid-Ohio, Buriram) o traçado é uma relação e as
    # vias membros não têm etiqueta nenhuma — o highway=raceway/sport=motor fica na
    # relação. Sem isto o recorte volta com zero trechos.
    $relationWays = @{}

    foreach ($relation in $Document.osm.relation) {
        $relationTags = @{}
        foreach ($tag in $relation.tag) {
            if ($null -ne $tag.k) { $relationTags[$tag.k] = $tag.v }
        }

        $isCircuit = $relationTags["sport"] -eq "motor" -or
                     $relationTags["highway"] -eq "raceway" -or
                     $relationTags["type"] -eq "circuit"

        if (-not $isCircuit) { continue }

        foreach ($member in $relation.member) {
            if ($member.type -eq "way") { $relationWays[$member.ref] = $true }
        }
    }

    $elements = New-Object "System.Collections.Generic.List[object]"

    foreach ($way in $Document.osm.way) {
        $tags = @{}
        foreach ($tag in $way.tag) {
            if ($null -ne $tag.k) { $tags[$tag.k] = $tag.v }
        }

        $isRaceway = $tags["highway"] -eq "raceway" -or $relationWays.ContainsKey($way.id)

        if (-not $isRaceway) { continue }

        $geometry = New-Object "System.Collections.Generic.List[object]"
        $complete = $true

        foreach ($nd in $way.nd) {
            $id = $nd.ref
            if (-not $nodes.ContainsKey($id)) { $complete = $false; break }
            $geometry.Add($nodes[$id])
        }

        if (-not $complete -or $geometry.Count -lt 2) { continue }

        $elements.Add([ordered]@{
            type = "way"
            tags = $tags
            geometry = $geometry
        })
    }

    return $elements
}

$built = 0

foreach ($circuit in $circuits) {
    if ($Only.Count -gt 0 -and $Only -notcontains $circuit.Id) { continue }

    $target = Join-Path $cacheDirectory ($circuit.Id + ".json")

    if (Test-Path $target) { continue }   # já existe: não gasta requisição

    $radius = [double]$circuit.Radius * $RadiusBoost
    $document = $null
    $lastError = ""

    # A API recusa caixas com nós demais (circuito dentro de cidade, como Brno).
    # Nesse caso a requisição é repetida com uma caixa menor, ainda centrada na pista.
    foreach ($scale in 1.0, 0.6, 0.4) {
        $box = Get-BoundingBox -Lat ([double]$circuit.Lat) -Lon ([double]$circuit.Lon) -RadiusMeters ($radius * $scale)

        Write-Host ("{0}: bbox {1:N4},{2:N4} .. {3:N4},{4:N4}" -f `
            $circuit.Id, $box.MinLon, $box.MinLat, $box.MaxLon, $box.MaxLat)

        try {
            $document = Invoke-OsmMap -Box $box
            break
        }
        catch {
            $lastError = $_.Exception.Message
            Write-Host ("{0}: caixa {1:N1}x recusada ({2})" -f $circuit.Id, $scale, $lastError)
            Start-Sleep -Milliseconds 1200
        }
    }

    if ($null -eq $document) {
        Write-Host ("{0}: FALHOU ({1})" -f $circuit.Id, $lastError)
        continue
    }

    $elements = Convert-OsmToCache -Document $document

    if ($elements.Count -lt 4) {
        Write-Host ("{0}: poucos trechos ({1})" -f $circuit.Id, $elements.Count)
        Start-Sleep -Milliseconds 1500
        continue
    }

    $payload = [ordered]@{ elements = $elements }
    $json = $payload | ConvertTo-Json -Depth 8 -Compress
    [System.IO.File]::WriteAllText($target, $json, (New-Object System.Text.UTF8Encoding($false)))

    Write-Host ("{0}: {1} ways" -f $circuit.Id, $elements.Count)
    $built++

    # A API do OSM pede no máximo uma requisição por segundo.
    Start-Sleep -Milliseconds 1500
}

Write-Host ("cache gravado: {0}" -f $built)
