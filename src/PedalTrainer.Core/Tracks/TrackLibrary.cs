using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Traçados que acompanham o aplicativo. Cada circuito é um JSON embutido no assembly
/// (Tracks/Data), gerado a partir das curvas mapeadas no OpenStreetMap: os pontos já vêm
/// reamostrados em passos iguais, com a linha de chegada na fração zero.
/// </summary>
public static class TrackLibrary
{
    private const string ResourcePrefix = "PedalTrainer.Core.Tracks.Data.";
    private const string ResourceSuffix = ".json";

    private static readonly Lazy<IReadOnlyList<TrackDefinition>> LazyTracks =
        new(LoadTracks, isThreadSafe: true);

    /// <summary>Todos os traçados do sistema, com os do Brasil primeiro.</summary>
    public static IReadOnlyList<TrackDefinition> Tracks => LazyTracks.Value;

    /// <summary>Pista padrão do aplicativo: Interlagos.</summary>
    public static TrackDefinition Default => Find(TrackCatalog.DefaultTrackId) ?? Tracks[0];

    /// <summary>Procura um traçado do sistema pelo identificador.</summary>
    public static TrackDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (TrackDefinition track in Tracks)
        {
            if (string.Equals(track.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return track;
            }
        }

        return null;
    }

    private static IReadOnlyList<TrackDefinition> LoadTracks()
    {
        Assembly assembly = typeof(TrackLibrary).Assembly;
        var tracks = new List<TrackDefinition>();

        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                !resource.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream? stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);

            try
            {
                tracks.Add(TrackFileParser.Parse(reader.ReadToEnd()));
            }
            catch (Exception exception) when (exception is TrackMapException or System.Text.Json.JsonException)
            {
                // Um traçado corrompido não pode impedir o aplicativo de abrir.
            }
        }

        return tracks
            .OrderBy(static track => track.CountryCode == "BR" ? 0 : 1)
            .ThenBy(static track => track.Country, StringComparer.CurrentCulture)
            .ThenBy(static track => track.Name, StringComparer.CurrentCulture)
            .ToList();
    }
}
