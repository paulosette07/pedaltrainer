using System;
using System.Diagnostics;
using System.Threading;
using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Input;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Recording;
using PedalTrainer.Core.Telemetry;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Motor de tempo real: lê os pedais e o iRacing em thread dedicada, alimenta a sessão
/// de treino e publica um quadro imutável que a interface consome a 60 Hz.
/// </summary>
public sealed class TrainingEngine : IDisposable
{
    /// <summary>Taxa do laço de processamento interno.</summary>
    public const double ProcessingRateHz = 200.0;

    private readonly TrainingSession _session = new();
    private readonly PedalSignalProcessor _processor = new();
    private readonly TraceRingBuffer _trace = new(30, ProcessingRateHz);
    private readonly CancellationTokenSource _cts = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _deviceGate = new();
    private readonly DrillRunner _drill = new();
    private readonly object _drillGate = new();

    private DrillProgram? _pendingDrillProgram;
    private bool _pendingDrillStart;
    private bool _pendingDrillStop;
    private bool _pendingDrillSkip;
    private DrillUpdate _latestDrill = DrillUpdate.Empty;

    private IPedalDevice? _device;
    private ICarTelemetrySource _carSource = new NullCarTelemetrySource();
    private AppSettings _settings = new();
    private TrainingFrame _latest = TrainingFrame.Empty;
    private Thread? _worker;
    private bool _disposed;

    /// <summary>Gravador de voltas: transforma uma volta do iRacing em referência.</summary>
    public LapRecorder Recorder { get; } = new();

    /// <summary>Sessão de treino (estado, estatísticas e zonas).</summary>
    public TrainingSession Session => _session;

    /// <summary>Último quadro publicado. Leitura segura entre threads.</summary>
    public TrainingFrame Latest => Volatile.Read(ref _latest);

    /// <summary>Execução do programa de treino (etapas, alvos e notas).</summary>
    public DrillRunner Drill => _drill;

    /// <summary>Último instantâneo do programa de treino.</summary>
    public DrillUpdate LatestDrill => Volatile.Read(ref _latestDrill);

    /// <summary>Relógio monotônico usado por todo o motor, em segundos.</summary>
    public double NowSeconds => _clock.Elapsed.TotalSeconds;

    /// <summary>Dispositivo de pedais em uso, quando houver.</summary>
    public IPedalDevice? Device
    {
        get
        {
            lock (_deviceGate)
            {
                return _device;
            }
        }
    }

