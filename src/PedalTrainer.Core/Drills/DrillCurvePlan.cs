using System;
using System.Collections.Generic;
using System.Globalization;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Traduz o perfil de uma repetição no plano mostrado antes de começar: a lista de trechos
/// (subir, segurar, soltar) e um resumo de uma linha com pico e duração.
/// </summary>
public static class DrillCurvePlan
{
    private static readonly IReadOnlyList<DrillCurveStep> Empty = Array.Empty<DrillCurveStep>();

    /// <summary>Lista os trechos do perfil com o texto que o piloto lê na prévia.</summary>
    public static IReadOnlyList<DrillCurveStep> Build(DrillProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.SegmentCount == 0)
        {
            return Empty;
        }

        var steps = new List<DrillCurveStep>(profile.SegmentCount);

        for (var index = 0; index < profile.SegmentCount; index++)
        {
            steps.Add(new DrillCurveStep
            {
                Kind = profile.SegmentKind(index),
                FromPercent = profile.SegmentStartPercent(index),
                ToPercent = profile.SegmentEndPercent(index),
                Seconds = profile.SegmentSeconds(index),
                Text = DrillInstructor.DescribeSegment(profile, index)
            });
        }

        return steps;
    }

    /// <summary>Resumo de uma linha da curva ("pico 90% · 3,3 s").</summary>
    public static string Summary(DrillProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Create(
            CultureInfo.CurrentCulture,
            $"pico {profile.PeakPercent:0}% · {profile.DurationSeconds:0.0} s");
    }
}
