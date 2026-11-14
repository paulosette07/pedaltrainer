using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PedalTrainer.App.Services;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry;
using PedalTrainer.Core.Telemetry.Comparison;
using PedalTrainer.Core.Telemetry.Recording;
using PedalTrainer.Core.Telemetry.Trace;
using PedalTrainer.Core.Tracks;
using PedalTrainer.Core.Training;

namespace PedalTrainer.App.ViewModels;

/// <summary>
/// Tela de telemetria: mostra o que o jogo escolhido publica, grava as amostras num arquivo
/// dentro da pasta <c>telemetria</c> e destaca a janela para usar durante a pilotagem.
/// A configuração exibida vem do perfil do simulador, não de valores fixos na interface.
/// </summary>
public sealed class TelemetryViewModel : ObservableObject
{
    private const double CounterRefreshSeconds = 0.2;
    private const string IdleStatus = "Sem telemetria";

    /// <summary>Tempo de volta ainda desconhecido, no mesmo formato do cronômetro.</summary>
    private const string NoLapTime = "--:--.---";

    private readonly TrainingEngine _engine;
    private readonly TelemetryOverlayController _overlay;
    private readonly TelemetryRecorder _recorder = new();
    private readonly BestLapComparer _bestLapComparer = new();
    private readonly LapTraceRecorder _trace = new();
    private readonly Stopwatch _counterClock = Stopwatch.StartNew();

    private LapSectorBook _sectorBook = new();
    private string _trackId = string.Empty;
    private double _trackLengthMeters;
    private IReadOnlyList<bool> _beatenSectors = Array.Empty<bool>();
    private double _lapPositionPercent;
    private string _lapTimeText = NoLapTime;
    private string _bestSectorLapText = "--";
    private string _compositeText = "--";
    private string _sectorStatusText = "Nenhuma volta completa ainda.";
    private string _sectorCountText = "0/20 trechos";
    private string _beatenText = string.Empty;
    private string _traceCaption = "Sem traço para desenhar ainda.";
    private IReadOnlyList<TelemetryTraceSample>? _previewTrace;
    private ReferenceLap? _previewSource;

    private SimulatorInfo _requested = SimulatorCatalog.Auto;
    private TelemetryProfile _profile = TelemetryProfileCatalog.Auto;
    private double _lastCounterRefreshSeconds;

    private string _profileName = TelemetryProfileCatalog.Auto.DisplayName;
    private string _memoryMapText = TelemetryProfileCatalog.Auto.MemoryMapSummary;
    private string _channelsText = TelemetryProfileCatalog.Auto.ChannelSummary;
    private string _rateText = FormatRate(TelemetryProfileCatalog.Auto.RecordingRateHz);
    private string _dataSourceText = TelemetryProfileCatalog.Auto.DataSourceNote;
    private string _setupHintText = TelemetryProfileCatalog.Auto.SetupHint;
    private string _pluginText = string.Empty;
    private string _missingChannelsText = string.Empty;

    private string _connectionText = "Simulador não conectado";
    private string _statusText = IdleStatus;
    private bool _isConnected;
    private double _speedKmh;
    private string _speedText = "--";
    private double _brakePercent;
    private double _throttlePercent;
    private string _brakeText = "0%";
    private string _throttleText = "0%";
    private string _lapText = "-";
    private string _lapPositionText = "-";
    private string _sessionTimeText = "-";
    private bool _hasLapPosition;

    private bool _hasBestLap;
    private bool _canCompareBestLap;
    private string _bestLapTitle = "Nenhuma volta carregada";
    private string _bestLapHint = BestLapComparison.None.Hint;
    private string _deltaText = "--";
    private bool _isDeltaAhead;
    private bool _isDeltaBehind;
    private string _bestDeltaText = "--";
    private string _lastLapDeltaText = "--";
    private string _speedCompareText = "--";
    private string _brakeCompareText = "--";
    private string _throttleCompareText = "--";
    private string _lapProgressText = "--";

