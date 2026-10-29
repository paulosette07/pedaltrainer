using System;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Mede uma repetição em tempo real: compara o pedal com a curva-alvo, acumula o erro
/// quadrático, guarda o pico, detecta sobressalto e calcula a nota.
/// </summary>
public sealed class DrillRepetitionAnalyzer
{
    /// <summary>Erro RMS (em pontos percentuais) que zera a nota.</summary>
    public const double RmsScoreScale = 22.0;

    /// <summary>Tempo inicial ignorado, para não punir o atraso de detecção do movimento.</summary>
    public const double GraceSeconds = 0.15;

    /// <summary>Janela usada para medir a taxa de subida.</summary>
    public const double RateWindowSeconds = 0.12;

    private readonly DrillProfile _profile;
    private readonly double _tolerancePercent;
    private readonly double[] _windowTimes = new double[64];
    private readonly double[] _windowValues = new double[64];

    private int _windowCount;
    private int _windowStart;
    private double _errorSumSquares;
    private int _sampleCount;
    private double _holdErrorSum;
    private int _holdSamples;
    private double _peakPercent;
    private double _peakSeconds;
    private double _lastPercent;
    private double _lastSeconds;
    private double _maxRiseRate;

    public DrillRepetitionAnalyzer(DrillProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _tolerancePercent = Math.Max(0.5, profile.Step.TolerancePercent);
    }

    /// <summary>Erro instantâneo em relação à curva (pode ser negativo).</summary>
    public double InstantErrorPercent { get; private set; }

    /// <summary>Percentual esperado neste instante.</summary>
    public double TargetPercent { get; private set; }

    /// <summary>Erro RMS acumulado até agora.</summary>
    public double RmsErrorPercent =>
        _sampleCount == 0 ? 0 : Math.Sqrt(_errorSumSquares / _sampleCount);

    /// <summary>Sobressalto atual acima do topo da curva.</summary>
    public double OvershootPercent => Math.Max(0, _peakPercent - _profile.PeakPercent);

    /// <summary>Maior taxa de subida medida até agora.</summary>
    public double RiseRatePercentPerSecond => _maxRiseRate;

    /// <summary>Nota projetada com o que já foi medido.</summary>
    public double LiveScore => ComputeScore();

    /// <summary>Registra um quadro do pedal.</summary>
    public void Add(double elapsedSeconds, double percent)
    {
        TargetPercent = _profile.TargetAt(elapsedSeconds);
        InstantErrorPercent = percent - TargetPercent;

        if (elapsedSeconds >= GraceSeconds)
        {
            _errorSumSquares += InstantErrorPercent * InstantErrorPercent;
            _sampleCount++;

            if (_profile.KindAt(elapsedSeconds) == DrillSegmentKind.Hold)
            {
                _holdErrorSum += Math.Abs(InstantErrorPercent);
                _holdSamples++;
            }
        }

        if (percent > _peakPercent)
        {
            _peakPercent = percent;
            _peakSeconds = elapsedSeconds;
        }

        PushRateWindow(elapsedSeconds, percent);

        _lastPercent = percent;
        _lastSeconds = elapsedSeconds;
    }

    /// <summary>Fecha a repetição e devolve o resultado consolidado.</summary>
    public DrillRepetitionResult Complete(int repetitionNumber, int repetitionCount)
    {
        double score = ComputeScore();

        return new DrillRepetitionResult
        {
            StepLabel = DrillInstructor.StepTitle(_profile.Step),
            PedalName = DrillPedalNames.Short(_profile.Step.Pedal),
            RepetitionNumber = repetitionNumber,
            RepetitionCount = repetitionCount,
            TargetPercent = _profile.TargetPercent,
            Score = score,
            Grade = ScoreGrade.From(score),
            RmsErrorPercent = RmsErrorPercent,
            HoldErrorPercent = _holdSamples == 0 ? 0 : _holdErrorSum / _holdSamples,
            PeakPercent = _peakPercent,
            OvershootPercent = OvershootPercent,
            RiseRatePercentPerSecond = _maxRiseRate,
            ReleaseRatePercentPerSecond = MeasureReleaseRate(),
            DurationSeconds = Math.Max(_lastSeconds, _profile.DurationSeconds)
        };
    }

    private double ComputeScore()
    {
        if (_sampleCount == 0)
        {
            return 0;
        }

        double score = ScoreGrade.ScoreFromRms(RmsErrorPercent, RmsScoreScale);

        double overshoot = Math.Max(0, OvershootPercent - (_tolerancePercent * 0.5));
        double overshootPenalty = Math.Clamp(overshoot * 1.4, 0, 35);

        return Math.Clamp(score - overshootPenalty - ReleasePenalty(), 0, 100);
    }

    /// <summary>
    /// Penaliza soltar o pedal muito mais rápido do que o pedido: o objetivo do exercício
    /// é justamente a soltura progressiva.
    /// </summary>
    private double ReleasePenalty()
    {
        double planned = PlannedReleaseRate();
        if (planned <= 0.5)
        {
            return 0;
        }

        double measured = MeasureReleaseRate();
        if (measured <= planned * 2.5)
        {
            return 0;
        }

        return Math.Clamp((measured / planned - 2.5) * 5, 0, 12);
    }

    private double MeasureReleaseRate()
    {
        double span = _lastSeconds - _peakSeconds;
        if (span <= 0.05)
        {
            return 0;
        }

        double drop = _peakPercent - _lastPercent;
        return drop <= 0 ? 0 : drop / span;
    }

    private double PlannedReleaseRate()
    {
        for (int index = _profile.SegmentCount - 1; index >= 0; index--)
        {
            if (_profile.SegmentKind(index) != DrillSegmentKind.Release)
            {
                continue;
            }

            double from = _profile.SegmentStartPercent(index);
            double to = _profile.SegmentEndPercent(index);
            double seconds = _profile.SegmentSeconds(index);

            return seconds <= 0 ? 0 : Math.Max(0, from - to) / seconds;
        }

        return 0;
    }

    private void PushRateWindow(double elapsedSeconds, double percent)
    {
        int slot = (_windowStart + _windowCount) % _windowTimes.Length;

        if (_windowCount < _windowTimes.Length)
        {
            _windowCount++;
        }
        else
        {
            _windowStart = (_windowStart + 1) % _windowTimes.Length;
            slot = (_windowStart + _windowCount - 1) % _windowTimes.Length;
        }

        _windowTimes[slot] = elapsedSeconds;
        _windowValues[slot] = percent;

        while (_windowCount > 2 && elapsedSeconds - _windowTimes[_windowStart] > RateWindowSeconds)
        {
            _windowStart = (_windowStart + 1) % _windowTimes.Length;
            _windowCount--;
        }

        double span = elapsedSeconds - _windowTimes[_windowStart];
        if (span <= 0.02)
        {
            return;
        }

        double rate = (percent - _windowValues[_windowStart]) / span;
        if (rate > _maxRiseRate)
        {
            _maxRiseRate = rate;
        }
    }
}
