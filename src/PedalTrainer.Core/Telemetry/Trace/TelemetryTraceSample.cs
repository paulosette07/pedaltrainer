namespace PedalTrainer.Core.Telemetry.Trace;

/// <summary>
/// Um ponto do traço de uma volta, posicionado na pista (não no relógio): é o que permite
/// comparar o pé no mesmo lugar da pista em voltas diferentes.
/// </summary>
public readonly record struct TelemetryTraceSample(
    double LapPercent,
    double BrakePercent,
    double ThrottlePercent,
    double SpeedKmh,
    double TimeSeconds)
{
    /// <summary>Distância percorrida na volta, em metros (0 quando o traçado é desconhecido).</summary>
    public double DistanceMeters(double trackLengthMeters) =>
        trackLengthMeters > 0 ? LapPercent * trackLengthMeters : 0;
}
