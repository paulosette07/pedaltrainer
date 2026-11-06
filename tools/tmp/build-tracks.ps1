param(
    [string]$Only = "",
    [switch]$KeepEmpty
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$cacheDirectory = Join-Path $PSScriptRoot "osm-cache"
$outputDirectory = Join-Path $PSScriptRoot "..\..\src\PedalTrainer.Core\Tracks\Data"
$catalog = (Import-PowerShellDataFile (Join-Path $PSScriptRoot "circuits.psd1")).Circuits

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$Spacing = 6.0

function Fix-Text {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) { return "" }

    $value = $Text.Trim()

    # O texto vem de arquivos UTF-8 lidos como ANSI (Get-Content sem -Encoding e o
    # Import-PowerShellDataFile do PowerShell 5.1). Em vez de comparar com letras
    # acentuadas - que o próprio script perderia ao ser lido -, procuramos caracteres
    # na faixa 0x80-0xFF e desfazemos a dupla codificação byte a byte.
    $high = $false
    foreach ($character in $value.ToCharArray()) {
        $code = [int]$character
        if ($code -ge 0x80 -and $code -le 0xFF) { $high = $true; break }
    }

    if ($high) {
        $latin = [System.Text.Encoding]::GetEncoding(28591)
        $decoded = [System.Text.Encoding]::UTF8.GetString($latin.GetBytes($value))

        if ($decoded.IndexOf([char]0xFFFD) -lt 0) {
            $value = $decoded
        }
    }

    return ($value -replace "\s+", " ")
}

function Get-Key {
    param($Point)
    return ("{0:F5}_{1:F5}" -f $Point.Lat, $Point.Lon)
}

function Get-SegmentLength {
    param($From, $To)

    $lat1 = [math]::PI * $From.Lat / 180.0
    $lat2 = [math]::PI * $To.Lat / 180.0
    $dLat = $lat2 - $lat1
    $dLon = [math]::PI * ($To.Lon - $From.Lon) / 180.0
    $a = [math]::Sin($dLat / 2) * [math]::Sin($dLat / 2) +
         [math]::Cos($lat1) * [math]::Cos($lat2) * [math]::Sin($dLon / 2) * [math]::Sin($dLon / 2)
    return 6371000.0 * 2.0 * [math]::Atan2([math]::Sqrt($a), [math]::Sqrt(1 - $a))
}

function Find-NearbySegment {
    param($Segments, $Used, $Probe, [double]$ToleranceMeters)

    $bestIndex = -1
    $bestEnd = 0
    $best = $ToleranceMeters

    for ($index = 0; $index -lt $Segments.Count; $index++) {
        if ($Used[$index]) { continue }

        $segmentPoints = $Segments[$index].Points
        if ($null -eq $segmentPoints -or $segmentPoints.Count -lt 2) { continue }

        foreach ($end in 0, 1) {
            $point = if ($end -eq 0) { $segmentPoints[0] } else { $segmentPoints[$segmentPoints.Count - 1] }
            $distance = Get-SegmentLength $point $Probe

            if ($distance -lt $best) {
                $best = $distance
                $bestIndex = $index
                $bestEnd = $end
            }
        }
    }

    return @{ Index = $bestIndex; End = $bestEnd; Distance = $best }
}

