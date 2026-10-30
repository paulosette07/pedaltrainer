namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Simuladores dos quais o aplicativo consegue ler telemetria de carro.
/// <see cref="Auto"/> faz o aplicativo procurar sozinho qual está aberto.
/// </summary>
public enum SimulatorId
{
    /// <summary>Detecta automaticamente o primeiro simulador encontrado.</summary>
    Auto,

    /// <summary>iRacing (memória compartilhada nativa do IRSDK).</summary>
    IRacing,

    /// <summary>Assetto Corsa Competizione (ACPMF Physics/Graphics/Static).</summary>
    AssettoCorsaCompetizione,

    /// <summary>Assetto Corsa EVO (acevo_pmf Physics/Graphics/Static).</summary>
    AssettoCorsaEvo,

    /// <summary>Automobilista 2 (Project CARS 2 shared memory).</summary>
    Automobilista2,

    /// <summary>Le Mans Ultimate (plugin rFactor 2 Shared Memory Map).</summary>
    LeMansUltimate
}
