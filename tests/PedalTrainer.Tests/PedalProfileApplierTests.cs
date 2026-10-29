using PedalTrainer.Core.Input.Pedals;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Tests;

/// <summary>
/// Aplicar um modelo não pode apagar a faixa bruta medida naquele exemplar de hardware:
/// só curva, zona morta e filtro devem mudar.
/// </summary>
public sealed class PedalProfileApplierTests
{
    private static PedalCalibration Calibrated() => new()
    {
        DeviceGuid = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        DeviceName = "Simsonn Pro",
        BrakeAxis = 2,
        ThrottleAxis = 5,
        BrakeMin = 1200,
        BrakeMax = 61000,
        ThrottleMin = 400,
        ThrottleMax = 64000,
        InvertBrake = true,
        BrakeDeadzonePercent = 9,
        BrakeExponent = 1.9,
        SmoothingAlpha = 0.2,
        IsCalibrated = true
    };

    [Fact]
    public void Apply_OverwritesTuningButKeepsMeasuredRanges()
    {
        PedalModel model = PedalModelCatalog.Find("fanatec-csl-elite-v2");
        PedalCalibration original = Calibrated();

        PedalCalibration applied = PedalProfileApplier.Apply(model, original);

        Assert.NotSame(original, applied);
        Assert.Equal(2, applied.BrakeAxis);
        Assert.Equal(5, applied.ThrottleAxis);
        Assert.Equal(1200, applied.BrakeMin);
        Assert.Equal(61000, applied.BrakeMax);
        Assert.Equal(400, applied.ThrottleMin);
        Assert.Equal(64000, applied.ThrottleMax);
        Assert.True(applied.InvertBrake);
        Assert.True(applied.IsCalibrated);
        Assert.Equal(original.DeviceName, applied.DeviceName);
        Assert.Equal(original.DeviceGuid, applied.DeviceGuid);
    }

    [Fact]
    public void Apply_CopiesModelTuningAndMarksTheModel()
    {
        PedalModel model = PedalModelCatalog.Find("logitech-g29");

        PedalCalibration applied = PedalProfileApplier.Apply(model, Calibrated());

        Assert.Equal(model.RecommendedBrakeExponent, applied.BrakeExponent, 3);
        Assert.Equal(model.RecommendedThrottleExponent, applied.ThrottleExponent, 3);
        Assert.Equal(model.RecommendedBrakeDeadzonePercent, applied.BrakeDeadzonePercent, 3);
        Assert.Equal(model.RecommendedThrottleDeadzonePercent, applied.ThrottleDeadzonePercent, 3);
        Assert.Equal(model.RecommendedSmoothingAlpha, applied.SmoothingAlpha, 3);
        Assert.Equal(model.Id, applied.PedalModelId);
    }

    [Fact]
    public void ApplyWithoutCurrentCalibration_StartsFromDefaults()
    {
        PedalModel model = PedalModelCatalog.Find("heusinkveld-sprint");

        PedalCalibration applied = PedalProfileApplier.Apply(model, null);

        Assert.False(applied.IsCalibrated);
        Assert.Equal(model.RecommendedBrakeExponent, applied.BrakeExponent, 3);
        Assert.Equal(model.Id, applied.PedalModelId);
    }

    [Fact]
    public void ApplyCatchAll_KeepsTuningAndClearsTheModel()
    {
        PedalCalibration current = PedalProfileApplier.Apply(PedalModelCatalog.Find("moza-crp"), Calibrated());

        PedalCalibration cleared = PedalProfileApplier.Apply(PedalModelCatalog.Unknown, current);

        Assert.Equal(current.BrakeExponent, cleared.BrakeExponent, 3);
        Assert.Equal(current.SmoothingAlpha, cleared.SmoothingAlpha, 3);
        Assert.Equal(string.Empty, cleared.PedalModelId);
    }

    [Fact]
    public void Matches_IsTrueRightAfterApplyingAndFalseWhenTuningWasHandEdited()
    {
        PedalModel model = PedalModelCatalog.Find("thrustmaster-tlcm");
        PedalCalibration applied = PedalProfileApplier.Apply(model, Calibrated());

        Assert.True(PedalProfileApplier.Matches(model, applied));

        applied.BrakeExponent += 0.25;

        Assert.False(PedalProfileApplier.Matches(model, applied));
    }

    [Fact]
    public void Matches_IsAlwaysFalseForTheCatchAllModel()
    {
        Assert.False(PedalProfileApplier.Matches(PedalModelCatalog.Unknown, Calibrated()));
    }
}