function Build-Chain {
    param($Segments, $EndpointMap, [int]$Seed, [double]$SnapTolerance)

    $used = New-Object bool[] $Segments.Count
    $used[$Seed] = $true
    $points = New-Object "System.Collections.Generic.List[object]"

    foreach ($point in $Segments[$Seed].Points) { $points.Add($point) }

    $grew = $true

    while ($grew) {
        $grew = $false

        $tail = Get-Key $points[$points.Count - 1]
        if ($EndpointMap.ContainsKey($tail)) {
            foreach ($candidate in $EndpointMap[$tail]) {
                if ($used[$candidate.Index]) { continue }

                $segmentPoints = $Segments[$candidate.Index].Points
                $used[$candidate.Index] = $true

                if ($candidate.End -eq 0) {
                    for ($i = 1; $i -lt $segmentPoints.Count; $i++) { $points.Add($segmentPoints[$i]) }
                }
                else {
                    for ($i = $segmentPoints.Count - 2; $i -ge 0; $i--) { $points.Add($segmentPoints[$i]) }
                }

                $grew = $true
                break
            }
        }

        if (-not $grew -and $SnapTolerance -gt 0) {
            # Vão de poucos metros (quebra de via, portão, viaduto) impede o encaixe
            # exato dos nós: a ponta é emendada no trecho livre mais próximo.
            $nearby = Find-NearbySegment -Segments $Segments -Used $used -Probe $points[$points.Count - 1] -ToleranceMeters $SnapTolerance

            if ($nearby.Index -ge 0) {
                $segmentPoints = $Segments[$nearby.Index].Points
                $used[$nearby.Index] = $true

                if ($nearby.End -eq 0) {
                    for ($i = 0; $i -lt $segmentPoints.Count; $i++) { $points.Add($segmentPoints[$i]) }
                }
                else {
                    for ($i = $segmentPoints.Count - 1; $i -ge 0; $i--) { $points.Add($segmentPoints[$i]) }
                }

                $grew = $true
            }
        }

        if ($grew) { continue }

        $head = Get-Key $points[0]
        if ($EndpointMap.ContainsKey($head)) {
            foreach ($candidate in $EndpointMap[$head]) {
                if ($used[$candidate.Index]) { continue }

                $segmentPoints = $Segments[$candidate.Index].Points
                $used[$candidate.Index] = $true

                if ($candidate.End -eq 1) {
                    for ($i = $segmentPoints.Count - 2; $i -ge 0; $i--) { $points.Insert(0, $segmentPoints[$i]) }
                }
                else {
                    for ($i = 1; $i -lt $segmentPoints.Count; $i++) { $points.Insert(0, $segmentPoints[$i]) }
                }

                $grew = $true
                break
            }
        }

        if (-not $grew -and $SnapTolerance -gt 0) {
            $nearby = Find-NearbySegment -Segments $Segments -Used $used -Probe $points[0] -ToleranceMeters $SnapTolerance

            if ($nearby.Index -ge 0) {
                $segmentPoints = $Segments[$nearby.Index].Points
                $used[$nearby.Index] = $true

                if ($nearby.End -eq 0) {
                    for ($i = $segmentPoints.Count - 1; $i -ge 0; $i--) { $points.Insert(0, $segmentPoints[$i]) }
                }
                else {
                    for ($i = 0; $i -lt $segmentPoints.Count; $i++) { $points.Insert(0, $segmentPoints[$i]) }
                }

                $grew = $true
            }
        }
    }

    $closed = (Get-Key $points[0]) -eq (Get-Key $points[$points.Count - 1])

    return @{ Points = $points; Closed = $closed; Used = $used }
}

function Get-BestLoop {
    param($Segments, [double]$OfficialLength)

    $endpointMap = @{}

    for ($index = 0; $index -lt $Segments.Count; $index++) {
        $segment = $Segments[$index]
        if ($null -eq $segment -or $segment.Points.Count -lt 2) { continue }

        foreach ($end in 0, 1) {
            $point = if ($end -eq 0) { $segment.Points[0] } else { $segment.Points[$segment.Points.Count - 1] }
            $key = Get-Key $point

            if (-not $endpointMap.ContainsKey($key)) {
                $endpointMap[$key] = New-Object "System.Collections.Generic.List[object]"
            }

            $endpointMap[$key].Add(@{ Index = $index; End = $end })
        }
    }

    $order = 0..($Segments.Count - 1) | Sort-Object { -($Segments[$_].Points.Count) }

    # A pista certa é a volta cujo comprimento mais se aproxima do oficial: variações
    # curtas (traçado antigo, variante de boxes) e atalhos ficam de fora.
    $best = $null
    $bestError = [double]::MaxValue
    $tried = 0

    # Duas estratégias competem: só encaixe exato de nós e encaixe com emenda de até
    # 40 m. A que chegar mais perto do comprimento oficial vence.
    foreach ($snap in 0.0, 40.0) {
    $tried = 0

    foreach ($seed in $order) {
        $tried++
        if ($tried -gt 60) { break }

        $chain = Build-Chain -Segments $Segments -EndpointMap $endpointMap -Seed $seed -SnapTolerance $snap
        if ($chain.Points.Count -lt 12) { continue }

        $length = 0.0
        for ($index = 0; $index -lt $chain.Points.Count - 1; $index++) {
            $length += Get-SegmentLength $chain.Points[$index] $chain.Points[$index + 1]
        }

        $bridged = -not $chain.Closed

        if ($bridged) {
            $gap = Get-SegmentLength $chain.Points[$chain.Points.Count - 1] $chain.Points[0]

            # Circuitos longos podem ter um vão maior entre trechos mapeados (boxes,
            # túnel, viaduto); a ponte só é aceita se for pequena perto da volta.
            $maxBridge = [math]::Max(400.0, $OfficialLength * 0.18)
            if ($gap -gt $maxBridge) { continue }

            $length += $gap
        }

        $lengthError = [math]::Abs($length - $OfficialLength) / $OfficialLength

        if ($lengthError -lt $bestError) {
            $bestError = $lengthError
            $best = @{
                Points = $chain.Points
                Bridged = $bridged
                Length = $length
                Error = $lengthError
                Used = $chain.Used
            }
        }
    }
    }

    if ($null -eq $best -or $bestError -gt 0.25) { return $null }

    return $best
}

