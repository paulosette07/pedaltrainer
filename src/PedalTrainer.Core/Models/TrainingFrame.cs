using System;

namespace PedalTrainer.Core.Models;

/// <summary>
/// Instantâneo imutável publicado pelo motor de treino para a interface.
/// A UI lê o último quadro a 60 Hz, sem nunca tocar no hardware.
/// </summary>
public sealed record TrainingFrame
{
    public static TrainingFrame Empty { get; } = new();

    public PedalReading Pedals { get; init; } = PedalReading.Idle(0);

    public CarTelemetry Car { get; init; } = CarTelemetry.Disconnected(0);

    public TrainingPhase Phase { get; init; } = TrainingPhase.Idle;

    public BrakingZone? ActiveZone { get; init; }

    public double ReferenceBrakePercent { get; init; }

    public double ReferenceThrottlePercent { get; init; }

    /// <summary>Precisão consolidada da janela atual (0 a 100).</summary>
    public double LiveScore { get; init; }

    /// <summary>Erro instantâneo em pontos percentuais de pedal.</summary>
    public double InstantErrorPercent { get; init; }

    public double RmsePercent { get; init; }

    /// <summary>Progresso da janela de desafio (0 a 1).</summary>
    public double ChallengeProgress { get; init; }

    public double ElapsedInZoneSeconds { get; init; }

    /// <summary>Instante (relógio monotônico do motor) em que a janela atual começou.</summary>
    public double ChallengeStartTimeSeconds { get; init; }

    /// <summary>Precisão do acelerador na janela atual (0 a 100).</summary>
    public double ThrottleAccuracy { get; init; }

    /// <summary>Média das notas da sessão.</summary>
    public double AverageScore { get; init; }

    public double BestScore { get; init; }

    public double SessionBestStreak { get; init; }

    public double BestScoreForZone { get; init; }

    public int Streak { get; init; }

    public CornerResult? LastResult { get; init; }

    public string StatusMessage { get; init; } = "Carregue uma volta de referência para começar.";

    public bool ReferenceLoaded { get; init; }

    public string ReferenceName { get; init; } = "Nenhuma";

    public int ZoneCount { get; init; }

    public bool DeviceConnected { get; init; }

    public string DeviceStatus { get; init; } = "Desconectado";

    public double InputRateHz { get; init; }

    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
