namespace PedalTrainer.Core.Drills;

/// <summary>
/// O que vem depois da repetição em execução. Define o título do painel de prévia e o
/// texto que o piloto lê antes de encarar a próxima curva.
/// </summary>
public enum DrillUpcomingKind
{
    /// <summary>Nada previsto (programa parado).</summary>
    None = 0,

    /// <summary>Primeira curva do programa escolhido, ainda sem treino em execução.</summary>
    FirstRepetition = 1,

    /// <summary>Próxima repetição da mesma etapa — a curva é a mesma.</summary>
    NextRepetition = 2,

    /// <summary>Primeira repetição da etapa seguinte — curva nova.</summary>
    NextStep = 3,

    /// <summary>Não há mais repetições: a curva que está na tela é a última do programa.</summary>
    ProgramEnd = 4
}
