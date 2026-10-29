using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Alinhamento por tempo: o desafio começa no instante em que o piloto toca o freio
/// e a referência avança a partir do início da zona. Não depende do iRacing.
/// </summary>
public sealed class TimeReferenceAligner : IReferenceAligner
{
    private readonly ReferenceLap _lap;
    private readonly double _tailSeconds;

    public TimeReferenceAligner(ReferenceLap lap, BrakingZone zone, double tailSeconds = 0.6)
    {
        _lap = lap ?? throw new ArgumentNullException(nameof(lap));
        Zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _tailSeconds = Math.Max(0, tailSeconds);
    }

    public BrakingZone Zone { get; }

    public string ModeName => "Tempo";

    public ReferenceSample Resolve(double elapsedSeconds, double? distanceMeters)
    {
        double time = Zone.StartTimeSeconds + Math.Max(0, elapsedSeconds);
        return _lap.SampleAtTime(time);
    }

    public double Progress(double elapsedSeconds, double? distanceMeters)
    {
        double duration = Math.Max(0.1, Zone.DurationSeconds);
        return Math.Clamp(elapsedSeconds / duration, 0, 1);
    }

    public bool IsComplete(double elapsedSeconds, double? distanceMeters) =>
        elapsedSeconds >= Zone.DurationSeconds + _tailSeconds;
}
