using PedalTrainer.Core.Models;
using PedalTrainer.Core.Recording;
using PedalTrainer.Core.Reference;

namespace PedalTrainer.Tests;

/// <summary>
/// O gravador é o caminho para o piloto criar a própria referência a partir de uma volta
/// do simulador conectado (iRacing, ACC, AC EVO, AMS2 ou LMU).
/// </summary>
public sealed class LapRecorderTests
{
    private const double SampleSpeedKmh = 200.0;
    private const double SampleIntervalSeconds = 0.4;
    private const double PercentPerSample = 0.02;
    private const int StepsPerLap = 50;

    [Fact]
    public void Observe_RecordsOneFullLapBetweenLineCrossings()
    {
        var recorder = new LapRecorder();
        double sessionTime = 0;

        recorder.Start();
        FeedLap(recorder, lapNumber: 1, ref sessionTime);

        Assert.True(recorder.IsActive);
        Assert.False(recorder.IsRecording);

        FeedLap(recorder, lapNumber: 2, ref sessionTime);

        Assert.True(recorder.IsRecording);
        Assert.True(recorder.SampleCount > 40);

        FeedLap(recorder, lapNumber: 3, ref sessionTime);

        Assert.True(recorder.TryTakeCompletedLap(out ReferenceLap lap));
        Assert.InRange(lap.Samples.Count, 40, 60);
        Assert.InRange(lap.DurationSeconds, 15.0, 25.0);
        Assert.InRange(lap.TrackLengthMeters, 1000.0, 1300.0);
        Assert.True(lap.HasDistanceData);
    }

    [Fact]
    public void Observe_KeepsDistanceMonotonicAcrossTheLap()
    {
        var recorder = new LapRecorder();
        double sessionTime = 0;

        recorder.Start();
        FeedLap(recorder, lapNumber: 1, ref sessionTime);
        FeedLap(recorder, lapNumber: 2, ref sessionTime);
        FeedLap(recorder, lapNumber: 3, ref sessionTime);

        Assert.True(recorder.TryTakeCompletedLap(out ReferenceLap lap));

        double previous = -1;
        foreach (ReferenceSample sample in lap.Samples)
        {
            Assert.True(sample.DistanceMeters >= previous);
            previous = sample.DistanceMeters;
        }
    }

    [Fact]
    public void Observe_IgnoresRepeatedTelemetryFrames()
    {
        var recorder = new LapRecorder();
        double sessionTime = 0;

        recorder.Start();
        FeedLap(recorder, lapNumber: 1, ref sessionTime);
        FeedLap(recorder, lapNumber: 2, ref sessionTime);

        int before = recorder.SampleCount;
        CarTelemetry repeated = Sample(lapNumber: 2, lapPercent: 0.5, sessionTime: sessionTime - 1);
        recorder.Observe(repeated);
        recorder.Observe(repeated);

        Assert.Equal(before, recorder.SampleCount);
    }

    [Fact]
    public void Observe_WithoutSimulator_AbortsRecording()
    {
        var recorder = new LapRecorder();

        recorder.Start();
        recorder.Observe(CarTelemetry.Disconnected(0));

        Assert.False(recorder.IsActive);
        Assert.Contains("publicando telemetria", recorder.StatusMessage);
    }

    [Fact]
    public void Observe_WithoutSimulator_NamesTheSimulatorInTheMessage()
    {
        var recorder = new LapRecorder { SimulatorName = "Assetto Corsa Competizione" };

        recorder.Start();
        recorder.Observe(CarTelemetry.Disconnected(0));

        Assert.Contains("Assetto Corsa Competizione", recorder.StatusMessage);
    }

    [Fact]
    public void Observe_IncompleteLap_IsDiscarded()
    {
        var recorder = new LapRecorder();
        double sessionTime = 0;

        recorder.Start();
        FeedLap(recorder, lapNumber: 1, ref sessionTime);
        FeedLap(recorder, lapNumber: 2, ref sessionTime, steps: 20);

        // Nova passagem pela linha com a volta anterior incompleta.
        FeedLap(recorder, lapNumber: 3, ref sessionTime, steps: 1);

        Assert.False(recorder.TryTakeCompletedLap(out _));
        Assert.Contains("descartada", recorder.StatusMessage);
    }

