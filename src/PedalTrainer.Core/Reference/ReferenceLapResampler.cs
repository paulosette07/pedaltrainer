using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>
/// Reamostra uma volta para uma taxa fixa (padrão 100 Hz). Isso deixa o alinhamento
/// previsível e barato, independentemente da taxa do arquivo original.
/// </summary>
public static class ReferenceLapResampler
{
    /// <summary>Taxa padrão usada pelo motor de treino.</summary>
    public const double DefaultRateHz = 100.0;

    /// <summary>
    /// Gera amostras em intervalos fixos a partir da primeira amostra da volta.
    /// Distância e velocidade só são propagadas quando existem no arquivo original.
    /// </summary>
    public static ReferenceLap Resample(ReferenceLap lap, double rateHz = DefaultRateHz)
    {
        ArgumentNullException.ThrowIfNull(lap);
        rateHz = Math.Clamp(rateHz, 10, 1000);

        double duration = lap.DurationSeconds;
        if (duration <= 0 || lap.Samples.Count < 2)
        {
            return lap;
        }

        double step = 1.0 / rateHz;
        int sampleCount = (int)Math.Round(duration * rateHz) + 1;
        var samples = new List<ReferenceSample>(sampleCount);

        double start = lap.StartTimeSeconds;
        bool hasDistance = lap.HasDistanceData;

        for (int i = 0; i < sampleCount; i++)
        {
            double time = start + (i * step);
            ReferenceSample interpolated = lap.SampleAtTime(time);

            samples.Add(new ReferenceSample(
                TimeSeconds: time - start,
                BrakePercent: interpolated.BrakePercent,
                ThrottlePercent: interpolated.ThrottlePercent,
                DistanceMeters: hasDistance ? interpolated.DistanceMeters : 0,
                SpeedKmh: interpolated.SpeedKmh));
        }

        return ReferenceLap.Create(lap.Name, samples, lap.TrackLengthMeters);
    }

    /// <summary>Tempo, em segundos, entre duas amostras consecutivas de uma volta reamostrada.</summary>
    public static double EstimateSampleInterval(ReferenceLap lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        if (lap.Samples.Count < 2)
        {
            return 0;
        }

        return lap.Samples[1].TimeSeconds - lap.Samples[0].TimeSeconds;
    }
}
