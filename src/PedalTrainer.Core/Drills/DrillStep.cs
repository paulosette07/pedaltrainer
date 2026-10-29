using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Etapa de um programa de treino: uma família de repetições iguais, definida pela
/// sequência de segmentos que o piloto deve seguir com o pedal.
/// </summary>
public sealed class DrillStep
{
    /// <summary>Percentual mínimo aceito para um alvo sorteado.</summary>
    public const double MinTargetPercent = 5.0;

    /// <summary>Nome curto da etapa, exibido na interface.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Pedal usado nesta etapa.</summary>
    public DrillPedal Pedal { get; set; } = DrillPedal.Brake;

    /// <summary>Percentual alvo fixo. Também é o valor usado quando o sorteio está desligado.</summary>
    public double TargetPercent { get; set; } = 50.0;

    /// <summary>Banda, em pontos percentuais, em que o erro não é penalizado.</summary>
    public double TolerancePercent { get; set; } = 5.0;

    /// <summary>Quantas vezes a sequência deve ser repetida.</summary>
    public int Repetitions { get; set; } = 1;

    /// <summary>Menor alvo sorteado quando <see cref="IsRandom"/> é verdadeiro.</summary>
    public double? RandomTargetMinPercent { get; set; }

    /// <summary>Maior alvo sorteado quando <see cref="IsRandom"/> é verdadeiro.</summary>
    public double? RandomTargetMaxPercent { get; set; }

    /// <summary>Sequência de trechos que formam uma repetição.</summary>
    public List<DrillSegment> Segments { get; set; } = new();

    /// <summary>Verdadeiro quando o alvo é sorteado a cada repetição.</summary>
    [JsonIgnore]
    public bool IsRandom => RandomTargetMinPercent.HasValue && RandomTargetMaxPercent.HasValue;

    /// <summary>Duração de uma repetição, em segundos.</summary>
    [JsonIgnore]
    public double RepetitionSeconds
    {
        get
        {
            double total = 0;
            foreach (DrillSegment segment in Segments)
            {
                if (segment.Seconds > 0)
                {
                    total += segment.Seconds;
                }
            }

            return total;
        }
    }

    /// <summary>
    /// Taxa de subida do primeiro trecho, em pontos percentuais por segundo.
    /// Serve para nomear o exercício ("rápido", "gradual").
    /// </summary>
    [JsonIgnore]
    public double PressRatePercentPerSecond
    {
        get
        {
            foreach (DrillSegment segment in Segments)
            {
                if (segment.Kind != DrillSegmentKind.Ramp || segment.Seconds <= 0)
                {
                    continue;
                }

                double to = segment.ToPercent ?? TargetPercent;
                return Math.Max(0, to) / segment.Seconds;
            }

            return 0;
        }
    }

    /// <summary>Menor e maior valor tocados pela curva-alvo.</summary>
    public (double Min, double Max) TargetRange()
    {
        double previous = 0;
        double min = 0;
        double max = 0;

        foreach (DrillSegment segment in Segments)
        {
            double to = segment.Kind == DrillSegmentKind.Hold
                ? previous
                : segment.ToPercent ?? TargetPercent;

            min = Math.Min(min, Math.Min(previous, to));
            max = Math.Max(max, Math.Max(previous, to));
            previous = to;
        }

        return (min, max);
    }

    /// <summary>
    /// Verifica se a etapa pode ser executada. Devolve falso com o motivo em
    /// <paramref name="error"/> quando falta segmento, alvo ou repetição.
    /// </summary>
    public bool TryValidate(out string error)
    {
        if (Segments.Count == 0)
        {
            error = "A etapa não tem nenhum segmento.";
            return false;
        }

        if (Repetitions < 1)
        {
            error = "A etapa precisa de pelo menos uma repetição.";
            return false;
        }

        double total = 0;
        foreach (DrillSegment segment in Segments)
        {
            if (segment.Seconds <= 0)
            {
                error = "Todo segmento precisa de duração maior que zero.";
                return false;
            }

            double to = segment.Kind == DrillSegmentKind.Hold ? 0 : segment.ToPercent ?? TargetPercent;
            if (to < 0 || to > 100)
            {
                error = "Os percentuais do segmento precisam ficar entre 0 e 100.";
                return false;
            }

            total += segment.Seconds;
        }

        if (total > 120)
        {
            error = "Uma repetição não pode passar de 120 segundos.";
            return false;
        }

        if (!IsRandom && (TargetPercent < MinTargetPercent || TargetPercent > 100))
        {
            error = "O alvo da etapa precisa ficar entre 5 e 100%.";
            return false;
        }

        if (IsRandom)
        {
            double min = RandomTargetMinPercent!.Value;
            double max = RandomTargetMaxPercent!.Value;

            if (min < MinTargetPercent || max > 100 || min > max)
            {
                error = "A faixa sorteada precisa ficar entre 5 e 100%, com mínimo menor que o máximo.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public DrillStep Clone() => new()
    {
        Label = Label,
        Pedal = Pedal,
        TargetPercent = TargetPercent,
        TolerancePercent = TolerancePercent,
        Repetitions = Repetitions,
        RandomTargetMinPercent = RandomTargetMinPercent,
        RandomTargetMaxPercent = RandomTargetMaxPercent,
        Segments = CloneSegments()
    };

    private List<DrillSegment> CloneSegments()
    {
        var copy = new List<DrillSegment>(Segments.Count);
        foreach (DrillSegment segment in Segments)
        {
            copy.Add(segment.Clone());
        }

        return copy;
    }
}
