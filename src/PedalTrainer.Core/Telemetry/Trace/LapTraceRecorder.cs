using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Telemetry.Trace;

/// <summary>
/// Monta o traço da volta em andamento a partir da telemetria do jogo e fecha a volta quando
/// o carro cruza a linha. A posição vem da pista (não do relógio), então o desenho e os
/// melhores trechos ficam alinhados por distância.
/// </summary>
public sealed class LapTraceRecorder
{
    /// <summary>Passo mínimo de posição para guardar um ponto (~3,5 m numa pista de 4,3 km).</summary>
    public const double MinimumSampleStep = 0.0008;

    /// <summary>Volta mais longa que isso é descartada (carro parado, jogo travado).</summary>
    public const double MaximumLapSeconds = 600;

    /// <summary>Queda de posição que caracteriza cruzamento de linha quando o jogo não numera a volta.</summary>
    public const double LineCrossingDrop = 0.3;

    private readonly List<TelemetryTraceSample> _samples = new();
    private int _lapNumber;
    private double _lapStartSeconds;
    private double _lastPercent;

    /// <summary>Pontos da volta em andamento, para o desenho ao vivo.</summary>
    public IReadOnlyList<TelemetryTraceSample> CurrentSamples => _samples;

    /// <summary>Posição atual na pista, de 0 a 1 (0 quando não há posição).</summary>
    public double CurrentLapPercent { get; private set; }

    /// <summary>Verdadeiro quando já existe um traço sendo montado.</summary>
    public bool IsRunning => _samples.Count > 0;

    /// <summary>Tempo acumulado da volta em andamento, em segundos.</summary>
    public double CurrentLapSeconds { get; private set; }

    /// <summary>Número da volta em andamento, como o jogo informa.</summary>
    public int CurrentLapNumber => _lapNumber;

    /// <summary>
    /// Consome um instantâneo do carro. Devolve a volta fechada quando ela termina — e só
    /// quando cobriu a pista inteira; voltas parciais são descartadas sem aviso.
    /// </summary>
    public TelemetryLapTrace? Observe(in CarTelemetry car, double nowSeconds)
    {
        if (!car.IsConnected || !car.HasLapPosition)
        {
            Reset();
            return null;
        }

        double percent = Math.Clamp(car.LapDistancePercent, 0, 1);
        CurrentLapPercent = percent;

        if (!IsRunning)
        {
            Start(car, percent, nowSeconds);
            return null;
        }

        CurrentLapSeconds = Math.Max(0, nowSeconds - _lapStartSeconds);

        bool crossedByNumber = car.LapNumber > 0 && _lapNumber > 0 && car.LapNumber != _lapNumber;
        bool crossedByPosition = percent < _lastPercent - LineCrossingDrop;

        if (crossedByNumber || crossedByPosition)
        {
            TelemetryLapTrace? completed = Complete(nowSeconds);
            Start(car, percent, nowSeconds);
            return completed;
        }

        if (CurrentLapSeconds > MaximumLapSeconds)
        {
            Start(car, percent, nowSeconds);
            return null;
        }

        if (percent - _lastPercent >= MinimumSampleStep)
        {
            _samples.Add(new TelemetryTraceSample(
                percent,
                car.BrakePercent,
                car.ThrottlePercent,
                car.SpeedKmh,
                nowSeconds));

            _lastPercent = percent;
        }

        return null;
    }

    /// <summary>Joga fora a volta em andamento (troca de pista, desconexão).</summary>
    public void Reset()
    {
        _samples.Clear();
        _lapNumber = 0;
        _lastPercent = 0;
        _lapStartSeconds = 0;
        CurrentLapPercent = 0;
        CurrentLapSeconds = 0;
    }

    private void Start(in CarTelemetry car, double percent, double nowSeconds)
    {
        _samples.Clear();

        _lapNumber = car.LapNumber;
        _lapStartSeconds = nowSeconds;
        _lastPercent = percent;
        CurrentLapSeconds = 0;

        _samples.Add(new TelemetryTraceSample(
            percent,
            car.BrakePercent,
            car.ThrottlePercent,
            car.SpeedKmh,
            nowSeconds));
    }

    private TelemetryLapTrace? Complete(double nowSeconds)
    {
        double duration = Math.Max(0, nowSeconds - _lapStartSeconds);

        // Cópia: a lista viva é reaproveitada pela volta seguinte.
        var frozen = new List<TelemetryTraceSample>(_samples);
        var lap = new TelemetryLapTrace(_lapNumber, duration, frozen);

        return lap.CoversLap() ? lap : null;
    }
}
