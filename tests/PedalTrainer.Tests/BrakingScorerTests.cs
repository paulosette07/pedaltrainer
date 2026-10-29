using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;
using PedalTrainer.Core.Training;

namespace PedalTrainer.Tests;

/// <summary>
/// A nota precisa ser honesta: reproduzir a referência dá ~100, errar o pé dá menos,
/// e a banda de tolerância não pode punir pequenas diferenças.
/// </summary>
public sealed class BrakingScorerTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(8, 1.0)]
    [InlineData(54, 0.5)]
    [InlineData(100, 0.0)]
    public void ComputeSampleScore_IsOneInsideToleranceAndFallsLinearly(double error, double expected)
    {
        double score = BrakingScorer.ComputeSampleScore(error, tolerancePercent: 8);

        Assert.Equal(expected, score, 2);
    }

    [Fact]
    public void ComputeSampleScore_IsSymmetric()
    {
        Assert.Equal(
            BrakingScorer.ComputeSampleScore(-30, 8),
            BrakingScorer.ComputeSampleScore(30, 8),
            6);
    }

    [Fact]
    public void PerfectPilot_ScoresNearHundred()
    {
        (ReferenceLap lap, BrakingZone zone) = DemoZone();

        var aligner = new TimeReferenceAligner(lap, zone);
        var scorer = new BrakingScorer(aligner);

        for (double t = 0; t <= zone.DurationSeconds; t += 0.02)
        {
            ReferenceSample reference = lap.SampleAtTime(zone.StartTimeSeconds + t);
            scorer.AddSample(t, reference.BrakePercent, reference.ThrottlePercent, null);
        }

        Assert.True(scorer.LiveScore > 99, $"nota inesperada: {scorer.LiveScore:0.0}");
        Assert.True(scorer.RmsePercent < 0.5, $"RMSE inesperado: {scorer.RmsePercent:0.00}");
        Assert.True(scorer.ThrottleAccuracy > 99);
    }

    [Fact]
    public void Overbraking_ScoresLowerThanPerfect()
    {
        (ReferenceLap lap, BrakingZone zone) = DemoZone();

        var aligner = new TimeReferenceAligner(lap, zone);
        var scorer = new BrakingScorer(aligner);

        for (double t = 0; t <= zone.DurationSeconds; t += 0.02)
        {
            scorer.AddSample(t, 100, 0, null);
        }

        Assert.True(scorer.LiveScore < 95, $"nota inesperada: {scorer.LiveScore:0.0}");
        Assert.True(scorer.RmsePercent > 5);
    }

    [Fact]
    public void Complete_BuildsResultWithPeaksAndGrade()
    {
        (ReferenceLap lap, BrakingZone zone) = DemoZone();

        var aligner = new TimeReferenceAligner(lap, zone);
        var scorer = new BrakingScorer(aligner);

        for (double t = 0; t <= zone.DurationSeconds; t += 0.02)
        {
            ReferenceSample reference = lap.SampleAtTime(zone.StartTimeSeconds + t);
            scorer.AddSample(t, reference.BrakePercent, reference.ThrottlePercent, null);
        }

        CornerResult result = scorer.Complete(DateTime.UtcNow);

        Assert.Equal(zone.Index, result.ZoneIndex);
        Assert.Equal(zone.Name, result.ZoneName);
        Assert.True(result.Score > 99);
        Assert.Equal("S", result.Grade);
        Assert.True(result.SampleCount > 10);
        Assert.Equal(0, result.PeakBrakeErrorPercent, 1);
    }

    [Fact]
    public void AddSample_ReportsReferenceAndProgress()
    {
        (ReferenceLap lap, BrakingZone zone) = DemoZone();

        var aligner = new TimeReferenceAligner(lap, zone);
        var scorer = new BrakingScorer(aligner);

        double score = scorer.AddSample(0, 0, 100, null);

        Assert.Equal(0, scorer.Progress, 3);
        Assert.True(score > 90, $"nota inesperada: {score:0.0}");
        Assert.InRange(scorer.ReferenceBrakePercent, 0, 100);
        Assert.Equal(1, scorer.SampleCount);
    }

    [Fact]
    public void EmptyScorer_ReturnsSafeValues()
    {
        (ReferenceLap lap, BrakingZone zone) = DemoZone();

        var scorer = new BrakingScorer(new TimeReferenceAligner(lap, zone));

        Assert.Equal(0, scorer.LiveScore);
        Assert.Equal(0, scorer.RmsePercent);
        Assert.Equal(0, scorer.ThrottleAccuracy);
        Assert.Equal(0, scorer.SampleCount);
    }

    private static (ReferenceLap Lap, BrakingZone Zone) DemoZone()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();
        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);
        return (lap, zones[0]);
    }
}
