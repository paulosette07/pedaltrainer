using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Telemetry.Trace;

/// <summary>
/// Converte uma volta de referência em traço por posição na pista. Serve para a tela mostrar
/// o desenho de freio e acelerador mesmo com o simulador fechado — é a referência, não a
/// volta do piloto, e a tela diz isso.
/// </summary>
public static class ReferenceTraceBuilder
{
    public static IReadOnlyList<TelemetryTraceSample> Build(ReferenceLap? lap)
    {
        if (lap is null || lap.Samples.Count < 2)
        {
            return Array.Empty<TelemetryTraceSample>();
        }

        IReadOnlyList<ReferenceSample> samples = lap.Samples;
        double length = lap.TrackLengthMeters;
        var trace = new List<TelemetryTraceSample>(samples.Count);

        for (int index = 0; index < samples.Count; index++)
        {
            ReferenceSample sample = samples[index];

            double percent = length > 0
                ? Math.Clamp(sample.DistanceMeters / length, 0, 1)
                : (double)index / (samples.Count - 1);

            trace.Add(new TelemetryTraceSample(
                percent,
                sample.BrakePercent,
                sample.ThrottlePercent,
                sample.SpeedKmh,
                sample.TimeSeconds));
        }

        return trace;
    }
}
