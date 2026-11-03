using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Tests;

/// <summary>
/// Curva seguinte: o piloto precisa ver o desenho da próxima repetição o tempo todo, e não
/// só entre uma repetição e outra. Cobre a escolha da curva (mesma etapa ou etapa seguinte)
/// e a publicação dela pelo motor durante a execução.
/// </summary>
public sealed class DrillUpcomingCurveTests
{
    [Fact]
    public void NextRepetitionOfTheSameStep_ShowsTheSameCurve()
    {
        DrillProgram program = Program(MakeStep("subida", 3, 40));

        DrillUpcomingCurve upcoming = DrillUpcomingCurveFactory.Build(program, 0, 0);

        Assert.Equal(DrillUpcomingKind.NextRepetition, upcoming.Kind);
        Assert.Equal("PRÓXIMA REPETIÇÃO", upcoming.Title);
        Assert.Equal(2, upcoming.RepetitionNumber);
        Assert.Equal(3, upcoming.RepetitionCount);
        Assert.True(upcoming.HasCurve);
        Assert.Equal(40, Peak(upcoming), 3);
        Assert.Contains("mesma curva", upcoming.Detail);
        Assert.Contains("alvo 40%", upcoming.TargetText);
        Assert.True(upcoming.Plan.Count > 0);
    }

    [Fact]
    public void LastRepetitionOfAStep_ShowsTheCurveOfTheNextStep()
    {
        DrillProgram program = Program(MakeStep("etapa um", 2, 20), MakeStep("etapa dois", 2, 70));

        DrillUpcomingCurve upcoming = DrillUpcomingCurveFactory.Build(program, 0, 1);

        Assert.Equal(DrillUpcomingKind.NextStep, upcoming.Kind);
        Assert.Equal("PRÓXIMA ETAPA", upcoming.Title);
        Assert.Equal(2, upcoming.StepNumber);
        Assert.Equal(2, upcoming.StepCount);
        Assert.Equal(1, upcoming.RepetitionNumber);
        Assert.Equal("ETAPA 2/2", upcoming.Badge);
        Assert.True(upcoming.HasCurve);
        Assert.Equal(70, Peak(upcoming), 3);
    }

    [Fact]
    public void LastRepetitionOfTheProgram_ReportsTheEndWithoutACurve()
    {
        DrillProgram program = Program(MakeStep("única", 1, 50));

        DrillUpcomingCurve upcoming = DrillUpcomingCurveFactory.Build(program, 0, 0);

        Assert.Equal(DrillUpcomingKind.ProgramEnd, upcoming.Kind);
        Assert.Equal("FIM DO PROGRAMA", upcoming.Title);
        Assert.False(upcoming.HasCurve);
        Assert.False(string.IsNullOrWhiteSpace(upcoming.Detail));
    }

    [Fact]
    public void EmptyProgram_ReturnsNothingToShow()
    {
        var program = new DrillProgram { Name = "vazio" };

        Assert.Equal(DrillUpcomingKind.None, DrillUpcomingCurveFactory.Build(program, 0, 0).Kind);
        Assert.Equal(DrillUpcomingKind.None, DrillUpcomingCurveFactory.BuildFirst(program).Kind);
    }

    [Fact]
    public void BuildFirst_ReturnsTheOpeningCurveOfTheProgram()
    {
        DrillProgram program = Program(MakeStep("abertura", 4, 35));

        DrillUpcomingCurve upcoming = DrillUpcomingCurveFactory.BuildFirst(program);

        Assert.Equal(DrillUpcomingKind.FirstRepetition, upcoming.Kind);
        Assert.Equal("PRIMEIRA CURVA", upcoming.Title);
        Assert.Equal(1, upcoming.RepetitionNumber);
        Assert.Equal(35, Peak(upcoming), 3);
    }

    [Fact]
    public void RandomStep_MarksTheUpcomingCurveAsEstimated()
    {
        DrillStep step = MakeStep("sorteada", 4, 50);
        step.RandomTargetMinPercent = 20;
        step.RandomTargetMaxPercent = 80;

        DrillUpcomingCurve upcoming = DrillUpcomingCurveFactory.Build(Program(step), 0, 0);

        Assert.Equal(DrillUpcomingKind.NextRepetition, upcoming.Kind);
        Assert.True(upcoming.IsTargetEstimated);
        Assert.Equal(50, Peak(upcoming), 3);
        Assert.Contains("sorteado", upcoming.TargetText);
    }

