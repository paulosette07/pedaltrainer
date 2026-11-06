using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Tracks;
using PedalTrainer.Core.Training;

namespace PedalTrainer.App.ViewModels;

/// <summary>
/// Expõe o quadro do motor de treino para a tela principal: números do painel,
/// estado do desafio e os dados do gráfico em tempo real.
/// </summary>
public sealed class TrainingViewModel : ObservableObject
{
    private const int MaxPlotSamples = 8192;
    private const double ArmedWindowSeconds = 4.0;
    private const string ZoneIdleLabel = "Nenhuma zona ativa";

    private readonly TrainingEngine _engine;
    private readonly TraceSample[] _rawBuffer = new TraceSample[MaxPlotSamples];
    private readonly TraceSample[] _plotBuffer = new TraceSample[MaxPlotSamples];

    private IReadOnlyList<TraceSample>? _ghost;
    private int _ghostZoneIndex = -1;
    private IReadOnlyList<DrillCurvePoint>? _nextCurve;
    private IReadOnlyList<TraceSample>? _nextGhost;
    private BrakingZone? _previewZone;
    private string _nextZoneName = "Nenhuma curva à frente";
    private string _nextZoneDetail = string.Empty;
    private string _nextZoneBadge = string.Empty;
    private ReadOnlyMemory<TraceSample> _plotTrace = ReadOnlyMemory<TraceSample>.Empty;
    private double _cursorSeconds;

    private string _scoreText = "--";
    private string _gradeText = string.Empty;
    private bool _isChallengeActive;
    private string _phaseText = "SEM REFERÊNCIA";
    private string _statusMessage = "Carregue uma volta de referência para começar.";
    private string _zoneName = "Nenhuma zona ativa";
    private double _brakePercent;
    private double _throttlePercent;
    private double _referenceBrakePercent;
    private double _referenceThrottlePercent;
    private string _brakeText = "0%";
    private string _throttleText = "0%";
    private string _referenceBrakeText = "-";
    private string _referenceThrottleText = "-";
    private string _speedText = "0";
    private string _errorText = "-";
    private string _rmseText = "-";
    private string _throttleAccuracyText = "-";
    private string _lastResultText = "Nenhuma curva avaliada ainda.";
    private string _streakText = "0";
    private string _bestText = "-";
    private string _averageText = "-";
    private string _zoneCountText = "0";
    private string _bestForZoneText = "-";
    private string _referenceName = "Nenhuma";
    private string _deviceText = "Sem controlador";
    private bool _deviceConnected;
    private string _simulatorText = "Simulador: não conectado";
    private bool _simulatorConnected;
    private string _rateText = "-- Hz";

    private double? _mapCarFraction;
    private double? _mapReferenceFraction;
    private int _mapActiveZoneIndex = -1;
    private int _mapNextZoneIndex = -1;