    private bool _isRecording;
    private string _recordButtonText = "Gravar telemetria";
    private string _recorderStatus = "Pronto para gravar.";
    private string _fileText = "Nenhum arquivo gravado ainda";
    private string _samplesText = "0";
    private string _durationText = "0,0 s";
    private string _folderText = TelemetryFolder.NextToProgram;

    private bool _overlayOpen;
    private bool _overlayPinned = true;
    private double _overlayOpacity = 0.92;
    private double _overlayWidth = 380;
    private double _overlayHeight = 420;

    public TelemetryViewModel(TrainingEngine engine, TelemetryOverlayController overlay, AppSettings settings)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));

        ArgumentNullException.ThrowIfNull(settings);

        _overlayOpacity = Math.Clamp(settings.TelemetryOverlayOpacity, 0.0, 1.0);
        _overlayPinned = settings.TelemetryOverlayTopmost;
        _overlay.ConfigureSize(settings.TelemetryOverlayWidth, settings.TelemetryOverlayHeight);
        _overlayWidth = _overlay.Width;
        _overlayHeight = _overlay.Height;

        ToggleRecordingCommand = new RelayCommand(ToggleRecording);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        OpenFileCommand = new RelayCommand(OpenLastFile);
        ToggleOverlayCommand = new RelayCommand(ToggleOverlay);
        ShowSimulatorSettingsCommand = new RelayCommand(RequestSimulatorSettings);

        _overlay.StateChanged += OnOverlayStateChanged;
        _overlay.SizeChanged += OnOverlaySizeChanged;
        _overlay.Configure(_overlayPinned, _overlayOpacity);
    }
