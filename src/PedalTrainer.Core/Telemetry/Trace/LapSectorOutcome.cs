using System.Collections.Generic;

namespace PedalTrainer.Core.Telemetry.Trace;

/// <summary>
/// O que uma volta rendeu ao livro de melhores trechos: quanto ela andou, quantos setores
/// entraram como novo melhor e como ficaria a volta composta pelos melhores setores até agora.
/// </summary>
public readonly record struct LapSectorOutcome(
    int LapNumber,
    double LapSeconds,
    double? CompositeSeconds,
    int BeatenSectors,
    int KnownSectors,
    int SectorCount,
    bool IsFastestLap,
    IReadOnlyList<bool> BeatenMask)
{
    /// <summary>Verdadeiro quando a soma dos melhores setores já cobre a volta inteira.</summary>
    public bool HasFullComposite => SectorCount > 0 && KnownSectors >= SectorCount;

    /// <summary>Volta composta menos esta volta (negativo = a composta é mais rápida).</summary>
    public double? CompositeGainSeconds =>
        CompositeSeconds is { } composite && LapSeconds > 0 ? composite - LapSeconds : null;
}