function Convert-ToLocal {
    param($Points, [double]$Lat0, [double]$Lon0)

    $result = New-Object "System.Collections.Generic.List[object]"
    $metersPerDegreeLat = 110540.0
    $metersPerDegreeLon = 111320.0 * [math]::Cos([math]::PI * $Lat0 / 180.0)

    foreach ($point in $Points) {
        $x = ($point.Lon - $Lon0) * $metersPerDegreeLon
        $y = ($point.Lat - $Lat0) * $metersPerDegreeLat
        $result.Add(@{ X = $x; Y = $y })
    }

    return $result
}

function Get-ClosedLength {
    param($Points)

    $total = 0.0
    for ($index = 0; $index -lt $Points.Count; $index++) {
        $from = $Points[$index]
        $to = $Points[($index + 1) % $Points.Count]
        $total += [math]::Sqrt([math]::Pow($to.X - $from.X, 2) + [math]::Pow($to.Y - $from.Y, 2))
    }

    return $total
}

function Resample-Closed {
    param($Points, [double]$SpacingMeters)

    $total = Get-ClosedLength $Points
    $count = [math]::Max(16, [int][math]::Round($total / $SpacingMeters))
    $step = $total / $count

    $result = New-Object "System.Collections.Generic.List[object]"
    $segment = 0
    $segmentStart = 0.0
    $segmentLength = [math]::Sqrt([math]::Pow($Points[1].X - $Points[0].X, 2) + [math]::Pow($Points[1].Y - $Points[0].Y, 2))

    for ($sample = 0; $sample -lt $count; $sample++) {
        $target = $sample * $step

        while ($segment -lt $Points.Count - 1 -and ($segmentStart + $segmentLength) -lt $target) {
            $segmentStart += $segmentLength
            $segment++
            $next = $Points[($segment + 1) % $Points.Count]
            $segmentLength = [math]::Sqrt([math]::Pow($next.X - $Points[$segment].X, 2) + [math]::Pow($next.Y - $Points[$segment].Y, 2))
        }

        $ratio = 0.0
        if ($segmentLength -gt 1e-9) {
            $ratio = [math]::Max(0.0, [math]::Min(1.0, ($target - $segmentStart) / $segmentLength))
        }

        $from = $Points[$segment]
        $to = $Points[($segment + 1) % $Points.Count]

        $result.Add(@{
            X = $from.X + (($to.X - $from.X) * $ratio)
            Y = $from.Y + (($to.Y - $from.Y) * $ratio)
        })
    }

    return @{ Points = $result; Step = $step }
}

function Find-NearestIndex {
    param($Points, $Probe)

    $bestIndex = 0
    $bestDistance = [double]::MaxValue

    for ($index = 0; $index -lt $Points.Count; $index++) {
        $dx = $Points[$index].X - $Probe.X
        $dy = $Points[$index].Y - $Probe.Y
        $distance = ($dx * $dx) + ($dy * $dy)

        if ($distance -lt $bestDistance) {
            $bestDistance = $distance
            $bestIndex = $index
        }
    }

    return $bestIndex
}
$built = 0
$skipped = 0

