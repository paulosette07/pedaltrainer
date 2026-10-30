using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Lista dos simuladores suportados, com o texto de ajuda que a tela de configurações mostra
/// para cada um. A ordem é a de prioridade usada na detecção automática.
/// </summary>
public static class SimulatorCatalog
{
    /// <summary>Entrada especial que manda o aplicativo procurar o simulador aberto.</summary>
    public static SimulatorInfo Auto { get; } = new(
        SimulatorId.Auto,
        "Detectar automaticamente",
        "Auto",
        "Procura iRacing, Assetto Corsa Competizione, Assetto Corsa EVO, Automobilista 2 e Le Mans Ultimate.",
        RequiresPlugin: false);

    public static SimulatorInfo IRacing { get; } = new(
        SimulatorId.IRacing,
        "iRacing",
        "iRacing",
        "Nada a instalar: abra o iRacing e entre na pista.",
        RequiresPlugin: false);

    public static SimulatorInfo AssettoCorsaCompetizione { get; } = new(
        SimulatorId.AssettoCorsaCompetizione,
        "Assetto Corsa Competizione",
        "ACC",
        "Nada a instalar: abra o ACC e entre na pista.",
        RequiresPlugin: false);

    public static SimulatorInfo AssettoCorsaEvo { get; } = new(
        SimulatorId.AssettoCorsaEvo,
        "Assetto Corsa EVO",
        "AC EVO",
        "Nada a instalar: exige AC EVO 0.6 ou mais novo, com a memória compartilhada ativa.",
        RequiresPlugin: false);

    public static SimulatorInfo Automobilista2 { get; } = new(
        SimulatorId.Automobilista2,
        "Automobilista 2",
        "AMS2",
        "No jogo, escolha Options > System > Shared Memory = \"Project CARS 2\".",
        RequiresPlugin: false);

    public static SimulatorInfo LeMansUltimate { get; } = new(
        SimulatorId.LeMansUltimate,
        "Le Mans Ultimate",
        "LMU",
        "Ative Settings > Gameplay > Enable Plugins (plugin rFactor 2 Shared Memory Map).",
        RequiresPlugin: true);

    /// <summary>Todos os simuladores, na ordem de detecção automática.</summary>
    public static IReadOnlyList<SimulatorInfo> All { get; } = new[]
    {
        IRacing,
        AssettoCorsaCompetizione,
        AssettoCorsaEvo,
        Automobilista2,
        LeMansUltimate
    };

    /// <summary>Opções exibidas na interface: automático primeiro.</summary>
    public static IReadOnlyList<SimulatorInfo> Selectable { get; } = new[]
    {
        Auto,
        IRacing,
        AssettoCorsaCompetizione,
        AssettoCorsaEvo,
        Automobilista2,
        LeMansUltimate
    };

    /// <summary>Descrição de um simulador; devolve <see cref="Auto"/> para valores desconhecidos.</summary>
    public static SimulatorInfo Find(SimulatorId id) => id switch
    {
        SimulatorId.IRacing => IRacing,
        SimulatorId.AssettoCorsaCompetizione => AssettoCorsaCompetizione,
        SimulatorId.AssettoCorsaEvo => AssettoCorsaEvo,
        SimulatorId.Automobilista2 => Automobilista2,
        SimulatorId.LeMansUltimate => LeMansUltimate,
        _ => Auto
    };

    /// <summary>Lê o identificador salvo nas preferências (texto), caindo no automático.</summary>
    public static SimulatorInfo Parse(string? storedId)
    {
        if (string.IsNullOrWhiteSpace(storedId))
        {
            return Auto;
        }

        return Enum.TryParse(storedId, ignoreCase: true, out SimulatorId id)
            ? Find(id)
            : Auto;
    }
}
