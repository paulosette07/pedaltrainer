using System;
using System.Collections.Generic;
using PedalTrainer.Core.IRacing;
using PedalTrainer.Core.Telemetry.AssettoCorsaCompetizione;
using PedalTrainer.Core.Telemetry.AssettoCorsaEvo;
using PedalTrainer.Core.Telemetry.Automobilista2;
using PedalTrainer.Core.Telemetry.LeMansUltimate;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Configuração de telemetria de cada jogo suportado: blocos de memória lidos, canais
/// publicados, taxa de gravação e o que ligar dentro do simulador. É a única fonte de
/// verdade da tela de telemetria — trocar de jogo troca tudo de uma vez.
/// </summary>
public static class TelemetryProfileCatalog
{
    private const double DefaultRecordingRateHz = 60.0;

    /// <summary>Perfil de "detectar automaticamente": vale o do jogo que conectar.</summary>
    public static TelemetryProfile Auto { get; } = new(
        SimulatorCatalog.Auto,
        Array.Empty<string>(),
        TelemetryChannel.All,
        DefaultRecordingRateHz,
        "O aplicativo procura a memória compartilhada de cada jogo da lista e usa a primeira que responder.",
        "Ao conectar, a gravação passa a usar os canais e a taxa do jogo encontrado.");

    public static TelemetryProfile IRacing { get; } = new(
        SimulatorCatalog.IRacing,
        new[] { IRacingSdkLayout.MemoryMapName },
        TelemetryChannel.All,
        DefaultRecordingRateHz,
        "Bloco do IRSDK: o jogo publica os canais crus (Speed, Brake, Throttle, LapDistPct, Lap, SessionTime).",
        "O IRSDK atualiza a 60 Hz; gravamos 60 amostras por segundo, uma por atualização.");

    public static TelemetryProfile AssettoCorsaCompetizione { get; } = new(
        SimulatorCatalog.AssettoCorsaCompetizione,
        new[] { AccSharedMemoryLayout.PhysicsMapName, AccSharedMemoryLayout.GraphicsMapName },
        TelemetryChannel.All & ~TelemetryChannel.SessionTime,
        DefaultRecordingRateHz,
        "Página de física (pedais e velocidade) + página gráfica (volta e posição na pista).",
        "Os pedais vêm do passo de física; gravamos 60 amostras por segundo, a mesma taxa do painel.");

    public static TelemetryProfile AssettoCorsaEvo { get; } = new(
        SimulatorCatalog.AssettoCorsaEvo,
        new[] { AcEvoSharedMemoryLayout.PhysicsMapName, AcEvoSharedMemoryLayout.GraphicsMapName },
        TelemetryChannel.All & ~TelemetryChannel.SessionTime,
        DefaultRecordingRateHz,
        "Página de física (pedais e velocidade) + página gráfica (volta e posição na pista).",
        "O jogo atualiza a física a cada passo; gravamos 60 amostras por segundo, a mesma taxa do painel.");

    public static TelemetryProfile Automobilista2 { get; } = new(
        SimulatorCatalog.Automobilista2,
        new[] { Pcars2SharedMemoryLayout.MemoryMapName },
        TelemetryChannel.All & ~TelemetryChannel.SessionTime,
        DefaultRecordingRateHz,
        "Bloco Project CARS 2: pedais e velocidade do carro, mais a distância na volta do piloto.",
        "Gravamos 60 amostras por segundo; a posição na volta é calculada pela distância dividida pelo comprimento da pista.");

    public static TelemetryProfile LeMansUltimate { get; } = new(
        SimulatorCatalog.LeMansUltimate,
        new[]
        {
            RFactorSharedMemoryLayout.TelemetryMapName,
            RFactorSharedMemoryLayout.ScoringMapName
        },
        TelemetryChannel.All,
        DefaultRecordingRateHz,
        "Bloco de telemetria (pedais, velocidade e tempo de sessão) + bloco de classificação (posição na pista).",
        "O plugin escreve em duas etapas; gravamos 60 amostras por segundo e descartamos os quadros com versão instável.");

    /// <summary>Todos os perfis, com o automático na frente (mesma ordem do catálogo).</summary>
    public static IReadOnlyList<TelemetryProfile> All { get; } = new[]
    {
        Auto,
        IRacing,
        AssettoCorsaCompetizione,
        AssettoCorsaEvo,
        Automobilista2,
        LeMansUltimate
    };

    /// <summary>Perfil do simulador escolhido nas preferências.</summary>
    public static TelemetryProfile Find(SimulatorInfo simulator) => simulator is null
        ? Auto
        : Find(simulator.Id);

    /// <summary>Perfil de um simulador; devolve o automático para valores desconhecidos.</summary>
    public static TelemetryProfile Find(SimulatorId simulatorId) => simulatorId switch
    {
        SimulatorId.IRacing => IRacing,
        SimulatorId.AssettoCorsaCompetizione => AssettoCorsaCompetizione,
        SimulatorId.AssettoCorsaEvo => AssettoCorsaEvo,
        SimulatorId.Automobilista2 => Automobilista2,
        SimulatorId.LeMansUltimate => LeMansUltimate,
        _ => Auto
    };

    /// <summary>
    /// Perfil efetivo da tela: quando o simulador é detectado sozinho, vale o perfil do jogo
    /// que está realmente publicando; sem jogo conectado, fica o "automático".
    /// </summary>
    public static TelemetryProfile Resolve(SimulatorInfo requested, SimulatorInfo? connected) =>
        requested is null || requested.IsAuto
            ? connected is null ? Auto : Find(connected)
            : Find(requested);
}
