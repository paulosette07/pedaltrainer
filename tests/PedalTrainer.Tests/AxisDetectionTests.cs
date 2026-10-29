using PedalTrainer.Core.Input;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Tests;

/// <summary>
/// A Simsonn expõe os pedais como eixos genéricos; o app descobre qual é qual
/// pelo movimento durante o assistente de calibração.
/// </summary>
public sealed class AxisDetectionTests
{
    [Fact]
    public void Tracker_LearnsObservedRange()
    {
        var tracker = new AxisRangeTracker(minimumSpan: 200);
        tracker.Observe(12000);
        tracker.Observe(4000);
        tracker.Observe(58000);

        Assert.Equal(4000, tracker.Min);
        Assert.Equal(58000, tracker.Max);
        Assert.Equal(54000, tracker.Span);
        Assert.True(tracker.HasUsableRange);
    }

    [Fact]
    public void Tracker_WithoutMovementHasNoUsableRange()
    {
        var tracker = new AxisRangeTracker(minimumSpan: 200);
        for (int i = 0; i < 20; i++)
        {
            tracker.Observe(32000 + (i % 3));
        }

        Assert.False(tracker.HasUsableRange);
    }

    [Fact]
    public void Tracker_ToCalibratedRange_AddsMargin()
    {
        var tracker = new AxisRangeTracker(minimumSpan: 200);
        tracker.Seed(1000, 11000);

        (int min, int max) = tracker.ToCalibratedRange(marginPercent: 5);

        Assert.Equal(500, min);
        Assert.Equal(11500, max);
    }

    [Fact]
    public void Tracker_ResetClearsRange()
    {
        var tracker = new AxisRangeTracker();
        tracker.Seed(100, 200);
        tracker.Reset();

        Assert.False(tracker.HasUsableRange);
        Assert.Equal(0, tracker.Span);
    }

    [Fact]
    public void Detector_FindsAxisWithMostMovement()
    {
        var frames = new List<RawAxesSnapshot>
        {
            Frame(new[] { 32000, 3000, 31000 }),
            Frame(new[] { 32100, 60000, 31100 }),
            Frame(new[] { 32050, 8000, 30900 })
        };

        int axis = PedalAxisDetector.DetectDominantAxis(frames);

        Assert.Equal(1, axis);
    }

    [Fact]
    public void Detector_SkipsExcludedAxis()
    {
        var frames = new List<RawAxesSnapshot>
        {
            Frame(new[] { 3000, 31000 }),
            Frame(new[] { 60000, 31100 }),
            Frame(new[] { 9000, 30950 })
        };

        // O eixo 0 se move muito, mas está excluído; o eixo 1 oscila menos que o mínimo aceito.
        int axis = PedalAxisDetector.DetectDominantAxis(frames, excludedAxis: 0);

        Assert.Equal(-1, axis);
    }

    [Fact]
    public void Detector_ReturnsMinusOneWhenNothingMoves()
    {
        var frames = new List<RawAxesSnapshot>
        {
            Frame(new[] { 32000, 15000 }),
            Frame(new[] { 32010, 15040 })
        };

        Assert.Equal(-1, PedalAxisDetector.DetectDominantAxis(frames));
        Assert.Equal(-1, PedalAxisDetector.DetectDominantAxis(Array.Empty<RawAxesSnapshot>()));
    }

    [Fact]
    public void MeasureSpan_UsesOnlyRequestedAxis()
    {
        var frames = new List<RawAxesSnapshot>
        {
            Frame(new[] { 0, 100 }),
            Frame(new[] { 1000, 150 })
        };

        Assert.Equal(1000, PedalAxisDetector.MeasureSpan(frames, 0));
        Assert.Equal(50, PedalAxisDetector.MeasureSpan(frames, 1));
        Assert.Equal(0, PedalAxisDetector.MeasureSpan(frames, 9));
    }

    private static RawAxesSnapshot Frame(int[] axes) => new(axes, 0);
}
