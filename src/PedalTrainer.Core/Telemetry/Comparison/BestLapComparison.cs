using System;

namespace PedalTrainer.Core.Telemetry.Comparison;

/// <summary>
/// Fotografia da comparação com a melhor volta: delta de tempo acumulado desde a linha de
/// chegada e os valores da referência no mesmo ponto da pista.
/// </summary>
public readonly record struct BestLapComparison
{
    /// <summary>Construtor explícito: os valores padrão vivem nos inicializadores.</summary>
    public BestLapComparison()
    {
    }

    /// <summary>Comparação vazia, usada quando não há referência carregada.</summary>
    public static BestLapComparison None { get; } = new()
    {
        Hint = "Nenhuma volta de referência carregada."
    };

    /// <summary>Nome da volta usada como melhor volta.</summary>
    public string ReferenceName { get; init; } = string.Empty;

    /// <summary>Duração da melhor volta, em segundos (0 quando desconhecida).</summary>
    public double ReferenceDurationSeconds { get; init; }

    /// <summary>Verdadeiro quando há referência e o jogo informa a posição na volta.</summary>
    public bool CanCompare { get; init; }

    /// <summary>Delta acumulado na volta atual, em segundos (negativo = à frente da melhor).</summary>
    public double DeltaSeconds { get; init; }

    /// <summary>Verdadeiro depois da primeira amostra somada na volta.</summary>
    public bool HasDelta { get; init; }

    /// <summary>Maior ganho já acumulado nesta volta (negativo quando à frente).</summary>
    public double BestDeltaSeconds { get; init; }

    public bool HasBestDelta { get; init; }

    /// <summary>Delta com que a última volta completa terminou.</summary>
    public double LastLapDeltaSeconds { get; init; }

    public bool HasLastLapDelta { get; init; }

    /// <summary>Fração da volta já percorrida (0 a 1).</summary>
    public double LapProgress { get; init; }

    public double SpeedKmh { get; init; }

    public double ReferenceSpeedKmh { get; init; }

    public double BrakePercent { get; init; }

    public double ReferenceBrakePercent { get; init; }

    public double ThrottlePercent { get; init; }

    public double ReferenceThrottlePercent { get; init; }

    /// <summary>Explica por que a comparação não está disponível (vazio quando está).</summary>
    public string Hint { get; init; } = string.Empty;

    /// <summary>Verdadeiro quando o piloto está à frente da melhor volta no ponto atual.</summary>
    public bool IsAhead => HasDelta && DeltaSeconds < 0;

    /// <summary>Diferença de velocidade para a referência no mesmo ponto, em km/h.</summary>
    public double SpeedDeltaKmh => SpeedKmh - ReferenceSpeedKmh;
}