    [Fact]
    public void StepWithoutSegments_IsSkipped()
    {
        var broken = new DrillStep { Label = "quebrada", Repetitions = 2 };
        DrillProgram program = Program(MakeStep("válida", 1, 60), broken);

        DrillUpcomingCurve upcoming = DrillUpcomingCurveFactory.Build(program, 0, 0);

        Assert.Equal(DrillUpcomingKind.ProgramEnd, upcoming.Kind);
        Assert.False(upcoming.HasCurve);
    }

    [Fact]
    public void Runner_PublishesTheUpcomingCurveBeforeAndDuringTheRepetition()
    {
        var runner = new DrillRunner();
        DrillProgram program = Program(MakeStep("dupla", 2, 40));

        Assert.True(runner.Start(program, 0));

        DrillUpdate armed = runner.Latest;
        Assert.Equal(DrillStage.Armed, armed.Stage);
        Assert.Equal(DrillUpcomingKind.NextRepetition, armed.NextCurve.Kind);
        Assert.True(armed.NextCurve.HasCurve, "a próxima curva precisa estar publicada antes de começar");

        runner.Update(new PedalReading(0, 0, 0.1), 0.1);
        DrillUpdate running = runner.Update(new PedalReading(10, 0, 0.2), 0.2);

        Assert.Equal(DrillStage.Running, running.Stage);
        Assert.Equal(DrillUpcomingKind.NextRepetition, running.NextCurve.Kind);
        Assert.True(running.NextCurve.HasCurve, "a próxima curva não pode sumir durante a repetição");
        Assert.Equal(40, Peak(running.NextCurve), 3);
    }

    [Fact]
    public void Runner_MovesTheUpcomingCurveToTheNextStepWhenTheStepEnds()
    {
        var runner = new DrillRunner();
        DrillProgram program = Program(MakeStep("etapa um", 1, 20), MakeStep("etapa dois", 1, 75));

        Assert.True(runner.Start(program, 0));
        Assert.Equal(DrillUpcomingKind.NextStep, runner.Latest.NextCurve.Kind);
        Assert.Equal(75, Peak(runner.Latest.NextCurve), 3);

        runner.Skip(0.1);

        Assert.Equal(DrillStage.Armed, runner.Stage);
        Assert.Equal(DrillUpcomingKind.ProgramEnd, runner.Latest.NextCurve.Kind);
        Assert.False(runner.Latest.NextCurve.HasCurve);
    }

    [Fact]
    public void StoppedProgram_PublishesNoUpcomingCurve()
    {
        var runner = new DrillRunner();

        Assert.True(runner.Start(Program(MakeStep("parada", 2, 40)), 0));
        runner.Stop(1.0);

        Assert.Equal(DrillStage.Idle, runner.Latest.Stage);
        Assert.Equal(DrillUpcomingKind.None, runner.Latest.NextCurve.Kind);
        Assert.False(runner.Latest.NextCurve.HasCurve);
    }

    private static double Peak(DrillUpcomingCurve upcoming)
    {
        if (upcoming.Curve is not { Count: > 0 } curve)
        {
            return 0;
        }

        double peak = 0;
        foreach (DrillCurvePoint point in curve)
        {
            peak = Math.Max(peak, point.Percent);
        }

        return peak;
    }

    private static DrillStep MakeStep(string label, int repetitions, double target)
    {
        var step = new DrillStep
        {
            Label = label,
            Pedal = DrillPedal.Brake,
            TargetPercent = target,
            TolerancePercent = 4,
            Repetitions = repetitions
        };

        step.Segments.Add(DrillSegment.Ramp(0.6));
        step.Segments.Add(DrillSegment.Hold(0.5));
        step.Segments.Add(DrillSegment.Release(2.0));

        return step;
    }

    private static DrillProgram Program(params DrillStep[] steps)
    {
        var program = new DrillProgram { Name = "programa de teste", RestPercent = 4 };

        foreach (DrillStep step in steps)
        {
            program.Steps.Add(step);
        }

        return program;
    }
}
