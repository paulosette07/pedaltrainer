using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;
using PedalTrainer.Core.Tracks;

namespace PedalTrainer.Tests;

/// <summary>
/// Catálogo embutido, volta ideal por pista e a biblioteca de referências (sistema + arquivos
/// do piloto). Cobre a pista padrão (Interlagos) e a geração da referência de cada traçado.
/// </summary>
public sealed class TrackCatalogTests
{
    [Fact]
    public void LibraryShipsInterlagosAsDefaultTrack()
    {
        Assert.NotEmpty(TrackLibrary.Tracks);

        TrackDefinition defaultTrack = TrackCatalog.Default;

        Assert.Equal(TrackCatalog.DefaultTrackId, defaultTrack.Id);
        Assert.Equal("Autódromo José Carlos Pace", defaultTrack.Name);
        Assert.Equal("São Paulo", defaultTrack.City);
        Assert.InRange(defaultTrack.LengthMeters, 4200, 4400);
        Assert.True(defaultTrack.Path.Count > 200);
    }

    [Fact]
    public void EveryTrackHasUsableGeometry()
    {
        foreach (TrackDefinition track in TrackLibrary.Tracks)
        {
            Assert.True(track.Path.Count >= 16, track.Id);
            Assert.InRange(track.Path.LengthMeters / track.LengthMeters, 0.97, 1.03);
            Assert.False(string.IsNullOrWhiteSpace(track.Country));
        }
    }

    /// <summary>
    /// Nenhum traçado pode ter um vão longo entre dois pontos: isso é o sintoma de uma
    /// volta costurada com uma ponte (retas cortando o miolo do circuito), que aparece
    /// no mapa como um risco atravessando a pista.
    /// </summary>
    [Fact]
    public void EveryTrackLoopIsContinuous()
    {
        foreach (TrackDefinition track in TrackLibrary.Tracks)
        {
            IReadOnlyList<TrackPoint> points = track.Path.Points;
            double step = track.Path.StepMeters;

            Assert.True(step > 0, track.Id);

            double longest = 0;

            for (var index = 0; index < points.Count; index++)
            {
                double distance = points[index].DistanceTo(points[(index + 1) % points.Count]);
                longest = Math.Max(longest, distance);
            }

            Assert.True(longest < step * 4, $"{track.Id}: vão de {longest:0} m (passo {step:0.0} m)");
        }
    }

    [Fact]
    public void IdealLapForEachTrackIsPlausible()
    {
        foreach (TrackDefinition track in TrackLibrary.Tracks)
        {
            ReferenceLap lap = IdealLapFactory.Build(track);

            double seconds = lap.DurationSeconds;
            double minimum = track.LengthMeters / 90.0;
            double maximum = track.LengthMeters / 20.0;

            Assert.InRange(seconds, minimum, maximum);
            Assert.True(lap.HasDistanceData, track.Id);
            Assert.Equal(track.Path.LengthMeters, lap.TrackLengthMeters, 1);
        }
    }

    [Fact]
    public void IdealLapBrakesOnTheCornersOfInterlagos()
    {
        TrackDefinition track = TrackCatalog.Default;
        ReferenceLap lap = IdealLapFactory.Build(track);

        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);

        // Interlagos tem 15 curvas; entre 6 e 20 frenagens é o esperado para uma volta.
        // A volta ideal de referência tem de ficar entre um carro de rua e um GT
        // (o recorde real é 1:08; acima de 1:40 o perfil estaria travado).
        Assert.InRange(zones.Count, 6, 20);
        Assert.InRange(lap.DurationSeconds, 68, 100);

        foreach (BrakingZone zone in zones)
        {
            Assert.True(zone.HasDistance);
            Assert.True(zone.PeakBrakePercent > 10);
        }
    }

    [Fact]
    public void ReferenceLibraryListsSystemFirstAndLoadsIt()
    {
        using var folder = new TempFolder();
        var library = new ReferenceLibrary(referencesRoot: folder.Path);

        IReadOnlyList<TrackReferenceSource> sources = library.ListSources(TrackCatalog.Default);

        Assert.NotEmpty(sources);
        Assert.True(sources[0].IsSystem);
        Assert.True(library.TryLoad(sources[0], TrackCatalog.Default, out ReferenceLap? lap, out string? error));
        Assert.Null(error);
        Assert.NotNull(lap);
    }

    [Fact]
    public void SavedLapShowsUpAsPilotReference()
    {
        using var folder = new TempFolder();
        var library = new ReferenceLibrary(referencesRoot: folder.Path);
        TrackDefinition track = TrackCatalog.Default;

        string path = library.SaveLap(track, IdealLapFactory.Build(track));

        Assert.True(File.Exists(path));

        IReadOnlyList<TrackReferenceSource> sources = library.ListSources(track);
        TrackReferenceSource file = Assert.Single(sources, source => !source.IsSystem);

        Assert.True(library.TryLoad(file, track, out ReferenceLap? loaded, out _));
        Assert.NotNull(loaded);
    }

    [Fact]
    public void StoreRoundTripsImportedTrack()
    {
        using var folder = new TempFolder();
        var store = new TrackStore(folder.Path);

        TrackDefinition original = TrackCatalog.Default;
        store.Save(original);

        IReadOnlyList<TrackDefinition> loaded = store.LoadAll();

        TrackDefinition track = Assert.Single(loaded);
        Assert.Equal(original.Id, track.Id);
        Assert.Equal(TrackSource.Imported, track.Source);
        Assert.InRange(track.Path.LengthMeters, original.Path.LengthMeters - 5, original.Path.LengthMeters + 5);
    }

    /// <summary>Pasta temporária descartável para não tocar no perfil do piloto.</summary>
    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pedaltrainer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
