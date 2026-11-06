using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PedalTrainer.Core.Services;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Traçados importados pelo piloto. Ficam em %APPDATA%\PedalTrainer\tracks\maps, um JSON por
/// pista, no mesmo formato dos traçados do sistema — assim dá para substituir um traçado
/// aproximado por um desenho preciso da pista.
/// </summary>
public sealed class TrackStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public TrackStore(string? directory = null)
    {
        Directory = directory ?? AppPaths.ImportedTracksDirectory;
    }

    /// <summary>Pasta onde os traçados importados são guardados.</summary>
    public string Directory { get; }

    /// <summary>Lê todos os traçados importados válidos.</summary>
    public IReadOnlyList<TrackDefinition> LoadAll()
    {
        var tracks = new List<TrackDefinition>();

        if (!System.IO.Directory.Exists(Directory))
        {
            return tracks;
        }

        foreach (string file in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
        {
            if (TryLoad(file, out TrackDefinition? track))
            {
                tracks.Add(track!);
            }
        }

        return tracks;
    }

    /// <summary>Lê um arquivo de traçado e o traz para a pasta do aplicativo.</summary>
    public bool TryImport(string sourcePath, out TrackDefinition? track, out string? error)
    {
        if (!TryLoad(sourcePath, out track, out error))
        {
            return false;
        }

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string destination = Path.Combine(Directory, $"{track!.Id}.json");

            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, destination, overwrite: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            track = null;
            return false;
        }
    }

    /// <summary>Grava um traçado na pasta de importados.</summary>
    public void Save(TrackDefinition track)
    {
        ArgumentNullException.ThrowIfNull(track);

        System.IO.Directory.CreateDirectory(Directory);

        var points = new double[track.Path.Count * 2];
        for (var index = 0; index < track.Path.Count; index++)
        {
            points[index * 2] = Math.Round(track.Path[index].X, 1);
            points[(index * 2) + 1] = Math.Round(track.Path[index].Y, 1);
        }

        var corners = new TrackFileCorner[track.Corners.Count];
        for (var index = 0; index < track.Corners.Count; index++)
        {
            corners[index] = new TrackFileCorner
            {
                Name = track.Corners[index].Name,
                Fraction = Math.Round(track.Corners[index].Fraction, 4)
            };
        }

        var document = new TrackFileDocument
        {
            Id = track.Id,
            Name = track.Name,
            City = track.City,
            Country = track.Country,
            CountryCode = track.CountryCode,
            LengthMeters = track.LengthMeters,
            Points = points,
            Corners = corners
        };

        File.WriteAllText(
            Path.Combine(Directory, $"{track.Id}.json"),
            JsonSerializer.Serialize(document, WriteOptions));
    }

    private static bool TryLoad(string path, out TrackDefinition? track)
    {
        return TryLoad(path, out track, out _);
    }

    private static bool TryLoad(string path, out TrackDefinition? track, out string? error)
    {
        track = null;
        error = null;

        try
        {
            string json = File.ReadAllText(path);
            TrackFileDocument? document = JsonSerializer.Deserialize<TrackFileDocument>(json, ReadOptions);
            track = TrackFileParser.FromDocument(document, TrackSource.Imported);
            return true;
        }
        catch (TrackMapException exception)
        {
            error = exception.Message;
            return false;
        }
        catch (JsonException exception)
        {
            error = $"Arquivo de traçado inválido: {exception.Message}";
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }
}
