using System;
using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Core.Telemetry.Automobilista2;

/// <summary>
/// Lê a telemetria do Automobilista 2 no formato Project CARS 2: velocidade em m/s,
/// pedais de 0 a 1 e a posição na pista que vem da distância percorrida do piloto.
/// </summary>
public sealed class Automobilista2TelemetryReader : ISimTelemetryReader
{
    private const double MetersPerSecondToKmh = 3.6;
    private const double MinimumTrackLengthMeters = 100.0;

    private static readonly string[] MapNames =
    {
        Pcars2SharedMemoryLayout.MemoryMapName,
        $"Local\\{Pcars2SharedMemoryLayout.MemoryMapName}",
        "Local\\$pcars2$"
    };

    private readonly SharedMemoryMap _map = new();

    public SimulatorInfo Simulator => SimulatorCatalog.Automobilista2;

    public bool IsOpen => _map.IsOpen;

    public bool TryOpen() => _map.TryOpen(MapNames);

    public void Close() => _map.Close();

    public bool TryRead(out SimTelemetrySample sample, out string status)
    {
        sample = default;

        if (!IsOpen)
        {
            status = "memória compartilhada fechada";
            return false;
        }

        int gameState = _map.ReadInt32(Pcars2SharedMemoryLayout.GameStateOffset);
        if (gameState != Pcars2SharedMemoryLayout.GameStateInGamePlaying)
        {
            status = gameState == Pcars2SharedMemoryLayout.GameStateInGamePaused
                ? "jogo pausado"
                : "jogo em menu";
            return false;
        }

        double trackLength = _map.ReadFloat(Pcars2SharedMemoryLayout.TrackLengthOffset);
        if (trackLength < MinimumTrackLengthMeters)
        {
            status = "comprimento da pista indisponível";
            return false;
        }

        int participantIndex = ResolveParticipantIndex();
        int participantBase = Pcars2SharedMemoryLayout.ParticipantsOffset +
                              (participantIndex * Pcars2SharedMemoryLayout.ParticipantSize);

        double lapDistance = _map.ReadFloat(participantBase + Pcars2SharedMemoryLayout.ParticipantLapDistanceOffset);
        int lapsCompleted = _map.ReadInt32(participantBase + Pcars2SharedMemoryLayout.ParticipantLapsCompletedOffset);
        double speedMetersPerSecond = _map.ReadFloat(Pcars2SharedMemoryLayout.SpeedOffset);
        double brake = Math.Clamp(_map.ReadFloat(Pcars2SharedMemoryLayout.BrakeOffset), 0, 1);
        double throttle = Math.Clamp(_map.ReadFloat(Pcars2SharedMemoryLayout.ThrottleOffset), 0, 1);

        if (double.IsNaN(lapDistance) || lapDistance < 0 || lapDistance > trackLength * 1.5)
        {
            status = "posição na pista inválida";
            return false;
        }

        double lapPercent = Math.Clamp(lapDistance / trackLength, 0, 1);

        sample = new SimTelemetrySample(
            SpeedKmh: Math.Max(0, speedMetersPerSecond) * MetersPerSecondToKmh,
            BrakePercent: brake * 100,
            ThrottlePercent: throttle * 100,
            LapDistancePercent: lapPercent,
            LapNumber: Math.Max(0, lapsCompleted) + 1,
            SessionTimeSeconds: null);

        status = $"ao vivo (volta {sample.LapNumber})";
        return true;
    }

    /// <summary>
    /// O carro do jogador é o que a câmera está seguindo; quando o índice vem inválido
    /// (menus, replay) usamos o primeiro piloto, que é o jogador em sessões locais.
    /// </summary>
    private int ResolveParticipantIndex()
    {
        int viewed = _map.ReadInt32(Pcars2SharedMemoryLayout.ViewedParticipantOffset);

        return viewed is >= 0 and < Pcars2SharedMemoryLayout.StoredParticipantsMax ? viewed : 0;
    }

    public void Dispose() => _map.Dispose();
}