foreach ($circuit in $catalog) {
    if ($Only -ne "" -and $circuit.Id -ne $Only) { continue }

    $source = Join-Path $cacheDirectory ($circuit.Id + ".json")

    if (-not (Test-Path $source)) {
        Write-Host ("{0,-16} sem cache" -f $circuit.Id)
        $skipped++
        continue
    }

    # A resposta do Overpass é UTF-8: ler como ANSI (padrão do PowerShell 5.1) produz
    # mojibake em nomes acentuados ("Courbe Paul Frère", "Café").
    $raw = [System.IO.File]::ReadAllText($source, [System.Text.Encoding]::UTF8)

    if ($raw.Length -lt 200) {
        Write-Host ("{0,-16} cache vazio" -f $circuit.Id)
        $skipped++
        continue
    }

    $data = $raw | ConvertFrom-Json
    $pitSegments = New-Object "System.Collections.Generic.List[object]"
    $loopSegments = New-Object "System.Collections.Generic.List[object]"
    $namedSegments = New-Object "System.Collections.Generic.List[object]"

    foreach ($element in $data.elements) {
        if ($element.type -ne "way") { continue }
        if ($null -eq $element.geometry -or $element.geometry.Count -lt 2) { continue }

        $name = ""
        if ($null -ne $element.tags -and $null -ne $element.tags.name) { $name = Fix-Text $element.tags.name }

        $points = New-Object "System.Collections.Generic.List[object]"
        foreach ($node in $element.geometry) { $points.Add(@{ Lat = [double]$node.lat; Lon = [double]$node.lon }) }

        $isPit = $name -match "(?i)pit|box|stand|garage|voie|gas"
        $isOther = $name -match "(?i)quartermile|drag|kart|drift|stunt"

        if ($isPit) {
            # Fica fora da costura da volta: a rua de boxes corre paralela à reta
            # principal e, quando entrava no conjunto, a cadeia desviava por ela e
            # deixava de fechar (Fuji, Portimão). Serve só para achar a linha de chegada.
            $pitSegments.Add(@{ Name = $name; Points = $points })
            continue
        }

        if ($isOther) { continue }

        $loopSegments.Add(@{ Name = $name; Points = $points })

        # "Circuito Raul Boesel", "Pista BMX", "Autódromo …": nome de praça esportiva, não
        # de curva — batizaria uma zona com o nome da pista vizinha.
        $isFacility = $name -match "(?i)circuito|aut.dromo|pista|kart|speedway|raceway|raceland|track|pit|box|stand|garage"

        if ($name -ne "" -and $name -notmatch "^\d+$" -and -not $isFacility) {
            # Guarda o índice no conjunto de trechos: só um trecho que realmente entrou
            # na volta pode batizar uma curva. Sem isso, ruas do entorno viram nome de
            # curva, todas empilhadas na mesma posição.
            $namedSegments.Add(@{ Name = $name; Points = $points; Index = $loopSegments.Count - 1 })
        }
    }

    if ($loopSegments.Count -lt 4) {
        Write-Host ("{0,-16} poucos trechos ({1})" -f $circuit.Id, $loopSegments.Count)
        $skipped++
        continue
    }

    $chain = Get-BestLoop -Segments $loopSegments -OfficialLength ([double]$circuit.Length)

    if ($null -eq $chain) {
        Write-Host ("{0,-16} sem volta fechada ({1} trechos)" -f $circuit.Id, $loopSegments.Count)
        $skipped++
        continue
    }

    if ($chain.Bridged) {
        $chain.Points.Add($chain.Points[0])
    }

    $localPoints = Convert-ToLocal -Points $chain.Points -Lat0 $circuit.Lat -Lon0 $circuit.Lon
    $measured = Get-ClosedLength $localPoints

    if ($measured -lt 400) {
        Write-Host ("{0,-16} traçado curto demais ({1:N0} m)" -f $circuit.Id, $measured)
        $skipped++
        continue
    }

    $scale = [double]$circuit.Length / $measured
    $scaled = New-Object "System.Collections.Generic.List[object]"

    foreach ($point in $localPoints) {
        $scaled.Add(@{ X = $point.X * $scale; Y = $point.Y * $scale })
    }

    $resampled = Resample-Closed -Points $scaled -SpacingMeters $Spacing
    $ring = $resampled.Points
    $count = $ring.Count

    # Linha de chegada: a saída dos boxes fica no início do maior vão entre os pontos
    # da rua de boxes — é exatamente onde o carro cruza a linha em cada volta.
    $startIndex = 0

    if ($pitSegments.Count -gt 0) {
        $pitIndices = New-Object "System.Collections.Generic.List[int]"

        foreach ($pit in $pitSegments) {
            foreach ($endpoint in @($pit.Points[0], $pit.Points[$pit.Points.Count - 1])) {
                $local = Convert-ToLocal -Points @($endpoint) -Lat0 $circuit.Lat -Lon0 $circuit.Lon
                $probe = @{ X = $local[0].X * $scale; Y = $local[0].Y * $scale }
                $pitIndices.Add((Find-NearestIndex -Points $ring -Probe $probe))
            }
        }

        $unique = $pitIndices | Sort-Object -Unique

        if ($unique.Count -ge 2) {
            $bestGap = -1

            for ($index = 0; $index -lt $unique.Count; $index++) {
                $current = $unique[$index]
                $next = if ($index -lt $unique.Count - 1) { $unique[$index + 1] } else { $unique[0] + $count }
                $gap = $next - $current

                if ($gap -gt $bestGap) {
                    $bestGap = $gap
                    $startIndex = $current
                }
            }
        }
    }

    $rotated = New-Object "System.Collections.Generic.List[object]"

    for ($offset = 0; $offset -lt $count; $offset++) {
        $rotated.Add($ring[($startIndex + $offset) % $count])
    }

    $flat = New-Object "System.Collections.Generic.List[double]"

    foreach ($point in $rotated) {
        $flat.Add([math]::Round($point.X, 1))
        $flat.Add([math]::Round($point.Y, 1))
    }

    $corners = New-Object "System.Collections.Generic.List[object]"
    $seenNames = @{}

    foreach ($segment in $namedSegments) {
        if ($null -ne $chain.Used -and -not $chain.Used[$segment.Index]) { continue }

        $mid = [int][math]::Floor($segment.Points.Count / 2)
        $midpoint = $segment.Points[$mid]
        $local = Convert-ToLocal -Points @($midpoint) -Lat0 $circuit.Lat -Lon0 $circuit.Lon
        $probe = @{ X = $local[0].X * $scale; Y = $local[0].Y * $scale }
        $index = Find-NearestIndex -Points $rotated -Probe $probe
        $fraction = [math]::Round($index / $count, 4)

        $name = $segment.Name

        if ($seenNames.ContainsKey($name)) { continue }
        $seenNames[$name] = $true

        $corners.Add([ordered]@{ name = $name; fraction = $fraction })
    }

    # O catálogo é lido como ANSI pelo PowerShell 5.1; Fix-Text desfaz a dupla codificação
    # para o nome chegar certo na interface ("Autódromo José Carlos Pace").
    $document = [ordered]@{
        id = $circuit.Id
        name = Fix-Text $circuit.Name
        city = Fix-Text $circuit.City
        country = Fix-Text $circuit.Country
        countryCode = $circuit.Code
        lengthMeters = [double]$circuit.Length
        measuredMeters = [math]::Round($measured, 1)
        points = $flat
        corners = $corners
    }

    $json = $document | ConvertTo-Json -Depth 6 -Compress
    $target = Join-Path $outputDirectory ($circuit.Id + ".json")
    [System.IO.File]::WriteAllText($target, $json, (New-Object System.Text.UTF8Encoding($false)))

    $deviation = [math]::Round(($measured / [double]$circuit.Length) * 100, 1)
    $bridgeLabel = if ($chain.Bridged) { " ponte" } else { "" }
    Write-Host ("{0,-16} ok   {1,6:N0} m ({2,5:N1}% do oficial) {3,4} pts {4,3} curvas{5}" -f `
        $circuit.Id, $measured, $deviation, $count, $corners.Count, $bridgeLabel)
    $built++
}

Write-Host ""
Write-Host ("traçados gerados: {0}   ignorados: {1}" -f $built, $skipped)
