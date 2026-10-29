using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;

namespace PedalTrainer.Tests;

/// <summary>
/// A detecção de zonas define quantos desafios existem na volta e onde eles começam.
/// </summary>
public sealed class BrakingZoneTests
{
    [Fact]
    public void Extract_DemoLap_FindsEveryPlannedZone()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();

        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);

        Assert.Equal(8, zones.Count);
        Assert.All(zones, zone => Assert.True(zone.DurationSeconds > 0.2));
        Assert.All(zones, zone => Assert.True(zone.PeakBrakePercent >= 5));
    }

    [Fact]
    public void Extract_DemoLap_PeaksMatchProfile()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();

        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);

        Assert.Equal(95, zones[0].PeakBrakePercent, 1);
        Assert.Equal(100, zones[3].PeakBrakePercent, 1);
        Assert.Equal(62, zones[4].PeakBrakePercent, 1);
    }

    [Fact]
    public void Extract_FirstZoneStartsWhereBrakingBegins()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();

        BrakingZone first = BrakingZoneExtractor.Extract(lap)[0];

        Assert.InRange(first.StartTimeSeconds, 5.95, 6.05);
    }

    [Fact]
    public void Extract_IgnoresShortBrakeTaps()
    {
        ReferenceLap lap = ReferenceLap.Create("tap", new[]
        {
            ReferenceSample.AtTime(0.00, 0, 100),
            ReferenceSample.AtTime(0.01, 40, 60),
            ReferenceSample.AtTime(0.02, 0, 100),
            ReferenceSample.AtTime(1.00, 0, 100),
            ReferenceSample.AtTime(1.01, 0, 100)
        });

        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);

        Assert.Empty(zones);
    }

    [Fact]
    public void Extract_MergesCloseBrakingPhases()
    {
        ReferenceLap lap = ReferenceLap.Create("merge", new[]
        {
            ReferenceSample.AtTime(0.0, 0, 100),
            ReferenceSample.AtTime(0.1, 60, 40),
            ReferenceSample.AtTime(0.3, 60, 40),
            ReferenceSample.AtTime(0.4, 0, 100),
            ReferenceSample.AtTime(0.45, 55, 45),
            ReferenceSample.AtTime(0.8, 55, 45),
            ReferenceSample.AtTime(1.0, 0, 100)
        });

        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);

        Assert.Single(zones);
    }

    [Fact]
    public void Extract_SplitsLongStops()
    {
        ReferenceLap lap = ReferenceLap.Create("split", new[]
        {
            ReferenceSample.AtTime(0.0, 0, 100),
            ReferenceSample.AtTime(0.1, 70, 30),
            ReferenceSample.AtTime(0.6, 70, 30),
            ReferenceSample.AtTime(0.7, 0, 100),
            ReferenceSample.AtTime(2.0, 0, 100),
            ReferenceSample.AtTime(2.1, 80, 20),
            ReferenceSample.AtTime(2.6, 80, 20),
            ReferenceSample.AtTime(2.7, 0, 100)
        });

        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(lap);

        Assert.Equal(2, zones.Count);
        Assert.Equal(0, zones[0].Index);
        Assert.Equal(1, zones[1].Index);
    }

    [Fact]
    public void Extract_RespectsMaxZones()
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();

        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(
            lap,
            new BrakingZoneOptions { MaxZones = 3 });

        Assert.Equal(3, zones.Count);
    }

    [Fact]
    public void Extract_WithoutDistance_HasNoDistanceMetadata()
    {
        ReferenceLap lap = ReferenceLap.Create("tempo", new[]
        {
            ReferenceSample.AtTime(0.0, 0, 100),
            ReferenceSample.AtTime(0.1, 80, 20),
            ReferenceSample.AtTime(0.8, 80, 20),
            ReferenceSample.AtTime(1.0, 0, 100)
        });

        BrakingZone zone = Assert.Single(BrakingZoneExtractor.Extract(lap));

        Assert.False(zone.HasDistance);
        Assert.Equal(0, zone.DistanceLengthMeters);
    }

    [Fact]
    public void Extract_Zone_ExposesDistanceWhenAvailable()
    {
        BrakingZone zone = BrakingZoneExtractor.Extract(DemoReferenceLapFactory.Create())[0];

        Assert.True(zone.HasDistance);
        Assert.True(zone.DistanceLengthMeters > 0);
    }
}