    public void Start()
    {
        if (_worker is not null || _disposed)
        {
            return;
        }

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "PedalTrainer.Engine",
            Priority = ThreadPriority.AboveNormal
        };
        _worker.Start();
    }

    public void Stop()
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        _cts.Cancel();
        _worker?.Join(TimeSpan.FromSeconds(2));
        _worker = null;
    }

    /// <summary>Troca o dispositivo de pedais (hardware ou simulado).</summary>
    public void SetDevice(IPedalDevice? device)
    {
        lock (_deviceGate)
        {
            _device?.Stop();
            _device = device;
            _device?.Start();
        }

        _processor.Reset();
        _trace.Clear();
    }

    /// <summary>Fonte de telemetria do carro em uso (iRacing, ACC, AC EVO, AMS2 ou LMU).</summary>
    public ICarTelemetrySource CarSource => _carSource;

    /// <summary>Troca a fonte de telemetria do carro (simulador escolhido ou nula).</summary>
    public void SetCarTelemetrySource(ICarTelemetrySource source)
    {
        _carSource.Stop();
        _carSource.Dispose();

        _carSource = source ?? new NullCarTelemetrySource();
        Recorder.SimulatorName = _carSource.Simulator.DisplayName;
        _carSource.Start();
    }

    /// <summary>Aplica uma nova calibração de pedais (pode vir da thread de UI).</summary>
    public void SetCalibration(PedalCalibration calibration) => _processor.Calibration = calibration;

    /// <summary>Faixa aprendida do freio, para o assistente de calibração.</summary>
    public AxisRangeTracker BrakeRange => _processor.BrakeRange;

    /// <summary>Faixa aprendida do acelerador, para o assistente de calibração.</summary>
    public AxisRangeTracker ThrottleRange => _processor.ThrottleRange;

    /// <summary>Mapa automático de eixos, ativo enquanto não houver calibração salva.</summary>
    public AutoAxisMapper AutoMapper => _processor.AutoMapper;

    /// <summary>Verdadeiro quando os eixos ainda estão sendo descobertos automaticamente.</summary>
    public bool IsAutoMapping => _processor.IsAutoMapping;

    /// <summary>
    /// Monta uma calibração a partir dos eixos descobertos. Devolve falso enquanto
    /// nenhum pedal tiver sido movido.
    /// </summary>
    public bool TryBuildLearnedCalibration(out PedalCalibration calibration) =>
        _processor.TryBuildLearnedCalibration(
            Device?.Descriptor.InstanceGuid ?? Guid.Empty,
            Device?.Descriptor.DisplayName ?? "Controlador",
            out calibration);

    /// <summary>Última leitura bruta, para diagnóstico.</summary>
    public RawAxesSnapshot LastRawAxes => _processor.LastRaw;

    /// <summary>Aplica as preferências de treino e da interface.</summary>
    public void ApplySettings(AppSettings settings)
    {
        _settings = (settings ?? new AppSettings()).Clone();
        _session.Configure(_settings);
    }

    /// <summary>Carrega a volta de referência usada nos desafios.</summary>
    public void LoadReference(ReferenceLap lap)
    {
        _session.LoadReference(lap);

        if (Device is SimulatedPedalDevice simulated)
        {
            simulated.SetReferenceLap(_session.ReferenceLap);
        }
    }

    public void ClearReference()
    {
        _session.ClearReference();

        if (Device is SimulatedPedalDevice simulated)
        {
            simulated.SetReferenceLap(null);
        }
    }

    /// <summary>Copia o histórico de pedais recente para desenhar o gráfico.</summary>
    public int CopyTrace(TraceSample[] destination, double fromTimeSeconds) =>
        _trace.CopySince(destination, fromTimeSeconds);

    /// <summary>Pede ao motor que comece a executar um programa de treino.</summary>
    public void StartDrill(DrillProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        lock (_drillGate)
        {
            _pendingDrillProgram = program;
            _pendingDrillStart = true;
            _pendingDrillStop = false;
        }
    }

    /// <summary>Pede ao motor que interrompa o programa em execução.</summary>
    public void StopDrill()
    {
        lock (_drillGate)
        {
            _pendingDrillStop = true;
            _pendingDrillStart = false;
        }
    }

    /// <summary>Pede ao motor que pule a repetição atual.</summary>
    public void SkipDrillRepetition()
    {
        lock (_drillGate)
        {
            _pendingDrillSkip = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();

        lock (_deviceGate)
        {
            _device?.Dispose();
            _device = null;
        }

        _carSource.Dispose();
        _cts.Dispose();
    }

    private void WorkerLoop()
    {
        using var timerResolution = TimerResolutionScope.Acquire();

        CancellationToken token = _cts.Token;
        long interval = (long)(Stopwatch.Frequency / ProcessingRateHz);
        long nextTick = Stopwatch.GetTimestamp();

        while (!token.IsCancellationRequested)
        {
            Step();

            nextTick += interval;
            double waitMilliseconds = (nextTick - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;

            if (waitMilliseconds > 1.5)
            {
                token.WaitHandle.WaitOne((int)waitMilliseconds);
            }
            else
            {
                Thread.SpinWait(200);
            }
        }
    }

    private void Step()
    {
        double now = NowSeconds;
        PedalReading pedals = ReadPedals(now);
        CarTelemetry car = _carSource.Latest;

        Recorder.Observe(car);

        _trace.Add(new TraceSample(now, pedals.BrakePercent, pedals.ThrottlePercent));

        PumpDrill(pedals, now);

        SessionUpdate update = _session.Update(pedals, car, now);
        Volatile.Write(ref _latest, BuildFrame(pedals, car, update));
    }

    /// <summary>
    /// Executa os pedidos vindos da interface e publica o estado do programa de treino.
    /// Todo o estado do drill vive nesta thread.
    /// </summary>
    private void PumpDrill(in PedalReading pedals, double nowSeconds)
    {
        DrillProgram? program = null;
        bool start;
        bool stop;
        bool skip;

        lock (_drillGate)
        {
            program = _pendingDrillProgram;
            start = _pendingDrillStart;
            stop = _pendingDrillStop;
            skip = _pendingDrillSkip;

            _pendingDrillStart = false;
            _pendingDrillStop = false;
            _pendingDrillSkip = false;
        }

        DrillUpdate update;

        if (stop)
        {
            update = _drill.Stop(nowSeconds);
        }
        else
        {
            if (start && program is not null)
            {
                _drill.Start(program, nowSeconds);
            }
            else if (skip)
            {
                _drill.Skip(nowSeconds);
            }

            update = _drill.Update(pedals, nowSeconds);
        }

        Volatile.Write(ref _latestDrill, update);
    }

    private PedalReading ReadPedals(double nowSeconds)
    {
        IPedalDevice? device = Device;

        if (device is null || !device.TryReadLatest(out RawAxesSnapshot raw) || raw.Count == 0)
        {
            return PedalReading.Idle(nowSeconds);
        }

        return _processor.Process(raw, nowSeconds);
    }

    private TrainingFrame BuildFrame(in PedalReading pedals, in CarTelemetry car, in SessionUpdate update)
    {
        SessionStats stats = _session.Stats;
        BrakingZone? zone = update.ActiveZone;
        IPedalDevice? device = Device;

        return new TrainingFrame
        {
            Pedals = pedals,
            Car = car,
            Phase = update.Phase,
            ActiveZone = zone,
            ReferenceBrakePercent = update.ReferenceBrakePercent,
            ReferenceThrottlePercent = update.ReferenceThrottlePercent,
            LiveScore = update.LiveScore,
            InstantErrorPercent = update.InstantErrorPercent,
            RmsePercent = update.RmsePercent,
            ChallengeProgress = update.Progress,
            ElapsedInZoneSeconds = update.ElapsedSeconds,
            ChallengeStartTimeSeconds = update.ChallengeStartSeconds,
            ThrottleAccuracy = _session.ActiveThrottleAccuracy,
            AverageScore = stats.AverageScore,
            BestScore = stats.BestScore,
            SessionBestStreak = stats.BestStreak,
            BestScoreForZone = zone is null ? 0 : stats.BestForZone(zone.Index),
            Streak = stats.Streak,
            LastResult = stats.LastResult,
            StatusMessage = BuildStatusMessage(update, device),
            ReferenceLoaded = _session.ReferenceLap is not null,
            ReferenceName = _session.ReferenceLap?.Name ?? "Nenhuma",
            ZoneCount = _session.Zones.Count,
            DeviceConnected = device?.IsConnected ?? false,
            DeviceStatus = DescribeDevice(device),
            InputRateHz = device?.SampleRateHz ?? 0,
            SimulatorName = _carSource.ConnectedSimulatorName,
            SimulatorStatus = _carSource.StatusDescription,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private string BuildStatusMessage(in SessionUpdate update, IPedalDevice? device)
    {
        bool waitingToLearn = device is not null
            && device.IsConnected
            && _processor.IsAutoMapping
            && !_processor.AutoMapper.HasBrake
            && update.Phase is TrainingPhase.Idle or TrainingPhase.Armed;

        return waitingToLearn
            ? "Pise no freio: o app está descobrindo o eixo do pedal."
            : update.StatusMessage;
    }

    private static string DescribeDevice(IPedalDevice? device)
    {
        if (device is null)
        {
            return "Sem controlador";
        }

        if (device.IsConnected)
        {
            return $"{device.Descriptor.DisplayName} · {device.SampleRateHz:0} Hz";
        }

        return device.LastError ?? $"{device.Descriptor.DisplayName} · aguardando";
    }
}
