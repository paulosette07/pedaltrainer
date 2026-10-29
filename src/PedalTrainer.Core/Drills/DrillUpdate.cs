using System.Collections.Generic;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Instantâneo imutável do programa em execução, publicado pelo motor e consumido pela
/// interface a 60 Hz. Nunca é alterado depois de publicado.
/// </summary>
public sealed record DrillUpdate
{
    public static DrillUpdate Empty { get; } = new();

    public DrillStage Stage { get; init; } = DrillStage.Idle;

    public string ProgramName { get; init; } = "Nenhum programa selecionado";

    /// <summary>Nome da etapa em execução.</summary>
    public string StepLabel { get; init; } = string.Empty;

    /// <summary>Instrução do trecho atual ("suba rápido até 20%").</summary>
    public string Instruction { get; init; } = "Escolha um programa e clique em Iniciar.";

    public DrillPedal Pedal { get; init; } = DrillPedal.Brake;

    /// <summary>Percentual esperado neste instante; negativo esconde o marcador.</summary>
    public double TargetPercent { get; init; } = -1;

    /// <summary>Percentual atual do pedal em uso.</summary>
    public double ValuePercent { get; init; }

    /// <summary>Alvo da repetição (o topo da curva).</summary>
    public double TargetLevelPercent { get; init; } = -1;

    public double TolerancePercent { get; init; } = 5;

    /// <summary>Repetição atual dentro da etapa (1 é a primeira).</summary>
    public int RepetitionNumber { get; init; }

    public int RepetitionCount { get; init; }

    public int StepNumber { get; init; }

    public int StepCount { get; init; }

    /// <summary>Repetições já concluídas no programa inteiro.</summary>
    public int RepetitionsDone { get; init; }

    public int RepetitionsTotal { get; init; }

    /// <summary>Progresso da repetição atual (0 a 1).</summary>
    public double Progress { get; init; }

    /// <summary>Progresso do programa inteiro (0 a 1).</summary>
    public double ProgramProgress { get; init; }

    /// <summary>Tempo decorrido da repetição atual.</summary>
    public double ElapsedSeconds { get; init; }

    /// <summary>Instante (relógio do motor) em que a repetição atual começou.</summary>
    public double RepetitionStartSeconds { get; init; }

    /// <summary>Nota projetada da repetição em andamento.</summary>
    public double LiveScore { get; init; }

    /// <summary>Média das repetições já concluídas.</summary>
    public double AverageScore { get; init; }

    /// <summary>Melhor nota do programa.</summary>
    public double BestScore { get; init; }

    /// <summary>Resultado da última repetição concluída.</summary>
    public DrillRepetitionResult? LastResult { get; init; }

    /// <summary>Curva-alvo da repetição atual, para o gráfico.</summary>
    public IReadOnlyList<DrillCurvePoint>? TargetCurve { get; init; }

    public string StatusMessage { get; init; } = "Simule ou conecte os pedais para começar.";

    /// <summary>Verdadeiro enquanto existe programa em execução.</summary>
    public bool IsRunning => Stage is DrillStage.Armed or DrillStage.Running or DrillStage.RepetitionDone;
}
