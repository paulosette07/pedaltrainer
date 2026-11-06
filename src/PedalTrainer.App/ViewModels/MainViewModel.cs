using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Input;
using PedalTrainer.Core.Input.Pedals;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Recording;
using PedalTrainer.Core.Reference;
using PedalTrainer.Core.Services;
using PedalTrainer.Core.Telemetry;
using PedalTrainer.Core.Tracks;
using PedalTrainer.Core.Training;

namespace PedalTrainer.App.ViewModels;

/// <summary>
/// Shell do aplicativo: mantém o motor, os dispositivos, as preferências e a navegação
/// entre treino, calibração e configurações. A interface só conversa com este ViewModel.
/// </summary>
public sealed class MainViewModel : ObservableObject, IShellActions
{
    /// <summary>Taxa de atualização da interface.</summary>
    public const double UiRateHz = 60.0;

    private readonly SettingsStore _settingsStore;
    private readonly CalibrationStore _calibrationStore;
    private readonly ReferenceLapFileLoader _referenceLoader = new();
    private readonly TrackStore _trackStore;
    private readonly ReferenceLibrary _referenceLibrary;
    private readonly DispatcherTimer _timer;
    private readonly List<PedalDeviceDescriptor> _devices = new();
    private readonly List<object> _screens = new();

    private List<TrackDefinition> _tracks;
    private TrackDefinition _selectedTrack;
    private AppSettings _settings;
    private PedalCalibration _calibration;
    private PedalModel _pedalModel = PedalModelCatalog.Unknown;
    private object _currentScreen;
    private int _selectedScreenIndex;
    private string _shellMessage = string.Empty;
    private string _referenceTitle = "Nenhuma referência carregada";
    private string _deviceSummary = "Procurando controlador...";
    private string _simulatorSummary = "Simulador: não conectado";
    private SimulatorInfo _simulator = SimulatorCatalog.Auto;
    private string _recordButtonText = "Gravar volta";
    private string _recordingStatusText = string.Empty;
    private string _lastRecorderStatus = string.Empty;
    private IntPtr _windowHandle;

