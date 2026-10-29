using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Máquina de estados do treino: aguarda o gatilho de frenagem, abre a janela de desafio,
/// acumula a pontuação e fecha o resultado. Toda a lógica é determinística e testável.
/// </summary>
public sealed class TrainingSession
{
    private const double ReleaseFactor = 0.6;
    private const double MaxExtraSeconds = 4.0;

    private AppSettings _settings = new();
    private ReferenceLap? _lap;
    private IReadOnlyList<BrakingZone> _zones = Array.Empty<BrakingZone>();
    private ChallengeDetector? _detector;
    private IReferenceAligner? _aligner;
    private BrakingScorer? _scorer;
    private double _challengeStartSeconds;
    private double _challengeLimitSeconds;

    /// <summary>Estatísticas acumuladas da sessão.</summary>
    public SessionStats Stats { get; } = new();

    public TrainingPhase Phase { get; private set; } = TrainingPhase.Idle;

    public string StatusMessage { get; private set; } = "Carregue uma volta de referência para começar.";

    public ReferenceLap? ReferenceLap => _lap;

    public IReadOnlyList<BrakingZone> Zones => _zones;

    public BrakingZone? ActiveZone => _aligner?.Zone;

    /// <summary>Precisão do acelerador na janela atual (0 a 100).</summary>
    public double ActiveThrottleAccuracy => _scorer?.ThrottleAccuracy ?? 0;

    /// <summary>Nome do modo de alinhamento em uso ("Distância" ou "Tempo").</summary>
    public string AlignmentModeName => _aligner?.ModeName ?? "-";

    /// <summary>Disparado ao fechar uma janela de desafio.</summary>
    public event Action<CornerResult>? ChallengeCompleted;

    /// <summary>Aplica as preferências atuais (gatilho e banda de tolerância).</summary>
    public void Configure(AppSettings settings)
    {
        _settings = (settings ?? new AppSettings()).Clone();
    }

    /// <summary>Carrega a volta de referência, reamostra e entra em espera.</summary>
    public void LoadReference(ReferenceLap lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        ReferenceLap resampled = ReferenceLapResampler.Resample(lap);
        var options = new BrakingZoneOptions { EnterThresholdPercent = _settings.BrakeTriggerPercent };
        IReadOnlyList<BrakingZone> zones = BrakingZoneExtractor.Extract(resampled, options);

        _lap = resampled;
        _zones = zones;
        _detector = new ChallengeDetector(resampled, zones);
        _aligner = null;
        _scorer = null;

        if (zones.Count == 0)
        {
            Phase = TrainingPhase.Idle;
            StatusMessage = "Nenhuma frenagem encontrada na referência. Ajuste o gatilho de frenagem nas configurações.";
            return;
        }

        Phase = TrainingPhase.Armed;
        StatusMessage = $"Referência carregada: {zones.Count} zonas de frenagem. Acelere e freie para começar.";
    }

    /// <summary>Descarta a referência e volta ao estado inicial.</summary>
    public void ClearReference()
    {
        _lap = null;
        _zones = Array.Empty<BrakingZone>();
        _detector = null;
        _aligner = null;
        _scorer = null;
        Phase = TrainingPhase.Idle;
        StatusMessage = "Carregue uma volta de referência para começar.";
    }

    /// <summary>Avalia um instante de telemetria e devolve o estado atualizado da sessão.</summary>
    public SessionUpdate Update(in PedalReading pedals, in CarTelemetry car, double nowSeconds)
    {
        double? distance = ResolveDistance(car);

        return Phase switch
        {
            TrainingPhase.Armed => UpdateArmed(pedals, distance, nowSeconds),
            TrainingPhase.Running => UpdateRunning(pedals, distance, nowSeconds),
            TrainingPhase.Finished => UpdateFinished(pedals, distance),
            _ => SessionUpdate.Idle(Phase, StatusMessage)
        };
    }

    private SessionUpdate UpdateArmed(in PedalReading pedals, double? distance, double nowSeconds)
    {
        if (_detector is null || _lap is null)
        {
            return SessionUpdate.Idle(Phase, StatusMessage);
        }

        if (pedals.BrakePercent >= _settings.BrakeTriggerPercent)
        {
            BrakingZone? zone = _detector.SelectNext(distance);
            if (zone is null)
            {
                StatusMessage = "Frenagem fora das zonas da referência — siga para a próxima curva.";
                return SessionUpdate.Idle(TrainingPhase.Armed, StatusMessage);
            }

            BeginChallenge(zone, distance, nowSeconds);
            return BuildRunningUpdate(pedals, distance, nowSeconds, completed: null);
        }

        ReferenceSample preview = PreviewReference(distance);
        StatusMessage = $"Aguardando frenagem ({_detector.ZoneCount} zonas na referência).";

        return new SessionUpdate(
            TrainingPhase.Armed,
            ActiveZone: null,
            LiveScore: 0,
            InstantErrorPercent: 0,
            RmsePercent: 0,
            Progress: 0,
            ElapsedSeconds: 0,
            ReferenceBrakePercent: preview.BrakePercent,
            ReferenceThrottlePercent: preview.ThrottlePercent,
            ChallengeStartSeconds: 0,
            StatusMessage: StatusMessage,
            CompletedResult: null);
    }

