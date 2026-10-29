using PedalTrainer.Core.Drills;

namespace PedalTrainer.Tests;

/// <summary>
/// Programas de treino embutidos, curvas-alvo e o formato de arquivo JSON que o piloto
/// pode editar à mão.
/// </summary>
public sealed class DrillProgramTests
{
    [Fact]
    public void BrakeProgram_GoesThrough20_50_80_AndRandomBlock()
    {
        DrillProgram program = DrillProgramCatalog.CreateBrakeProgressive();

        Assert.True(program.TryValidate(out string error), error);
        Assert.Equal(4, program.Steps.Count);
        Assert.Equal(20, program.Steps[0].TargetPercent, 3);
        Assert.Equal(50, program.Steps[1].TargetPercent, 3);
        Assert.Equal(80, program.Steps[2].TargetPercent, 3);
        Assert.Equal(10, program.Steps[0].Repetitions);

        DrillStep random = program.Steps[3];
        Assert.True(random.IsRandom);
        Assert.Equal(20, random.RandomTargetMinPercent!.Value, 3);
        Assert.Equal(85, random.RandomTargetMaxPercent!.Value, 3);
        Assert.Equal(42, program.TotalRepetitions);
        Assert.All(program.Steps, step => Assert.Equal(DrillPedal.Brake, step.Pedal));
    }

    [Fact]
    public void BrakeSteps_PressFirmlyAndReleaseSlowly()
    {
        DrillProgram program = DrillProgramCatalog.CreateBrakeProgressive();

        foreach (DrillStep step in program.Steps)
        {
            Assert.Equal(DrillSegmentKind.Ramp, step.Segments[0].Kind);
            Assert.Equal(DrillSegmentKind.Hold, step.Segments[1].Kind);
            Assert.Equal(DrillSegmentKind.Release, step.Segments[2].Kind);

            double pressRate = DrillInstructor.Rate(0, step.Segments[0].ToPercent ?? step.TargetPercent, step.Segments[0].Seconds);
            double releaseRate = DrillInstructor.Rate(step.TargetPercent, 0, step.Segments[2].Seconds);

            Assert.True(releaseRate < pressRate, $"soltura mais rápida que a pisada na etapa {step.Label}");
        }
    }

    [Fact]
    public void ThrottleProgram_StartsWithFastBlipThenGradualThenProgressiveTo100()
    {
        DrillProgram program = DrillProgramCatalog.CreateThrottleProgressive();

        Assert.True(program.TryValidate(out string error), error);
        Assert.Equal(4, program.Steps.Count);
        Assert.All(program.Steps, step => Assert.Equal(DrillPedal.Throttle, step.Pedal));

        DrillStep fast = program.Steps[0];
        DrillStep gradual = program.Steps[1];
        DrillStep progressive = program.Steps[2];
        DrillStep eighty = program.Steps[3];

        Assert.Equal(20, fast.TargetPercent, 3);
        Assert.True(fast.PressRatePercentPerSecond >= DrillInstructor.FastRate);
        Assert.Equal("rápido", DrillInstructor.PressPace(fast));

        Assert.Equal(20, gradual.TargetPercent, 3);
        Assert.True(gradual.PressRatePercentPerSecond < DrillInstructor.ModerateRate);
        Assert.Equal("gradualmente", DrillInstructor.PressPace(gradual));

        Assert.Equal(50, progressive.Segments[0].ToPercent!.Value, 3);
        Assert.Equal(100, progressive.Segments[2].ToPercent ?? progressive.TargetPercent, 3);
        Assert.Equal(100, progressive.TargetRange().Max, 3);

        Assert.Equal(80, eighty.TargetPercent, 3);
        Assert.Equal(2.0, eighty.Segments[1].Seconds, 3);
    }

    [Fact]
    public void Profile_FollowsRampHoldAndRelease()
    {
        DrillStep step = DrillProgramCatalog.CreateBrakeProgressive().Steps[0];
        var profile = new DrillProfile(step, step.TargetPercent);

        Assert.Equal(0, profile.TargetAt(0), 3);
        Assert.Equal(10, profile.TargetAt(0.5), 3);
        Assert.Equal(20, profile.TargetAt(1.0), 3);
        Assert.Equal(20, profile.TargetAt(1.4), 3);
        Assert.Equal(10, profile.TargetAt(3.05), 1);
        Assert.Equal(0, profile.TargetAt(profile.DurationSeconds), 3);
        Assert.Equal(20, profile.PeakPercent, 3);
    }

