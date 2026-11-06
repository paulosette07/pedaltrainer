using System;
using System.Collections.Generic;
using Xunit;
using PedalTrainer.Core.Tracks;

namespace PedalTrainer.Tests;

/// <summary>
/// Geometria do traçado: fechamento da volta, reamostragem, escala e ancoragem da
/// linha de chegada. São as contas que sustentam o mapa e a volta ideal.
/// </summary>
public sealed class TrackPathTests
{
    private static List<TrackPoint> Square(double side) => new()
    {
        new TrackPoint(0, 0),
        new TrackPoint(side, 0),
        new TrackPoint(side, side),
        new TrackPoint(0, side)
    };

    [Fact]
    public void PathIsClosedAndStartsAtFirstPoint()
    {
        var path = new TrackPath(Square(100));

        Assert.Equal(400, path.LengthMeters, 1);
        Assert.True(path.Count >= 4);
        Assert.Equal(0, path.PointAtFraction(0).X, 3);
        Assert.Equal(0, path.PointAtFraction(0).Y, 3);
    }

    [Fact]
    public void FractionHalfwayLandsOnOppositeCorner()
    {
        var path = new TrackPath(Square(100));

        TrackPoint half = path.PointAtFraction(0.5);

        Assert.Equal(100, half.X, 1);
        Assert.Equal(100, half.Y, 1);
    }

    [Fact]
    public void NearestFractionFindsPointOnTrack()
    {
        var path = new TrackPath(Square(100));

        // O quadrado tem 400 m: andar 100 m na base e 50 m na lateral dá 150 m = 0,375.
        double fraction = path.NearestFraction(new TrackPoint(100, 50), out double distance);

        Assert.InRange(fraction, 0.36, 0.39);
        Assert.InRange(distance, 0, 1);
    }

    [Fact]
    public void BuilderScalesGeometryToOfficialLength()
    {
        TrackPath path = TrackPathBuilder.Build(Square(100), targetLengthMeters: 1000);

        Assert.InRange(path.LengthMeters, 990, 1010);
        Assert.True(path.StepMeters > 1);
    }

    [Fact]
    public void BuilderStartsTheLoopAtTheAnchor()
    {
        var anchor = new TrackPoint(100, 100);

        TrackPath path = TrackPathBuilder.Build(Square(100), startAnchor: anchor);

        TrackPoint start = path.PointAtFraction(0);

        Assert.InRange(start.DistanceTo(anchor), 0, 8);
    }

    [Fact]
    public void ParserReadsFlatPointListAndCorners()
    {
        const string Json = """
        {
          "id": "teste",
          "name": "Pista de teste",
          "city": "Lugar",
          "country": "Brasil",
          "countryCode": "BR",
          "lengthMeters": 1000,
          "points": [0, 0, 250, 0, 250, 250, 0, 250],
          "corners": [ { "name": "Curva 1", "fraction": 0.25 } ]
        }
        """;

        TrackDefinition track = TrackFileParser.Parse(Json, TrackSource.Imported);

        Assert.Equal("teste", track.Id);
        Assert.Equal(TrackSource.Imported, track.Source);
        Assert.InRange(track.Path.LengthMeters, 990, 1010);
        Assert.Single(track.Corners);
        Assert.Equal("Curva 1", track.Corners[0].Name);
    }

    [Fact]
    public void ParserRejectsTrackWithoutPoints()
    {
        const string Json = """{ "id": "vazio", "points": [] }""";

        Assert.Throws<TrackMapException>(() => TrackFileParser.Parse(Json));
    }

    [Fact]
    public void CornerNearMatchesOnlyInsideTolerance()
    {
        TrackCorner corner = new("Ferradura", 0.5);

        Assert.True(corner.Matches(0.505));
        Assert.False(corner.Matches(0.55));
    }
}
