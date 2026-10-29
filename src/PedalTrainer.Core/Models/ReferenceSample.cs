using System;

namespace PedalTrainer.Core.Models;

/// <summary>Amostra de uma volta de referência (piloto "fantasma").</summary>
public readonly record struct ReferenceSample(
    double TimeSeconds,
    double BrakePercent,
    double ThrottlePercent,
    double DistanceMeters,
    double SpeedKmh)
{
    /// <summary>Distância preenchida apenas quando a referência traz essa coluna.</summary>
    public bool HasDistance => DistanceMeters > 0;

    public static ReferenceSample AtTime(double timeSeconds, double brakePercent, double throttlePercent)
        => new(timeSeconds, brakePercent, throttlePercent, 0, 0);
}
