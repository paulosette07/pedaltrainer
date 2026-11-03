using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;
using PedalTrainer.Core.Training;

namespace PedalTrainer.Tests;

/// <summary>
/// Próxima curva do Treino: o piloto precisa ver o desenho da frenagem seguinte antes de
/// chegar nela. Cobre a escolha da zona (fila ou posição na pista) sem mexer no treino.
/// </summary>
public sealed class TrainingPreviewTests
{
    [Fact]
    public void Detector_PeekNext_WithoutDistance_FollowsTheQueueWithoutAdvancing()
    {
        var detector = CreateDetector(out _);

        Assert.Equal(0, detector.PeekNext(null)!.Index);

        detector.SelectNext(null);

        Assert.Equal(1, detector.PeekNext(null)!.Index);
        Assert.Equal(1, detector.PeekNext(null)!.Index);
    }

    [Fact]
    public void Detector_PeekAfter_WithoutDistance_WrapsToTheFirstZone()
    {
        var detector = CreateDetector(out IReadOnlyList<BrakingZone> zones);

        Assert.Equal(0, detector.PeekAfter(zones[^1], null)!.Index);
        Assert.Equal(1, detector.PeekAfter(zones[0], null)!.Index);
    }

    [Fact]
    public void Detector_PeekNext_WithDistance_PicksTheZoneAhead()
    {
        var detector = CreateDetector(out IReadOnlyList<BrakingZone> zones);
        BrakingZone target = zones[1];

        BrakingZone? upcoming = detector.PeekNext(target.StartDistanceMeters - 50);

        Assert.NotNull(upcoming);
        Assert.Equal(target.Index, upcoming!.Index);
    }

    [Fact]
    public void Detector_PeekAfter_WithDistance_PicksTheFollowingZoneOnTheLap()
    {
        var detector = CreateDetector(out IReadOnlyList<BrakingZone> zones);
        BrakingZone first = zones[0];

        BrakingZone? upcoming = detector.PeekAfter(first, first.EndDistanceMeters);

        Assert.NotNull(upcoming);
        Assert.True(upcoming!.StartDistanceMeters > first.EndDistanceMeters);
        Assert.NotEqual(first.Index, upcoming.Index);
    }

    [Fact]
    public void Detector_PeekAfter_WithDistance_WrapsAfterTheLastZone()
    {
        var detector = CreateDetector(out IReadOnlyList<BrakingZone> zones);
        BrakingZone last = zones[^1];

        BrakingZone? upcoming = detector.PeekAfter(last, last.EndDistanceMeters);

        Assert.NotNull(upcoming);
        Assert.Equal(0, upcoming!.Index);
    }

    [Fact]
    public void Session_WithoutReference_HasNoUpcomingCurve()
    {
        var session = new TrainingSession();

        Assert.Null(session.PeekNextZone(CarTelemetry.Disconnected(0)));
    }

    [Fact]
    public void Session_AfterLoadingTheDemoLap_PublishesTheFirstUpcomingZone()
    {
        var session = new TrainingSession();
        session.LoadReference(DemoReferenceLapFactory.Create());

        Assert.NotEmpty(session.Zones);

        BrakingZone? upcoming = session.PeekNextZone(CarTelemetry.Disconnected(0));

        Assert.NotNull(upcoming);
        Assert.Equal(session.Zones[0].Index, upcoming!.Index);
    }

    private static ChallengeDetector CreateDetector(out IReadOnlyList<BrakingZone> zones)
    {
        ReferenceLap lap = DemoReferenceLapFactory.Create();
        zones = BrakingZoneExtractor.Extract(lap);
        return new ChallengeDetector(lap, zones);
    }
}
