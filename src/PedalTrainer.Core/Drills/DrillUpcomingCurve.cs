using System;
using System.Collections.Generic;
using System.Globalization;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Curva que o piloto vai encarar depois da repetição em execução: o desenho, os trechos
/// em texto e o alvo previsto. É publicada o tempo todo pelo motor, inclusive durante a
/// repetição, para o painel "próxima curva" nunca ficar vazio no meio do treino.
/// </summary>
public sealed record DrillUpcomingCurve
{
    /// <summary>Nada previsto — programa parado.</summary>
    public static DrillUpcomingCurve None { get; } = new()
    {
        Kind = DrillUpcomingKind.None,
        Detail = "Escolha um programa para ver a próxima curva."
    };

    /// <summary>Não há mais repetições no programa.</summary>
    public static DrillUpcomingCurve ProgramEnd { get; } = new()
    {
        Kind = DrillUpcomingKind.ProgramEnd,
        Detail = "Esta é a última curva do programa."
    };

    /// <summary>O que vem depois da repetição atual.</summary>
    public DrillUpcomingKind Kind { get; init; } = DrillUpcomingKind.None;

    /// <summary>Título curto do painel ("PRÓXIMA CURVA", "PRÓXIMA ETAPA"...).</summary>
    public string Title => Kind switch
    {
        DrillUpcomingKind.FirstRepetition => "PRIMEIRA CURVA",
        DrillUpcomingKind.NextRepetition => "PRÓXIMA REPETIÇÃO",
        DrillUpcomingKind.NextStep => "PRÓXIMA ETAPA",
        DrillUpcomingKind.ProgramEnd => "FIM DO PROGRAMA",
        _ => "PRÓXIMA CURVA"
    };

    /// <summary>Polilinha da curva prevista, pronta para o gráfico.</summary>
    public IReadOnlyList<DrillCurvePoint>? Curve { get; init; }

    /// <summary>Trechos da repetição prevista ("suba rápido até 90%", "segure 0,4 s").</summary>
    public IReadOnlyList<DrillCurveStep> Plan { get; init; } = Array.Empty<DrillCurveStep>();

    /// <summary>Resumo de uma linha ("pico 90% · 3,3 s").</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Nome da etapa da curva prevista.</summary>
    public string StepLabel { get; init; } = string.Empty;

    /// <summary>Explicação curta do que a curva é ("Repetição 3 de 5 · mesma etapa").</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Alvo previsto ("alvo 90%") ou a faixa sorteada ("alvo sorteado 40–80%").</summary>
    public string TargetText { get; init; } = string.Empty;

    /// <summary>
    /// Verdadeiro quando o alvo é sorteado: o desenho é feito no meio da faixa e a curva
    /// é apresentada tracejada, porque a altura só é conhecida na hora.
    /// </summary>
    public bool IsTargetEstimated { get; init; }

    /// <summary>Tolerância da etapa prevista, desenhada como banda no gráfico.</summary>
    public double TolerancePercent { get; init; } = 5;

    /// <summary>Pedal da etapa prevista.</summary>
    public DrillPedal Pedal { get; init; } = DrillPedal.Brake;

    /// <summary>Etapa prevista (1 é a primeira do programa).</summary>
    public int StepNumber { get; init; }

    /// <summary>Etapas do programa.</summary>
    public int StepCount { get; init; }

    /// <summary>Repetição prevista dentro da etapa (1 é a primeira).</summary>
    public int RepetitionNumber { get; init; }

    /// <summary>Repetições da etapa prevista.</summary>
    public int RepetitionCount { get; init; }

    /// <summary>Verdadeiro quando existe desenho para mostrar.</summary>
    public bool HasCurve => Curve is { Count: > 1 };

    /// <summary>Etiqueta curta do canto do painel ("3/5", "ETAPA 2/4"); vazia quando não se aplica.</summary>
    public string Badge => Kind switch
    {
        DrillUpcomingKind.FirstRepetition => "PRÉVIA",
        DrillUpcomingKind.NextRepetition => string.Create(
            CultureInfo.CurrentCulture,
            $"REP {RepetitionNumber}/{RepetitionCount}"),
        DrillUpcomingKind.NextStep => string.Create(
            CultureInfo.CurrentCulture,
            $"ETAPA {StepNumber}/{StepCount}"),
        _ => string.Empty
    };
}
