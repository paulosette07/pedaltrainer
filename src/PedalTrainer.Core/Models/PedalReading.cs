using System;

namespace PedalTrainer.Core.Models;

/// <summary>
/// Amostra de pedal já calibrada, com valores em percentual (0 a 100).
/// Imutável para poder ser publicada entre threads sem sincronização.
/// </summary>
public readonly record struct PedalReading(
    double BrakePercent,
    double ThrottlePercent,
    double TimestampSeconds)
{
    public static PedalReading Idle(double timestampSeconds)
        => new(0, 0, timestampSeconds);

    public bool IsBraking(double thresholdPercent) => BrakePercent >= thresholdPercent;

    public PedalReading Clamped() => new(
        Math.Clamp(BrakePercent, 0, 100),
        Math.Clamp(ThrottlePercent, 0, 100),
        TimestampSeconds);
}
