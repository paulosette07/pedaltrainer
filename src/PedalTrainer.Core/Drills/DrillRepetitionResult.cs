using System;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Resultado de uma repetição concluída: nota, erro médio, pico, sobressalto e as
/// taxas de subida/soltura medidas no pedal.
/// </summary>
public sealed class DrillRepetitionResult
{
    public required string StepLabel { get; init; }

    public required string PedalName { get; init; }

    /// <summary>Número da repetição dentro da etapa (1 é a primeira).</summary>
    public required int RepetitionNumber { get; init; }

    /// <summary>Total de repetições da etapa.</summary>
    public required int RepetitionCount { get; init; }

    /// <summary>Alvo desta repetição (pode ter sido sorteado).</summary>
    public required double TargetPercent { get; init; }

    /// <summary>Nota de 0 a 100.</summary>
    public required double Score { get; init; }

    /// <summary>Conceito da nota (S, A, B, C, D, E).</summary>
    public required string Grade { get; init; }

    /// <summary>Erro RMS em relação à curva-alvo, em pontos percentuais.</summary>
    public required double RmsErrorPercent { get; init; }

    /// <summary>Erro médio durante os trechos de segurar.</summary>
    public required double HoldErrorPercent { get; init; }

    /// <summary>Maior percentual atingido.</summary>
    public required double PeakPercent { get; init; }

    /// <summary>Quanto passou do topo da curva-alvo, em pontos percentuais.</summary>
    public required double OvershootPercent { get; init; }

    /// <summary>Maior taxa de subida medida, em pontos percentuais por segundo.</summary>
    public required double RiseRatePercentPerSecond { get; init; }

    /// <summary>Taxa de soltura medida, em pontos percentuais por segundo.</summary>
    public required double ReleaseRatePercentPerSecond { get; init; }

    /// <summary>Duração efetiva da repetição.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Instante em que a repetição terminou.</summary>
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}
