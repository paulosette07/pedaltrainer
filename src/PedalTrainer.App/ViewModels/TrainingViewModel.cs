using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PedalTrainer.Core.Models;
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

    private readonly TrainingEngine _engine;
    private readonly TraceSample[] _rawBuffer = new TraceSample[MaxPlotSamples];
    private readonly TraceSample[] _plotBuffer = new TraceSample[MaxPlotSamples];

    private IReadOnlyList<TraceSample>? _ghost;
    private int _ghostZoneIndex = -1;
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
    private string _iRacingText = "iRacing: não conectado";
    private bool _iRacingConnected;
    private string _rateText = "-- Hz";

    public TrainingViewModel(TrainingEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

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

    public string IRacingText { get => _iRacingText; private set => SetProperty(ref _iRacingText, value); }

    public bool IRacingConnected { get => _iRacingConnected; private set => SetProperty(ref _iRacingConnected, value); }

    public string RateText { get => _rateText; private set => SetProperty(ref _rateText, value); }

    /// <summary>Curva fantasma da zona atual (nula fora de um desafio).</summary>
    public IReadOnlyList<TraceSample>? Ghost => _ghost;

    /// <summary>Amostras do piloto em tempo relativo ao início do desafio.</summary>
    public ReadOnlyMemory<TraceSample> PlotTrace => _plotTrace;

    public double CursorSeconds => _cursorSeconds;

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

        ZoneName = frame.ActiveZone?.Name ?? "Nenhuma zona ativa";
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
        IRacingText = frame.Car.IsConnected ? "iRacing: conectado" : "iRacing: não conectado";
        IRacingConnected = frame.Car.IsConnected;
        RateText = $"{frame.InputRateHz:0} Hz";

        UpdatePlot(frame, hasChallenge);
    }

    /// <summary>Limpa o gráfico e os números (ao trocar de referência).</summary>
    public void Reset()
    {
        _ghost = null;
        _ghostZoneIndex = -1;
        _plotTrace = ReadOnlyMemory<TraceSample>.Empty;
        _cursorSeconds = 0;
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
