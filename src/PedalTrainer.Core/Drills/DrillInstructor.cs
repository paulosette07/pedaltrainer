using System;
using System.Globalization;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Transforma a curva-alvo em instruções curtas em português, do jeito que o piloto
/// precisa ler no meio do exercício ("suba rápido até 20%", "solte bem devagar").
/// </summary>
public static class DrillInstructor
{
    /// <summary>Acima desta taxa (pontos por segundo) a pegada é considerada rápida.</summary>
    public const double FastRate = 45.0;

    /// <summary>Acima desta taxa a pegada é considerada firme.</summary>
    public const double ModerateRate = 18.0;

    /// <summary>Abaixo desta taxa a pegada é considerada lenta.</summary>
    public const double SlowRate = 8.0;

    /// <summary>Instrução do trecho informado do perfil.</summary>
    public static string DescribeSegment(DrillProfile profile, int index)
    {
        ArgumentNullException.ThrowIfNull(profile);

        double from = profile.SegmentStartPercent(index);
        double to = profile.SegmentEndPercent(index);
        double seconds = profile.SegmentSeconds(index);
        double rate = Rate(from, to, seconds);

        return profile.SegmentKind(index) switch
        {
            DrillSegmentKind.Hold => string.Create(
                CultureInfo.InvariantCulture,
                $"Segure em {Percent(to)} por {seconds:0.0} s"),
            DrillSegmentKind.Release => string.Create(
                CultureInfo.InvariantCulture,
                $"Solte {Pace(rate)} até {Percent(to)}"),
            _ => to >= from
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"Suba {Pace(rate)} até {Percent(to)}")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"Desça {Pace(rate)} até {Percent(to)}")
        };
    }

    /// <summary>Instrução com o nome do pedal na frente, usada no cartão do exercício.</summary>
    public static string DescribeWithPedal(DrillProfile profile, int index)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return $"{DrillPedalNames.Short(profile.Step.Pedal)} · {DescribeSegment(profile, index)}";
    }

    /// <summary>Título curto da etapa, usado na lista e no cabeçalho do exercício.</summary>
    public static string StepTitle(DrillStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (!string.IsNullOrWhiteSpace(step.Label))
        {
            return step.Label;
        }

        (double _, double max) = step.TargetRange();
        return $"{DrillPedalNames.Short(step.Pedal)} até {Percent(max)}";
    }

    /// <summary>Adjetivo da pegada da etapa inteira: "rápido", "gradualmente"...</summary>
    public static string PressPace(DrillStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        double rate = step.PressRatePercentPerSecond;
        return rate <= 0 ? "sem subida" : Pace(rate);
    }

    /// <summary>Adjetivo de velocidade para uma taxa em pontos percentuais por segundo.</summary>
    public static string Pace(double ratePercentPerSecond)
    {
        if (ratePercentPerSecond >= FastRate)
        {
            return "rápido";
        }

        if (ratePercentPerSecond >= ModerateRate)
        {
            return "firme";
        }

        if (ratePercentPerSecond >= SlowRate)
        {
            return "gradualmente";
        }

        return "bem devagar";
    }

    /// <summary>Percentual formatado sem casas decimais, com o símbolo.</summary>
    public static string Percent(double percent) => string.Create(
        CultureInfo.InvariantCulture,
        $"{Math.Round(percent, 0, MidpointRounding.AwayFromZero):0}%");

    /// <summary>Taxa de variação entre dois percentuais em um tempo.</summary>
    public static double Rate(double fromPercent, double toPercent, double seconds)
    {
        if (seconds <= 0.001)
        {
            return Math.Abs(toPercent - fromPercent) * 1000;
        }

        return Math.Abs(toPercent - fromPercent) / seconds;
    }
}
