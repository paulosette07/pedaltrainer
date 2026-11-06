using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Lê um traçado em JSON (embutido no aplicativo ou importado pelo piloto) e devolve a
/// pista pronta. Os pontos podem vir em metros (lista) ou como coordenadas cruas
/// (polilinha aberta), que passam pelo suavizador e pela reamostragem.
/// </summary>
public static class TrackFileParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Interpreta o JSON de um traçado.</summary>
    public static TrackDefinition Parse(string json, TrackSource source = TrackSource.System)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        TrackFileDocument? document = JsonSerializer.Deserialize<TrackFileDocument>(json, Options);
        return FromDocument(document, source);
    }

    internal static TrackDefinition FromDocument(TrackFileDocument? document, TrackSource source)
    {
        if (document is null || string.IsNullOrWhiteSpace(document.Id))
        {
            throw new TrackMapException("O arquivo do traçado não informa o identificador da pista.");
        }

        if (document.Points is null || document.Points.Length < 6 || document.Points.Length % 2 != 0)
        {
            throw new TrackMapException(
                "O traçado precisa de pelo menos três pontos (lista achatada x0, y0, x1, y1...).");
        }

        var points = new List<TrackPoint>(document.Points.Length / 2);

        for (var index = 0; index < document.Points.Length; index += 2)
        {
            points.Add(new TrackPoint(document.Points[index], document.Points[index + 1]));
        }

        TrackPath path = TrackPathBuilder.Build(
            points,
            document.LengthMeters,
            startAnchor: points[0],
            spacingMeters: TrackPathBuilder.DefaultSpacingMeters);

        var corners = new List<TrackCorner>();

        foreach (TrackFileCorner corner in document.Corners ?? Array.Empty<TrackFileCorner>())
        {
            if (string.IsNullOrWhiteSpace(corner.Name))
            {
                continue;
            }

            corners.Add(new TrackCorner(corner.Name.Trim(), Math.Clamp(corner.Fraction, 0, 0.9999)));
        }

        corners.Sort(static (left, right) => left.Fraction.CompareTo(right.Fraction));

        return new TrackDefinition(
            document.Id.Trim(),
            document.Name ?? document.Id,
            document.City ?? string.Empty,
            document.Country ?? string.Empty,
            document.CountryCode ?? string.Empty,
            document.LengthMeters,
            path,
            corners,
            source);
    }
}
