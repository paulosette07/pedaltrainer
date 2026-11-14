using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Telemetry.Trace;

/// <summary>
/// Uma volta fechada: os pontos do traço, o tempo total e quanto da pista ela cobriu.
/// Volta só entra no livro de melhores trechos quando cobriu praticamente a pista inteira.
/// </summary>
public sealed class TelemetryLapTrace
{
    private readonly List<TelemetryTraceSample> _samples;

    public TelemetryLapTrace(int lapNumber, double durationSeconds, List<TelemetryTraceSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        LapNumber = lapNumber;
        DurationSeconds = Math.Max(0, durationSeconds);
        _samples = samples;
    }

    /// <summary>Número da volta informado pelo jogo (0 quando o jogo não publica).</summary>
    public int LapNumber { get; }

    /// <summary>Tempo da volta, em segundos.</summary>
    public double DurationSeconds { get; }

    /// <summary>Pontos do traço, em ordem crescente de posição na pista.</summary>
    public IReadOnlyList<TelemetryTraceSample> Samples => _samples;

    /// <summary>Fração da pista coberta pela gravação (1 = volta inteira).</summary>
    public double CoveragePercent
    {
        get
        {
            if (_samples.Count == 0)
            {
                return 0;
            }

            double first = _samples[0].LapPercent;
            double last = _samples[^1].LapPercent;
            return Math.Clamp(last - first, 0, 1);
        }
    }

    /// <summary>Verdadeiro quando a volta cobriu pelo menos <paramref name="minimum"/> da pista.</summary>
    public bool CoversLap(double minimum = 0.9) => CoveragePercent >= minimum;
}
