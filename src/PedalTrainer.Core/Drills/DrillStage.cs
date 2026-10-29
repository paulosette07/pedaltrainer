namespace PedalTrainer.Core.Drills;

/// <summary>Estágio atual da execução de um programa de treino.</summary>
public enum DrillStage
{
    /// <summary>Nenhum programa em execução.</summary>
    Idle = 0,

    /// <summary>Esperando o pedal voltar ao repouso e começar o movimento.</summary>
    Armed = 1,

    /// <summary>Repetição em andamento.</summary>
    Running = 2,

    /// <summary>Repetição concluída, mostrando a nota antes da próxima.</summary>
    RepetitionDone = 3,

    /// <summary>Programa inteiro concluído.</summary>
    Complete = 4
}

/// <summary>Nomes dos estágios usados na interface.</summary>
public static class DrillStageNames
{
    public static string Label(DrillStage stage) => stage switch
    {
        DrillStage.Armed => "PRONTO",
        DrillStage.Running => "EXECUTANDO",
        DrillStage.RepetitionDone => "REPETIÇÃO CONCLUÍDA",
        DrillStage.Complete => "PROGRAMA CONCLUÍDO",
        _ => "PARADO"
    };
}
