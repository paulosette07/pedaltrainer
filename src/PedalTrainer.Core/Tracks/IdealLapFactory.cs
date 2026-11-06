using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Monta a volta de referência do sistema para uma pista: o melhor traçado possível dentro
/// dos limites de aderência, com as frenagens exatamente nas curvas do mapa. Assim toda
/// pista do catálogo tem uma referência utilizável, mesmo sem arquivo de telemetria.
/// </summary>
public static class IdealLapFactory
{
    /// <summary>Prefixo do nome exibido nas voltas geradas.</summary>
    public const string NamePrefix = "Volta ideal";

    /// <summary>Nome curto usado na lista de referências ("Sistema").</summary>
    public const string SystemLabel = "Volta ideal do sistema";

    public static ReferenceLap Build(TrackDefinition track) =>
        Build(track, LapPhysicsOptions.Default);

    public static ReferenceLap Build(TrackDefinition track, LapPhysicsOptions options)
    {
        ArgumentNullException.ThrowIfNull(track);

        LapSpeedProfile profile = LapSpeedProfile.Build(track.Path, options);
        var samples = new List<ReferenceSample>(profile.Count);

        for (var index = 0; index < profile.Count; index++)
        {
            samples.Add(new ReferenceSample(
                TimeSeconds: profile.Times[index],
                BrakePercent: profile.BrakePercent[index],
                ThrottlePercent: profile.ThrottlePercent[index],
                DistanceMeters: profile.Distances[index],
                SpeedKmh: profile.Speeds[index] * 3.6));
        }

        return ReferenceLap.Create(
            $"{NamePrefix} · {track.Name} (sistema)",
            samples,
            track.Path.LengthMeters);
    }
}
