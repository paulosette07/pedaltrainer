using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Tests;

/// <summary>
/// Execução de um programa de treino: armar no repouso, medir a repetição, pontuar,
/// sortear alvos e avançar de etapa até concluir.
/// </summary>
public sealed class DrillRunnerTests
{
    [Fact]
    public void Armed_RequiresPedalAtRestBeforeStarting()
    {
        var runner = new DrillRunner();
        DrillProgram program = SingleStepProgram(1, 20);

        Assert.True(runner.Start(program, 0));
        Assert.Equal(DrillStage.Armed, runner.Stage);

        runner.Update(new PedalReading(30, 0, 0.1), 0.1);
        Assert.Equal(DrillStage.Armed, runner.Stage);

        runner.Update(new PedalReading(0, 0, 0.2), 0.2);
        Assert.Equal(DrillStage.Armed, runner.Stage);

        runner.Update(new PedalReading(10, 0, 0.3), 0.3);
        Assert.Equal(DrillStage.Running, runner.Stage);
    }

    [Fact]
    public void PerfectRepetition_ScoresFullMarks()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(1, 20);

        Assert.True(runner.Start(Program(step), 0));
        SimulateRepetition(runner, step, 20, offsetPercent: 0, startSeconds: 0.1);

        Assert.Equal(DrillStage.RepetitionDone, runner.Stage);
        Assert.NotNull(runner.LastResult);

