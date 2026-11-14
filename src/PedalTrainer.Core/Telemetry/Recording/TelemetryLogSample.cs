using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Telemetry.Recording;

/// <summary>
/// Uma linha do arquivo de telemetria. Guarda todos os canais do aplicativo e deixa o
/// formatador escolher quais viram coluna, conforme o que o jogo publica.
/// </summary>
public readonly record struct TelemetryLogSample(
    double ElapsedSeconds,
    double SpeedKmh,
    double BrakePercent,
    double ThrottlePercent,
    double LapDistancePercent,
    int LapNumber,
    double SessionTimeSeconds)
{
    /// <summary>
    /// Converte o instantâneo do carro em amostra gravável. <paramref name="elapsedSeconds"/>
    /// é o relógio da própria gravação; o tempo de sessão vem do jogo, quando houver.
    /// </summary>
    public static TelemetryLogSample From(
        in CarTelemetry car,
        double elapsedSeconds) => new(
            ElapsedSeconds: elapsedSeconds,
            SpeedKmh: car.SpeedKmh,
            BrakePercent: car.BrakePercent,
            ThrottlePercent: car.ThrottlePercent,
            LapDistancePercent: car.LapDistancePercent,
            LapNumber: car.LapNumber,
            SessionTimeSeconds: car.SessionTimeSeconds);
}
