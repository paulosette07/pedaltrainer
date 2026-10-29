using System;

namespace PedalTrainer.Core.Models;

/// <summary>Resultado de uma zona de frenagem concluída pelo jogador.</summary>
public sealed record CornerResult(
    int ZoneIndex,
    string ZoneName,
    double Score,
    double RmsePercent,
    double PeakBrakeUserPercent,
    double PeakBrakeReferencePercent,
    double TimeToPeakUserSeconds,
    double TimeToPeakReferenceSeconds,
    int SampleCount,
    DateTime CompletedAtUtc)
{
    /// <summary>Diferença positiva significa que o jogador levou mais tempo até o pico de freio.</summary>
    public double PeakTimingErrorSeconds => TimeToPeakUserSeconds - TimeToPeakReferenceSeconds;

    public double PeakBrakeErrorPercent => PeakBrakeUserPercent - PeakBrakeReferencePercent;

    public string Grade => Score switch
    {
        >= 95 => "S",
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 55 => "D",
        _ => "E"
    };
}
