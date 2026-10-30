using System;
using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Core.Telemetry.AssettoCorsaCompetizione;

/// <summary>
/// Lê a telemetria do Assetto Corsa Competizione: os pedais e a velocidade vêm da página de
/// física, o número de voltas e a posição na pista vêm da página gráfica.
/// </summary>
public sealed class AccTelemetryReader : ISimTelemetryReader
{
    private static readonly string[] PhysicsNames = { AccSharedMemoryLayout.PhysicsMapName, "acpmf_physics" };
    private static readonly string[] GraphicsNames = { AccSharedMemoryLayout.GraphicsMapName, "acpmf_graphics" };

    private readonly SharedMemoryMap _physics = new();
    private readonly SharedMemoryMap _graphics = new();

    public SimulatorInfo Simulator => SimulatorCatalog.AssettoCorsaCompetizione;

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

        int gameStatus = _graphics.ReadInt32(AccSharedMemoryLayout.StatusOffset);
        if (gameStatus != AccSharedMemoryLayout.StatusLive)
        {
            status = DescribeStatus(gameStatus);
            return false;
        }

        double gas = Math.Clamp(_physics.ReadFloat(AccSharedMemoryLayout.GasOffset), 0, 1);
        double brake = Math.Clamp(_physics.ReadFloat(AccSharedMemoryLayout.BrakeOffset), 0, 1);
        double speedKmh = _physics.ReadFloat(AccSharedMemoryLayout.SpeedKmhOffset);

        int completedLaps = _graphics.ReadInt32(AccSharedMemoryLayout.CompletedLapsOffset);
        double lapPercent = _graphics.ReadFloat(AccSharedMemoryLayout.NormalizedPositionOffset);

        if (lapPercent is < 0 or > 1.5 || double.IsNaN(lapPercent))
        {
            status = "posição na pista inválida";
            return false;
        }

        sample = new SimTelemetrySample(
            SpeedKmh: Math.Max(0, speedKmh),
            BrakePercent: brake * 100,
            ThrottlePercent: gas * 100,
            LapDistancePercent: Math.Clamp(lapPercent, 0, 1),
            LapNumber: Math.Max(0, completedLaps) + 1,
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