    private SessionUpdate UpdateRunning(in PedalReading pedals, double? distance, double nowSeconds)
    {
        if (_scorer is null || _aligner is null)
        {
            return SessionUpdate.Idle(Phase, StatusMessage);
        }

        double elapsed = nowSeconds - _challengeStartSeconds;
        _scorer.AddSample(elapsed, pedals.BrakePercent, pedals.ThrottlePercent, distance);

        bool finished = _aligner.IsComplete(elapsed, distance) || elapsed >= _challengeLimitSeconds;
        if (!finished)
        {
            return BuildRunningUpdate(pedals, distance, nowSeconds, completed: null);
        }

        return CompleteChallenge(elapsed, nowSeconds);
    }

    private SessionUpdate UpdateFinished(in PedalReading pedals, double? distance)
    {
        CornerResult? last = Stats.LastResult;

        if (pedals.BrakePercent <= _settings.BrakeTriggerPercent * ReleaseFactor)
        {
            Phase = TrainingPhase.Armed;
            StatusMessage = "Solte e prepare a próxima frenagem.";
            return new SessionUpdate(
                TrainingPhase.Armed,
                ActiveZone: null,
                LiveScore: 0,
                InstantErrorPercent: 0,
                RmsePercent: 0,
                Progress: 0,
                ElapsedSeconds: 0,
                ReferenceBrakePercent: 0,
                ReferenceThrottlePercent: 0,
                ChallengeStartSeconds: 0,
                StatusMessage: StatusMessage,
                CompletedResult: last);
        }

        return SessionUpdate.Idle(TrainingPhase.Finished, StatusMessage);
    }

    private void BeginChallenge(BrakingZone zone, double? distance, double nowSeconds)
    {
        IReferenceAligner aligner = CreateAligner(zone, distance);
        _aligner = aligner;
        _scorer = new BrakingScorer(aligner, new BrakingScoreOptions
        {
            TolerancePercent = _settings.ToleranceBandPercent
        });
        _challengeStartSeconds = nowSeconds;
        _challengeLimitSeconds = zone.DurationSeconds + MaxExtraSeconds;
        Phase = TrainingPhase.Running;
        StatusMessage = $"Desafio: {zone.Name} ({aligner.ModeName})";
    }

    private SessionUpdate CompleteChallenge(double elapsed, double nowSeconds)
    {
        if (_scorer is null)
        {
            return SessionUpdate.Idle(TrainingPhase.Finished, StatusMessage);
        }

        CornerResult result = _scorer.Complete(DateTime.UtcNow);
        Stats.Register(result);
        Phase = TrainingPhase.Finished;
        StatusMessage = $"Concluído: {result.Score:0} pontos (nota {result.Grade}).";

        ChallengeCompleted?.Invoke(result);

        return new SessionUpdate(
            TrainingPhase.Finished,
            ActiveZone: _aligner?.Zone,
            LiveScore: result.Score,
            InstantErrorPercent: _scorer.InstantErrorPercent,
            RmsePercent: result.RmsePercent,
            Progress: 1,
            ElapsedSeconds: elapsed,
            ReferenceBrakePercent: _scorer.ReferenceBrakePercent,
            ReferenceThrottlePercent: _scorer.ReferenceThrottlePercent,
            ChallengeStartSeconds: _challengeStartSeconds,
            StatusMessage: StatusMessage,
            CompletedResult: result);
    }

    private SessionUpdate BuildRunningUpdate(
        in PedalReading pedals,
        double? distance,
        double nowSeconds,
        CornerResult? completed)
    {
        if (_scorer is null || _aligner is null)
        {
            return SessionUpdate.Idle(Phase, StatusMessage);
        }

        double elapsed = nowSeconds - _challengeStartSeconds;

        return new SessionUpdate(
            Phase,
            ActiveZone: _aligner.Zone,
            LiveScore: _scorer.LiveScore,
            InstantErrorPercent: _scorer.InstantErrorPercent,
            RmsePercent: _scorer.RmsePercent,
            Progress: _scorer.Progress,
            ElapsedSeconds: elapsed,
            ReferenceBrakePercent: _scorer.ReferenceBrakePercent,
            ReferenceThrottlePercent: _scorer.ReferenceThrottlePercent,
            ChallengeStartSeconds: _challengeStartSeconds,
            StatusMessage: StatusMessage,
            CompletedResult: completed);
    }

    private IReferenceAligner CreateAligner(BrakingZone zone, double? distance)
    {
        ReferenceLap lap = _lap!;

        if (_settings.UseIRacingAlignment && distance is not null && lap.HasDistanceData && zone.HasDistance)
        {
            return new DistanceReferenceAligner(lap, zone);
        }

        return new TimeReferenceAligner(lap, zone);
    }

    private ReferenceSample PreviewReference(double? distance)
    {
        if (_lap is null)
        {
            return ReferenceSample.AtTime(0, 0, 0);
        }

        if (distance is not null && _lap.HasDistanceData)
        {
            return _lap.SampleAtDistance(distance.Value);
        }

        ReferenceSample first = _lap.Samples[0];
        return new ReferenceSample(0, first.BrakePercent, first.ThrottlePercent, 0, first.SpeedKmh);
    }

    private double? ResolveDistance(in CarTelemetry car)
    {
        if (!_settings.UseIRacingAlignment || _lap is null || !_lap.HasDistanceData)
        {
            return null;
        }

        if (!car.IsConnected || !car.HasLapPosition)
        {
            return null;
        }

        return car.LapDistancePercent * _lap.TrackLengthMeters;
    }
}
