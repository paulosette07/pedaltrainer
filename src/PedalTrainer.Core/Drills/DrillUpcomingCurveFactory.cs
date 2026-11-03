using System;
using System.Globalization;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Descobre qual curva o piloto vai encarar depois da repetição atual: a próxima repetição
/// (curva igual) ou a primeira repetição da etapa seguinte (curva nova). Assim a tela de
/// treino consegue manter o "próximo gráfico" sempre à vista.
/// </summary>
public static class DrillUpcomingCurveFactory
{
    /// <summary>
    /// Curva seguinte à repetição <paramref name="repetitionIndex"/> (base zero) da etapa
    /// <paramref name="stepIndex"/>. Use <c>-1</c> na repetição para obter a primeira curva
    /// de uma etapa, como faz a prévia do programa escolhido na lista.
    /// </summary>
    public static DrillUpcomingCurve Build(DrillProgram program, int stepIndex, int repetitionIndex)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (program.Steps.Count == 0)
        {
            return DrillUpcomingCurve.None;
        }

        int current = Math.Clamp(stepIndex, 0, program.Steps.Count - 1);
        DrillStep step = program.Steps[current];
        int repetitions = Math.Max(1, step.Repetitions);

        if (repetitionIndex + 1 < repetitions)
        {
            DrillUpcomingKind kind = repetitionIndex < 0
                ? DrillUpcomingKind.FirstRepetition
                : DrillUpcomingKind.NextRepetition;

            if (Describe(program, current, repetitionIndex + 1, kind) is { } sameStep)
            {
                return sameStep;
            }
        }

        int next = FirstPlayableStep(program, current + 1);

        return next < 0
            ? DrillUpcomingCurve.ProgramEnd
            : Describe(program, next, 0, DrillUpcomingKind.NextStep) ?? DrillUpcomingCurve.ProgramEnd;
    }

    /// <summary>Primeira curva do programa, usada enquanto o treino está parado.</summary>
    public static DrillUpcomingCurve BuildFirst(DrillProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        int first = FirstPlayableStep(program, 0);

        return first < 0
            ? DrillUpcomingCurve.None
            : Describe(program, first, 0, DrillUpcomingKind.FirstRepetition) ?? DrillUpcomingCurve.None;
    }

    /// <summary>Índice da primeira etapa desenhável a partir de <paramref name="fromIndex"/>.</summary>
    private static int FirstPlayableStep(DrillProgram program, int fromIndex)
    {
        for (int index = Math.Max(0, fromIndex); index < program.Steps.Count; index++)
        {
            if (HasPositiveSegment(program.Steps[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool HasPositiveSegment(DrillStep step)
    {
        foreach (DrillSegment segment in step.Segments)
        {
            if (segment.Seconds > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static DrillUpcomingCurve? Describe(
        DrillProgram program,
        int stepIndex,
        int repetitionIndex,
        DrillUpcomingKind kind)
    {
        if (!HasPositiveSegment(program.Steps[stepIndex]))
        {
            return null;
        }

        DrillStep step = program.Steps[stepIndex];
        double target = DrillCurvePreview.EstimateTarget(step);
        var profile = new DrillProfile(step, target);
        int repetitions = Math.Max(1, step.Repetitions);

        return new DrillUpcomingCurve
        {
            Kind = kind,
            Curve = DrillTargetCurveBuilder.Build(profile),
            Plan = DrillCurvePlan.Build(profile),
            Summary = DrillCurvePlan.Summary(profile),
            StepLabel = DrillInstructor.StepTitle(step),
            Detail = BuildDetail(program, stepIndex, repetitionIndex, repetitions, kind),
            TargetText = BuildTargetText(step, target),
            IsTargetEstimated = step.IsRandom,
            TolerancePercent = step.TolerancePercent,
            Pedal = step.Pedal,
            StepNumber = stepIndex + 1,
            StepCount = program.Steps.Count,
            RepetitionNumber = repetitionIndex + 1,
            RepetitionCount = repetitions
        };
    }

    private static string BuildDetail(
        DrillProgram program,
        int stepIndex,
        int repetitionIndex,
        int repetitions,
        DrillUpcomingKind kind) => kind switch
    {
        DrillUpcomingKind.FirstRepetition => string.Create(
            CultureInfo.CurrentCulture,
            $"{program.Steps[stepIndex].Repetitions}× desta etapa · solte o pedal para começar"),
        DrillUpcomingKind.NextStep => string.Create(
            CultureInfo.CurrentCulture,
            $"Etapa {stepIndex + 1} de {program.Steps.Count} · " +
            $"{DrillInstructor.StepTitle(program.Steps[stepIndex])}"),
        _ => string.Create(
            CultureInfo.CurrentCulture,
            $"Repetição {repetitionIndex + 1} de {repetitions} · mesma curva")
    };

    private static string BuildTargetText(DrillStep step, double estimatedTarget)
    {
        if (!step.IsRandom)
        {
            return string.Create(
                CultureInfo.CurrentCulture,
                $"alvo {DrillInstructor.Percent(estimatedTarget)}");
        }

        return string.Create(
            CultureInfo.CurrentCulture,
            $"alvo sorteado {DrillInstructor.Percent(step.RandomTargetMinPercent!.Value)}–" +
            $"{DrillInstructor.Percent(step.RandomTargetMaxPercent!.Value)}");
    }
}