/// <summary>Pedido para abrir a tela de configurações (o shell decide a navegação).</summary>
    public event EventHandler? SimulatorSettingsRequested;

    public IRelayCommand ToggleRecordingCommand { get; }

    public IRelayCommand OpenFolderCommand { get; }

    public IRelayCommand OpenFileCommand { get; }

    public IRelayCommand ToggleOverlayCommand { get; }

    public IRelayCommand ShowSimulatorSettingsCommand { get; }

    /// <summary>Nome do jogo cuja configuração está sendo mostrada.</summary>
    public string ProfileName
    {
        get => _profileName;
        private set => SetProperty(ref _profileName, value);
    }

    /// <summary>Blocos de memória compartilhada lidos do jogo.</summary>
    public string MemoryMapText
    {
        get => _memoryMapText;
        private set => SetProperty(ref _memoryMapText, value);
    }

    /// <summary>Canais que o jogo publica.</summary>
    public string ChannelsText
    {
        get => _channelsText;
        private set => SetProperty(ref _channelsText, value);
    }

    /// <summary>Taxa de gravação usada para o jogo.</summary>
    public string RateText
    {
        get => _rateText;
        private set => SetProperty(ref _rateText, value);
    }

    /// <summary>De onde vêm os dados, com a cadência de publicação do jogo.</summary>
    public string DataSourceText
    {
        get => _dataSourceText;
        private set => SetProperty(ref _dataSourceText, value);
    }

    /// <summary>O que ligar dentro do simulador.</summary>
    public string SetupHintText
    {
        get => _setupHintText;
        private set => SetProperty(ref _setupHintText, value);
    }

    /// <summary>Aviso de plugin obrigatório (vazio quando o jogo não exige).</summary>
    public string PluginText
    {
        get => _pluginText;
        private set => SetProperty(ref _pluginText, value);
    }

    /// <summary>Canais que o jogo não publica (vazio quando publica todos).</summary>
    public string MissingChannelsText
    {
        get => _missingChannelsText;
        private set => SetProperty(ref _missingChannelsText, value);
    }

    /// <summary>Estado do jogo em uso, para o cartão de conexão.</summary>
    public string ConnectionText
    {
        get => _connectionText;
        private set => SetProperty(ref _connectionText, value);
    }

    /// <summary>Status detalhado da fonte de telemetria.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Verdadeiro quando o jogo está publicando telemetria.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    /// <summary>Velocidade em km/h, para o ponteiro grande.</summary>
    public double SpeedKmh
    {
        get => _speedKmh;
        private set => SetProperty(ref _speedKmh, value);
    }

    public string SpeedText
    {
        get => _speedText;
        private set => SetProperty(ref _speedText, value);
    }

    public double BrakePercent
    {
        get => _brakePercent;
        private set => SetProperty(ref _brakePercent, value);
    }

    public double ThrottlePercent
    {
        get => _throttlePercent;
        private set => SetProperty(ref _throttlePercent, value);
    }

    public string BrakeText
    {
        get => _brakeText;
        private set => SetProperty(ref _brakeText, value);
    }

    public string ThrottleText
    {
        get => _throttleText;
        private set => SetProperty(ref _throttleText, value);
    }

    /// <summary>Número da volta publicada pelo jogo.</summary>
    public string LapText
    {
        get => _lapText;
        private set => SetProperty(ref _lapText, value);
    }

    /// <summary>Fração da volta percorrida, em porcentagem.</summary>
    public string LapPositionText
    {
        get => _lapPositionText;
        private set => SetProperty(ref _lapPositionText, value);
    }

    /// <summary>Tempo de sessão publicado pelo jogo (traço quando o jogo não informa).</summary>
    public string SessionTimeText
    {
        get => _sessionTimeText;
        private set => SetProperty(ref _sessionTimeText, value);
    }

    /// <summary>Verdadeiro quando o jogo informa a posição na volta.</summary>
    public bool HasLapPosition
    {
        get => _hasLapPosition;
        private set => SetProperty(ref _hasLapPosition, value);
    }

    /// <summary>Verdadeiro quando existe uma volta de referência (a melhor volta) carregada.</summary>
    public bool HasBestLap
    {
        get => _hasBestLap;
        private set => SetProperty(ref _hasBestLap, value);
    }

    /// <summary>Verdadeiro quando o delta com a melhor volta pode ser calculado agora.</summary>
    public bool CanCompareBestLap
    {
        get => _canCompareBestLap;
        private set => SetProperty(ref _canCompareBestLap, value);
    }

    /// <summary>Nome da melhor volta e a duração dela.</summary>
    public string BestLapTitle
    {
        get => _bestLapTitle;
        private set => SetProperty(ref _bestLapTitle, value);
    }

    /// <summary>Explica por que a comparação está indisponível (vazio quando está).</summary>
    public string BestLapHint
    {
        get => _bestLapHint;
        private set => SetProperty(ref _bestLapHint, value);
    }

    /// <summary>Delta acumulado na volta atual ("-0,42 s" quando à frente).</summary>
    public string DeltaText
    {
        get => _deltaText;
        private set => SetProperty(ref _deltaText, value);
    }

    /// <summary>Verdadeiro quando o piloto está à frente da melhor volta.</summary>
    public bool IsDeltaAhead
    {
        get => _isDeltaAhead;
        private set => SetProperty(ref _isDeltaAhead, value);
    }

    /// <summary>Verdadeiro quando o piloto está atrás da melhor volta.</summary>
    public bool IsDeltaBehind
    {
        get => _isDeltaBehind;
        private set => SetProperty(ref _isDeltaBehind, value);
    }

    /// <summary>Maior ganho já acumulado nesta volta.</summary>
    public string BestDeltaText
    {
        get => _bestDeltaText;
        private set => SetProperty(ref _bestDeltaText, value);
    }

    /// <summary>Delta com que a última volta completa terminou.</summary>
    public string LastLapDeltaText
    {
        get => _lastLapDeltaText;
        private set => SetProperty(ref _lastLapDeltaText, value);
    }

    /// <summary>Velocidade atual contra a da melhor volta no mesmo ponto.</summary>
    public string SpeedCompareText
    {
        get => _speedCompareText;
        private set => SetProperty(ref _speedCompareText, value);
    }

    /// <summary>Freio atual contra o da melhor volta no mesmo ponto.</summary>
    public string BrakeCompareText
    {
        get => _brakeCompareText;
        private set => SetProperty(ref _brakeCompareText, value);
    }

    /// <summary>Acelerador atual contra o da melhor volta no mesmo ponto.</summary>
    public string ThrottleCompareText
    {
        get => _throttleCompareText;
        private set => SetProperty(ref _throttleCompareText, value);
    }

    /// <summary>Quanto da volta já foi percorrido.</summary>
    public string LapProgressText
    {
        get => _lapProgressText;
        private set => SetProperty(ref _lapProgressText, value);
    }
