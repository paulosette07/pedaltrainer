using System;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Aprende a faixa bruta de um eixo observando os valores recebidos.
/// Usado para auto-calibrar quando o usuário ainda não rodou o assistente.
/// </summary>
public sealed class AxisRangeTracker
{
    private readonly int _minimumSpan;

    public AxisRangeTracker(int minimumSpan = 200)
    {
        _minimumSpan = Math.Max(1, minimumSpan);
    }

    public int Min { get; private set; } = int.MaxValue;

    public int Max { get; private set; } = int.MinValue;

    public int Span => Min > Max ? 0 : Max - Min;

    /// <summary>Verdadeiro quando a faixa observada é grande o bastante para calibrar.</summary>
    public bool HasUsableRange => Span >= _minimumSpan;

    public void Observe(int value)
    {
        if (value < Min)
        {
            Min = value;
        }

        if (value > Max)
        {
            Max = value;
        }
    }

    public void Seed(int min, int max)
    {
        Min = Math.Min(min, max);
        Max = Math.Max(min, max);
    }

    public void Reset()
    {
        Min = int.MaxValue;
        Max = int.MinValue;
    }

    /// <summary>Amplia a faixa com uma margem, evitando que o ruído corte o curso útil.</summary>
    public (int Min, int Max) ToCalibratedRange(int marginPercent = 2)
    {
        if (!HasUsableRange)
        {
            return (Min > Max ? 0 : Min, Max < Min ? 0 : Max);
        }

        int margin = Math.Max(1, Span * Math.Clamp(marginPercent, 0, 20) / 100);
        return (Min - margin, Max + margin);
    }
}
