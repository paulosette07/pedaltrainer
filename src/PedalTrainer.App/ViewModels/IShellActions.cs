using System;
using System.Collections.Generic;
using PedalTrainer.Core.Input.Pedals;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry;
using PedalTrainer.Core.Tracks;
using PedalTrainer.Core.Training;

namespace PedalTrainer.App.ViewModels;

/// <summary>
/// Operações do shell que as telas de configuração precisam disparar
/// (trocar dispositivo, salvar preferências, carregar referência).
/// </summary>
public interface IShellActions
{
    TrainingEngine Engine { get; }

    AppSettings Settings { get; }

    IReadOnlyList<PedalDeviceDescriptor> Devices { get; }

    Guid? SelectedDeviceGuid { get; }

    /// <summary>Seleciona o controlador de pedais (nulo = usar o simulado).</summary>
    void SelectDevice(Guid? deviceGuid);

    /// <summary>Modelos de pedal do mercado disponíveis para escolha.</summary>
    IReadOnlyList<PedalModel> PedalModels { get; }

    /// <summary>Modelo de pedal escolhido (nunca nulo: "não listado" quando nada foi escolhido).</summary>
    PedalModel SelectedPedalModel { get; }

    /// <summary>Modelo reconhecido pelo nome do controlador conectado, quando houver.</summary>
    PedalModel? DetectedPedalModel { get; }

    /// <summary>Calibração em uso, para a tela mostrar de onde vieram curva e filtro.</summary>
    PedalCalibration CurrentCalibration { get; }

    /// <summary>Troca o modelo de pedal e reaplica os ajustes recomendados na calibração.</summary>
    void SelectPedalModel(PedalModel model);

    /// <summary>Reaplica os ajustes recomendados do modelo escolhido.</summary>
    void ApplyPedalProfile();

    /// <summary>Redescobre os controladores conectados.</summary>
    void RefreshDevices();

    /// <summary>Aplica e grava as preferências atuais.</summary>
    void SaveSettings();

    /// <summary>Carrega a volta de demonstração embutida.</summary>
    void LoadDemoLap();

    /// <summary>Aplica uma calibração recém-editada ao motor.</summary>
    void ApplyCalibration(PedalCalibration calibration, bool persist);

    /// <summary>Simuladores disponíveis para escolha, com "detectar automaticamente" primeiro.</summary>
    IReadOnlyList<SimulatorInfo> Simulators { get; }

    /// <summary>Simulador escolhido para a telemetria (nunca nulo).</summary>
    SimulatorInfo SelectedSimulator { get; }

    /// <summary>Troca o simulador lido e reconecta a fonte de telemetria na hora.</summary>
    void SelectSimulator(SimulatorInfo simulator);

    /// <summary>Pistas disponíveis: as do sistema mais as importadas pelo piloto.</summary>
    IReadOnlyList<TrackDefinition> Tracks { get; }

    /// <summary>Pista em uso na aba de treino (nunca nula).</summary>
    TrackDefinition SelectedTrack { get; }

    /// <summary>Troca a pista, carrega a referência dela e grava a escolha.</summary>
    void SelectTrack(TrackDefinition track);

    /// <summary>Referências oferecidas para a pista atual (sistema + arquivos do piloto).</summary>
    IReadOnlyList<TrackReferenceSource> ListReferenceSources();

    /// <summary>Carrega a referência escolhida na lista.</summary>
    bool LoadReferenceSource(TrackReferenceSource source, out string? error);

    /// <summary>Copia uma telemetria escolhida pelo piloto para a pasta da pista atual.</summary>
    string ImportReferenceFile(string sourcePath);

    /// <summary>Importa um traçado e passa a usá-lo como pista atual.</summary>
    TrackDefinition? ImportTrackMap(string sourcePath, out string? error);

    /// <summary>Filtro de arquivo para importar um traçado (formato JSON).</summary>
    string TrackMapFileFilter { get; }

    /// <summary>Filtro de arquivo para carregar uma telemetria de referência.</summary>
    string ReferenceFileFilter { get; }
}
