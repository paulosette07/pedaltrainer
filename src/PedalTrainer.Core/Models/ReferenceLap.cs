using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Models;

/// <summary>
/// Volta de referência completa (o "fantasma" do piloto profissional).
/// Oferece consulta interpolada por tempo e por distância.
/// </summary>
public sealed class ReferenceLap
{
    private ReferenceLap(string name, IReadOnlyList<ReferenceSample> samples, double trackLengthMeters)
    {
        Name = name;
        Samples = samples;
        TrackLengthMeters = trackLengthMeters;
    }

    public string Name { get; }

    public IReadOnlyList<ReferenceSample> Samples { get; }

    /// <summary>Comprimento da pista em metros (0 quando desconhecido).</summary>
    public double TrackLengthMeters { get; }

    /// <summary>Duração da volta de referência em segundos.</summary>
    public double DurationSeconds =>
        Samples.Count == 0 ? 0 : Samples[^1].TimeSeconds - Samples[0].TimeSeconds;

    public bool HasDistanceData => TrackLengthMeters > 0 && Samples.Count > 1 && Samples[^1].HasDistance;

    public double StartTimeSeconds => Samples.Count == 0 ? 0 : Samples[0].TimeSeconds;

    /// <summary>
    /// Cria a volta validando e ordenando as amostras por tempo.
    /// O comprimento da pista é inferido da última distância informada.
    /// </summary>
    public static ReferenceLap Create(
        string name,
        IEnumerable<ReferenceSample> samples,
        double trackLengthMeters = 0)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var ordered = new List<ReferenceSample>();
        foreach (var sample in samples)
        {
            ordered.Add(WithNormalizedValues(sample));
        }

        ordered.Sort(static (a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));

        if (ordered.Count == 0)
        {
            throw new ArgumentException("A volta de referência não possui amostras.", nameof(samples));
        }

        double inferredLength = trackLengthMeters;
        if (inferredLength <= 0 && ordered[^1].HasDistance)
        {
            inferredLength = ordered[^1].DistanceMeters;
        }

        return new ReferenceLap(string.IsNullOrWhiteSpace(name) ? "Referência" : name, ordered, inferredLength);
    }

    /// <summary>Amostra interpolada linearmente no instante informado.</summary>
    public ReferenceSample SampleAtTime(double timeSeconds)
    {
        if (Samples.Count == 1)
        {
            return Samples[0];
        }

        if (timeSeconds <= Samples[0].TimeSeconds)
        {
            return Samples[0];
        }

        if (timeSeconds >= Samples[^1].TimeSeconds)
        {
            return Samples[^1];
        }

        int low = 0;
        int high = Samples.Count - 1;
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (Samples[mid].TimeSeconds <= timeSeconds)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return Interpolate(Samples[low], Samples[high], timeSeconds);
    }

    /// <summary>Amostra interpolada linearmente na distância percorrida (metros).</summary>
    public ReferenceSample SampleAtDistance(double distanceMeters)
    {
        if (!HasDistanceData)
        {
            return SampleAtTime(StartTimeSeconds);
        }

        if (distanceMeters <= Samples[0].DistanceMeters)
        {
            return Samples[0];
        }

        if (distanceMeters >= Samples[^1].DistanceMeters)
        {
            return Samples[^1];
        }

        int low = 0;
        int high = Samples.Count - 1;
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (Samples[mid].DistanceMeters <= distanceMeters)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        double span = Samples[high].DistanceMeters - Samples[low].DistanceMeters;
        if (span <= 0)
        {
            return Samples[low];
        }

        double ratio = (distanceMeters - Samples[low].DistanceMeters) / span;
        return Blend(Samples[low], Samples[high], ratio);
    }

    /// <summary>Converte um percentual de volta (0..1, como no iRacing) em metros.</summary>
    public double DistanceFromLapPercent(double lapDistancePercent)
    {
        if (TrackLengthMeters <= 0)
        {
            return 0;
        }

        return Math.Clamp(lapDistancePercent, 0, 1) * TrackLengthMeters;
    }

    private static ReferenceSample Interpolate(ReferenceSample from, ReferenceSample to, double timeSeconds)
    {
        double span = to.TimeSeconds - from.TimeSeconds;
        if (span <= 0)
        {
            return from;
        }

        return Blend(from, to, (timeSeconds - from.TimeSeconds) / span);
    }

    private static ReferenceSample Blend(ReferenceSample from, ReferenceSample to, double ratio)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        return new ReferenceSample(
            from.TimeSeconds + ((to.TimeSeconds - from.TimeSeconds) * ratio),
            from.BrakePercent + ((to.BrakePercent - from.BrakePercent) * ratio),
            from.ThrottlePercent + ((to.ThrottlePercent - from.ThrottlePercent) * ratio),
            from.DistanceMeters + ((to.DistanceMeters - from.DistanceMeters) * ratio),
            from.SpeedKmh + ((to.SpeedKmh - from.SpeedKmh) * ratio));
    }

    private static ReferenceSample WithNormalizedValues(ReferenceSample sample) => new(
        sample.TimeSeconds,
        Math.Clamp(sample.BrakePercent, 0, 100),
        Math.Clamp(sample.ThrottlePercent, 0, 100),
        Math.Max(0, sample.DistanceMeters),
        Math.Max(0, sample.SpeedKmh));
}
