using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Tests;

/// <summary>
/// Prévia da curva: o piloto precisa ver o desenho da próxima repetição antes de começar,
/// com os trechos descritos em texto e o mesmo alvo que o motor vai cobrar.
/// </summary>
public sealed class DrillCurvePreviewTests
{
    [Fact]
    public void Plan_ListsEverySegmentWithActionAndTarget()
    {
        DrillStep step = MakeStep(1, 40);
        var profile = new DrillProfile(step, 40);

        IReadOnlyList<DrillCurveStep> plan = DrillCurvePlan.Build(profile);

        Assert.Equal(3, plan.Count);

        Assert.Equal(DrillSegmentKind.Ramp, plan[0].Kind);
        Assert.Equal("SUBIR", plan[0].Action);
        Assert.Equal("até 40%", plan[0].TargetText);
        Assert.Equal(0.4, plan[0].Seconds, 3);
        Assert.Equal("Suba rápido até 40%", plan[0].Text);

        Assert.Equal(DrillSegmentKind.Hold, plan[1].Kind);
        Assert.Equal("SEGURAR", plan[1].Action);
        Assert.Equal("em 40%", plan[1].TargetText);

        Assert.Equal(DrillSegmentKind.Release, plan[2].Kind);
        Assert.Equal("SOLTAR", plan[2].Action);
        Assert.Equal("até 0%", plan[2].TargetText);
    }

    [Fact]
    public void Plan_SummaryShowsPeakAndTotalDuration()
    {
        DrillStep step = MakeStep(1, 40);
        var profile = new DrillProfile(step, 40);

        string summary = DrillCurvePlan.Summary(profile);

        Assert.Contains("pico 40%", summary);
        Assert.EndsWith("s", summary);
    }

    [Fact]
    public void Preview_OfRandomStep_UsesTheMiddleOfTheRange()
    {
        DrillStep step = MakeStep(1, 50);
        step.RandomTargetMinPercent = 20;
        step.RandomTargetMaxPercent = 80;

        DrillCurvePreview preview = Assert.IsType<DrillCurvePreview>(DrillCurvePreview.TryCreate(step));

        Assert.Equal(50, DrillCurvePreview.EstimateTarget(step), 3);
        Assert.Equal(50, preview.Profile.PeakPercent, 3);
        Assert.True(preview.Curve.Count > 10);
        Assert.Equal(3, preview.Plan.Count);
        Assert.Contains("pico 50%", preview.Summary);
    }

    [Fact]
    public void Preview_WithoutSegments_ReturnsNull()
    {
        var step = new DrillStep { TargetPercent = 50, Repetitions = 1 };

        Assert.Null(DrillCurvePreview.TryCreate(step));
    }

    [Fact]
    public void Armed_PublishesTheUpcomingCurveAndPlan()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(2, 40);

        Assert.True(runner.Start(Program(step), 0));

        DrillUpdate armed = runner.Latest;

        Assert.Equal(DrillStage.Armed, armed.Stage);
        Assert.True(armed.HasCurve, "a curva precisa estar publicada antes de começar");
        Assert.NotNull(armed.TargetCurve);
        Assert.Equal(3, armed.CurvePlan.Count);
        Assert.Equal(-1, armed.CurvePlanIndex);
        Assert.Contains("pico 40%", armed.CurveSummary);

        // O desenho da prévia tem que ser o mesmo alvo que a repetição vai cobrar.
        Assert.Equal(40, armed.TargetCurve!.Max(point => point.Percent), 3);
        Assert.Equal(40, armed.TargetLevelPercent, 3);
    }

    [Fact]
    public void Running_MarksTheSegmentThatIsBeingExecuted()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(1, 40);
        var profile = new DrillProfile(step, 40);

        Assert.True(runner.Start(Program(step), 0));
        runner.Update(new PedalReading(0, 0, 0.1), 0.1);
        runner.Update(new PedalReading(10, 0, 0.2), 0.2);   // dispara a repetição aqui

        DrillUpdate ramp = runner.Update(new PedalReading(20, 0, 0.4), 0.4);
        Assert.Equal(DrillStage.Running, ramp.Stage);
        Assert.Equal(0, ramp.CurvePlanIndex);

        double holdSeconds = 0.2 + profile.SegmentSeconds(0) + 0.1;
        runner.Update(new PedalReading(40, 0, holdSeconds), holdSeconds);

        Assert.Equal(1, runner.Latest.CurvePlanIndex);
    }

    [Fact]
    public void LastRepetitionOfStep_AnnouncesTheStepThatChangesTheCurve()
    {
        var runner = new DrillRunner();

        var program = new DrillProgram { Name = "duas etapas", RestPercent = 4 };
        program.Steps.Add(MakeStep(2, 20));
        program.Steps.Add(MakeStep(1, 60));

        Assert.True(runner.Start(program, 0));

        Assert.Equal(string.Empty, runner.Latest.NextStepLabel);

        // Ainda falta uma repetição desta etapa: a curva não muda depois dela.
        runner.Skip(0.1);

        Assert.Equal(DrillStage.Armed, runner.Stage);
        Assert.Equal("teste 60%", runner.Latest.NextStepLabel);
        Assert.Equal(20, runner.Latest.TargetLevelPercent, 3);

        // Última repetição da etapa: o aviso já aponta a próxima curva.
        runner.Skip(0.2);

        Assert.Equal(DrillStage.Armed, runner.Stage);
        Assert.Equal(string.Empty, runner.Latest.NextStepLabel);
        Assert.Equal(60, runner.Latest.TargetLevelPercent, 3);
    }

    [Fact]
    public void StoppedProgram_ClearsTheCurveAndThePlan()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(2, 40);

        Assert.True(runner.Start(Program(step), 0));
        Assert.NotEmpty(runner.Latest.CurvePlan);

        runner.Stop(1.0);

        Assert.Equal(DrillStage.Idle, runner.Latest.Stage);
        Assert.False(runner.Latest.HasCurve);
        Assert.Empty(runner.Latest.CurvePlan);
        Assert.Equal(string.Empty, runner.Latest.CurveSummary);
    }

    private static DrillStep MakeStep(int repetitions, double target)
    {
        var step = new DrillStep
        {
            Label = $"teste {target:0}%",
            Pedal = DrillPedal.Brake,
            TargetPercent = target,
            TolerancePercent = 4,
            Repetitions = repetitions
        };

        step.Segments.Add(DrillSegment.Ramp(0.4));
        step.Segments.Add(DrillSegment.Hold(0.6));
        step.Segments.Add(DrillSegment.Release(3.0));

        return step;
    }

    private static DrillProgram Program(DrillStep step)
    {
        var program = new DrillProgram { Name = "programa de teste", RestPercent = 4 };
        program.Steps.Add(step);
        return program;
    }
}
