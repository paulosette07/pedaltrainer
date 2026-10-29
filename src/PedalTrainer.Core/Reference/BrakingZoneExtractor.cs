using System;
using System.Collections.Generic;
using System.Globalization;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>Parâmetros da detecção automática de zonas de frenagem.</summary>
public sealed class BrakingZoneOptions
{
    /// <summary>Pressão (%) que inicia uma zona.</summary>
    public double EnterThresholdPercent { get; init; } = 5.0;

    /// <summary>Pressão (%) abaixo da qual a zona é considerada encerrada (histerese).</summary>
    public double ExitThresholdPercent { get; init; } = 3.0;

    /// <summary>Duração mínima para a zona valer como desafio.</summary>
    public double MinDurationSeconds { get; init; } = 0.20;

    /// <summary>Pausas menores que isso entre duas frenagens são unificadas.</summary>
    public double MergeGapSeconds { get; init; } = 0.15;

    /// <summary>Quantidade máxima de zonas devolvidas (as mais relevantes primeiro).</summary>
    public int MaxZones { get; init; } = 40;
}

/// <summary>
/// Extrai da volta de referência os trechos em que o piloto freou.
/// Cada trecho vira uma zona de treino independente.
/// </summary>
public static class BrakingZoneExtractor
{
    public static IReadOnlyList<BrakingZone> Extract(ReferenceLap lap, BrakingZoneOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(lap);
        options ??= new BrakingZoneOptions();

        var runs = FindActiveRuns(lap, options);
        var merged = MergeRuns(lap, runs, options.MergeGapSeconds);

        var zones = new List<BrakingZone>();
        for (int i = 0; i < merged.Count && zones.Count < options.MaxZones; i++)
        {
            (int startIndex, int endIndex) = merged[i];
            BrakingZone zone = BuildZone(lap, startIndex, endIndex, zones.Count);
            if (zone.DurationSeconds >= options.MinDurationSeconds && zone.PeakBrakePercent >= options.EnterThresholdPercent)
            {
                zones.Add(zone);
            }
        }

        return zones;
    }

    private static List<(int Start, int End)> FindActiveRuns(ReferenceLap lap, BrakingZoneOptions options)
    {
        var runs = new List<(int Start, int End)>();
        int startIndex = -1;

        IReadOnlyList<ReferenceSample> samples = lap.Samples;
        for (int i = 0; i < samples.Count; i++)
        {
            double brake = samples[i].BrakePercent;

            if (startIndex < 0)
            {
                if (brake >= options.EnterThresholdPercent)
                {
                    startIndex = i;
                }

                continue;
            }

            if (brake <= options.ExitThresholdPercent)
            {
                runs.Add((startIndex, Math.Max(startIndex, i - 1)));
                startIndex = -1;
            }
        }

        if (startIndex >= 0)
        {
            runs.Add((startIndex, samples.Count - 1));
        }

        return runs;
    }

    private static List<(int Start, int End)> MergeRuns(
        ReferenceLap lap,
        List<(int Start, int End)> runs,
        double mergeGapSeconds)
    {
        var merged = new List<(int Start, int End)>(runs.Count);
        foreach ((int start, int end) in runs)
        {
            if (merged.Count == 0)
            {
                merged.Add((start, end));
                continue;
            }

            (int previousStart, int previousEnd) = merged[^1];
            double gap = lap.Samples[start].TimeSeconds - lap.Samples[previousEnd].TimeSeconds;

            if (gap <= mergeGapSeconds)
            {
                merged[^1] = (previousStart, Math.Max(previousEnd, end));
            }
            else
            {
                merged.Add((start, end));
            }
        }

        return merged;
    }

    private static BrakingZone BuildZone(ReferenceLap lap, int startIndex, int endIndex, int ordinal)
    {
        IReadOnlyList<ReferenceSample> samples = lap.Samples;
        ReferenceSample first = samples[startIndex];
        ReferenceSample last = samples[endIndex];

        double peak = 0;
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (samples[i].BrakePercent > peak)
            {
                peak = samples[i].BrakePercent;
            }
        }

        bool hasDistance = lap.HasDistanceData;
        string label = string.Create(
            CultureInfo.InvariantCulture,
            $"Zona {ordinal + 1} · {first.TimeSeconds:0.0}s");

        return new BrakingZone(
            Index: ordinal,
            StartSampleIndex: startIndex,
            EndSampleIndex: endIndex,
            StartTimeSeconds: first.TimeSeconds,
            EndTimeSeconds: last.TimeSeconds,
            StartDistanceMeters: hasDistance ? first.DistanceMeters : 0,
            EndDistanceMeters: hasDistance ? last.DistanceMeters : 0,
            PeakBrakePercent: peak,
            Name: label);
    }
}