    [Fact]
    public void Profile_UsesDrawnTargetWhenSegmentHasNoExplicitPercent()
    {
        DrillStep step = DrillProgramCatalog.CreateBrakeProgressive().Steps[3];
        var profile = new DrillProfile(step, 65);

        Assert.Equal(65, profile.TargetPercent, 3);
        Assert.Equal(65, profile.PeakPercent, 3);
        Assert.Equal(65, profile.TargetAt(1.5), 3);
    }

    [Fact]
    public void TargetCurve_StartsAtZeroAndEndsAtRest()
    {
        DrillStep step = DrillProgramCatalog.CreateThrottleProgressive().Steps[2];
        var profile = new DrillProfile(step, step.TargetPercent);

        IReadOnlyList<DrillCurvePoint> curve = DrillTargetCurveBuilder.Build(profile);

        Assert.True(curve.Count > 20);
        Assert.Equal(0, curve[0].TimeSeconds, 3);
        Assert.Equal(profile.DurationSeconds, curve[^1].TimeSeconds, 3);
        Assert.Equal(100, curve.Max(point => point.Percent), 3);
        Assert.Equal(0, curve[^1].Percent, 3);
    }

    [Fact]
    public void Instruction_DescribesEachSegmentInPortuguese()
    {
        DrillStep step = DrillProgramCatalog.CreateBrakeProgressive().Steps[0];
        var profile = new DrillProfile(step, 20);

        Assert.Equal("Suba firme até 20%", DrillInstructor.DescribeSegment(profile, 0));
        Assert.Equal("Segure em 20% por 0.8 s", DrillInstructor.DescribeSegment(profile, 1));
        Assert.Equal("Solte gradualmente até 0%", DrillInstructor.DescribeSegment(profile, 2));
        Assert.Equal("FREIO · Suba firme até 20%", DrillInstructor.DescribeWithPedal(profile, 0));
    }

    [Fact]
    public void Json_RoundTripsSegmentsTargetsAndRandomRange()
    {
        DrillProgram original = DrillProgramCatalog.CreateBrakeProgressive();

        string json = DrillProgramFile.ToJson(original);

        Assert.Contains("freio", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(DrillProgramFile.TryParse(json, out DrillProgram? parsed, out string error), error);
        Assert.NotNull(parsed);
        Assert.Equal(original.Name, parsed!.Name);
        Assert.Equal(original.Steps.Count, parsed.Steps.Count);

        for (var index = 0; index < original.Steps.Count; index++)
        {
            DrillStep expected = original.Steps[index];
            DrillStep actual = parsed.Steps[index];

            Assert.Equal(expected.TargetPercent, actual.TargetPercent, 3);
            Assert.Equal(expected.Repetitions, actual.Repetitions);
            Assert.Equal(expected.Segments.Count, actual.Segments.Count);
            Assert.Equal(expected.IsRandom, actual.IsRandom);

            for (var segment = 0; segment < expected.Segments.Count; segment++)
            {
                Assert.Equal(expected.Segments[segment].Kind, actual.Segments[segment].Kind);
                Assert.Equal(expected.Segments[segment].Seconds, actual.Segments[segment].Seconds, 3);
                Assert.Equal(expected.Segments[segment].ToPercent, actual.Segments[segment].ToPercent);
            }
        }
    }

    [Fact]
    public void Json_RejectsProgramWithoutSteps()
    {
        const string json = "{ \"name\": \"vazio\", \"steps\": [] }";

        Assert.False(DrillProgramFile.TryParse(json, out DrillProgram? program, out string error));
        Assert.Null(program);
        Assert.Contains("nenhuma etapa", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Json_RejectsTargetAboveFullScale()
    {
        const string json = """
            {
              "name": "alvo inválido",
              "steps": [
                {
                  "label": "errado",
                  "pedal": "brake",
                  "targetPercent": 140,
                  "repetitions": 1,
                  "segments": [ { "kind": "ramp", "seconds": 1, "toPercent": 140 } ]
                }
              ]
            }
            """;

        Assert.False(DrillProgramFile.TryParse(json, out _, out string error));
        Assert.Contains("Etapa 1", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Store_SlugBuildsSafeFileNames()
    {
        Assert.Equal("freio-20-50-80", DrillProgramStore.Slug("Freio — 20 / 50 / 80"));
        Assert.Equal("programa", DrillProgramStore.Slug("   "));
        Assert.Equal("aceleracao-progressiva", DrillProgramStore.Slug("Aceleração progressiva"));
    }

    [Fact]
    public void Step_WithoutSegments_IsInvalid()
    {
        var step = new DrillStep { TargetPercent = 50, Repetitions = 1 };

        Assert.False(step.TryValidate(out string error));
        Assert.Contains("segmento", error, StringComparison.OrdinalIgnoreCase);
    }
}
