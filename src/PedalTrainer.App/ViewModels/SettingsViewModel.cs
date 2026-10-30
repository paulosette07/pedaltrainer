using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PedalTrainer.Core.Input.Pedals;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry;

namespace PedalTrainer.App.ViewModels;

/// <summary>Tela de configurações: parâmetros do treino e seleção do controlador.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly IShellActions _shell;

    private double _brakeTriggerPercent;
    private double _toleranceBandPercent;
    private double _chartWindowSeconds;
    private bool _showThrottleTrace;
    private bool _useIRacingAlignment;
    private bool _useSimulatedPedals;
    private PedalDeviceDescriptor? _selectedDevice;
    private PedalModel _selectedPedalModel = PedalModelCatalog.Unknown;
    private SimulatorInfo _selectedSimulator = SimulatorCatalog.Auto;
    private string _simulatorHint = SimulatorCatalog.Auto.SetupHint;
    private string _pedalProfileText = string.Empty;
    private string _detectedModelText = string.Empty;
    private string _message = string.Empty;

    public SettingsViewModel(IShellActions shell)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));

        Devices = new ObservableCollection<PedalDeviceDescriptor>();
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        SaveCommand = new RelayCommand(Save);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
        LoadDemoCommand = new RelayCommand(() => _shell.LoadDemoLap());
        ApplyPedalProfileCommand = new RelayCommand(ApplyPedalProfile);
        UseDetectedModelCommand = new RelayCommand(UseDetectedModel);

        LoadFromSettings();
        LoadPedalModels();
        LoadSimulators();
        RefreshDevices();
    }

    /// <summary>Simuladores suportados, com "detectar automaticamente" primeiro.</summary>
    public ObservableCollection<SimulatorInfo> Simulators { get; } = new();

    /// <summary>Catálogo de pedais do mercado, com o item "não listado" no fim.</summary>
    public ObservableCollection<PedalModel> PedalModels { get; } = new();

    public IRelayCommand ApplyPedalProfileCommand { get; }

    public IRelayCommand UseDetectedModelCommand { get; }

    public ObservableCollection<PedalDeviceDescriptor> Devices { get; }

    public IRelayCommand RefreshDevicesCommand { get; }

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand ResetDefaultsCommand { get; }

    public IRelayCommand LoadDemoCommand { get; }

    public double BrakeTriggerPercent
    {
        get => _brakeTriggerPercent;
        set => SetProperty(ref _brakeTriggerPercent, Math.Clamp(value, 1, 50));
    }

    public double ToleranceBandPercent
    {
        get => _toleranceBandPercent;
        set => SetProperty(ref _toleranceBandPercent, Math.Clamp(value, 0, 30));
    }

    public double ChartWindowSeconds
    {
        get => _chartWindowSeconds;
        set => SetProperty(ref _chartWindowSeconds, Math.Clamp(value, 3, 15));
    }

    public bool ShowThrottleTrace
    {
        get => _showThrottleTrace;
        set => SetProperty(ref _showThrottleTrace, value);
    }

    public bool UseIRacingAlignment
    {
        get => _useIRacingAlignment;
        set => SetProperty(ref _useIRacingAlignment, value);
    }

    public bool UseSimulatedPedals
    {
        get => _useSimulatedPedals;
        set
        {
            if (!SetProperty(ref _useSimulatedPedals, value))
            {
                return;
            }

            if (value)
            {
                _shell.SelectDevice(null);
            }
            else if (_selectedDevice is { } device)
            {
                _shell.SelectDevice(device.InstanceGuid);
            }
        }
    }

    public PedalDeviceDescriptor? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!SetProperty(ref _selectedDevice, value))
            {
                return;
            }

            if (value is not null)
            {
                _useSimulatedPedals = false;
                OnPropertyChanged(nameof(UseSimulatedPedals));
                _shell.SelectDevice(value.InstanceGuid);
            }
        }
    }

    /// <summary>Modelo de pedal escolhido; a troca já aplica os ajustes recomendados.</summary>
    public PedalModel SelectedPedalModel
    {
        get => _selectedPedalModel;
        set
        {
            if (value is null || !SetProperty(ref _selectedPedalModel, value))
            {
                return;
            }

            _shell.SelectPedalModel(value);
            UpdatePedalTexts();

            Message = value.IsCatchAll
                ? "Modelo removido: a calibração atual foi mantida."
                : $"Ajustes de {value.DisplayName} aplicados na calibração.";
        }
    }

    /// <summary>
    /// Simulador de onde vem a telemetria. A troca já reconecta a fonte e grava a escolha,
    /// então a barra superior passa a mostrar o estado do novo simulador imediatamente.
    /// </summary>
    public SimulatorInfo SelectedSimulator
    {
        get => _selectedSimulator;
        set
        {
            if (value is null || !SetProperty(ref _selectedSimulator, value))
            {
                return;
            }

            SimulatorHint = value.SetupHint;
            _shell.SelectSimulator(value);

            Message = value.IsAuto
                ? "Detecção automática ativa: o app procura qualquer simulador suportado."
                : $"Telemetria passando a ler {value.DisplayName}.";
        }
    }

    /// <summary>O que o piloto precisa fazer no jogo escolhido para a telemetria aparecer.</summary>
    public string SimulatorHint
    {
        get => _simulatorHint;
        private set => SetProperty(ref _simulatorHint, value);
    }

    /// <summary>Resumo do modelo escolhido (hardware, ajustes e observação).</summary>
    public string PedalProfileText
    {
        get => _pedalProfileText;
        private set => SetProperty(ref _pedalProfileText, value);
    }

    /// <summary>Sugestão de modelo reconhecida pelo nome do controlador conectado.</summary>
    public string DetectedModelText
    {
        get => _detectedModelText;
        private set => SetProperty(ref _detectedModelText, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    private void LoadFromSettings()
    {
        AppSettings settings = _shell.Settings;
        BrakeTriggerPercent = settings.BrakeTriggerPercent;
        ToleranceBandPercent = settings.ToleranceBandPercent;
        ChartWindowSeconds = settings.ChartWindowSeconds;
        ShowThrottleTrace = settings.ShowThrottleTrace;
        UseIRacingAlignment = settings.UseIRacingAlignment;
        UseSimulatedPedals = settings.UseSimulatedPedals;
    }

    /// <summary>Preenche a lista de simuladores e marca o que está em uso.</summary>
    private void LoadSimulators()
    {
        Simulators.Clear();

        foreach (SimulatorInfo simulator in _shell.Simulators)
        {
            Simulators.Add(simulator);
        }

        _selectedSimulator = _shell.SelectedSimulator;
        _simulatorHint = _selectedSimulator.SetupHint;

        OnPropertyChanged(nameof(SelectedSimulator));
        OnPropertyChanged(nameof(SimulatorHint));
    }

    private void RefreshDevices()
    {
        _shell.RefreshDevices();

        Devices.Clear();
        foreach (PedalDeviceDescriptor device in _shell.Devices)
        {
            Devices.Add(device);
        }

        Guid? selected = _shell.SelectedDeviceGuid;
        _selectedDevice = null;

        foreach (PedalDeviceDescriptor device in Devices)
        {
            if (selected is not null && device.InstanceGuid == selected)
            {
                _selectedDevice = device;
                break;
            }
        }

        OnPropertyChanged(nameof(SelectedDevice));
        Message = Devices.Count == 0
            ? "Nenhum controlador encontrado — use o modo simulado."
            : $"{Devices.Count} controlador(es) encontrado(s).";
    }

    /// <summary>Preenche a lista com o catálogo inteiro e marca o modelo em uso.</summary>
    private void LoadPedalModels()
    {
        PedalModels.Clear();

        foreach (PedalModel model in _shell.PedalModels)
        {
            PedalModels.Add(model);
        }

        PedalModels.Add(PedalModelCatalog.Unknown);

        _selectedPedalModel = _shell.SelectedPedalModel;
        OnPropertyChanged(nameof(SelectedPedalModel));
        UpdatePedalTexts();
    }

    /// <summary>Reaplica curva, zona morta e filtro do modelo escolhido.</summary>
    private void ApplyPedalProfile()
    {
        if (_selectedPedalModel.IsCatchAll)
        {
            Message = "Escolha um modelo de pedal para aplicar os ajustes recomendados.";
            return;
        }

        _shell.ApplyPedalProfile();
        UpdatePedalTexts();
        Message = $"Ajustes de {_selectedPedalModel.DisplayName} reaplicados na calibração.";
    }

    /// <summary>Adota o modelo reconhecido pelo nome do controlador conectado.</summary>
    private void UseDetectedModel()
    {
        PedalModel? detected = _shell.DetectedPedalModel;
        if (detected is null)
        {
            Message = "Nenhum modelo conhecido foi reconhecido no nome do controlador.";
            return;
        }

        SelectedPedalModel = detected;
    }

    /// <summary>Atualiza o resumo do modelo e a sugestão vinda do controlador.</summary>
    private void UpdatePedalTexts()
    {
        PedalProfileText = _selectedPedalModel.IsCatchAll
            ? _selectedPedalModel.Notes
            : $"{_selectedPedalModel.HardwareLabel}\n{_selectedPedalModel.TuningLabel}\n{_selectedPedalModel.Notes}";

        PedalModel? detected = _shell.DetectedPedalModel;
        DetectedModelText = detected is null
            ? "Nenhum modelo do catálogo reconhecido no controlador atual."
            : $"Reconhecido: {detected.DisplayName}.";
    }

    private void Save()
    {
        AppSettings settings = _shell.Settings;
        settings.BrakeTriggerPercent = BrakeTriggerPercent;
        settings.ToleranceBandPercent = ToleranceBandPercent;
        settings.ChartWindowSeconds = ChartWindowSeconds;
        settings.ShowThrottleTrace = ShowThrottleTrace;
        settings.UseIRacingAlignment = UseIRacingAlignment;
        settings.UseSimulatedPedals = UseSimulatedPedals;

        _shell.SaveSettings();
        Message = "Configurações salvas.";
    }

    private void ResetDefaults()
    {
        var defaults = new AppSettings();
        BrakeTriggerPercent = defaults.BrakeTriggerPercent;
        ToleranceBandPercent = defaults.ToleranceBandPercent;
        ChartWindowSeconds = defaults.ChartWindowSeconds;
        ShowThrottleTrace = defaults.ShowThrottleTrace;
        UseIRacingAlignment = defaults.UseIRacingAlignment;
        Message = "Padrões restaurados — clique em Salvar para aplicar.";
    }
}
