using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Tests;

/// <summary>
/// Alinhamento do início da repetição: o cronômetro tem de começar onde o pé estava parado
/// e a curva-alvo tem de sair do mesmo percentual do pedal. Sem isso a repetição "começa
/// alguns pontos acima" e o piloto aparece fora da curva já no primeiro quadro.
/// </summary>
public sealed class DrillStartAlignmentTests
{
    private const double FrameSeconds = 0.005;
    private const double RestPercent = 4.0;

    [Fact]
    public void Profile_StartsAtThePedalRestLevel_KeepingShapeAndDuration()
    {
        DrillStep step = FastBrakeStep();

        var profile = new DrillProfile(step, targetPercent: 20, startPercent: 3);

        Assert.Equal(3, profile.StartPercent, 3);
        Assert.Equal(3, profile.TargetAt(0), 3);
        Assert.Equal(20, profile.TargetAt(0.30), 3);
        Assert.Equal(20, profile.PeakPercent, 3);
        Assert.Equal(0, profile.FinalPercent, 3);
        Assert.Equal(3.3, profile.DurationSeconds, 3);

        IReadOnlyList<DrillCurvePoint> curve = DrillTargetCurveBuilder.Build(profile);
        Assert.Equal(3, curve[0].Percent, 3);
    }

    [Fact]
    public void Repetition_IsAnchoredAtTheRestFrame_NotWhenThePedalAlreadyMoved()
    {
        var runner = new DrillRunner();

        Assert.True(runner.Start(SingleStepProgram(), 0));

        // Pé parado em 2% até 1,0 s; a partir daí o piloto pisa.
        DrillUpdate update = FeedUntilTrigger(runner, 1.0, 2.0, pressPercentPerSecond: 290);

        Assert.Equal(DrillStage.Running, update.Stage);

        // O pé cruzou os 4% de repouso em 1,0069 s (entre o quadro de 1,005 s a 3,45% e o
        // de 1,010 s a 4,9%): o cronômetro nasce ali, e não no disparo (1,015 s).
        Assert.InRange(update.RepetitionStartSeconds, 1.004, 1.010);
        Assert.True(
            update.ElapsedSeconds > 0.002,
            $"a repetição começou atrasada: elapsed={update.ElapsedSeconds:0.000}");

        // A curva nasce no percentual em que o pé estava parado (último quadro dentro do
        // repouso), e não em 0 — é isso que faz o traçado do piloto sair em cima da curva.
        Assert.NotNull(update.TargetCurve);
        Assert.InRange(update.TargetCurve![0].Percent, 2.0, RestPercent);
    }

    [Fact]
    public void Repetition_FollowedFromRest_ScoresHigh()
    {
        var runner = new DrillRunner();
        DrillStep step = FastBrakeStep();

        Assert.True(runner.Start(SingleStepProgram(), 0));

        run(t0: 1.0);

        Assert.Equal(DrillStage.RepetitionDone, runner.Stage);
        Assert.NotNull(runner.LastResult);

        DrillRepetitionResult result = runner.LastResult!;
        Assert.True(result.Score >= 90, $"nota {result.Score:0.0} · rmse {result.RmsErrorPercent:0.0}");
        Assert.True(result.RmsErrorPercent < 3, $"rmse {result.RmsErrorPercent:0.00}");

        void run(double t0)
        {
            // Curva que o piloto deve seguir, saindo do repouso em 2%.
            var profile = new DrillProfile(step, targetPercent: 20, startPercent: 2);
            double now = 0;

            for (; now < t0; now += FrameSeconds)
            {
                runner.Update(new PedalReading(2.0, 0, now), now);
            }

            double pressStart = now;

            while (now < pressStart + profile.DurationSeconds + 0.2)
            {
                double elapsed = now - pressStart;
                double value = profile.TargetAt(elapsed);
                runner.Update(new PedalReading(value, 0, now), now);

                if (runner.Stage == DrillStage.RepetitionDone)
                {
                    return;
                }

                now += FrameSeconds;
            }
        }
    }

    private static DrillUpdate FeedUntilTrigger(
        DrillRunner runner,
        double restUntilSeconds,
        double restPercent,
        double pressPercentPerSecond)
    {
        DrillUpdate update = runner.Latest;

        for (double now = 0; now < restUntilSeconds + 0.5; now += FrameSeconds)
        {
            double value = now < restUntilSeconds
                ? restPercent
                : restPercent + ((now - restUntilSeconds) * pressPercentPerSecond);

            update = runner.Update(new PedalReading(Math.Min(value, 100), 0, now), now);

            if (update.Stage == DrillStage.Running)
            {
                return update;
            }
        }

        return update;
    }

    private static DrillStep FastBrakeStep()
    {
        var step = new DrillStep
        {
            Label = "freio 20%",
            Pedal = DrillPedal.Brake,
            TargetPercent = 20,
            TolerancePercent = 4,
            Repetitions = 1
        };

        step.Segments.Add(DrillSegment.Ramp(0.30));
        step.Segments.Add(DrillSegment.Hold(1.0));
        step.Segments.Add(DrillSegment.Release(2.0));

        return step;
    }

    private static DrillProgram SingleStepProgram()
    {
        var program = new DrillProgram { Name = "programa de teste", RestPercent = RestPercent };
        program.Steps.Add(FastBrakeStep());
        return program;
    }
}