    public TrainingViewModel(TrainingEngine engine, TrackPanelViewModel trackPanel)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        TrackPanel = trackPanel ?? throw new ArgumentNullException(nameof(trackPanel));
    }

    /// <summary>Cartão da pista (seleção, referência e mapa) ligado a esta tela.</summary>
    public TrackPanelViewModel TrackPanel { get; }

    /// <summary>Pista desenhada no mapa.</summary>
    public TrackDefinition MapTrack => TrackPanel.SelectedTrack;

    /// <summary>Zonas de frenagem da referência atual, para marcar no mapa.</summary>
    public IReadOnlyList<BrakingZone> MapZones => _engine.Session.Zones;

    /// <summary>Comprimento da referência em metros (base das frações do mapa).</summary>
    public double MapTrackLengthMeters => _engine.Session.ReferenceLap?.TrackLengthMeters ?? 0;

    /// <summary>Fração de volta onde o carro está (nulo sem telemetria de posição).</summary>
    public double? MapCarFraction => _mapCarFraction;

    /// <summary>Fração de volta onde a referência está freando (nulo sem referência).</summary>
    public double? MapReferenceFraction => _mapReferenceFraction;

    /// <summary>Zona do desafio atual (-1 quando nenhuma).</summary>
    public int MapActiveZoneIndex => _mapActiveZoneIndex;

    /// <summary>Próxima zona da fila (-1 quando nenhuma).</summary>
    public int MapNextZoneIndex => _mapNextZoneIndex;

    /// <summary>Disparado quando os dados do gráfico mudam; a view redesenha.</summary>
    public event EventHandler? PlotDataChanged;

    public string ScoreText { get => _scoreText; private set => SetProperty(ref _scoreText, value); }

    public string GradeText { get => _gradeText; private set => SetProperty(ref _gradeText, value); }

    public bool IsChallengeActive { get => _isChallengeActive; private set => SetProperty(ref _isChallengeActive, value); }

    public string PhaseText { get => _phaseText; private set => SetProperty(ref _phaseText, value); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public string ZoneName { get => _zoneName; private set => SetProperty(ref _zoneName, value); }

    public double BrakePercent { get => _brakePercent; private set => SetProperty(ref _brakePercent, value); }

    public double ThrottlePercent { get => _throttlePercent; private set => SetProperty(ref _throttlePercent, value); }

    public double ReferenceBrakePercent { get => _referenceBrakePercent; private set => SetProperty(ref _referenceBrakePercent, value); }

    public double ReferenceThrottlePercent { get => _referenceThrottlePercent; private set => SetProperty(ref _referenceThrottlePercent, value); }

    public string BrakeText { get => _brakeText; private set => SetProperty(ref _brakeText, value); }

    public string ThrottleText { get => _throttleText; private set => SetProperty(ref _throttleText, value); }

    public string ReferenceBrakeText { get => _referenceBrakeText; private set => SetProperty(ref _referenceBrakeText, value); }

    public string ReferenceThrottleText { get => _referenceThrottleText; private set => SetProperty(ref _referenceThrottleText, value); }

    public string SpeedText { get => _speedText; private set => SetProperty(ref _speedText, value); }

    public string ErrorText { get => _errorText; private set => SetProperty(ref _errorText, value); }

    public string RmseText { get => _rmseText; private set => SetProperty(ref _rmseText, value); }

    public string ThrottleAccuracyText { get => _throttleAccuracyText; private set => SetProperty(ref _throttleAccuracyText, value); }

    public string LastResultText { get => _lastResultText; private set => SetProperty(ref _lastResultText, value); }

    public string StreakText { get => _streakText; private set => SetProperty(ref _streakText, value); }

    public string BestText { get => _bestText; private set => SetProperty(ref _bestText, value); }

    public string AverageText { get => _averageText; private set => SetProperty(ref _averageText, value); }

    public string ZoneCountText { get => _zoneCountText; private set => SetProperty(ref _zoneCountText, value); }

    public string BestForZoneText { get => _bestForZoneText; private set => SetProperty(ref _bestForZoneText, value); }

    public string ReferenceName { get => _referenceName; private set => SetProperty(ref _referenceName, value); }

    public string DeviceText { get => _deviceText; private set => SetProperty(ref _deviceText, value); }

    public bool DeviceConnected { get => _deviceConnected; private set => SetProperty(ref _deviceConnected, value); }

    /// <summary>Estado da telemetria do simulador, exibido na barra superior.</summary>
    public string SimulatorText { get => _simulatorText; private set => SetProperty(ref _simulatorText, value); }

    public bool SimulatorConnected { get => _simulatorConnected; private set => SetProperty(ref _simulatorConnected, value); }

    public string RateText { get => _rateText; private set => SetProperty(ref _rateText, value); }

    /// <summary>Curva fantasma da zona atual (nula fora de um desafio).</summary>
    public IReadOnlyList<TraceSample>? Ghost => _ghost;

    /// <summary>Amostras do piloto em tempo relativo ao início do desafio.</summary>
    public ReadOnlyMemory<TraceSample> PlotTrace => _plotTrace;

    public double CursorSeconds => _cursorSeconds;

    /// <summary>
    /// Desenho da próxima curva: a frenagem que o piloto vai encarar depois desta.
    /// Fica sempre preenchido para a tela nunca esconder o gráfico seguinte.
    /// </summary>
    public IReadOnlyList<DrillCurvePoint>? NextCurve => _nextCurve;

    /// <summary>
    /// Curva completa da próxima frenagem (com a entrada antes do ponto de freada), no
    /// mesmo eixo do gráfico principal: é o desenho grande que aparece antes de frear.
    /// </summary>
    public IReadOnlyList<TraceSample>? NextGhost => _nextGhost;

    /// <summary>Nome da próxima curva de referência.</summary>
    public string NextZoneName { get => _nextZoneName; private set => SetProperty(ref _nextZoneName, value); }

    /// <summary>Pico e duração da próxima curva ("pico 82% · 1,4 s").</summary>
    public string NextZoneDetail { get => _nextZoneDetail; private set => SetProperty(ref _nextZoneDetail, value); }

    /// <summary>Etiqueta da próxima curva na referência ("ZONA 3/12").</summary>
    public string NextZoneBadge { get => _nextZoneBadge; private set => SetProperty(ref _nextZoneBadge, value); }

    /// <summary>Banda de tolerância usada no desenho da próxima curva.</summary>
    public double NextCurveTolerance => TolerancePercent;

    /// <summary>Nota numérica usada pelo medidor circular.</summary>
    public double ScoreValue { get; private set; }

    /// <summary>Progresso da janela de desafio (0 a 1), usado pela barra de progresso.</summary>
    public double ChallengeProgress { get; private set; }

    /// <summary>Exibe a curva do acelerador no gráfico.</summary>
    public bool ShowThrottle { get; set; } = true;

    /// <summary>Banda de tolerância (%) desenhada no gráfico.</summary>
    public double TolerancePercent { get; set; } = 8;

    /// <summary>Aplica um quadro publicado pelo motor.</summary>
    public void Apply(TrainingFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        double brake = frame.Pedals.BrakePercent;
        double throttle = frame.Pedals.ThrottlePercent;

        BrakePercent = brake;
        ThrottlePercent = throttle;
        BrakeText = FormatPercent(brake);
        ThrottleText = FormatPercent(throttle);

        bool hasChallenge = frame.Phase is TrainingPhase.Running or TrainingPhase.Finished;
        IsChallengeActive = hasChallenge;
        ScoreText = hasChallenge ? frame.LiveScore.ToString("0", CultureInfo.CurrentCulture) : "--";
        GradeText = hasChallenge ? GradeFor(frame.LiveScore) : string.Empty;
        ScoreValue = hasChallenge ? frame.LiveScore : 0;
        ChallengeProgress = hasChallenge ? Math.Clamp(frame.ChallengeProgress, 0, 1) : 0;

        ReferenceBrakePercent = hasChallenge ? frame.ReferenceBrakePercent : -1;
        ReferenceThrottlePercent = hasChallenge ? frame.ReferenceThrottlePercent : -1;
        ReferenceBrakeText = hasChallenge ? FormatPercent(frame.ReferenceBrakePercent) : "-";
        ReferenceThrottleText = hasChallenge ? FormatPercent(frame.ReferenceThrottlePercent) : "-";

        SpeedText = frame.Car.IsConnected ? frame.Car.SpeedKmh.ToString("0", CultureInfo.CurrentCulture) : "--";
        ErrorText = hasChallenge ? FormatPercent(frame.InstantErrorPercent) : "-";
        RmseText = hasChallenge ? FormatPercent(frame.RmsePercent) : "-";
        ThrottleAccuracyText = hasChallenge ? FormatPercent(frame.ThrottleAccuracy) : "-";

        ZoneName = ZoneHeadline(frame);
        PhaseText = PhaseLabel(frame.Phase);
        StatusMessage = frame.StatusMessage;

        StreakText = frame.Streak.ToString(CultureInfo.CurrentCulture);
        BestText = frame.BestScore > 0 ? frame.BestScore.ToString("0", CultureInfo.CurrentCulture) : "-";
        AverageText = frame.AverageScore > 0 ? frame.AverageScore.ToString("0", CultureInfo.CurrentCulture) : "-";
        ZoneCountText = frame.ZoneCount.ToString(CultureInfo.CurrentCulture);
        BestForZoneText = frame.BestScoreForZone > 0
            ? frame.BestScoreForZone.ToString("0", CultureInfo.CurrentCulture)
            : "-";

        LastResultText = frame.LastResult is { } result
            ? $"{result.ZoneName} · {result.Score:0} pts · nota {result.Grade}"
            : "Nenhuma curva avaliada ainda.";

        ReferenceName = frame.ReferenceName;
        DeviceText = frame.DeviceStatus;
        DeviceConnected = frame.DeviceConnected;
        SimulatorText = frame.SimulatorStatus;
        SimulatorConnected = frame.Car.IsConnected;
        RateText = $"{frame.InputRateHz:0} Hz";

        UpdateNextCurve(frame);
        UpdatePlot(frame, hasChallenge);
        UpdateMap(frame);
    }

    /// <summary>Limpa o gráfico e os números (ao trocar de referência).</summary>
    public void Reset()
    {
        _ghost = null;
        _ghostZoneIndex = -1;
        _nextCurve = null;
        _nextGhost = null;
        _previewZone = null;
        _plotTrace = ReadOnlyMemory<TraceSample>.Empty;
        _cursorSeconds = 0;
        _mapCarFraction = null;
        _mapReferenceFraction = null;
        _mapActiveZoneIndex = -1;
        _mapNextZoneIndex = -1;
        IsChallengeActive = false;
        ScoreText = "--";
        GradeText = string.Empty;
        PlotDataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePlot(TrainingFrame frame, bool hasChallenge)
    {
        if (hasChallenge && frame.ActiveZone is { } zone && _engine.Session.ReferenceLap is { } lap)
        {
            if (_ghostZoneIndex != zone.Index)
            {
                _ghost = GhostCurveBuilder.Build(lap, zone);
                _ghostZoneIndex = zone.Index;
            }

            double start = frame.ChallengeStartTimeSeconds;
            int count = CopyWindow(start - GhostCurveBuilder.LeadInSeconds);

            for (var i = 0; i < count; i++)
            {
                TraceSample sample = _rawBuffer[i];
                _plotBuffer[i] = new TraceSample(
                    sample.TimeSeconds - start,
                    sample.BrakePercent,
                    sample.ThrottlePercent);
            }

            _plotTrace = new ReadOnlyMemory<TraceSample>(_plotBuffer, 0, count);
            _cursorSeconds = frame.ElapsedInZoneSeconds;
        }
        else
        {
            _ghost = null;
            _ghostZoneIndex = -1;

            double now = _engine.NowSeconds;
            int count = CopyWindow(now - ArmedWindowSeconds);

            for (var i = 0; i < count; i++)
            {
                TraceSample sample = _rawBuffer[i];
                _plotBuffer[i] = new TraceSample(
                    sample.TimeSeconds - now,
                    sample.BrakePercent,
                    sample.ThrottlePercent);
            }

            _plotTrace = new ReadOnlyMemory<TraceSample>(_plotBuffer, 0, count);
            _cursorSeconds = 0;
        }

        PlotDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Mantém o desenho da próxima curva pronto o tempo todo: o piloto vê a forma da
    /// frenagem seguinte antes de chegar nela, com o desafio parado ou em andamento.
    /// </summary>
    private void UpdateNextCurve(TrainingFrame frame)
    {
        if (frame.NextZone is not { } zone || _engine.Session.ReferenceLap is not { } lap)
        {
            _previewZone = null;
            _nextCurve = null;
            _nextGhost = null;
            NextZoneName = frame.ReferenceLoaded ? "Fim da volta" : "Sem referência";
            NextZoneDetail = frame.ReferenceLoaded
                ? "Nenhuma frenagem à frente."
                : "Carregue uma volta de referência.";
            NextZoneBadge = string.Empty;
            return;
        }

        if (!ReferenceEquals(zone, _previewZone))
        {
            _previewZone = zone;
            _nextGhost = GhostCurveBuilder.Build(lap, zone);
            _nextCurve = BuildPreviewCurve(_nextGhost);
        }

        NextZoneName = ZoneTitle(zone);
        NextZoneDetail = string.Create(
            CultureInfo.CurrentCulture,
            $"pico {zone.PeakBrakePercent:0}% · {zone.DurationSeconds:0.0} s");
        NextZoneBadge = string.Create(
            CultureInfo.CurrentCulture,
            $"ZONA {zone.Index + 1}/{Math.Max(zone.Index + 1, frame.ZoneCount)}");
    }

    /// <summary>Converte a curva fantasma da zona no desenho do painel, começando em zero.</summary>
    private static IReadOnlyList<DrillCurvePoint> BuildPreviewCurve(IReadOnlyList<TraceSample> ghost)
    {
        var points = new List<DrillCurvePoint>(ghost.Count);
        double origin = ghost.Count > 0 ? ghost[0].TimeSeconds : 0;

        foreach (TraceSample sample in ghost)
        {
            points.Add(new DrillCurvePoint(sample.TimeSeconds - origin, sample.BrakePercent));
        }

        return points;
    }

    /// <summary>
    /// Preenche o mapa: posição do carro pela telemetria e posição da referência no ponto
    /// de frenagem que está sendo treinado (ou no próximo, com o desafio parado).
    /// </summary>
    private void UpdateMap(TrainingFrame frame)
    {
        double length = _engine.Session.ReferenceLap?.TrackLengthMeters ?? 0;

        _mapCarFraction = frame.Car is { IsConnected: true, HasLapPosition: true }
            ? Math.Clamp(frame.Car.LapDistancePercent, 0, 0.9999)
            : null;

        BrakingZone? active = frame.ActiveZone;
        BrakingZone? next = frame.NextZone;

        _mapActiveZoneIndex = active?.Index ?? -1;
        _mapNextZoneIndex = next?.Index ?? -1;
        _mapReferenceFraction = ReferenceFraction(frame, active ?? next, length);
    }

    private static double? ReferenceFraction(TrainingFrame frame, BrakingZone? zone, double trackLengthMeters)
    {
        if (zone is null || trackLengthMeters <= 0)
        {
            return null;
        }

        double progress = 0;

        if (frame.ActiveZone is not null && zone.DurationSeconds > 0)
        {
            progress = Math.Clamp(frame.ElapsedInZoneSeconds / zone.DurationSeconds, 0, 1);
        }

        double distance = zone.StartDistanceMeters + (zone.DistanceLengthMeters * progress);
        return Math.Clamp(distance / trackLengthMeters, 0, 0.9999);
    }

    /// <summary>
    /// Título do cartão do desafio: com a frenagem em andamento mostra a zona atual;
    /// parado, anuncia a próxima — nunca "nenhuma zona" quando há uma curva à frente.
    /// </summary>
    private string ZoneHeadline(TrainingFrame frame)
    {
        if (frame.ActiveZone is { } active)
        {
            return ZoneTitle(active);
        }

        if (frame.NextZone is { } next)
        {
            return "A seguir: " + ZoneTitle(next);
        }

        return ZoneIdleLabel;
    }

    /// <summary>
    /// Nome da zona para a tela: quando o traçado conhece a curva, usa o nome dela
    /// ("Ferradura") em vez do rótulo genérico ("Zona 4").
    /// </summary>
    private string ZoneTitle(BrakingZone? zone)
    {
        if (zone is null)
        {
            return ZoneIdleLabel;
        }

        if (!zone.HasDistance || _engine.Session.ReferenceLap is not { } lap || lap.TrackLengthMeters <= 0)
        {
            return zone.Name;
        }

        double fraction = zone.StartDistanceMeters / lap.TrackLengthMeters;
        return TrackPanel.SelectedTrack.CornerNear(fraction)?.Name ?? zone.Name;
    }

    private int CopyWindow(double fromTimeSeconds) => _engine.CopyTrace(_rawBuffer, fromTimeSeconds);

    private static string FormatPercent(double value) =>
        value.ToString("0", CultureInfo.CurrentCulture) + "%";

    private static string GradeFor(double score) => score switch
    {
        >= 95 => "S",
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 55 => "D",
        _ => "E"
    };

    private static string PhaseLabel(TrainingPhase phase) => phase switch
    {
        TrainingPhase.Armed => "AGUARDANDO FRENAGEM",
        TrainingPhase.Running => "DESAFIO EM ANDAMENTO",
        TrainingPhase.Finished => "CURVA CONCLUÍDA",
        _ => "SEM REFERÊNCIA"
    };
}
