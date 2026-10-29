using System;

namespace PedalTrainer.Core.Models;

/// <summary>
/// Dados do carro lidos do iRacing. Quando o sim não está aberto,
/// <see cref="IsConnected"/> é falso e todos os campos ficam zerados.
/// </summary>
public readonly record struct CarTelemetry(
    bool IsConnected,
    double SpeedKmh,
    double BrakePercent,
    double ThrottlePercent,
    double LapDistancePercent,
    double SessionTimeSeconds,
    int LapNumber,
    double TimestampSeconds)
{
    public static CarTelemetry Disconnected(double timestampSeconds) =>
        new(false, 0, 0, 0, 0, 0, 0, timestampSeconds);

    public bool HasLapPosition => IsConnected && LapDistancePercent >= 0;
}
