using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Alinhamento por posição na pista: a referência é consultada pela distância percorrida,
/// então o piloto é comparado no mesmo ponto do traçado, independentemente de quando freou.
/// Requer dados de distância na referência e a posição do carro vinda do iRacing.
/// </summary>
public sealed class DistanceReferenceAligner : IReferenceAligner
{
    private readonly ReferenceLap _lap;
    private readonly double _tailMeters;

    public DistanceReferenceAligner(ReferenceLap lap, BrakingZone zone, double tailMeters = 30)
    {
        _lap = lap ?? throw new ArgumentNullException(nameof(lap));
        Zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _tailMeters = Math.Max(0, tailMeters);
    }

    public BrakingZone Zone { get; }

    public string ModeName => "Distância";

    /// <summary>Distância usada quando o carro não informa posição (fallback para o início da zona).</summary>
    private double FallbackDistance => Zone.StartDistanceMeters;

    public ReferenceSample Resolve(double elapsedSeconds, double? distanceMeters)
    {
        double distance = distanceMeters ?? FallbackDistance;
        return _lap.SampleAtDistance(distance);
    }

    public double Progress(double elapsedSeconds, double? distanceMeters)
    {
        if (distanceMeters is null)
        {
            double duration = Math.Max(0.1, Zone.DurationSeconds);
            return Math.Clamp(elapsedSeconds / duration, 0, 1);
        }

        double length = Math.Max(1, Zone.DistanceLengthMeters);
        return Math.Clamp((distanceMeters.Value - Zone.StartDistanceMeters) / length, 0, 1);
    }

    public bool IsComplete(double elapsedSeconds, double? distanceMeters)
    {
        double limit = Zone.EndDistanceMeters + _tailMeters;
        double distance = distanceMeters ?? FallbackDistance;
        return distance >= limit;
    }
}
