using System;
using System.Globalization;

namespace PedalTrainer.Core.Telemetry.Comparison;

/// <summary>
/// Textos da comparação com a melhor volta. Ficam no Core, e não na interface, para que os
/// números que o piloto lê (sinal, casas decimais, unidade) sejam cobertos por teste.
/// </summary>
public static class BestLapComparisonText
{
    /// <summary>Delta abaixo disso conta como empate (evita piscar em toda amostra).</summary>
    public const double ToleranceSeconds = 0.005;

    /// <summary>Texto exibido quando ainda não há número para mostrar.</summary>
    public const string Placeholder = "--";

    /// <summary>Nome da volta de referência com a duração: "Interlagos · 01:34.2".</summary>
    public static string Title(in BestLapComparison comparison)
    {
        if (string.IsNullOrWhiteSpace(comparison.ReferenceName))
        {
            return "Nenhuma volta carregada";
        }

        if (comparison.ReferenceDurationSeconds <= 0)
        {
            return comparison.ReferenceName;
        }

        return $"{comparison.ReferenceName} · {Duration(comparison.ReferenceDurationSeconds)}";
    }

    /// <summary>Delta com sinal, em segundos: à frente aparece negativo (-0,42 s).</summary>
    public static string Delta(double seconds)
    {
        double rounded = Math.Round(seconds, 2);

        if (Math.Abs(rounded) < ToleranceSeconds)
        {
            return "0,00 s";
        }

        string sign = rounded < 0 ? "-" : "+";
        return sign + Math.Abs(rounded).ToString("0.00", CultureInfo.CurrentCulture) + " s";
    }

    /// <summary>Delta pronto para a tela, usando o marcador quando ainda não existe.</summary>
    public static string DeltaOrPlaceholder(in BestLapComparison comparison) =>
        comparison.HasDelta ? Delta(comparison.DeltaSeconds) : Placeholder;

    public static string BestDeltaOrPlaceholder(in BestLapComparison comparison) =>
        comparison.HasBestDelta ? Delta(comparison.BestDeltaSeconds) : Placeholder;

    public static string LastLapDeltaOrPlaceholder(in BestLapComparison comparison) =>
        comparison.HasLastLapDelta ? Delta(comparison.LastLapDeltaSeconds) : Placeholder;

    /// <summary>Valor do piloto contra o da referência: "182 · ref 187 km/h".</summary>
    public static string Compare(double current, double reference, string unit)
    {
        string currentText = current.ToString("0", CultureInfo.CurrentCulture);
        string referenceText = reference.ToString("0", CultureInfo.CurrentCulture);

        return $"{currentText} · ref {referenceText} {unit}";
    }

    /// <summary>Progresso na volta: "42% da volta".</summary>
    public static string Progress(double lapProgress) =>
        (lapProgress * 100).ToString("0", CultureInfo.CurrentCulture) + "% da volta";

    /// <summary>Duração em mm:ss,d (ou hh:mm:ss acima de uma hora).</summary>
    public static string Duration(double seconds)
    {
        if (seconds <= 0)
        {
            return Placeholder;
        }

        var span = TimeSpan.FromSeconds(seconds);

        if (span.TotalHours >= 1)
        {
            return string.Create(
                CultureInfo.CurrentCulture,
                $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}");
        }

        string decimals = (span.Milliseconds / 100).ToString(CultureInfo.CurrentCulture);

        return string.Create(
            CultureInfo.CurrentCulture,
            $"{span.Minutes:00}:{span.Seconds:00}.{decimals}");
    }
}