/// <summary>Verdadeiro enquanto o arquivo de telemetria está aberto.</summary>
    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                RecordButtonText = value ? "Parar gravação" : "Gravar telemetria";
            }
        }
    }

    /// <summary>Texto do botão de gravação.</summary>
    public string RecordButtonText
    {
        get => _recordButtonText;
        private set => SetProperty(ref _recordButtonText, value);
    }

    /// <summary>Última mensagem do gravador (criado, gravando, gravado).</summary>
    public string RecorderStatus
    {
        get => _recorderStatus;
        private set => SetProperty(ref _recorderStatus, value);
    }

    /// <summary>Nome do arquivo em gravação (ou do último gravado).</summary>
    public string FileText
    {
        get => _fileText;
        private set => SetProperty(ref _fileText, value);
    }

    /// <summary>Amostras já escritas no disco.</summary>
    public string SamplesText
    {
        get => _samplesText;
        private set => SetProperty(ref _samplesText, value);
    }

    /// <summary>Duração da gravação em andamento.</summary>
    public string DurationText
    {
        get => _durationText;
        private set => SetProperty(ref _durationText, value);
    }

    /// <summary>Pasta onde os arquivos ficam (raiz do programa).</summary>
    public string FolderText
    {
        get => _folderText;
        private set => SetProperty(ref _folderText, value);
    }

    /// <summary>Verdadeiro quando a janela destacada está aberta.</summary>
    public bool IsOverlayOpen
    {
        get => _overlayOpen;
        private set
        {
            if (SetProperty(ref _overlayOpen, value))
            {
                OnPropertyChanged(nameof(OverlayButtonText));
            }
        }
    }

    /// <summary>Texto do botão que destaca a janela.</summary>
    public string OverlayButtonText => _overlayOpen ? "Fechar janela destacada" : "Destacar janela";

    /// <summary>Mantém a janela destacada acima do simulador.</summary>
    public bool OverlayPinned
    {
        get => _overlayPinned;
        set
        {
            if (SetProperty(ref _overlayPinned, value))
            {
                _overlay.Configure(_overlayPinned, _overlayOpacity);
            }
        }
    }

    /// <summary>Transparência do fundo da janela destacada (0 = só os números sobre o jogo).</summary>
    public double OverlayOpacity
    {
        get => _overlayOpacity;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 1.0);

            if (SetProperty(ref _overlayOpacity, clamped))
            {
                _overlay.Configure(_overlayPinned, clamped);
            }
        }
    }

    /// <summary>Largura da janela destacada; o piloto pode arrastar a borda para mudar.</summary>
    public double OverlayWidth
    {
        get => _overlayWidth;
        private set => SetProperty(ref _overlayWidth, value);
    }

    /// <summary>Altura da janela destacada; o piloto pode arrastar a borda para mudar.</summary>
    public double OverlayHeight
    {
        get => _overlayHeight;
        private set => SetProperty(ref _overlayHeight, value);
    }
