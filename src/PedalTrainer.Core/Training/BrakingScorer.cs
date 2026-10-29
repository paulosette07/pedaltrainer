using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>Parâmetros da pontuação de uma zona de frenagem.</summary>
public sealed class BrakingScoreOptions
{
    /// <summary>Diferença (%) para a referência que ainda conta como acerto.</summary>
    public double TolerancePercent { get; init; } = 8.0;

    /// <summary>Peso mínimo de uma amostra: evita que trechos leves dominem a nota.</summary>
    public double WeightFloor { get; init; } = 0.25;
}

/// <summary>
/// Compara, amostra a amostra, a curva de freio do piloto com a curva de referência e
/// condensa tudo em uma nota de 0 a 100. Compara também o acelerador, apenas como métrica
/// auxiliar — a nota principal é a precisão da frenagem.
/// </summary>
public sealed class BrakingScorer
{
    private readonly IReferenceAligner _aligner;
    private readonly BrakingScoreOptions _options;

    private double _weightedScoreSum;
    private double _weightSum;
    private double _squaredErrorSum;
    private double _throttleScoreSum;
    private int _sampleCount;

    private double _peakUserBrake;
    private double _peakReferenceBrake;
    private double _timeToPeakUser;
    private double _timeToPeakReference;

    public BrakingScorer(IReferenceAligner aligner, BrakingScoreOptions? options = null)
    {
        _aligner = aligner ?? throw new ArgumentNullException(nameof(aligner));
        _options = options ?? new BrakingScoreOptions();
    }

    /// <summary>Zona em avaliação.</summary>
    public BrakingZone Zone => _aligner.Zone;

    /// <summary>Nota acumulada da janela (0 a 100).</summary>
    public double LiveScore => _weightSum <= 0 ? 0 : 100.0 * _weightedScoreSum / _weightSum;

    /// <summary>Erro instantâneo em pontos percentuais.</summary>
    public double InstantErrorPercent { get; private set; }

    /// <summary>Precisão do acelerador na janela (0 a 100), apenas informativa.</summary>
    public double ThrottleAccuracy => _sampleCount == 0 ? 0 : 100.0 * _throttleScoreSum / _sampleCount;

    public double RmsePercent => _sampleCount == 0 ? 0 : Math.Sqrt(_squaredErrorSum / _sampleCount);

    public int SampleCount => _sampleCount;

    public double ReferenceBrakePercent { get; private set; }

    public double ReferenceThrottlePercent { get; private set; }

    public double Progress { get; private set; }

    /// <summary>Registra uma amostra do piloto e devolve a nota acumulada até aqui.</summary>
    public double AddSample(
        double elapsedSeconds,
        double userBrakePercent,
        double userThrottlePercent,
        double? distanceMeters)
    {
        ReferenceSample reference = _aligner.Resolve(elapsedSeconds, distanceMeters);
        ReferenceBrakePercent = reference.BrakePercent;
        ReferenceThrottlePercent = reference.ThrottlePercent;
        Progress = _aligner.Progress(elapsedSeconds, distanceMeters);

        double brakeError = Math.Abs(userBrakePercent - reference.BrakePercent);
        InstantErrorPercent = brakeError;

        double brakeScore = ComputeSampleScore(brakeError, _options.TolerancePercent);
        double throttleScore = ComputeSampleScore(
            Math.Abs(userThrottlePercent - reference.ThrottlePercent),
            _options.TolerancePercent);

        double weight = _options.WeightFloor +
            ((1 - _options.WeightFloor) * Math.Clamp(reference.BrakePercent / 100.0, 0, 1));

        _weightedScoreSum += weight * brakeScore;
        _weightSum += weight;
        _throttleScoreSum += throttleScore;
        _squaredErrorSum += brakeError * brakeError;
        _sampleCount++;

        TrackPeaks(elapsedSeconds, userBrakePercent, reference.BrakePercent);

        return LiveScore;
    }

    /// <summary>Fecha a janela e devolve o resultado consolidado.</summary>
    public CornerResult Complete(DateTime utcNow) => new(
        ZoneIndex: Zone.Index,
        ZoneName: Zone.Name,
        Score: Math.Clamp(LiveScore, 0, 100),
        RmsePercent: RmsePercent,
        PeakBrakeUserPercent: _peakUserBrake,
        PeakBrakeReferencePercent: _peakReferenceBrake,
        TimeToPeakUserSeconds: _timeToPeakUser,
        TimeToPeakReferenceSeconds: _timeToPeakReference,
        SampleCount: _sampleCount,
        CompletedAtUtc: utcNow);

    /// <summary>
    /// Nota de uma amostra: 1 dentro da banda de tolerância, caindo linearmente até 0
    /// quando o erro chega a 100 pontos percentuais.
    /// </summary>
    public static double ComputeSampleScore(double errorPercent, double tolerancePercent)
    {
        double tolerance = Math.Clamp(tolerancePercent, 0, 95);
        double span = Math.Max(1, 100 - tolerance);
        double penalty = Math.Clamp((Math.Abs(errorPercent) - tolerance) / span, 0, 1);
        return 1 - penalty;
    }

    private void TrackPeaks(double elapsedSeconds, double userBrake, double referenceBrake)
    {
        if (userBrake > _peakUserBrake)
        {
            _peakUserBrake = userBrake;
            _timeToPeakUser = elapsedSeconds;
        }

        if (referenceBrake > _peakReferenceBrake)
        {
            _peakReferenceBrake = referenceBrake;
            _timeToPeakReference = elapsedSeconds;
        }
    }
}
