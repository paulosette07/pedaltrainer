using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>
/// Gera uma volta de demonstração em memória (8 zonas de frenagem, 60 s),
/// para que o app funcione imediatamente sem iRacing e sem arquivo externo.
/// </summary>
public static class DemoReferenceLapFactory
{
    /// <summary>Nome exibido na interface para a volta gerada.</summary>
    public const string DemoLapName = "Volta demo (PedalTrainer)";

    private const double LapDurationSeconds = 60.0;
    private const double SampleRateHz = 100.0;
    private const double TrackLengthMeters = 2400.0;
    private const double ReferenceSpeedMps = TrackLengthMeters / LapDurationSeconds;

    // Início, duração do platô, pico e tempo total de frenagem.
    private static readonly (double Start, double Plateau, double Peak, double Length)[] Zones =
    {
        (6.0, 0.9, 95, 2.6),
        (14.0, 1.1, 88, 3.0),
        (22.0, 0.8, 78, 2.5),
        (30.0, 1.2, 100, 3.2),
        (38.0, 0.7, 62, 2.0),
        (45.0, 1.0, 90, 3.0),
        (52.0, 0.6, 70, 2.0),
        (56.5, 0.8, 85, 2.5)
    };

    public static ReferenceLap Create()
    {
        int sampleCount = (int)Math.Round(LapDurationSeconds * SampleRateHz) + 1;
        var samples = new List<ReferenceSample>(sampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            double time = i / SampleRateHz;
            double brake = BrakeAt(time);
            double throttle = ThrottleAt(brake);
            double distance = Math.Min(TrackLengthMeters, time * ReferenceSpeedMps);

            samples.Add(new ReferenceSample(time, brake, throttle, distance, ReferenceSpeedMps * 3.6));
        }

        return ReferenceLap.Create(DemoLapName, samples, TrackLengthMeters);
    }

    /// <summary>Serializa a volta em CSV no formato aceito pelo carregador.</summary>
    public static string ToCsv(ReferenceLap lap) => ReferenceLapCsvWriter.ToCsv(lap);

    private static double BrakeAt(double time)
    {
        double brake = 0;

        foreach ((double start, double plateau, double peak, double length) in Zones)
        {
            if (time < start || time > start + length)
            {
                continue;
            }

            brake = Math.Max(brake, ZoneBrake(time - start, plateau, peak, length));
        }

        return Math.Clamp(brake, 0, 100);
    }

    private static double ZoneBrake(double elapsed, double plateau, double peak, double length)
    {
        const double attackSeconds = 0.30;
        double holdEnd = attackSeconds + plateau;

        if (elapsed <= attackSeconds)
        {
            return peak * (elapsed / attackSeconds);
        }

        if (elapsed <= holdEnd)
        {
            return peak;
        }

        double releaseSpan = Math.Max(0.05, length - holdEnd);
        double ratio = (elapsed - holdEnd) / releaseSpan;
        return Math.Max(0, peak * (1 - Math.Clamp(ratio, 0, 1)));
    }

    private static double ThrottleAt(double brake) =>
        Math.Clamp(100 - (brake * 1.25), 0, 100);
}