/// <summary>
    /// Informa qual simulador foi escolhido nas preferências. Na detecção automática o perfil
    /// mostrado passa a ser o do jogo que estiver publicando.
    /// </summary>
    public void SyncSimulator(SimulatorInfo simulator)
    {
        _requested = simulator ?? SimulatorCatalog.Auto;
        _profile = TelemetryProfileCatalog.Resolve(_requested, null);
        ApplyProfile(_profile);
    }

    /// <summary>Aplica o último instantâneo do carro e alimenta a gravação.</summary>
    public void Apply(CarTelemetry car)
    {
        RefreshProfile();

        IsConnected = car.IsConnected;
        StatusText = _engine.CarSource.StatusDescription;
        ConnectionText = _engine.CarSource.IsConnected
            ? $"{_engine.CarSource.ConnectedSimulatorName} publicando telemetria"
            : "Simulador não conectado";

        if (car.IsConnected)
        {
            SpeedKmh = car.SpeedKmh;
            SpeedText = car.SpeedKmh.ToString("0", CultureInfo.CurrentCulture);
            BrakePercent = car.BrakePercent;
            ThrottlePercent = car.ThrottlePercent;
            BrakeText = FormatPercent(car.BrakePercent);
            ThrottleText = FormatPercent(car.ThrottlePercent);
            LapText = car.LapNumber.ToString(CultureInfo.CurrentCulture);
            LapPositionText = (car.LapDistancePercent * 100).ToString("0.0", CultureInfo.CurrentCulture) + "%";
            HasLapPosition = _profile.Provides(TelemetryChannel.LapDistance);
            SessionTimeText = _profile.Provides(TelemetryChannel.SessionTime)
                ? FormatClock(car.SessionTimeSeconds)
                : "-";
        }
        else
        {
            ResetLiveValues();
        }

        ApplyBestLap(car);
        ApplyTrace(car);

        _recorder.Observe(car);
        PumpRecorder();
    }

    /// <summary>Fecha a gravação e a janela destacada (ao sair do aplicativo).</summary>
    public void Shutdown()
    {
        _overlay.StateChanged -= OnOverlayStateChanged;
        _overlay.SizeChanged -= OnOverlaySizeChanged;
        _overlay.Close();
        _recorder.Dispose();
    }
    /// <summary>
    /// Compara o carro com a melhor volta carregada: delta de tempo acumulado desde a linha
    /// mais velocidade, freio e acelerador da referência no mesmo ponto da pista.
    /// </summary>
    private void ApplyBestLap(in CarTelemetry car)
    {
        ReferenceLap? reference = _engine.Session.ReferenceLap;
        double nowSeconds = car.TimestampSeconds > 0
            ? car.TimestampSeconds
            : _counterClock.Elapsed.TotalSeconds;

        BestLapComparison comparison = _bestLapComparer.Update(reference, car, nowSeconds);

        HasBestLap = reference is not null;
        CanCompareBestLap = comparison.CanCompare;
        BestLapTitle = BestLapComparisonText.Title(comparison);
        BestLapHint = comparison.Hint;
        DeltaText = BestLapComparisonText.DeltaOrPlaceholder(comparison);
        IsDeltaAhead = comparison.IsAhead;
        IsDeltaBehind = comparison.HasDelta &&
                        comparison.DeltaSeconds > BestLapComparisonText.ToleranceSeconds;
        BestDeltaText = BestLapComparisonText.BestDeltaOrPlaceholder(comparison);
        LastLapDeltaText = BestLapComparisonText.LastLapDeltaOrPlaceholder(comparison);
        LapProgressText = comparison.CanCompare
            ? BestLapComparisonText.Progress(comparison.LapProgress)
            : BestLapComparisonText.Placeholder;

        if (!comparison.CanCompare)
        {
            SpeedCompareText = BestLapComparisonText.Placeholder;
            BrakeCompareText = BestLapComparisonText.Placeholder;
            ThrottleCompareText = BestLapComparisonText.Placeholder;
            return;
        }

        SpeedCompareText = BestLapComparisonText.Compare(
            car.SpeedKmh,
            comparison.ReferenceSpeedKmh,
            "km/h");
        BrakeCompareText = BestLapComparisonText.Compare(
            car.BrakePercent,
            comparison.ReferenceBrakePercent,
            "%");
        ThrottleCompareText = BestLapComparisonText.Compare(
            car.ThrottlePercent,
            comparison.ReferenceThrottlePercent,
            "%");
    }

    /// <summary>
    /// Traço desenhado: a volta do piloto quando o jogo publica a posição na pista; sem jogo,
    /// a volta de referência carregada (a tela avisa qual das duas está no gráfico).
    /// </summary>
    public IReadOnlyList<TelemetryTraceSample>? TraceSamples =>
        _trace.CurrentSamples.Count > 1 ? _trace.CurrentSamples : _previewTrace;

    /// <summary>Legenda do gráfico: diz se o desenho é a sua volta ou a de referência.</summary>
    public string TraceCaption
    {
        get => _traceCaption;
        private set => SetProperty(ref _traceCaption, value);
    }

    /// <summary>Comprimento da pista em uso, em metros (0 quando desconhecido).</summary>
    public double TrackLengthMeters => _trackLengthMeters;

    /// <summary>Posição do carro na volta, de 0 a 1.</summary>
    public double LapPositionPercent => _lapPositionPercent;

    /// <summary>Melhor tempo de cada trecho da pista (0 = trecho ainda sem marca).</summary>
    public IReadOnlyList<double> BestSectors => _sectorBook.BestSectorSeconds;

    /// <summary>Trechos que a última volta melhorou.</summary>
    public IReadOnlyList<bool> BeatenSectors => _beatenSectors;

    /// <summary>Tempo da volta em andamento.</summary>
    public string LapTimeText
    {
        get => _lapTimeText;
        private set => SetProperty(ref _lapTimeText, value);
    }

    /// <summary>Melhor volta completa do piloto nesta pista.</summary>
    public string BestSectorLapText
    {
        get => _bestSectorLapText;
        private set => SetProperty(ref _bestSectorLapText, value);
    }

    /// <summary>Volta composta pelos melhores trechos.</summary>
    public string CompositeText
    {
        get => _compositeText;
        private set => SetProperty(ref _compositeText, value);
    }

    /// <summary>Quantos trechos da pista já têm marca.</summary>
    public string SectorStatusText
    {
        get => _sectorStatusText;
        private set => SetProperty(ref _sectorStatusText, value);
    }

    /// <summary>Contador compacto de trechos com marca, para a janela destacada.</summary>
    public string SectorCountText
    {
        get => _sectorCountText;
        private set => SetProperty(ref _sectorCountText, value);
    }

    /// <summary>Quantos trechos a última volta melhorou (vazio quando nenhum).</summary>
    public string BeatenText
    {
        get => _beatenText;
        private set => SetProperty(ref _beatenText, value);
    }

    /// <summary>Avisa a view para redesenhar o traço (roda a cada quadro da telemetria).</summary>
    public event EventHandler? TraceChanged;

    /// <summary>
    /// Troca a pista em uso: carrega os melhores trechos guardados dela e recomeça o traço.
    /// Os melhores trechos são por pista — Interlagos não se mistura com Monza.
    /// </summary>
    public void SyncTrack(TrackDefinition? track)
    {
        if (track is null)
        {
            return;
        }

        _trackLengthMeters = track.LengthMeters;

        if (string.Equals(track.Id, _trackId, StringComparison.Ordinal))
        {
            TraceChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _trackId = track.Id;
        _sectorBook = LapSectorStore.Load(track.Id);
        _trace.Reset();
        _beatenSectors = Array.Empty<bool>();
        _lapPositionPercent = 0;
        _lapTimeText = NoLapTime;
        _previewTrace = null;
        _previewSource = null;
        RefreshPreview();
        RefreshSectorTexts(null);
        TraceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Alimenta o traço da volta: guarda a posição na pista, fecha a volta quando o carro cruza
    /// a linha e, a cada volta, atualiza os melhores trechos da pista.
    /// </summary>
    private void ApplyTrace(in CarTelemetry car)
    {
        if (!car.IsConnected || !car.HasLapPosition)
        {
            _lapPositionPercent = 0;
            LapTimeText = NoLapTime;
            RefreshPreview();
            return;
        }

        _lapPositionPercent = Math.Clamp(car.LapDistancePercent, 0, 1);
        TraceCaption = _trace.IsRunning
            ? "Sua volta: freio em vermelho, acelerador em verde."
            : "Aguardando a primeira volta do jogo para desenhar o traço.";

        double nowSeconds = car.TimestampSeconds > 0
            ? car.TimestampSeconds
            : _counterClock.Elapsed.TotalSeconds;

        TelemetryLapTrace? lap = _trace.Observe(car, nowSeconds);
        LapTimeText = _trace.IsRunning ? FormatLapTime(_trace.CurrentLapSeconds) : NoLapTime;

        if (lap is not null)
        {
            LapSectorOutcome outcome = _sectorBook.Add(lap);
            LapSectorStore.Save(_trackId, _sectorBook);
            _beatenSectors = outcome.BeatenMask;
            RefreshSectorTexts(outcome);
        }

        TraceChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sem posição na pista vinda do jogo, o gráfico mostra a volta de referência carregada:
    /// o piloto vê o desenho de freio e acelerador mesmo com o simulador fechado.
    /// </summary>
    private void RefreshPreview()
    {
        ReferenceLap? reference = _engine.Session.ReferenceLap;

        if (ReferenceEquals(reference, _previewSource))
        {
            return;
        }

        _previewSource = reference;
        _previewTrace = ReferenceTraceBuilder.Build(reference);

        TraceCaption = _previewTrace.Count > 1
            ? "Volta de referência: o jogo ainda não publica a sua posição na pista."
            : "Sem traço para desenhar: carregue uma volta de referência ou abra o simulador.";
    }

    private void RefreshSectorTexts(LapSectorOutcome? outcome)
    {
        int known = _sectorBook.KnownSectorCount;
        int total = _sectorBook.SectorCount;

        SectorStatusText = known == 0
            ? "Nenhum trecho marcado ainda — complete uma volta para começar."
            : string.Create(CultureInfo.CurrentCulture, $"{known} de {total} trechos já têm marca.");

        SectorCountText = string.Create(CultureInfo.CurrentCulture, $"{known}/{total} trechos");

        BestSectorLapText = _sectorBook.BestLapSeconds > 0
            ? "Melhor volta: " + FormatLapTime(_sectorBook.BestLapSeconds)
            : "Melhor volta: --";

        CompositeText = _sectorBook.CompositeSeconds is { } composite
            ? "Melhores trechos: " + FormatLapTime(composite)
            : "Melhores trechos: --";

        BeatenText = outcome is { BeatenSectors: > 0 } covered
            ? string.Create(CultureInfo.CurrentCulture, $"Última volta marcou {covered.BeatenSectors} trecho(s).")
            : string.Empty;
    }

    /// <summary>Tempo de volta no formato 1:24.633, com ponto nos milésimos (padrão de cronômetro).</summary>
    private static string FormatLapTime(double seconds)
    {
        double clamped = Math.Max(0, seconds);
        var span = TimeSpan.FromSeconds(clamped);

        string fraction = ((int)(span.Milliseconds)).ToString("000", CultureInfo.InvariantCulture);

        return span.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}.{fraction}")
            : string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}:{span.Seconds:00}.{fraction}");
    }

    private void ToggleRecording()
    {
        if (_recorder.IsRecording)
        {
            _recorder.Stop();
            PumpRecorder();
            return;
        }

        TelemetryProfile profile = TelemetryProfileCatalog.Resolve(
            _requested,
            _engine.CarSource.ActiveSimulator);

        string simulatorName = _engine.CarSource.ConnectedSimulatorName;

        if (_recorder.Start(profile, simulatorName))
        {
            FolderText = _recorder.FolderPath;
        }

        PumpRecorder();
    }

    private void PumpRecorder()
    {
        IsRecording = _recorder.IsRecording;

        string status = _recorder.StatusMessage;

        if (status != RecorderStatus)
        {
            RecorderStatus = status;
        }

        if (_recorder.IsRecording)
        {
            FileText = Path.GetFileName(_recorder.FilePath);
            SamplesText = _recorder.SampleCount.ToString(CultureInfo.CurrentCulture);
            DurationText = FormatClock(_recorder.ElapsedSeconds);
            return;
        }

        if (_counterClock.Elapsed.TotalSeconds - _lastCounterRefreshSeconds < CounterRefreshSeconds)
        {
            return;
        }

        _lastCounterRefreshSeconds = _counterClock.Elapsed.TotalSeconds;

        string lastFile = _recorder.LastFilePath;

        if (lastFile.Length > 0)
        {
            FileText = Path.GetFileName(lastFile);
        }
    }

    /// <summary>Recalcula o perfil quando o jogo conectado muda (detecção automática).</summary>
    private void RefreshProfile()
    {
        TelemetryProfile resolved = TelemetryProfileCatalog.Resolve(
            _requested,
            _engine.CarSource.ActiveSimulator);

        if (ReferenceEquals(resolved, _profile) || resolved == _profile)
        {
            return;
        }

        ApplyProfile(resolved);
    }

    private void ApplyProfile(TelemetryProfile profile)
    {
        _profile = profile;
        ProfileName = profile.DisplayName;
        MemoryMapText = profile.MemoryMapSummary;
        ChannelsText = profile.ChannelSummary;
        RateText = FormatRate(profile.RecordingRateHz);
        DataSourceText = profile.DataSourceNote;
        SetupHintText = profile.SetupHint;
        PluginText = profile.RequiresPlugin
            ? "Este jogo exige plugin de memória compartilhada instalado."
            : string.Empty;
        MissingChannelsText = TelemetryChannelNames.Missing(profile.Channels) is { Length: > 0 } missing
            ? "O jogo não publica: " + missing + "."
            : string.Empty;
    }

    private void ResetLiveValues()
    {
        SpeedKmh = 0;
        SpeedText = "--";
        BrakePercent = 0;
        ThrottlePercent = 0;
        BrakeText = "0%";
        ThrottleText = "0%";
        LapText = "-";
        LapPositionText = "-";
        SessionTimeText = "-";
        HasLapPosition = false;
    }

    private void ToggleOverlay()
    {
        _overlay.Toggle(this);
        IsOverlayOpen = _overlay.IsOpen;
    }

    private void OnOverlayStateChanged(object? sender, EventArgs e) => IsOverlayOpen = _overlay.IsOpen;

    /// <summary>Guarda o tamanho escolhido pelo piloto para a próxima sessão.</summary>
    private void OnOverlaySizeChanged(object? sender, EventArgs e)
    {
        OverlayWidth = _overlay.Width;
        OverlayHeight = _overlay.Height;
    }

    private void RequestSimulatorSettings() => SimulatorSettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OpenFolder()
    {
        string folder = _recorder.FolderPath;

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            RecorderStatus = "Não foi possível abrir a pasta: " + exception.Message;
        }
    }

    private void OpenLastFile()
    {
        string path = _recorder.LastFilePath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            RecorderStatus = "Nenhum arquivo de telemetria para abrir ainda.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            RecorderStatus = "Não foi possível abrir o arquivo: " + exception.Message;
        }
    }

    private static string FormatRate(double rateHz) =>
        rateHz.ToString("0.#", CultureInfo.CurrentCulture) + " Hz";

    private static string FormatPercent(double value) =>
        value.ToString("0", CultureInfo.CurrentCulture) + "%";

    private static string FormatClock(double seconds)
    {
        double clamped = Math.Max(0, seconds);
        var span = TimeSpan.FromSeconds(clamped);

        return span.TotalHours >= 1
            ? string.Create(CultureInfo.CurrentCulture, $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}")
            : string.Create(CultureInfo.CurrentCulture, $"{span.Minutes:00}:{span.Seconds:00}");
    }
}
