using System;
using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Core.Telemetry.LeMansUltimate;

/// <summary>
/// Lê a telemetria do Le Mans Ultimate pelo plugin "rFactor 2 Shared Memory Map": pedais e
/// velocidade vêm do bloco de telemetria (primeiro carro, que é o do jogador) e a posição na
/// pista vem do bloco de classificação, dividida pelo comprimento da volta.
/// </summary>
public sealed class LeMansUltimateTelemetryReader : ISimTelemetryReader
{
    private const double MetersPerSecondToKmh = 3.6;
    private const double MinimumTrackLengthMeters = 100.0;

    private static readonly string[] TelemetryNames =
    {
        RFactorSharedMemoryLayout.TelemetryMapName,
        $"Local\\{RFactorSharedMemoryLayout.TelemetryMapName}"
    };

    private static readonly string[] ScoringNames =
    {
        RFactorSharedMemoryLayout.ScoringMapName,
        $"Local\\{RFactorSharedMemoryLayout.ScoringMapName}"
    };

    private readonly SharedMemoryMap _telemetry = new();
    private readonly SharedMemoryMap _scoring = new();

    public SimulatorInfo Simulator => SimulatorCatalog.LeMansUltimate;

    public bool IsOpen => _telemetry.IsOpen && _scoring.IsOpen;

    public bool TryOpen()
    {
        if (IsOpen)
        {
            return true;
        }

        return _telemetry.TryOpen(TelemetryNames) && _scoring.TryOpen(ScoringNames);
    }

    public void Close()
    {
        _telemetry.Close();
        _scoring.Close();
    }

    public bool TryRead(out SimTelemetrySample sample, out string status)
    {
        sample = default;

        if (!IsOpen)
        {
            status = "memória compartilhada fechada";
            return false;
        }

        if (!IsVersionStable(_telemetry) || !IsVersionStable(_scoring))
        {
            status = "atualizando a telemetria";
            return false;
        }

        int vehicleCount = _telemetry.ReadInt32(RFactorSharedMemoryLayout.TelemetryVehicleCountOffset);
        if (vehicleCount <= 0)
        {
            status = "simulador sem carro na pista";
            return false;
        }

        double trackLength = _scoring.ReadDouble(RFactorSharedMemoryLayout.ScoringTrackLengthOffset);
        if (trackLength < MinimumTrackLengthMeters || double.IsNaN(trackLength))
        {
            status = "comprimento da pista indisponível";
            return false;
        }

        double lapDistance = FindPlayerLapDistance();
        if (lapDistance < 0)
        {
            status = "carro do jogador não encontrado";
            return false;
        }

        double sessionTime = _telemetry.ReadDouble(RFactorSharedMemoryLayout.TelemetryElapsedTimeOffset);
        if (sessionTime <= 0)
        {
            status = "sessão ainda não começou";
            return false;
        }

        int lapNumber = _telemetry.ReadInt32(RFactorSharedMemoryLayout.TelemetryLapNumberOffset);
        double throttle = _telemetry.ReadDouble(RFactorSharedMemoryLayout.TelemetryFilteredThrottleOffset);
        double brake = _telemetry.ReadDouble(RFactorSharedMemoryLayout.TelemetryFilteredBrakeOffset);
        double speedMetersPerSecond = ReadSpeedMetersPerSecond();

        if (double.IsNaN(speedMetersPerSecond))
        {
            status = "velocidade inválida";
            return false;
        }

        sample = new SimTelemetrySample(
            SpeedKmh: speedMetersPerSecond * MetersPerSecondToKmh,
            BrakePercent: Math.Clamp(brake, 0, 1) * 100,
            ThrottlePercent: Math.Clamp(throttle, 0, 1) * 100,
            LapDistancePercent: Math.Clamp(lapDistance / trackLength, 0, 1),
            LapNumber: Math.Max(0, lapNumber),
            SessionTimeSeconds: sessionTime);

        status = $"ao vivo (volta {sample.LapNumber})";
        return true;
    }

    /// <summary>Módulo da velocidade local (m/s), publicada como vetor em três doubles.</summary>
    private double ReadSpeedMetersPerSecond()
    {
        int offset = RFactorSharedMemoryLayout.TelemetryLocalVelocityOffset;

        double x = _telemetry.ReadDouble(offset);
        double y = _telemetry.ReadDouble(offset + sizeof(double));
        double z = _telemetry.ReadDouble(offset + (2 * sizeof(double)));

        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    /// <summary>
    /// O plugin escreve o bloco em duas etapas; quando os contadores divergem, o conteúdo
    /// pode estar pela metade e a leitura é descartada.
    /// </summary>
    private static bool IsVersionStable(SharedMemoryMap map)
    {
        int begin = map.ReadInt32(RFactorSharedMemoryLayout.VersionBeginOffset);
        int end = map.ReadInt32(RFactorSharedMemoryLayout.VersionEndOffset);
        return begin == end;
    }

    /// <summary>
    /// Procura o carro marcado como jogador no bloco de classificação e devolve a distância
    /// percorrida na volta atual (em metros); devolve -1 quando não encontra.
    /// </summary>
    private double FindPlayerLapDistance()
    {
        int vehicleCount = _scoring.ReadInt32(RFactorSharedMemoryLayout.ScoringVehicleCountOffset);
        int count = Math.Clamp(vehicleCount, 0, RFactorSharedMemoryLayout.MaxMappedVehicles);

        for (var index = 0; index < count; index++)
        {
            int baseOffset = RFactorSharedMemoryLayout.ScoringVehiclesOffset +
                             (index * RFactorSharedMemoryLayout.ScoringVehicleSize);

            byte isPlayer = _scoring.ReadByte(baseOffset + RFactorSharedMemoryLayout.ScoringVehicleIsPlayerOffset);
            if (isPlayer == 0)
            {
                continue;
            }

            return _scoring.ReadDouble(baseOffset + RFactorSharedMemoryLayout.ScoringVehicleLapDistanceOffset);
        }

        return -1;
    }

    public void Dispose()
    {
        _telemetry.Dispose();
        _scoring.Dispose();
    }
}
