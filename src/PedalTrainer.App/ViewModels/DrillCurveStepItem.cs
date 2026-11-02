using System;
using System.Collections.Generic;
using System.Globalization;
using PedalTrainer.Core.Drills;

namespace PedalTrainer.App.ViewModels;

/// <summary>
/// Um trecho da curva na tira de prévia do exercício ("SUBIR até 90% · 0,8 s"), com o
/// trecho em execução destacado.
/// </summary>
public sealed class DrillCurveStepItem
{
    private DrillCurveStepItem(DrillCurveStep step, int index, bool isCurrent)
    {
        NumberText = (index + 1).ToString(CultureInfo.CurrentCulture);
        Action = step.Action;
        TargetText = step.TargetText;
        TimeText = step.TimeText;
        Instruction = step.Text;
        IsCurrent = isCurrent;
    }

    /// <summary>Posição do trecho na repetição (1 é o primeiro).</summary>
    public string NumberText { get; }

    /// <summary>Verbo do trecho ("SUBIR", "SEGURAR", "SOLTAR").</summary>
    public string Action { get; }

    /// <summary>Destino do trecho ("até 90%").</summary>
    public string TargetText { get; }

    /// <summary>Duração do trecho ("0,8 s").</summary>
    public string TimeText { get; }

    /// <summary>Instrução completa do trecho, mostrada como dica.</summary>
    public string Instruction { get; }

    /// <summary>Verdadeiro no trecho que está sendo executado agora.</summary>
    public bool IsCurrent { get; }

    /// <summary>Converte o plano do motor em itens da tira, destacando o trecho atual.</summary>
    public static IReadOnlyList<DrillCurveStepItem> From(IReadOnlyList<DrillCurveStep> plan, int currentIndex)
    {
        if (plan.Count == 0)
        {
            return Array.Empty<DrillCurveStepItem>();
        }

        var items = new List<DrillCurveStepItem>(plan.Count);

        for (var index = 0; index < plan.Count; index++)
        {
            items.Add(new DrillCurveStepItem(plan[index], index, index == currentIndex));
        }

        return items;
    }
}
