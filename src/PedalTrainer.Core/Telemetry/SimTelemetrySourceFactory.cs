using System;
using System.Collections.Generic;
using PedalTrainer.Core.Telemetry.AssettoCorsaCompetizione;
using PedalTrainer.Core.Telemetry.AssettoCorsaEvo;
using PedalTrainer.Core.Telemetry.Automobilista2;
using PedalTrainer.Core.Telemetry.IRacing;
using PedalTrainer.Core.Telemetry.LeMansUltimate;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Monta a fonte de telemetria para o simulador escolhido. Em "detectar automaticamente"
/// a fonte recebe todos os leitores, na ordem do catálogo, e usa o primeiro que publicar.
/// </summary>
public static class SimTelemetrySourceFactory
{
    /// <summary>Cria a fonte de telemetria descrita por <paramref name="simulator"/>.</summary>
    public static ICarTelemetrySource Create(SimulatorInfo simulator)
    {
        ArgumentNullException.ThrowIfNull(simulator);

        IReadOnlyList<ISimTelemetryReader> readers = CreateReaders(simulator);
        return readers.Count == 0
            ? new NullCarTelemetrySource()
            : new SimTelemetrySource(simulator, readers);
    }

    /// <summary>Cria a fonte de telemetria do simulador pedido.</summary>
    public static ICarTelemetrySource Create(SimulatorId simulatorId) =>
        Create(SimulatorCatalog.Find(simulatorId));

    /// <summary>Leitores usados por um simulador (todos eles quando a escolha é automática).</summary>
    public static IReadOnlyList<ISimTelemetryReader> CreateReaders(SimulatorInfo simulator)
    {
        ArgumentNullException.ThrowIfNull(simulator);

        var readers = new List<ISimTelemetryReader>();

        if (simulator.Id is SimulatorId.Auto or SimulatorId.IRacing)
        {
            readers.Add(new IRacingTelemetryReader());
        }

        if (simulator.Id is SimulatorId.Auto or SimulatorId.AssettoCorsaCompetizione)
        {
            readers.Add(new AccTelemetryReader());
        }

        if (simulator.Id is SimulatorId.Auto or SimulatorId.AssettoCorsaEvo)
        {
            readers.Add(new AcEvoTelemetryReader());
        }

        if (simulator.Id is SimulatorId.Auto or SimulatorId.Automobilista2)
        {
            readers.Add(new Automobilista2TelemetryReader());
        }

        if (simulator.Id is SimulatorId.Auto or SimulatorId.LeMansUltimate)
        {
            readers.Add(new LeMansUltimateTelemetryReader());
        }

        return readers;
    }
}
