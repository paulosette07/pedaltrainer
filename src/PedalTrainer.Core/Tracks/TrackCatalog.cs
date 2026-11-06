using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Catálogo de pistas oferecido na tela de treino: os traçados do sistema mais os que o
/// piloto importou. Interlagos é a pista padrão quando o aplicativo abre.
/// </summary>
public static class TrackCatalog
{
    /// <summary>Identificador da pista padrão (Autódromo José Carlos Pace, São Paulo).</summary>
    public const string DefaultTrackId = "interlagos";

    /// <summary>Traçados embutidos no aplicativo.</summary>
    public static IReadOnlyList<TrackDefinition> System => TrackLibrary.Tracks;

    /// <summary>Pista padrão, usada quando nenhuma escolha foi salva.</summary>
    public static TrackDefinition Default => TrackLibrary.Default;

    /// <summary>
    /// Junta os traçados do sistema e os importados numa lista única, sem identificadores
    /// repetidos (o importado substitui o do sistema com o mesmo id).
    /// </summary>
    public static IReadOnlyList<TrackDefinition> Merge(IEnumerable<TrackDefinition> imported)
    {
        var byId = new Dictionary<string, TrackDefinition>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<TrackDefinition>();

        foreach (TrackDefinition track in System)
        {
            byId[track.Id] = track;
            ordered.Add(track);
        }

        if (imported is null)
        {
            return ordered;
        }

        foreach (TrackDefinition track in imported)
        {
            if (byId.TryGetValue(track.Id, out TrackDefinition? existing))
            {
                int position = ordered.IndexOf(existing);
                if (position >= 0)
                {
                    ordered[position] = track;
                }

                byId[track.Id] = track;
                continue;
            }

            byId[track.Id] = track;
            ordered.Insert(0, track);
        }

        return ordered;
    }

    /// <summary>Procura uma pista (do sistema ou importada) pelo identificador.</summary>
    public static TrackDefinition? Find(IEnumerable<TrackDefinition> tracks, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || tracks is null)
        {
            return null;
        }

        foreach (TrackDefinition track in tracks)
        {
            if (string.Equals(track.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return track;
            }
        }

        return null;
    }
}
