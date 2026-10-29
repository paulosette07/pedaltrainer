using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;
using PedalTrainer.Core.Training;

namespace PedalTrainer.Tests;

/// <summary>
/// Peças que sustentam a sessão ao vivo: escolha do desafio, curva fantasma,
/// buffer do gráfico e estatísticas.
/// </summary>
public sealed class TrainingSessionTests
{
    [Fact]
    public void ChallengeDetector_WithoutDistance_CyclesThroughZones()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();
        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);
        var detector = new ChallengeDetector(lap, zones);

        Assert.Equal(0, detector.SelectNext(null)!.Index);
        Assert.Equal(1, detector.SelectNext(null)!.Index);
        Assert.Equal(2, detector.SelectNext(null)!.Index);

        detector.Reset();
        Assert.Equal(0, detector.SelectNext(null)!.Index);
    }

    [Fact]
    public void ChallengeDetector_WithDistance_PicksNearestZone()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();
        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);
        var detector = new ChallengeDetector(lap, zones);

        // Freio da terceira zona (índice 2) começa perto de 22 s de volta.
        double targetDistance = zones[2].StartDistanceMeters;
        BrakingZone? selected = detector.SelectNext(targetDistance - 20);

        Assert.NotNull(selected);
        Assert.Equal(2, selected!.Index);
    }

    [Fact]
    public void ChallengeDetector_ReturnsNullFarFromEveryZone()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();
        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);

        // Entre a última frenagem e o fim da volta, longe de qualquer ponto de freada.
        var detector = new ChallengeDetector(lap, zones);
        BrakingZone? selected = detector.SelectNext(zones[^1].EndDistanceMeters + 300);

        Assert.Null(selected);
    }

    [Fact]
    public void ChallengeDetector_EmptyZoneListReturnsNull()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();
        var detector = new ChallengeDetector(lap, Array.Empty<BrakingZone>());

        Assert.Equal(0, detector.ZoneCount);
        Assert.Null(detector.SelectNext(null));
    }

    [Fact]
    public void GhostCurve_StartsBeforeZoneAndEndsAfter()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();
        BrakingZone zone = BrakingZoneExtractor.Extract(lap)[1];

        IReadOnlyList<TraceSample> ghost = GhostCurveBuilder.Build(lap, zone);

        Assert.True(ghost.Count > 20);
        Assert.Equal(-GhostCurveBuilder.LeadInSeconds, ghost[0].TimeSeconds, 3);
        Assert.True(ghost[^1].TimeSeconds > zone.DurationSeconds);

        double peak = 0;
        foreach (TraceSample sample in ghost)
        {
            peak = Math.Max(peak, sample.BrakePercent);
        }

        Assert.InRange(peak, zone.PeakBrakePercent - 1, zone.PeakBrakePercent + 1);
    }

    [Fact]
    public void GhostCurve_TimeRangeMatchesBuilder()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();
        BrakingZone zone = BrakingZoneExtractor.Extract(lap)[0];

        (double min, double max) = GhostCurveBuilder.GetTimeRange(zone);

        Assert.Equal(-GhostCurveBuilder.LeadInSeconds, min, 6);
        Assert.Equal(zone.DurationSeconds + GhostCurveBuilder.TailSeconds, max, 6);
    }

    [Fact]
    public void TraceRingBuffer_CopiesOnlyRequestedWindow()
    {
        var buffer = new TraceRingBuffer(historySeconds: 5, rateHz: 200);
        for (int i = 0; i < 100; i++)
        {
            buffer.Add(new TraceSample(i * 0.01, i, 0));
        }

        var destination = new TraceSample[200];
        int copied = buffer.CopySince(destination, 0.50);

        Assert.Equal(50, copied);
        Assert.Equal(0.50, destination[0].TimeSeconds, 3);
        Assert.Equal(0.99, destination[copied - 1].TimeSeconds, 3);
    }

    [Fact]
    public void TraceRingBuffer_KeepsChronologicalOrderAfterWrap()
    {
        var buffer = new TraceRingBuffer(historySeconds: 0.32, rateHz: 200);
        for (int i = 0; i < 1000; i++)
        {
            buffer.Add(new TraceSample(i * 0.01, i % 100, 0));
        }

        var destination = new TraceSample[64];
        int copied = buffer.CopySince(destination, 0);

        Assert.Equal(64, copied);
        Assert.Equal(9.36, destination[0].TimeSeconds, 3);
        Assert.Equal(9.99, destination[copied - 1].TimeSeconds, 3);

        for (int i = 1; i < copied; i++)
        {
            Assert.True(
                destination[i].TimeSeconds > destination[i - 1].TimeSeconds,
                $"ordem quebrada em {i}: {destination[i - 1].TimeSeconds} -> {destination[i].TimeSeconds}");
        }
    }

    [Fact]
    public void TraceRingBuffer_RespectsDestinationSize()
    {
        var buffer = new TraceRingBuffer(historySeconds: 5, rateHz: 200);
        for (int i = 0; i < 500; i++)
        {
            buffer.Add(new TraceSample(i * 0.005, 10, 0));
        }

        var destination = new TraceSample[10];
        int copied = buffer.CopySince(destination, 0);

        Assert.Equal(10, copied);
    }

    [Fact]
    public void SessionStats_TracksAverageBestAndStreak()
    {
        var stats = new SessionStats();

        stats.Register(Result(zoneIndex: 0, score: 96));
        stats.Register(Result(zoneIndex: 1, score: 92));
        stats.Register(Result(zoneIndex: 0, score: 70));

        Assert.Equal(3, stats.CompletedChallenges);
        Assert.Equal(96, stats.BestScore, 3);
        Assert.Equal((96 + 92 + 70) / 3.0, stats.AverageScore, 3);
        Assert.Equal(0, stats.Streak);
        Assert.Equal(2, stats.BestStreak);
        Assert.Equal(96, stats.BestForZone(0), 3);
        Assert.Equal(92, stats.BestForZone(1), 3);
        Assert.Equal(70, stats.LastResult!.Score, 3);
    }

    [Fact]
    public void SessionStats_ResetClearsHistory()
    {
        var stats = new SessionStats();
        stats.Register(Result(0, 99));

        stats.Reset();

        Assert.Equal(0, stats.CompletedChallenges);
        Assert.Equal(0, stats.BestScore);
        Assert.Equal(0, stats.AverageScore);
        Assert.Equal(0, stats.Streak);
        Assert.Null(stats.LastResult);
    }

    private static CornerResult Result(int zoneIndex, double score) => new(
        ZoneIndex: zoneIndex,
        ZoneName: $"Zona {zoneIndex + 1}",
        Score: score,
        RmsePercent: 5,
        PeakBrakeUserPercent: 90,
        PeakBrakeReferencePercent: 92,
        TimeToPeakUserSeconds: 0.4,
        TimeToPeakReferenceSeconds: 0.35,
        SampleCount: 60,
        CompletedAtUtc: DateTime.UtcNow);
}