    [Fact]
    public void Cancel_DiscardsTheLapInProgress()
    {
        var recorder = new LapRecorder();
        double sessionTime = 0;

        recorder.Start();
        FeedLap(recorder, lapNumber: 1, ref sessionTime);
        FeedLap(recorder, lapNumber: 2, ref sessionTime);

        recorder.Cancel();

        Assert.False(recorder.IsActive);
        Assert.False(recorder.TryTakeCompletedLap(out _));
        Assert.Equal("Gravação cancelada.", recorder.StatusMessage);
    }

    [Fact]
    public void RecordedLap_SurvivesCsvRoundTripWithBrakingZones()
    {
        var recorder = new LapRecorder();
        double sessionTime = 0;

        recorder.Start();
        FeedLap(recorder, lapNumber: 1, ref sessionTime, injectBraking: true);
        FeedLap(recorder, lapNumber: 2, ref sessionTime, injectBraking: true);
        FeedLap(recorder, lapNumber: 3, ref sessionTime, injectBraking: true);

        Assert.True(recorder.TryTakeCompletedLap(out ReferenceLap recorded));

        string path = Path.Combine(Path.GetTempPath(), $"pedaltrainer-{Guid.NewGuid():N}.csv");

        try
        {
            ReferenceLapCsvWriter.WriteFile(recorded, path);

            var loader = new ReferenceLapFileLoader();
            Assert.True(loader.TryLoad(path, out ReferenceLap? loaded, out string? error), error);
            Assert.NotNull(loaded);

            Assert.InRange(loaded.Samples.Count, recorded.Samples.Count - 2, recorded.Samples.Count + 2);
            Assert.InRange(loaded.DurationSeconds, recorded.DurationSeconds - 0.1, recorded.DurationSeconds + 0.1);

            IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(loaded);
            Assert.NotEmpty(zones);
            Assert.All(zones, zone => Assert.True(zone.PeakBrakePercent >= 5));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DemoLapCsv_RoundTripsThroughTheSameWriter()
    {
        ReferenceLap demo = DemoReferenceLapFactory.Create();
        string path = Path.Combine(Path.GetTempPath(), $"pedaltrainer-demo-{Guid.NewGuid():N}.csv");

        try
        {
            ReferenceLapCsvWriter.WriteFile(demo, path);

            var loader = new ReferenceLapFileLoader();
            Assert.True(loader.TryLoad(path, out ReferenceLap? loaded, out string? error), error);
            Assert.NotNull(loaded);

            Assert.Equal(8, BrakingZoneExtractor.Extract(loaded).Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Alimenta uma volta completa (0% a 98%) com passos fixos de tempo, distância e percentual.
    /// </summary>
    private static void FeedLap(
        LapRecorder recorder,
        int lapNumber,
        ref double sessionTime,
        int steps = StepsPerLap,
        bool injectBraking = false)
    {
        for (int step = 0; step <= steps; step++)
        {
            double percent = Math.Min(0.98, step * PercentPerSample);
            double brake = injectBraking && step % 10 < 3 ? 80 : 0;

            recorder.Observe(Sample(lapNumber, percent, sessionTime, brake));
            sessionTime += SampleIntervalSeconds;
        }
    }

    private static CarTelemetry Sample(
        int lapNumber,
        double lapPercent,
        double sessionTime,
        double brakePercent = 0)
    {
        return new CarTelemetry(
            IsConnected: true,
            SpeedKmh: SampleSpeedKmh,
            BrakePercent: brakePercent,
            ThrottlePercent: brakePercent > 0 ? 0 : 100,
            LapDistancePercent: lapPercent,
            SessionTimeSeconds: sessionTime,
            LapNumber: lapNumber,
            TimestampSeconds: 0);
    }
}
