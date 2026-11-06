using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;
using PedalTrainer.Core.Services;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Voltas de referência por pista: a volta ideal gerada do traçado (sempre disponível) e as
/// voltas do piloto, guardadas numa pasta por pista para a lista não virar uma bagunça.
/// </summary>
public sealed class ReferenceLibrary
{
    private static readonly string[] SupportedExtensions = { ".csv", ".tsv", ".txt", ".json" };

    private readonly object _gate = new();
    private readonly Dictionary<string, ReferenceLap> _idealLaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReferenceLapFileLoader _loader = new();
    private readonly LapPhysicsOptions _physics;
    private readonly string? _referencesRoot;

    public ReferenceLibrary(LapPhysicsOptions? physics = null, string? referencesRoot = null)
    {
        _physics = physics ?? LapPhysicsOptions.Default;
        _referencesRoot = referencesRoot;
    }

    /// <summary>Volta ideal da pista (calculada uma vez e reaproveitada).</summary>
    public ReferenceLap GetIdealLap(TrackDefinition track)
    {
        ArgumentNullException.ThrowIfNull(track);

        lock (_gate)
        {
            if (_idealLaps.TryGetValue(track.Id, out ReferenceLap? cached))
            {
                return cached;
            }

            ReferenceLap lap = IdealLapFactory.Build(track, _physics);
            _idealLaps[track.Id] = lap;
            return lap;
        }
    }

    /// <summary>Pasta onde as voltas do piloto para a pista são guardadas.</summary>
    public string GetReferenceDirectory(TrackDefinition track)
    {
        ArgumentNullException.ThrowIfNull(track);

        return _referencesRoot is null
            ? AppPaths.TrackReferencesDirectory(track.Id)
            : Path.Combine(_referencesRoot, AppPaths.SafeTrackFolder(track.Id));
    }

    /// <summary>Opções mostradas na lista: o sistema primeiro, depois os arquivos do piloto.</summary>
    public IReadOnlyList<TrackReferenceSource> ListSources(TrackDefinition track)
    {
        ArgumentNullException.ThrowIfNull(track);

        ReferenceLap ideal = GetIdealLap(track);
        var sources = new List<TrackReferenceSource>
        {
            TrackReferenceSource.System(
                IdealLapFactory.SystemLabel,
                string.Create(CultureInfo.CurrentCulture, $"calculada do traçado · {ideal.DurationSeconds:0,0} s"))
        };

        foreach (string file in EnumerateUserFiles(track))
        {
            sources.Add(TrackReferenceSource.File(
                Path.GetFileNameWithoutExtension(file),
                Describe(file),
                file));
        }

        return sources;
    }

    /// <summary>Carrega a volta escolhida na lista.</summary>
    public bool TryLoad(TrackReferenceSource source, TrackDefinition track, out ReferenceLap? lap, out string? error)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(track);

        if (source.IsSystem)
        {
            lap = GetIdealLap(track);
            error = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(source.Path))
        {
            lap = null;
            error = "A referência escolhida não tem arquivo.";
            return false;
        }

        return _loader.TryLoad(source.Path, out lap, out error);
    }

    /// <summary>Grava uma volta do piloto (gravada no simulador) na pasta da pista.</summary>
    public string SaveLap(TrackDefinition track, ReferenceLap lap)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(lap);

        string directory = GetReferenceDirectory(track);
        Directory.CreateDirectory(directory);

        string name = BuildFileName(track, DateTime.Now);
        string path = Path.Combine(directory, name);
        ReferenceLapCsvWriter.WriteFile(lap, path);
        return path;
    }

    /// <summary>Copia uma telemetria escolhida pelo piloto para a pasta da pista.</summary>
    public string ImportLap(TrackDefinition track, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        string directory = GetReferenceDirectory(track);
        Directory.CreateDirectory(directory);

        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".csv";
        }

        string destination = Path.Combine(directory, BuildFileName(track, DateTime.Now, extension));
        File.Copy(sourcePath, destination, overwrite: false);
        return destination;
    }

    private IEnumerable<string> EnumerateUserFiles(TrackDefinition track)
    {
        string directory = GetReferenceDirectory(track);

        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        var files = new List<string>();

        foreach (string extension in SupportedExtensions)
        {
            files.AddRange(Directory.EnumerateFiles(directory, $"*{extension}"));
        }

        files.Sort(static (left, right) => File.GetLastWriteTime(right).CompareTo(File.GetLastWriteTime(left)));
        return files;
    }

    private static string Describe(string path)
    {
        DateTime savedAt = File.GetLastWriteTime(path);
        return string.Create(CultureInfo.CurrentCulture, $"arquivo · {savedAt:dd/MM HH:mm}");
    }

    private static string BuildFileName(TrackDefinition track, DateTime timestamp, string extension = ".csv")
    {
        var builder = new StringBuilder("volta-");

        foreach (char character in track.Id)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                builder.Append(character);
            }
        }

        builder.Append('-');
        builder.Append(timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        builder.Append(extension);
        return builder.ToString();
    }
}
