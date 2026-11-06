using System;

namespace PedalTrainer.Core.Tracks;

/// <summary>Limites de aderência usados para estimar a volta ideal de um traçado.</summary>
public sealed record LapPhysicsOptions
{
    /// <summary>Aceleração lateral máxima, em m/s² (≈1,5 g).</summary>
    public double LateralAcceleration { get; init; } = 14.5;

    /// <summary>Desaceleração máxima de frenagem, em m/s².</summary>
    public double BrakingDeceleration { get; init; } = 22.0;

    /// <summary>Aceleração máxima na saída das curvas, em m/s².</summary>
    public double AccelerationRate { get; init; } = 7.5;

    /// <summary>Velocidade máxima do carro, em km/h.</summary>
    public double TopSpeedKmh { get; init; } = 300.0;

    /// <summary>Folga de aderência lateral usada no cálculo (curva segura).</summary>
    public double LateralMargin { get; init; } = 0.94;

    public double TopSpeedMetersPerSecond => TopSpeedKmh / 3.6;

    public static LapPhysicsOptions Default { get; } = new();

    public LapPhysicsOptions Validated()
    {
        return this with
        {
            LateralAcceleration = Math.Clamp(LateralAcceleration, 4, 40),
            BrakingDeceleration = Math.Clamp(BrakingDeceleration, 4, 60),
            AccelerationRate = Math.Clamp(AccelerationRate, 1, 30),
            TopSpeedKmh = Math.Clamp(TopSpeedKmh, 80, 500),
            LateralMargin = Math.Clamp(LateralMargin, 0.5, 1.0)
        };
    }
}
