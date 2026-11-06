param(
    [switch]$Force,
    [double]$RadiusBoost = 1.6
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$cacheDirectory = Join-Path $PSScriptRoot "osm-cache"
New-Item -ItemType Directory -Force -Path $cacheDirectory | Out-Null

$circuits = (Import-PowerShellDataFile (Join-Path $PSScriptRoot "circuits.psd1")).Circuits

# Espelhos do Overpass: um deles costuma estar livre quando o principal está sobrecarregado.
$endpoints = @(
    "https://overpass-api.de/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter",
    "https://overpass.private.coffee/api/interpreter"
)

function Invoke-Overpass {
    param([string]$Query)

    $encoded = [uri]::EscapeDataString($Query)
    $headers = @{ "User-Agent" = "PedalTrainer-TrackMapBuilder/1.0 (pedal trainer for sim racing)" }

    # Uma tentativa por espelho e timeout curto: um espelho sobrecarregado não pode
    # segurar a lista inteira (são dezenas de circuitos por execução).
    foreach ($endpoint in $endpoints) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri "$endpoint`?data=$encoded" -Headers $headers -TimeoutSec 70

            if ($response.Content.Length -gt 200) {
                return $response.Content
            }
        }
        catch {
            Write-Host ("  {0}: {1}" -f $endpoint.Split('/')[2], $_.Exception.Message)
            Start-Sleep -Seconds 2
        }
    }

    return $null
}

function Get-WayCount {
    param([string]$Content)

    if ([string]::IsNullOrEmpty($Content)) { return 0 }
    return ([regex]::Matches($Content, '"type"\s*:\s*"way"')).Count
}

function Get-DistinctiveWord {
    param([string]$Name)

    # O nome do catálogo quase sempre carrega o topônimo que o OSM usa
    # ("Autódromo Internacional de Curitiba" -> "Curitiba").
    $best = ""

    foreach ($word in ($Name -split "[^0-9A-Za-z]+")) {
        if ($word.Length -ge 5 -and $word.Length -gt $best.Length) { $best = $word }
    }

    return $best
}

foreach ($circuit in $circuits) {
    $target = Join-Path $cacheDirectory ($circuit.Id + ".json")

    if ((Test-Path $target) -and -not $Force) {
        continue
    }

    # Raio grande: pega o traçado inteiro mesmo com o centro aproximado, e trechos do
    # entorno (kart, variantes) ficam de fora na hora de fechar a volta.
    $radius = [int]([double]$circuit.Radius * $RadiusBoost)
    $wide = [int]([double]$circuit.Radius * $RadiusBoost * 2.5)
    $word = Get-DistinctiveWord -Name $circuit.Name

    # Três tentativas em ordem de confiança: o traçado mapeado como pista de corrida,
    # o mesmo com raio bem maior (centro aproximado) e, por fim, qualquer via com o
    # nome do autódromo — cobre pista desenhada como rua ou como área.
    $queries = @(
        "[out:json][timeout:120];way[""highway""=""raceway""](around:$radius,$($circuit.Lat),$($circuit.Lon));out tags geom;",
        "[out:json][timeout:120];way[""highway""=""raceway""](around:$wide,$($circuit.Lat),$($circuit.Lon));out tags geom;"
    )

    if ($word.Length -ge 5) {
        $queries += "[out:json][timeout:120];(way[""name""~""$word"",i](around:$wide,$($circuit.Lat),$($circuit.Lon));way[""highway""~""raceway|track""](around:$wide,$($circuit.Lat),$($circuit.Lon)););out tags geom;"
    }

    Write-Host ("{0}: buscando (raio {1} m)..." -f $circuit.Id, $radius)

    $content = $null
    $best = 0

    foreach ($query in $queries) {
        $attempt = Invoke-Overpass -Query $query
        $count = Get-WayCount $attempt

        if ($count -gt $best) {
            $best = $count
            $content = $attempt
        }

        if ($best -ge 8) { break }
    }

    if ($null -eq $content) {
        Write-Host ("{0}: FALHOU" -f $circuit.Id)
        continue
    }

    [System.IO.File]::WriteAllText($target, $content, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("{0}: {1} ways" -f $circuit.Id, $best)
    Start-Sleep -Milliseconds 900
}

Write-Host "fim da busca"
