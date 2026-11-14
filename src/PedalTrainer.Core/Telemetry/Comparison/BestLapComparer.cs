using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Telemetry.Comparison;

/// <summary>
/// Compara o carro em pista com a melhor volta (a referência ativa) ponto a ponto.
/// O delta de tempo é integrado por distância: a cada passo somamos o tempo que o carro
/// gastou menos o tempo que a referência gastou no mesmo trecho, então o número vale
/// mesmo quando o piloto freia em outro instante.
/// </summary>
public sealed class BestLapComparer
{
    /// <summary>Passo mínimo para somar (evita ruído de posição parada).</summary>
    private const double MinStepMeters = 0.2;

    /// <summary>Passo máximo aceito como um quadro normal.</summary>
    private const double MaxStepMeters = 60;

    /// <summary>Tempo máximo de um quadro antes de tratar como salto.</summary>
    private const double MaxStepSeconds = 1.5;

    /// <summary>Queda de posição que caracteriza nova volta (cruzou a linha).</summary>
    private const double LapRestartBehindMeters = 30;

    /// <summary>Trecho de referência longo demais indica buraco na volta; não soma.</summary>
    private const double MaxReferenceStepSeconds = 5;

    private ReferenceLap? _reference;
    private int _lapNumber = int.MinValue;
    private double _lastDistanceMeters = double.NaN;
    private double _lastTimeSeconds;
    private double _accumulatedSeconds;
    private double _bestSeconds = double.PositiveInfinity;
    private double _lastLapSeconds = double.NaN;
    private bool _hasAccumulation;

    /// <summary>Zera o acumulado (troca de pista ou de referência).</summary>
    public void Reset()
    {
        _reference = null;
        _lapNumber = int.MinValue;
        _lastDistanceMeters = double.NaN;
        _lastTimeSeconds = 0;
        _accumulatedSeconds = 0;
        _bestSeconds = double.PositiveInfinity;
        _lastLapSeconds = double.NaN;
        _hasAccumulation = false;
    }

    /// <summary>
    /// Atualiza a comparação com o instantâneo do carro. <paramref name="nowSeconds"/> precisa
    /// ser monotônico entre chamadas (serve o relógio do motor ou o carimbo do jogo).
    /// </summary>
    public BestLapComparison Update(ReferenceLap? reference, in CarTelemetry car, double nowSeconds)
    {
        if (reference is null)
        {
            Reset();
            return BestLapComparison.None;
        }

        if (!ReferenceEquals(reference, _reference))
        {
            Reset();
            _reference = reference;
        }

        var comparison = new BestLapComparison
        {
            ReferenceName = reference.Name,
            ReferenceDurationSeconds = reference.DurationSeconds
        };

        if (!reference.HasDistanceData)
        {
            return comparison with { Hint = "A melhor volta não traz a distância percorrida." };
        }

        if (!car.HasLapPosition)
        {
            return comparison with { Hint = "O jogo não publica a posição na volta." };
        }

        double distance = reference.DistanceFromLapPercent(car.LapDistancePercent);
        ReferenceSample sample = reference.SampleAtDistance(distance);

        StartLapIfNeeded(car.LapNumber, distance, nowSeconds);
        Accumulate(reference, distance, nowSeconds);

        bool hasBest = !double.IsPositiveInfinity(_bestSeconds);

        return comparison with
        {
            CanCompare = true,
            DeltaSeconds = _accumulatedSeconds,
            HasDelta = _hasAccumulation,
            BestDeltaSeconds = hasBest ? _bestSeconds : 0,
            HasBestDelta = hasBest,
            LastLapDeltaSeconds = double.IsNaN(_lastLapSeconds) ? 0 : _lastLapSeconds,
            HasLastLapDelta = !double.IsNaN(_lastLapSeconds),
            LapProgress = Math.Clamp(car.LapDistancePercent, 0, 1),
            SpeedKmh = car.SpeedKmh,
            ReferenceSpeedKmh = sample.SpeedKmh,
            BrakePercent = car.BrakePercent,
            ReferenceBrakePercent = sample.BrakePercent,
            ThrottlePercent = car.ThrottlePercent,
            ReferenceThrottlePercent = sample.ThrottlePercent
        };
    }

    /// <summary>Detecta volta nova (número mudou ou a posição voltou para trás) e zera o delta.</summary>
    private void StartLapIfNeeded(int lapNumber, double distanceMeters, double nowSeconds)
    {
        bool lapChanged = lapNumber != _lapNumber;
        bool crossedLine = !double.IsNaN(_lastDistanceMeters)
                           && distanceMeters + LapRestartBehindMeters < _lastDistanceMeters;

        if (!lapChanged && !crossedLine)
        {
            return;
        }

        if (_hasAccumulation)
        {
            _lastLapSeconds = _accumulatedSeconds;
        }

        _lapNumber = lapNumber;
        _accumulatedSeconds = 0;
        _bestSeconds = double.PositiveInfinity;
        _hasAccumulation = false;
        _lastDistanceMeters = double.NaN;
        _lastTimeSeconds = nowSeconds;
    }

    /// <summary>Soma o delta do trecho entre a última posição e a atual.</summary>
    private void Accumulate(ReferenceLap reference, double distanceMeters, double nowSeconds)
    {
        if (double.IsNaN(_lastDistanceMeters))
        {
            _lastDistanceMeters = distanceMeters;
            _lastTimeSeconds = nowSeconds;
            return;
        }

        double step = distanceMeters - _lastDistanceMeters;
        double elapsed = nowSeconds - _lastTimeSeconds;

        if (step < MinStepMeters || step > MaxStepMeters || elapsed <= 0 || elapsed > MaxStepSeconds)
        {
            // Quadro perdido, carro parado ou salto do jogo: realinha sem somar nada.
            if (step >= 0 && elapsed > 0)
            {
                _lastDistanceMeters = distanceMeters;
                _lastTimeSeconds = nowSeconds;
            }

            return;
        }

        double referenceStep = reference.SampleAtDistance(distanceMeters).TimeSeconds
                               - reference.SampleAtDistance(_lastDistanceMeters).TimeSeconds;

        _lastDistanceMeters = distanceMeters;
        _lastTimeSeconds = nowSeconds;

        if (referenceStep <= 0 || referenceStep > MaxReferenceStepSeconds)
        {
            return;
        }

        _accumulatedSeconds += elapsed - referenceStep;
        _hasAccumulation = true;

        if (_accumulatedSeconds < _bestSeconds)
        {
            _bestSeconds = _accumulatedSeconds;
        }
    }
}
