using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Desenho de amostra de uma etapa, usado para o piloto ver a curva antes de começar:
/// vale para o programa escolhido na lista (treino parado) e para a etapa seguinte.
/// </summary>
public sealed class DrillCurvePreview
{
    private DrillCurvePreview(DrillProfile profile)
    {
        Profile = profile;
        Curve = DrillTargetCurveBuilder.Build(profile);
        Plan = DrillCurvePlan.Build(profile);
        Summary = DrillCurvePlan.Summary(profile);
    }

    /// <summary>Perfil usado no desenho (alvo estimado quando a etapa sorteia o alvo).</summary>
    public DrillProfile Profile { get; }

    /// <summary>Polilinha da curva, pronta para o gráfico.</summary>
    public IReadOnlyList<DrillCurvePoint> Curve { get; }

    /// <summary>Trechos da repetição, em texto.</summary>
    public IReadOnlyList<DrillCurveStep> Plan { get; }

    /// <summary>Resumo de uma linha com pico e duração.</summary>
    public string Summary { get; }

    /// <summary>
    /// Monta a prévia da etapa. Devolve nulo quando a etapa não tem trecho válido, em vez
    /// de deixar a exceção do perfil chegar à interface.
    /// </summary>
    public static DrillCurvePreview? TryCreate(DrillStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (!HasValidSegment(step))
        {
            return null;
        }

        return new DrillCurvePreview(new DrillProfile(step, EstimateTarget(step)));
    }

    /// <summary>
    /// Alvo usado no desenho: o fixo da etapa ou, quando o alvo é sorteado, o meio da faixa
    /// (a forma da curva não muda, só a altura).
    /// </summary>
    public static double EstimateTarget(DrillStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (!step.IsRandom)
        {
            return step.TargetPercent;
        }

        double min = step.RandomTargetMinPercent!.Value;
        double max = step.RandomTargetMaxPercent!.Value;
        return Math.Clamp(Math.Round((min + max) / 2, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static bool HasValidSegment(DrillStep step)
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
}
