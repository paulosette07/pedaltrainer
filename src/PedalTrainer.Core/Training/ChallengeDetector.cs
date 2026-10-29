using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Decide qual zona de referência o piloto está tentando reproduzir no momento em que
/// encosta no freio. Com posição de pista usa a zona mais próxima; sem ela, percorre
/// as zonas em sequência para manter o treino produtivo.
/// </summary>
public sealed class ChallengeDetector
{
    /// <summary>Quanto antes do ponto ideal o piloto pode frear e ainda ser avaliado (metros).</summary>
    public const double LookbackMeters = 200.0;

    /// <summary>Quanto depois do fim da zona ainda aceitamos a seleção (metros).</summary>
    public const double LookaheadMeters = 40.0;

    private readonly ReferenceLap _lap;
    private readonly IReadOnlyList<BrakingZone> _zones;
    private int _lastSelectedIndex = -1;

    public ChallengeDetector(ReferenceLap lap, IReadOnlyList<BrakingZone> zones)
    {
        _lap = lap ?? throw new ArgumentNullException(nameof(lap));
        _zones = zones ?? throw new ArgumentNullException(nameof(zones));
    }

    /// <summary>Zonas disponíveis para treino.</summary>
    public IReadOnlyList<BrakingZone> Zones => _zones;

    /// <summary>Quantidade de zonas detectadas na referência.</summary>
    public int ZoneCount => _zones.Count;

    /// <summary>Próxima zona a treinar, ou nulo quando o piloto freou fora de qualquer zona.</summary>
    public BrakingZone? SelectNext(double? distanceMeters)
    {
        if (_zones.Count == 0)
        {
            return null;
        }

        if (distanceMeters is double distance && _lap.HasDistanceData)
        {
            BrakingZone? best = FindNearestZoneByDistance(distance);
            if (best is not null)
            {
                _lastSelectedIndex = best.Index;
            }

            return best;
        }

        return SelectNextSequential();
    }

    /// <summary>Reinicia a sequência (usado ao carregar uma nova referência).</summary>
    public void Reset() => _lastSelectedIndex = -1;

    private BrakingZone? FindNearestZoneByDistance(double distanceMeters)
    {
        BrakingZone? best = null;
        double bestDelta = double.MaxValue;

        foreach (BrakingZone zone in _zones)
        {
            if (!zone.HasDistance)
            {
                continue;
            }

            double delta = distanceMeters - zone.StartDistanceMeters;
            if (delta < -LookbackMeters || delta > zone.DistanceLengthMeters + LookaheadMeters)
            {
                continue;
            }

            double absolute = Math.Abs(delta);
            if (absolute < bestDelta)
            {
                bestDelta = absolute;
                best = zone;
            }
        }

        return best;
    }

    private BrakingZone SelectNextSequential()
    {
        _lastSelectedIndex = (_lastSelectedIndex + 1) % _zones.Count;
        return _zones[_lastSelectedIndex];
    }
}
