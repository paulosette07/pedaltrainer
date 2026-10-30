using System;
using PedalTrainer.Core.IRacing;

namespace PedalTrainer.Core.Telemetry.IRacing;

/// <summary>
/// Adapta o leitor nativo do iRacing (memória compartilhada do IRSDK) à interface comum
/// de leitores. O iRacing é o único simulador aqui que publica o tempo de sessão.
/// </summary>
public sealed class IRacingTelemetryReader : ISimTelemetryReader
{
    private const double MetersPerSecondToKmh = 3.6;

    private readonly IRacingSharedMemoryReader _reader = new();

    public SimulatorInfo Simulator => SimulatorCatalog.IRacing;

    public bool IsOpen => _reader.IsOpen;

    public bool TryOpen() => _reader.TryConnect();

    public void Close() => _reader.Close();

    public bool TryRead(out SimTelemetrySample sample, out string status)
    {
        sample = default;

        if (!_reader.TryReadSample(out IRacingRawSample raw) || !raw.IsValid)
        {
            status = "sim aberto, telemetria inativa";
            return false;
        }

        sample = new SimTelemetrySample(
            SpeedKmh: Math.Max(0, raw.SpeedMetersPerSecond) * MetersPerSecondToKmh,
            BrakePercent: Math.Clamp(raw.BrakeFraction, 0, 1) * 100,
            ThrottlePercent: Math.Clamp(raw.ThrottleFraction, 0, 1) * 100,
            LapDistancePercent: Math.Clamp(raw.LapDistancePercent, 0, 1),
            LapNumber: raw.LapNumber,
            SessionTimeSeconds: raw.SessionTimeSeconds);

        status = $"ao vivo (volta {raw.LapNumber})";
        return true;
    }

    public void Dispose() => _reader.Dispose();
}
