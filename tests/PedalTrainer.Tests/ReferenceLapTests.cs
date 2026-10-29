using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;

namespace PedalTrainer.Tests;

/// <summary>
/// A volta de referência é a fonte da verdade do treino: criação, ordenação,
/// interpolação e reamostragem precisam ser exatas.
/// </summary>
public sealed class ReferenceLapTests
{
    [Fact]
    public void Create_SortsSamplesByTime()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            Sample(2.0, 50),
            Sample(0.0, 10),
            Sample(1.0, 30)
        });

        Assert.Equal(3, lap.Samples.Count);
        Assert.Equal(0.0, lap.Samples[0].TimeSeconds);
        Assert.Equal(1.0, lap.Samples[1].TimeSeconds);
        Assert.Equal(2.0, lap.Samples[2].TimeSeconds);
        Assert.Equal(2.0, lap.DurationSeconds, 3);
    }

    [Fact]
    public void Create_ClampsOutOfRangeValues()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            new ReferenceSample(0, 150, -20, -50, -10),
            new ReferenceSample(1, 40, 60, 100, 120)
        });

        Assert.Equal(100, lap.Samples[0].BrakePercent);
        Assert.Equal(0, lap.Samples[0].ThrottlePercent);
        Assert.Equal(0, lap.Samples[0].DistanceMeters);
        Assert.Equal(0, lap.Samples[0].SpeedKmh);
    }

    [Fact]
    public void Create_ThrowsWithoutSamples()
    {
        Assert.Throws<ArgumentException>(() => ReferenceLap.Create("vazia", Array.Empty<ReferenceSample>()));
    }

    [Fact]
    public void Create_InfersTrackLengthFromLastDistance()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            new ReferenceSample(0, 0, 100, 0, 100),
            new ReferenceSample(1, 0, 100, 800, 100)
        });

        Assert.Equal(800, lap.TrackLengthMeters);
        Assert.True(lap.HasDistanceData);
    }

    [Fact]
    public void SampleAtTime_InterpolatesLinearly()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            Sample(0, 0),
            Sample(2, 80)
        });

        ReferenceSample midpoint = lap.SampleAtTime(1.0);

        Assert.Equal(1.0, midpoint.TimeSeconds, 6);
        Assert.Equal(40, midpoint.BrakePercent, 6);
    }

    [Fact]
    public void SampleAtTime_ClampsBeforeAndAfter()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            Sample(1, 10),
            Sample(3, 90)
        });

        Assert.Equal(10, lap.SampleAtTime(-5).BrakePercent, 3);
        Assert.Equal(90, lap.SampleAtTime(50).BrakePercent, 3);
    }

    [Fact]
    public void SampleAtDistance_InterpolatesByDistance()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            new ReferenceSample(0, 0, 100, 0, 100),
            new ReferenceSample(1, 100, 0, 200, 80)
        }, trackLengthMeters: 200);

        ReferenceSample halfway = lap.SampleAtDistance(100);

        Assert.Equal(50, halfway.BrakePercent, 3);
        Assert.Equal(50, halfway.ThrottlePercent, 3);
    }

    [Fact]
    public void SampleAtDistance_FallsBackToTimeWithoutDistanceData()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            new ReferenceSample(0, 25, 75, 0, 90),
            new ReferenceSample(2, 25, 75, 0, 90)
        });

        Assert.False(lap.HasDistanceData);
        Assert.Equal(25, lap.SampleAtDistance(120).BrakePercent, 3);
    }

    [Fact]
    public void DistanceFromLapPercent_ScalesByTrackLength()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            new ReferenceSample(0, 0, 100, 0, 100),
            new ReferenceSample(1, 0, 100, 1000, 100)
        });

        Assert.Equal(500, lap.DistanceFromLapPercent(0.5), 3);
        Assert.Equal(0, lap.DistanceFromLapPercent(-1), 3);
        Assert.Equal(1000, lap.DistanceFromLapPercent(4), 3);
    }

    [Fact]
    public void Resampler_ProducesFixedGridAndKeepsDuration()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[]
        {
            Sample(0, 0),
            Sample(1, 100),
            Sample(2, 0)
        });

        ReferenceLap resampled = ReferenceLapResampler.Resample(lap, rateHz: 100);

        Assert.Equal(0.01, ReferenceLapResampler.EstimateSampleInterval(resampled), 4);
        Assert.Equal(2.0, resampled.DurationSeconds, 3);
        Assert.Equal(201, resampled.Samples.Count);
        Assert.Equal(0.0, resampled.Samples[0].TimeSeconds, 6);
        Assert.Equal(50, resampled.SampleAtTime(0.5).BrakePercent, 1);
    }

    [Fact]
    public void Resampler_ReturnsSameLapWhenTooShort()
    {
        ReferenceLap lap = ReferenceLap.Create("teste", new[] { Sample(0, 10) });

        Assert.Same(lap, ReferenceLapResampler.Resample(lap));
    }

    private static ReferenceSample Sample(double time, double brake) =>
        ReferenceSample.AtTime(time, brake, 100 - brake);
}
