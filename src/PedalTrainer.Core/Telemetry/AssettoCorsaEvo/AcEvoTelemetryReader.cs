using System;
using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Core.Telemetry.AssettoCorsaEvo;

/// <summary>
/// Lê a telemetria do Assetto Corsa EVO: pedais e velocidade na página de física,
/// posição na pista e voltas completadas na página gráfica.
/// </summary>
public sealed class AcEvoTelemetryReader : ISimTelemetryReader
{
    private static readonly string[] PhysicsNames = { AcEvoSharedMemoryLayout.PhysicsMapName, "acevo_pmf_physics" };
    private static readonly string[] GraphicsNames = { AcEvoSharedMemoryLayout.GraphicsMapName, "acevo_pmf_graphics" };

    private readonly SharedMemoryMap _physics = new();
    private readonly SharedMemoryMap _graphics = new();

    public SimulatorInfo Simulator => SimulatorCatalog.AssettoCorsaEvo;

    public bool IsOpen => _physics.IsOpen && _graphics.IsOpen;

    public bool TryOpen()
    {
        if (IsOpen)
        {
            return true;
        }

        return _physics.TryOpen(PhysicsNames) && _graphics.TryOpen(GraphicsNames);
    }

    public void Close()
    {
        _physics.Close();
        _graphics.Close();
    }

    public bool TryRead(out SimTelemetrySample sample, out string status)
    {
        sample = default;

        if (!IsOpen)
        {
            status = "memória compartilhada fechada";
            return false;
        }

        int gameStatus = _graphics.ReadInt32(AcEvoSharedMemoryLayout.StatusOffset);
        if (gameStatus != AcEvoSharedMemoryLayout.StatusLive)
        {
            status = DescribeStatus(gameStatus);
            return false;
        }

        double gas = Math.Clamp(_physics.ReadFloat(AcEvoSharedMemoryLayout.GasOffset), 0, 1);
        double brake = Math.Clamp(_physics.ReadFloat(AcEvoSharedMemoryLayout.BrakeOffset), 0, 1);
        double speedKmh = _physics.ReadFloat(AcEvoSharedMemoryLayout.SpeedKmhOffset);

        double lapPercent = _graphics.ReadFloat(AcEvoSharedMemoryLayout.NormalizedPositionOffset);
        if (lapPercent is < 0 or > 1.5 || double.IsNaN(lapPercent))
        {
            status = "posição na pista inválida";
            return false;
        }

        int totalLaps = _graphics.ReadInt32(AcEvoSharedMemoryLayout.TotalLapCountOffset);

        sample = new SimTelemetrySample(
            SpeedKmh: Math.Max(0, speedKmh),
            BrakePercent: brake * 100,
            ThrottlePercent: gas * 100,
            LapDistancePercent: Math.Clamp(lapPercent, 0, 1),
            LapNumber: Math.Max(0, totalLaps) + 1,
            SessionTimeSeconds: null);

        status = $"ao vivo (volta {sample.LapNumber})";
        return true;
    }

    private static string DescribeStatus(int gameStatus) => gameStatus switch
    {
        0 => "jogo fora de sessão",
        1 => "replay",
        3 => "jogo pausado",
        _ => "jogo em menu"
    };

    public void Dispose()
    {
        _physics.Dispose();
        _graphics.Dispose();
    }
}
