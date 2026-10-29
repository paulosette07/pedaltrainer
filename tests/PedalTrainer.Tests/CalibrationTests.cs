using PedalTrainer.Core.Models;

namespace PedalTrainer.Tests;

/// <summary>
/// A conversão bruto → percentual é o coração da calibração da Load Cell:
/// se ela erra, todo o treino erra junto.
/// </summary>
public sealed class CalibrationTests
{
    private const int Min = 1000;
    private const int Max = 65000;

    [Theory]
    [InlineData(Min, 0)]
    [InlineData(Max, 100)]
    [InlineData(33000, 50)]
    public void AxisToPercent_MapsLinearRange(int raw, double expected)
    {
        double percent = PedalCalibration.AxisToPercent(raw, Min, Max, invert: false, deadzonePercent: 0, exponent: 1);

        Assert.Equal(expected, percent, 1);
    }

    [Fact]
    public void AxisToPercent_ClampsOutsideRange()
    {
        Assert.Equal(0, PedalCalibration.AxisToPercent(-5000, Min, Max, false, 0, 1), 3);
        Assert.Equal(100, PedalCalibration.AxisToPercent(90000, Min, Max, false, 0, 1), 3);
    }

    [Fact]
    public void AxisToPercent_InvertsWhenRequested()
    {
        double atMin = PedalCalibration.AxisToPercent(Min, Min, Max, invert: true, deadzonePercent: 0, exponent: 1);
        double atMax = PedalCalibration.AxisToPercent(Max, Min, Max, invert: true, deadzonePercent: 0, exponent: 1);

        Assert.Equal(100, atMin, 3);
        Assert.Equal(0, atMax, 3);
    }

    [Fact]
    public void AxisToPercent_AppliesDeadzone()
    {
        // 33000 = 50% de curso; com 10% de zona morta sobra (0.5 - 0.1) / 0.9 = 44.44%.
        double percent = PedalCalibration.AxisToPercent(33000, Min, Max, false, deadzonePercent: 10, exponent: 1);

        Assert.Equal(44.444, percent, 2);
    }

    [Fact]
    public void AxisToPercent_ExponentShapesResponse()
    {
        double linear = PedalCalibration.AxisToPercent(33000, Min, Max, false, 0, exponent: 1);
        double shaped = PedalCalibration.AxisToPercent(33000, Min, Max, false, 0, exponent: 2);

        Assert.True(shaped < linear, "expoente > 1 deve comprimir a parte inicial do curso");
        Assert.Equal(25, shaped, 1);
    }

    [Fact]
    public void AxisToPercent_SpanTooSmallReturnsZero()
    {
        double percent = PedalCalibration.AxisToPercent(500, 490, 500, false, 0, 1);

        Assert.Equal(0, percent, 3);
    }

    [Fact]
    public void BrakePercent_UsesConfiguredAxisAndInversion()
    {
        var calibration = new PedalCalibration
        {
            BrakeAxis = 1,
            BrakeMin = 0,
            BrakeMax = 65535,
            InvertBrake = true,
            BrakeDeadzonePercent = 0,
            BrakeExponent = 1
        };

        var raw = new RawAxesSnapshot(new[] { 1000, 32768, 0, 0, 0, 0, 0, 0 }, 0);
        double brake = calibration.BrakePercent(raw);

        Assert.Equal(50, brake, 1);
    }

    [Fact]
    public void Clone_CopiesEveryField()
    {
        var original = new PedalCalibration
        {
            DeviceName = "Simsonn",
            BrakeAxis = 2,
            ThrottleAxis = 5,
            BrakeMin = 1200,
            BrakeMax = 60000,
            BrakeDeadzonePercent = 4,
            BrakeExponent = 1.4,
            InvertThrottle = true,
            IsCalibrated = true
        };

        PedalCalibration copy = original.Clone();

        Assert.NotSame(original, copy);
        Assert.Equal(original.BrakeAxis, copy.BrakeAxis);
        Assert.Equal(original.ThrottleAxis, copy.ThrottleAxis);
        Assert.Equal(original.BrakeMin, copy.BrakeMin);
        Assert.Equal(original.BrakeMax, copy.BrakeMax);
        Assert.Equal(original.BrakeDeadzonePercent, copy.BrakeDeadzonePercent);
        Assert.Equal(original.BrakeExponent, copy.BrakeExponent);
        Assert.True(copy.InvertThrottle);
        Assert.True(copy.IsCalibrated);
    }
}