        DrillRepetitionResult result = runner.LastResult!;
        Assert.Equal("S", result.Grade);
        Assert.True(result.Score >= 95, $"nota {result.Score:0.0}");
        Assert.True(result.RmsErrorPercent < 1.5, $"rmse {result.RmsErrorPercent:0.00}");
        Assert.Equal(20, result.PeakPercent, 2);
        Assert.Equal(0, result.OvershootPercent, 2);
        Assert.Equal(1, result.RepetitionNumber);
        Assert.Equal(1, result.RepetitionCount);
    }

    [Fact]
    public void OvershootAndOffset_LowerTheScore()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(1, 20);

        Assert.True(runner.Start(Program(step), 0));
        SimulateRepetition(runner, step, 20, offsetPercent: 15, startSeconds: 0.1);

        DrillRepetitionResult result = runner.LastResult!;

        Assert.True(result.Score < 60, $"nota {result.Score:0.0}");
        Assert.True(result.OvershootPercent > 10, $"pico {result.PeakPercent:0.0}");
        Assert.True(result.RmsErrorPercent > 10, $"rmse {result.RmsErrorPercent:0.0}");
    }

    [Fact]
    public void RepetitionDone_WaitsThenArmsTheNextOne()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(2, 20);

        Assert.True(runner.Start(Program(step), 0));
        double finished = SimulateRepetition(runner, step, 20, 0, 0.1);

        Assert.Equal(DrillStage.RepetitionDone, runner.Stage);
        Assert.Equal(1, runner.Latest.RepetitionsDone);

        runner.Update(new PedalReading(0, 0, finished + 0.5), finished + 0.5);
        Assert.Equal(DrillStage.RepetitionDone, runner.Stage);

        runner.Update(new PedalReading(0, 0, finished + 1.5), finished + 1.5);
        Assert.Equal(DrillStage.Armed, runner.Stage);
        Assert.Equal(2, runner.Latest.RepetitionNumber);
        Assert.Equal(2, runner.Latest.RepetitionCount);
    }

    [Fact]
    public void Program_CompletesAfterTheLastRepetition()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(1, 20);

        Assert.True(runner.Start(Program(step), 0));
        double finished = SimulateRepetition(runner, step, 20, 0, 0.1);

        runner.Update(new PedalReading(0, 0, finished + 1.5), finished + 1.5);

        Assert.Equal(DrillStage.Complete, runner.Stage);
        Assert.Equal(1, runner.Latest.ProgramProgress, 3);
        Assert.Equal(1, runner.Latest.RepetitionsDone);
        Assert.Contains("concluído", runner.Latest.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(runner.LastResult!.Score, runner.AverageScore, 3);
    }

    [Fact]
    public void Skip_AdvancesWithoutScoring()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(3, 40);

        Assert.True(runner.Start(Program(step), 0));
        runner.Update(new PedalReading(0, 0, 0.1), 0.1);
        runner.Update(new PedalReading(12, 0, 0.2), 0.2);
        Assert.Equal(DrillStage.Running, runner.Stage);

        runner.Skip(0.3);

        Assert.Empty(runner.Results);
        Assert.Equal(DrillStage.Armed, runner.Stage);
        Assert.Equal(2, runner.Latest.RepetitionNumber);
    }

    [Fact]
    public void RandomTargets_StayInsideRangeAndVary()
    {
        var runner = new DrillRunner(new Random(2024));
        DrillStep step = MakeStep(1, 50);
        step.RandomTargetMinPercent = 20;
        step.RandomTargetMaxPercent = 85;
        step.Repetitions = 18;

        Assert.True(runner.Start(Program(step), 0));

        var seen = new HashSet<double>();

        for (var index = 0; index < step.Repetitions; index++)
        {
            Assert.Equal(DrillStage.Armed, runner.Stage);

            double target = runner.Latest.TargetLevelPercent;
            Assert.InRange(target, 20, 85);
            Assert.Equal(0, (target - 20) % DrillRunner.RandomTargetStepPercent, 3);

            seen.Add(target);
            runner.Skip(index + 1);
        }

        Assert.Equal(DrillStage.Complete, runner.Stage);
        Assert.True(seen.Count > 5, $"só {seen.Count} alvos diferentes sorteados");
    }

    [Fact]
    public void Program_WithInvalidSteps_DoesNotStart()
    {
        var runner = new DrillRunner();
        var broken = new DrillProgram { Name = "quebrado" };

        Assert.False(runner.Start(broken, 0));
        Assert.Equal(DrillStage.Idle, runner.Stage);
        Assert.Contains("etapa", runner.Latest.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThrottleStep_ReadsThrottlePedalOnly()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(1, 20);
        step.Pedal = DrillPedal.Throttle;

        Assert.True(runner.Start(Program(step), 0));

        runner.Update(new PedalReading(0, 30, 0.1), 0.1);
        Assert.Equal(DrillStage.Armed, runner.Stage);

        runner.Update(new PedalReading(0, 0, 0.2), 0.2);
        Assert.Equal(DrillStage.Armed, runner.Stage);

        runner.Update(new PedalReading(60, 10, 0.3), 0.3);
        Assert.Equal(DrillStage.Running, runner.Stage);
        Assert.Equal(DrillPedal.Throttle, runner.Latest.Pedal);
        Assert.Equal(10, runner.Latest.ValuePercent, 3);
    }

    [Fact]
    public void Running_PublishesLiveScoreAndTargetCurve()
    {
        var runner = new DrillRunner();
        DrillStep step = MakeStep(1, 20);

        Assert.True(runner.Start(Program(step), 0));
        runner.Update(new PedalReading(0, 0, 0.1), 0.1);
        runner.Update(new PedalReading(10, 0, 0.2), 0.2);

        DrillUpdate ramp = runner.Update(new PedalReading(10, 0, 0.8), 0.8);

        Assert.Equal(DrillStage.Running, ramp.Stage);
        Assert.NotNull(ramp.TargetCurve);
        Assert.True(ramp.TargetCurve!.Count > 10);
        Assert.InRange(ramp.TargetPercent, 5, 15);
        Assert.Contains("até", ramp.Instruction, StringComparison.OrdinalIgnoreCase);

        DrillUpdate update = runner.Update(new PedalReading(20, 0, 1.3), 1.3);

        Assert.Equal(DrillStage.Running, update.Stage);
        Assert.True(update.LiveScore > 0);
        Assert.InRange(update.Progress, 0.01, 1.0);
        Assert.Contains("Segure", update.Instruction, StringComparison.OrdinalIgnoreCase);
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

        step.Segments.Add(DrillSegment.Ramp(1.0));
        step.Segments.Add(DrillSegment.Hold(0.8));
        step.Segments.Add(DrillSegment.Release(2.5));

        return step;
    }

    private static DrillProgram SingleStepProgram(int repetitions, double target) =>
        Program(MakeStep(repetitions, target));

    private static DrillProgram Program(DrillStep step)
    {
        var program = new DrillProgram { Name = "programa de teste", RestPercent = 4 };
        program.Steps.Add(step);
        return program;
    }

    /// <summary>
    /// Executa uma repetição inteira seguindo a curva-alvo com o desvio informado e
    /// devolve o instante em que a repetição terminou.
    /// </summary>
    private static double SimulateRepetition(
        DrillRunner runner,
        DrillStep step,
        double target,
        double offsetPercent,
        double startSeconds)
    {
        var profile = new DrillProfile(step, target);
        double now = startSeconds;

        runner.Update(new PedalReading(0, 0, now), now);
        now += 0.02;
        runner.Update(new PedalReading(10, 0, now), now);

        double repetitionStart = now;

        for (double elapsed = 0; elapsed <= profile.DurationSeconds + 0.4; elapsed += 0.01)
        {
            double value = Math.Clamp(profile.TargetAt(elapsed) + offsetPercent, 0, 100);
            now = repetitionStart + elapsed;
            runner.Update(new PedalReading(value, 0, now), now);
        }

        return now;
    }
}
