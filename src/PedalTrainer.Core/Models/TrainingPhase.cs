namespace PedalTrainer.Core.Models;

/// <summary>Estados do desafio de frenagem.</summary>
public enum TrainingPhase
{
    /// <summary>Sem referência carregada.</summary>
    Idle = 0,

    /// <summary>Referência pronta, aguardando o piloto começar a frear.</summary>
    Armed = 1,

    /// <summary>Janela de desafio em andamento.</summary>
    Running = 2,

    /// <summary>Zona concluída, mostrando o resultado.</summary>
    Finished = 3
}
