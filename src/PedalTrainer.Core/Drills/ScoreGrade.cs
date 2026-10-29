using System;

namespace PedalTrainer.Core.Drills;

/// <summary>Converte uma nota de 0 a 100 no conceito exibido na interface.</summary>
public static class ScoreGrade
{
    /// <summary>Conceito da nota: S, A, B, C, D ou E.</summary>
    public static string From(double score) => score switch
    {
        >= 95 => "S",
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 55 => "D",
        _ => "E"
    };

    /// <summary>Nota a partir de um erro RMS, sem penalidades adicionais.</summary>
    public static double ScoreFromRms(double rmsPercent, double scalePercent)
    {
        if (scalePercent <= 0)
        {
            return 100;
        }

        double ratio = Math.Clamp(rmsPercent / scalePercent, 0, 1);
        return Math.Clamp((1 - ratio) * 100, 0, 100);
    }
}