    public MainViewModel(
        TrainingEngine engine,
        SettingsStore settingsStore,
        CalibrationStore calibrationStore,
        DrillProgramStore drillProgramStore,
        TrackStore trackStore,
        ReferenceLibrary referenceLibrary)
    {
        Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _calibrationStore = calibrationStore ?? throw new ArgumentNullException(nameof(calibrationStore));
        _trackStore = trackStore ?? throw new ArgumentNullException(nameof(trackStore));
        _referenceLibrary = referenceLibrary ?? throw new ArgumentNullException(nameof(referenceLibrary));

        _settings = _settingsStore.Load();
        _calibration = _calibrationStore.Load();
        _pedalModel = PedalModelCatalog.Find(_settings.PedalModelId);
        _simulator = SimulatorCatalog.Parse(_settings.SimulatorId);

        _tracks = new List<TrackDefinition>(TrackCatalog.Merge(_trackStore.LoadAll()));
        _selectedTrack = TrackCatalog.Find(_tracks, _settings.TrackId) ?? TrackCatalog.Default;

        TrackPanel = new TrackPanelViewModel(this);
        Training = new TrainingViewModel(engine, TrackPanel);
        Drills = new DrillViewModel(engine, drillProgramStore ?? throw new ArgumentNullException(nameof(drillProgramStore)));
        Calibration = new CalibrationViewModel(this);
        SettingsScreen = new SettingsViewModel(this);

        _screens.Add(Training);
        _screens.Add(Drills);
        _screens.Add(Calibration);
        _screens.Add(SettingsScreen);
        _currentScreen = Training;

        ScreenTitles = new ObservableCollection<string> { "Treino", "Programas", "Calibração", "Configurações" };

        LoadReferenceCommand = new RelayCommand(LoadReferenceWithDialog);
        LoadDemoCommand = new RelayCommand(LoadDemoLap);
        RecordLapCommand = new RelayCommand(ToggleRecording);
        ShowTrainingCommand = new RelayCommand(() => SelectedScreenIndex = 0);
        ShowDrillsCommand = new RelayCommand(() => SelectedScreenIndex = 1);
        ShowCalibrationCommand = new RelayCommand(() => SelectedScreenIndex = 2);
        ShowSettingsCommand = new RelayCommand(() => SelectedScreenIndex = 3);

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(1.0 / UiRateHz)
        };
        _timer.Tick += OnUiTick;
    }

    public TrainingEngine Engine { get; }

    public TrainingViewModel Training { get; }

    /// <summary>Cartão da pista dentro da aba de treino (mapa, seleção e referência).</summary>
    public TrackPanelViewModel TrackPanel { get; }

    /// <summary>Tela de programas de treino (exercícios de freio e acelerador).</summary>
    public DrillViewModel Drills { get; }

    /// <summary>Pistas disponíveis: do sistema e importadas.</summary>
    public IReadOnlyList<TrackDefinition> Tracks => _tracks;

    /// <summary>Pista em uso na aba de treino.</summary>
    public TrackDefinition SelectedTrack => _selectedTrack;

    /// <summary>Filtro do diálogo de importação de traçado.</summary>
    public string TrackMapFileFilter => "Traçado de pista (*.json)|*.json|Todos os arquivos|*.*";

    /// <summary>Filtro do diálogo de telemetria de referência.</summary>
    public string ReferenceFileFilter => _referenceLoader.BuildFileDialogFilter();

    public CalibrationViewModel Calibration { get; }

    public SettingsViewModel SettingsScreen { get; }

    public ObservableCollection<string> ScreenTitles { get; }

    public IRelayCommand LoadReferenceCommand { get; }

    public IRelayCommand LoadDemoCommand { get; }

    /// <summary>Arma ou cancela a gravação de uma volta do iRacing.</summary>
    public IRelayCommand RecordLapCommand { get; }

    public IRelayCommand ShowTrainingCommand { get; }

    public IRelayCommand ShowDrillsCommand { get; }

    public IRelayCommand ShowCalibrationCommand { get; }

    public IRelayCommand ShowSettingsCommand { get; }

    /// <summary>Texto do botão de gravação (alterna entre gravar e cancelar).</summary>
    public string RecordButtonText
    {
        get => _recordButtonText;
        private set => SetProperty(ref _recordButtonText, value);
    }

    /// <summary>Status da gravação exibido na barra superior (vazio quando parado).</summary>
    public string RecordingStatusText
    {
        get => _recordingStatusText;
        private set => SetProperty(ref _recordingStatusText, value);
    }

    public AppSettings Settings => _settings;

    public IReadOnlyList<PedalDeviceDescriptor> Devices => _devices;

    public Guid? SelectedDeviceGuid { get; private set; }

    public IReadOnlyList<PedalModel> PedalModels => PedalModelCatalog.All;

    public PedalModel SelectedPedalModel => _pedalModel;

    public PedalCalibration CurrentCalibration => _calibration;

    /// <summary>Modelo reconhecido pelo nome do controlador conectado, quando houver.</summary>
    public PedalModel? DetectedPedalModel
    {
        get
        {
            PedalDeviceDescriptor? descriptor = Engine.Device?.Descriptor;
            return descriptor is null ? null : PedalModelCatalog.Match(descriptor.Name, descriptor.ProductName);
        }
    }

    /// <summary>Troca o modelo de pedal e aplica os ajustes recomendados na calibração.</summary>
    public void SelectPedalModel(PedalModel model)
    {
        _pedalModel = model ?? PedalModelCatalog.Unknown;
        _settings.PedalModelId = _pedalModel.IsCatchAll ? string.Empty : _pedalModel.Id;
        _settingsStore.Save(_settings);

        if (!_pedalModel.IsCatchAll)
        {
            ApplyPedalProfile();
        }
    }

    /// <summary>Reaplica curva, zona morta e filtro recomendados pelo modelo escolhido.</summary>
    public void ApplyPedalProfile()
    {
        PedalCalibration updated = PedalProfileApplier.Apply(_pedalModel, _calibration);

        updated.DeviceGuid = _calibration.DeviceGuid;
        updated.DeviceName = _calibration.DeviceName;

        _calibration = updated;
        Engine.SetCalibration(_calibration);
        _calibrationStore.Save(_calibration);
        Calibration.ReloadFromStore(_calibration);
    }

    /// <summary>Tela exibida no momento (treino, calibração ou configurações).</summary>
    public object CurrentScreen
    {
        get => _currentScreen;
        private set => SetProperty(ref _currentScreen, value);
    }

    public int SelectedScreenIndex
    {
        get => _selectedScreenIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, _screens.Count - 1);
            if (!SetProperty(ref _selectedScreenIndex, clamped))
            {
                return;
            }

            CurrentScreen = _screens[clamped];

            if (CurrentScreen is CalibrationViewModel calibration)
            {
                calibration.ReloadFromStore(_calibration);
            }
        }
    }

    /// <summary>Mensagem curta exibida no rodapé (erros de arquivo, avisos).</summary>
    public string ShellMessage
    {
        get => _shellMessage;
        private set => SetProperty(ref _shellMessage, value);
    }

    /// <summary>Versão do app exibida no rodapé, lida dos metadados do executável.</summary>
    public string AppVersion { get; } = ReadAppVersion();

    /// <summary>Nome da referência em uso, para a barra superior.</summary>
    public string ReferenceTitle
    {
        get => _referenceTitle;
        private set => SetProperty(ref _referenceTitle, value);
    }

    /// <summary>Resumo do controlador em uso, para a barra superior.</summary>
    public string DeviceSummary
    {
        get => _deviceSummary;
        private set => SetProperty(ref _deviceSummary, value);
    }

    /// <summary>Simuladores oferecidos na tela de configurações.</summary>
    public IReadOnlyList<SimulatorInfo> Simulators => SimulatorCatalog.Selectable;

    /// <summary>Simulador escolhido para a telemetria.</summary>
    public SimulatorInfo SelectedSimulator => _simulator;

    /// <summary>Estado da telemetria do simulador, para a barra superior.</summary>
    public string SimulatorSummary
    {
        get => _simulatorSummary;
        private set => SetProperty(ref _simulatorSummary, value);
    }

    /// <summary>
    /// Troca o simulador lido: a fonte antiga é encerrada e a nova começa a procurar a
    /// memória compartilhada na hora. A escolha vai para as preferências.
    /// </summary>
    public void SelectSimulator(SimulatorInfo simulator)
    {
        ArgumentNullException.ThrowIfNull(simulator);

        _simulator = simulator;
        _settings.SimulatorId = simulator.IsAuto ? string.Empty : simulator.Id.ToString();
        _settingsStore.Save(_settings);

        ApplySimulatorSource();
        OnPropertyChanged(nameof(SelectedSimulator));
    }

    /// <summary>Monta a fonte de telemetria do simulador escolhido (todos, no automático).</summary>
    private void ApplySimulatorSource()
    {
        Engine.SetCarTelemetrySource(SimTelemetrySourceFactory.Create(_simulator));
        SimulatorSummary = Engine.CarSource.StatusDescription;
    }

    /// <summary>Troca a pista e já carrega a volta ideal dela como referência.</summary>
    public void SelectTrack(TrackDefinition track)
    {
        ArgumentNullException.ThrowIfNull(track);

        _selectedTrack = track;
        _settings.TrackId = track.Id;
        _settingsStore.Save(_settings);

        LoadSystemReference(track);
        OnPropertyChanged(nameof(SelectedTrack));

        ShellMessage = $"Pista: {track.Name} · {track.LocationText}.";
    }

    /// <summary>Referências oferecidas para a pista atual.</summary>
    public IReadOnlyList<TrackReferenceSource> ListReferenceSources() =>
        _referenceLibrary.ListSources(_selectedTrack);

    /// <summary>Carrega a referência escolhida na lista da pista atual.</summary>
    public bool LoadReferenceSource(TrackReferenceSource source, out string? error)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!_referenceLibrary.TryLoad(source, _selectedTrack, out ReferenceLap? lap, out error) || lap is null)
        {
            return false;
        }

        ApplyReference(lap, source.IsSystem ? null : source.Path);
        TrackPanel.NotifyReferenceChanged();
        ShellMessage = $"Referência: {source.Label} · {_selectedTrack.ShortName}.";
        return true;
    }

    /// <summary>Copia a telemetria escolhida para a pasta da pista atual.</summary>
    public string ImportReferenceFile(string sourcePath)
    {
        string destination = _referenceLibrary.ImportLap(_selectedTrack, sourcePath);
        ShellMessage = $"Telemetria importada para {_selectedTrack.ShortName}.";
        return destination;
    }

    /// <summary>Importa um traçado (JSON) e passa a usá-lo como pista atual.</summary>
    public TrackDefinition? ImportTrackMap(string sourcePath, out string? error)
    {
        if (!_trackStore.TryImport(sourcePath, out TrackDefinition? track, out error) || track is null)
        {
            return null;
        }

        _tracks = new List<TrackDefinition>(TrackCatalog.Merge(_trackStore.LoadAll()));
        OnPropertyChanged(nameof(Tracks));

        SelectTrack(track);
        ShellMessage = $"Traçado de {track.Name} importado de {Path.GetFileName(sourcePath)}.";
        return track;
    }

    /// <summary>Carrega a volta ideal gerada do traçado (a referência padrão da pista).</summary>
    private void LoadSystemReference(TrackDefinition track)
    {
        ReferenceLap lap = _referenceLibrary.GetIdealLap(track);
        ApplyReference(lap, null);
    }

    /// <summary>Verdadeiro quando o arquivo está na pasta de referências desta pista.</summary>
    private bool IsTrackReferenceFile(TrackDefinition track, string path)
    {
        try
        {
            string directory = Path.GetFullPath(_referenceLibrary.GetReferenceDirectory(track));
            string? parent = Path.GetDirectoryName(Path.GetFullPath(path));

            return parent is not null && string.Equals(parent, directory, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Janela usada pelo DirectInput no nível de cooperação em background.</summary>
    public void AttachWindow(IntPtr handle)
    {
        _windowHandle = handle;
    }

    /// <summary>Inicializa hardware, motor e temporizador da interface.</summary>
    public void Initialize()
    {
        AppPaths.EnsureCreated();

        Engine.ApplySettings(_settings);
        Engine.SetCalibration(_calibration);
        ApplySimulatorSource();

        Training.TolerancePercent = _settings.ToleranceBandPercent;
        Training.ShowThrottle = _settings.ShowThrottleTrace;

        RefreshDevices();
        ReconnectDevice();
        DetectPedalModel();

        Engine.Start();
        _timer.Start();
        RestoreStartupReference();
    }

    /// <summary>Encerra threads e libera hardware.</summary>
    public void Shutdown()
    {
        _timer.Stop();
        _settings.UseSimulatedPedals = Engine.Device is SimulatedPedalDevice;
        _settings.LastDeviceGuid = SelectedDeviceGuid ?? Guid.Empty;
        _settingsStore.Save(_settings);
        _calibrationStore.Save(_calibration);
        Engine.Dispose();
    }

    public void SelectDevice(Guid? deviceGuid)
    {
        SelectedDeviceGuid = deviceGuid;
        _settings.UseSimulatedPedals = deviceGuid is null;
        ReconnectDevice();
    }

    public void RefreshDevices()
    {
        _devices.Clear();
        _devices.AddRange(PedalDeviceEnumerator.Enumerate());

        if (SelectedDeviceGuid is null && !_settings.UseSimulatedPedals)
        {
            Guid stored = _settings.LastDeviceGuid;
            PedalDeviceDescriptor? match = stored == Guid.Empty
                ? PedalDeviceEnumerator.PickDefault(_devices)
                : _devices.Find(d => d.InstanceGuid == stored) ?? PedalDeviceEnumerator.PickDefault(_devices);

            SelectedDeviceGuid = match?.InstanceGuid;
        }
    }

    public void SaveSettings()
    {
        _settings.LastDeviceGuid = SelectedDeviceGuid ?? Guid.Empty;
        Engine.ApplySettings(_settings);
        _settingsStore.Save(_settings);

        Training.TolerancePercent = _settings.ToleranceBandPercent;
        Training.ShowThrottle = _settings.ShowThrottleTrace;

        ShellMessage = "Preferências salvas.";
    }

    public void ApplyCalibration(PedalCalibration calibration, bool persist)
    {
        _calibration = calibration ?? new PedalCalibration();
        Engine.SetCalibration(_calibration);

        if (persist)
        {
            _calibrationStore.Save(_calibration);
        }
    }

    /// <summary>Carrega a volta ideal do sistema para a pista atual.</summary>
    public void LoadDemoLap()
    {
        LoadSystemReference(_selectedTrack);
        TrackPanel.ReloadReferences();

        ShellMessage = $"Volta ideal de {_selectedTrack.ShortName} carregada. Pise no freio para começar.";
    }

    private void LoadReferenceWithDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Carregar telemetria de referência",
            Filter = _referenceLoader.BuildFileDialogFilter(),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string imported;

        try
        {
            imported = ImportReferenceFile(dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShellMessage = exception.Message;
            return;
        }

        if (LoadReferenceFromFile(imported))
        {
            TrackPanel.ReloadReferences();
            TrackPanel.SelectReferenceFromPath(imported);
        }
    }

    /// <summary>Carrega uma referência de um arquivo e a aplica ao motor.</summary>
    public bool LoadReferenceFromFile(string path)
    {
        if (!_referenceLoader.TryLoad(path, out ReferenceLap? lap, out string? error) || lap is null)
        {
            ShellMessage = error ?? "Não foi possível ler o arquivo.";
            return false;
        }

        ApplyReference(lap, path);
        ShellMessage = $"Referência carregada de {Path.GetFileName(path)}.";
        return true;
    }

    /// <summary>Aplica uma volta ao motor, atualiza a barra e guarda o caminho para a próxima sessão.</summary>
    private void ApplyReference(ReferenceLap lap, string? path)
    {
        Engine.LoadReference(lap);
        Training.Reset();

        _settings.LastReferencePath = string.IsNullOrWhiteSpace(path) ? string.Empty : path;

        ReferenceTitle = $"{_selectedTrack.ShortName} · {lap.DurationSeconds:0.0} s · {Engine.Session.Zones.Count} zonas";
    }

    /// <summary>
    /// Abre o aplicativo pronto para treinar: Interlagos (ou a pista salva) com a volta ideal
    /// do sistema, ou com a última telemetria do piloto para aquela pista.
    /// </summary>
    private void RestoreStartupReference()
    {
        string lastPath = _settings.LastReferencePath;
        bool restoreFile = !string.IsNullOrWhiteSpace(lastPath) &&
                           File.Exists(lastPath) &&
                           IsTrackReferenceFile(_selectedTrack, lastPath);

        if (restoreFile && LoadReferenceFromFile(lastPath))
        {
            TrackPanel.ReloadReferences();
            TrackPanel.SelectReferenceFromPath(lastPath);
            ShellMessage = $"{_selectedTrack.ShortName}: referência {Path.GetFileName(lastPath)} recarregada.";
            return;
        }

        LoadSystemReference(_selectedTrack);
        TrackPanel.ReloadReferences();
        ShellMessage = $"{_selectedTrack.ShortName} pronta: volta ideal do sistema carregada. Pise no freio para treinar.";
    }

    /// <summary>
    /// Reconhece o modelo pelo nome do controlador quando o piloto ainda não escolheu
    /// nenhum: um conjunto conhecido já entra com curva e filtro adequados.
    /// </summary>
    private void DetectPedalModel()
    {
        if (!_pedalModel.IsCatchAll)
        {
            return;
        }

        PedalModel? detected = DetectedPedalModel;
        if (detected is null)
        {
            return;
        }

        _pedalModel = detected;
        _settings.PedalModelId = detected.Id;
        _settingsStore.Save(_settings);
        ApplyPedalProfile();
    }

    private void ReconnectDevice()
    {
        IPedalDevice device = CreateDevice();
        Engine.SetDevice(device);
        DeviceSummary = $"{device.Descriptor.DisplayName}";
    }

    private IPedalDevice CreateDevice()
    {
        if (_settings.UseSimulatedPedals || SelectedDeviceGuid is null)
        {
            var simulated = new SimulatedPedalDevice();
            simulated.SetReferenceLap(Engine.Session.ReferenceLap);
            return simulated;
        }

        PedalDeviceDescriptor? descriptor = _devices.Find(d => d.InstanceGuid == SelectedDeviceGuid.Value);
        if (descriptor is null)
        {
            var fallback = new SimulatedPedalDevice();
            fallback.SetReferenceLap(Engine.Session.ReferenceLap);
            return fallback;
        }

        return new DirectInputPedalDevice(descriptor, _windowHandle);
    }

    private void OnUiTick(object? sender, EventArgs e)
    {
        PumpRecording();

        Training.Apply(Engine.Latest);

        // O estado dos programas é sempre atualizado (para não perder resultados quando a
        // tela está oculta); o gráfico só é reconstruído quando ela está visível.
        Drills.Apply(Engine.LatestDrill, CurrentScreen is DrillViewModel);

        if (CurrentScreen is CalibrationViewModel calibration)
        {
            calibration.Tick();
        }

        UpdateDeviceSummary();
        UpdateSimulatorSummary();
    }

    /// <summary>Atualiza o texto da barra superior com o estado da telemetria do simulador.</summary>
    private void UpdateSimulatorSummary()
    {
        string status = Engine.CarSource.StatusDescription;

        if (status != SimulatorSummary)
        {
            SimulatorSummary = status;
        }
    }

    private void UpdateDeviceSummary()
    {
        IPedalDevice? device = Engine.Device;

        if (device is null)
        {
            return;
        }

        string status = device.IsConnected
            ? $"{device.Descriptor.DisplayName} · {device.SampleRateHz:0} Hz"
            : device.LastError ?? $"{device.Descriptor.DisplayName} · aguardando";

        if (status != DeviceSummary)
        {
            DeviceSummary = status;
        }
    }

    private static string ReadAppVersion()
    {
        Assembly assembly = typeof(MainViewModel).Assembly;

        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // O SDK anexa "+<commit>" quando há metadados de fonte; o rodapé só quer o número.
            int separator = informational.IndexOf('+', StringComparison.Ordinal);
            return separator > 0 ? informational[..separator] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>Arma ou cancela a gravação de uma volta do iRacing.</summary>
    private void ToggleRecording()
    {
        LapRecorder recorder = Engine.Recorder;

        if (recorder.IsActive)
        {
            recorder.Cancel();
        }
        else
        {
            recorder.Start();
        }

        UpdateRecordingStatus(recorder);
        _lastRecorderStatus = recorder.StatusMessage;
        ShellMessage = recorder.StatusMessage;
    }

    /// <summary>
    /// Atualiza o indicador de gravação e, quando a volta fica pronta,
    /// grava o CSV e a carrega como referência.
    /// </summary>
    private void PumpRecording()
    {
        LapRecorder recorder = Engine.Recorder;

        UpdateRecordingStatus(recorder);

        if (recorder.StatusMessage != _lastRecorderStatus)
        {
            _lastRecorderStatus = recorder.StatusMessage;

            if (!recorder.IsActive)
            {
                ShellMessage = _lastRecorderStatus;
            }
        }

        if (!recorder.TryTakeCompletedLap(out ReferenceLap lap))
        {
            return;
        }

        SaveAndLoadRecordedLap(lap);
        _lastRecorderStatus = recorder.StatusMessage;
    }

    private void UpdateRecordingStatus(LapRecorder recorder)
    {
        RecordButtonText = recorder.IsActive ? "Cancelar gravação" : "Gravar volta";

        if (!recorder.IsActive)
        {
            RecordingStatusText = string.Empty;
            return;
        }

        RecordingStatusText = recorder.IsRecording
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"GRAVANDO · {recorder.Progress * 100:0}% · {recorder.SampleCount} am.")
            : "AGUARDANDO VOLTA";
    }

    private void SaveAndLoadRecordedLap(ReferenceLap lap)
    {
        string path;

        try
        {
            path = _referenceLibrary.SaveLap(_selectedTrack, lap);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ApplyReference(lap, null);
            ShellMessage = $"Volta gravada, mas o arquivo não pôde ser salvo: {exception.Message}";
            return;
        }

        ApplyReference(lap, path);
        TrackPanel.ReloadReferences();
        TrackPanel.SelectReferenceFromPath(path);
        ShellMessage = string.Create(
            CultureInfo.InvariantCulture,
            $"Volta gravada em {_selectedTrack.ShortName} — {Engine.Session.Zones.Count} zonas de frenagem.");
    }
}
