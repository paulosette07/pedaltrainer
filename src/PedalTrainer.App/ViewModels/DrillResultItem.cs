using System.Globalization;
using PedalTrainer.Core.Drills;

namespace PedalTrainer.App.ViewModels;

/// <summary>Linha da lista de repetições concluídas na tela de programas.</summary>
public sealed class DrillResultItem
{
    private DrillResultItem(DrillRepetitionResult result)
    {
        ScoreText = result.Score.ToString("0", CultureInfo.CurrentCulture);
        Grade = result.Grade;
        StepLabel = result.StepLabel;
        Detail = string.Create(
            CultureInfo.CurrentCulture,
            $"rep {result.RepetitionNumber}/{result.RepetitionCount} · alvo {result.TargetPercent:0}% · pico {result.PeakPercent:0}% · erro {result.RmsErrorPercent:0.0} pp");
    }

    public string ScoreText { get; }

    public string Grade { get; }

    public string StepLabel { get; }

    public string Detail { get; }

    /// <summary>Cria a linha a partir do resultado medido.</summary>
    public static DrillResultItem From(DrillRepetitionResult result) => new(result);
}
