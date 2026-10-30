namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Leitura crua de um instante do carro, já normalizada para as unidades do aplicativo
/// (km/h, 0 a 100% e 0 a 1 na posição da pista). O relógio da sessão fica opcional:
/// simuladores que não publicam tempo de sessão ganham o relógio monotônico da fonte.
/// </summary>
public readonly record struct SimTelemetrySample(
    double SpeedKmh,
    double BrakePercent,
    double ThrottlePercent,
    double LapDistancePercent,
    int LapNumber,
    double? SessionTimeSeconds);
