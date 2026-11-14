using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry.Comparison;
using PedalTrainer.Core.Tracks;
using Xunit;

namespace PedalTrainer.Tests;

/// <summary>
/// Comparação com a melhor volta: o delta é integrado por distância, então precisa dar o
/// mesmo resultado qualquer que seja a cadência de quadros e sem depender do tempo de volta
/// que o jogo publica.
/// </summary>
public sealed class BestLapComparerTests
{
    private const double ReferenceSpeedMps = 20;
    private const double TrackLengthMeters = 1000;

    [Fact]
    public void SemReferencia_NaoCompara()
    {
        var comparer = new BestLapComparer();

        BestLapComparison comparison = comparer.Update(null, Connected(0, 0), 0);

        Assert.False(comparison.CanCompare);
        Assert.False(comparison.HasDelta);
        Assert.Contains("referência", comparison.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferenciaSemDistancia_Avisa()
    {
        var comparer = new BestLapComparer();
        ReferenceLap lap = ReferenceLap.Create("Sem distância", new[]
        {
            ReferenceSample.AtTime(0, 0, 100),
            ReferenceSample.AtTime(1, 50, 50),
            ReferenceSample.AtTime(2, 0, 100)
        });

        BestLapComparison comparison = comparer.Update(lap, Connected(200, 10), 10);

        Assert.False(comparison.CanCompare);
        Assert.Contains("distância", comparison.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CarroDesconectado_Avisa()
    {
        var comparer = new BestLapComparer();
        var car = new CarTelemetry(false, 0, 0, 0, 0, 0, 0, 5);

        BestLapComparison comparison = comparer.Update(BuildLap(), car, 5);

        Assert.False(comparison.CanCompare);
        Assert.Contains("posição", comparison.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CarroMaisLento_AcumulaDeltaPositivo()
    {
        var comparer = new BestLapComparer();
        ReferenceLap lap = BuildLap();

        BestLapComparison comparison = Drive(comparer, lap, ReferenceSpeedMps * 0.9);

        // 1000 m a 18 m/s contra 20 m/s: 55,6 s contra 50 s.
        Assert.True(comparison.CanCompare);
        Assert.True(comparison.HasDelta);
        Assert.InRange(comparison.DeltaSeconds, 5.2, 5.9);
        Assert.False(comparison.IsAhead);
        Assert.True(comparison.HasBestDelta);
        Assert.True(comparison.BestDeltaSeconds > 0);
    }

    [Fact]
    public void CarroMaisRapido_AcumulaDeltaNegativo()
    {
        var comparer = new BestLapComparer();
        ReferenceLap lap = BuildLap();

        BestLapComparison comparison = Drive(comparer, lap, ReferenceSpeedMps * 1.1);

        Assert.InRange(comparison.DeltaSeconds, -4.8, -4.2);
        Assert.True(comparison.IsAhead);
        Assert.True(comparison.BestDeltaSeconds < 0);
    }

    [Fact]
    public void CarroNaVelocidadeDaReferencia_ZeraDelta()
    {
        var comparer = new BestLapComparer();
        ReferenceLap lap = BuildLap();

        BestLapComparison comparison = Drive(comparer, lap, ReferenceSpeedMps);

        Assert.Equal(0, comparison.DeltaSeconds, 2);
        Assert.False(comparison.IsAhead);
    }

    [Fact]
    public void MesmaDistanciaComMenosQuadros_DaOMesmoDelta()
    {
        var coarse = new BestLapComparer();
        var fine = new BestLapComparer();
        ReferenceLap lap = BuildLap();

        double coarseDelta = Drive(coarse, lap, ReferenceSpeedMps * 0.95, steps: 100).DeltaSeconds;
        double fineDelta = Drive(fine, lap, ReferenceSpeedMps * 0.95, steps: 600).DeltaSeconds;

        Assert.Equal(coarseDelta, fineDelta, 2);
    }

    [Fact]
    public void VoltaNova_ZeraODelta_EGuardaAUltimaVolta()
    {
        var comparer = new BestLapComparer();
        ReferenceLap lap = BuildLap();

        BestLapComparison first = Drive(comparer, lap, ReferenceSpeedMps * 0.9, lapNumber: 3);
        Assert.False(first.HasLastLapDelta);

        BestLapComparison second = Drive(comparer, lap, ReferenceSpeedMps * 0.9, lapNumber: 4);

        Assert.True(second.HasLastLapDelta);
        Assert.InRange(second.LastLapDeltaSeconds, 5.2, 5.9);
        Assert.InRange(second.DeltaSeconds, 5.2, 5.9);
    }

    [Fact]
    public void MelhorDelta_GuardaOMaiorGanho()
    {
        var comparer = new BestLapComparer();
        ReferenceLap lap = BuildLap();

        // Primeiro quinto da volta mais rápido, o resto mais lento: o melhor delta fica
        // negativo e o delta final, positivo.
        BestLapComparison comparison = DriveMixed(comparer, lap);

        Assert.True(comparison.HasBestDelta);
        Assert.True(comparison.DeltaSeconds > 0);
        Assert.True(comparison.BestDeltaSeconds < 0);
        Assert.True(comparison.BestDeltaSeconds < comparison.DeltaSeconds);
    }

    [Fact]
    public void VoltaIdealDoCatalogo_TemDistanciaEPermiteComparar()
    {
        TrackDefinition track = TrackCatalog.Default;
        ReferenceLap lap = IdealLapFactory.Build(track);

        var comparer = new BestLapComparer();
        BestLapComparison comparison = comparer.Update(lap, Connected(0.25, 12), 12);

        Assert.True(lap.HasDistanceData);
        Assert.True(comparison.CanCompare);
        Assert.True(comparison.ReferenceDurationSeconds > 0);
        Assert.InRange(comparison.LapProgress, 0.24, 0.26);
        Assert.True(comparison.ReferenceSpeedKmh > 0);
    }

    private static ReferenceLap BuildLap(
        double speedMps = ReferenceSpeedMps,
        double lengthMeters = TrackLengthMeters,
        double stepMeters = 25)
    {
        var samples = new List<ReferenceSample>();

        for (double distance = 0; distance <= lengthMeters + 0.001; distance += stepMeters)
        {
            samples.Add(new ReferenceSample(distance / speedMps, 0, 100, distance, speedMps * 3.6));
        }

        return ReferenceLap.Create("Volta de teste", samples, lengthMeters);
    }

    private static CarTelemetry Connected(double lapPercent, double seconds) => new(
        true,
        ReferenceSpeedMps * 3.6,
        0,
        100,
        lapPercent,
        seconds,
        1,
        seconds);

    /// <summary>Volta inteira: 20% mais rápido que a referência, o resto mais lento.</summary>
    private static BestLapComparison DriveMixed(BestLapComparer comparer, ReferenceLap lap)
    {
        const int steps = 100;

        BestLapComparison last = BestLapComparison.None;
        double time = 0;
        double previousDistance = 0;

        for (int index = 0; index <= steps; index++)
        {
            double distance = TrackLengthMeters * index / steps;
            double speed = index <= 20 ? ReferenceSpeedMps * 1.2 : ReferenceSpeedMps * 0.8;

            time += (distance - previousDistance) / speed;
            previousDistance = distance;

            last = comparer.Update(
                lap,
                new CarTelemetry(true, speed * 3.6, 0, 100, distance / TrackLengthMeters, time, 1, time),
                time);
        }

        return last;
    }

    private static BestLapComparison Drive(
        BestLapComparer comparer,
        ReferenceLap lap,
        double speedMps,
        int steps = 100,
        int lapNumber = 1)
    {
        BestLapComparison last = BestLapComparison.None;

        for (int index = 0; index <= steps; index++)
        {
            double distance = TrackLengthMeters * index / steps;
            double seconds = distance / speedMps;

            var car = new CarTelemetry(
                true,
                speedMps * 3.6,
                0,
                100,
                distance / TrackLengthMeters,
                seconds,
                lapNumber,
                seconds);

            last = comparer.Update(lap, car, seconds);
        }

        return last;
    }
}
