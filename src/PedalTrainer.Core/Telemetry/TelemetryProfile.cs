using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Configuração de telemetria de um jogo: onde ele publica os dados, quais canais entrega,
/// em que taxa são gravados e o que o piloto precisa ligar dentro do simulador.
/// A tela de telemetria e o gravador leem tudo daqui — nada fica escrito no código da UI.
/// </summary>
public sealed record TelemetryProfile(
    SimulatorInfo Simulator,
    IReadOnlyList<string> MemoryMapNames,
    TelemetryChannel Channels,
    double RecordingRateHz,
    string DataSourceNote,
    string RecordingRateNote)
{
    /// <summary>Identificador do simulador dono do perfil.</summary>
    public SimulatorId Id => Simulator.Id;

    /// <summary>Nome exibido do jogo.</summary>
    public string DisplayName => Simulator.DisplayName;

    /// <summary>Apelido curto, usado no nome do arquivo gravado.</summary>
    public string ShortName => Simulator.ShortName;

    /// <summary>O que ligar dentro do jogo para a telemetria aparecer.</summary>
    public string SetupHint => Simulator.SetupHint;

    /// <summary>Verdadeiro quando o jogo exige um plugin instalado.</summary>
    public bool RequiresPlugin => Simulator.RequiresPlugin;

    /// <summary>Verdadeiro para o perfil "detectar automaticamente".</summary>
    public bool IsAuto => Simulator.IsAuto;

    /// <summary>Blocos de memória compartilhada lidos, um por linha.</summary>
    public string MemoryMapSummary => MemoryMapNames.Count == 0
        ? "detectado ao conectar"
        : string.Join(Environment.NewLine, MemoryMapNames);

    /// <summary>Canais publicados pelo jogo, em texto.</summary>
    public string ChannelSummary => TelemetryChannelNames.Describe(Channels);

    /// <summary>Verdadeiro quando o jogo entrega o canal pedido.</summary>
    public bool Provides(TelemetryChannel channel) => (Channels & channel) == channel;

    /// <summary>Colunas do arquivo gravado para este jogo.</summary>
    public IReadOnlyList<TelemetryColumn> Columns => TelemetryColumn.For(Channels);
}
