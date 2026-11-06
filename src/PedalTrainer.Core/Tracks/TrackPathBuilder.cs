using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Transforma a lista crua de pontos de um traçado numa <see cref="TrackPath"/>: arredonda
/// os cantos (Catmull-Rom), fecha a volta, ancora a linha de chegada, ajusta a escala para o
/// comprimento oficial e reamostra em passos iguais.
/// </summary>
public static class TrackPathBuilder
{
    /// <summary>Distância entre pontos do traçado final (metros).</summary>
    public const double DefaultSpacingMeters = 6.0;

    private const int SmoothingSubdivisions = 6;

    public static TrackPath Build(
        IReadOnlyList<TrackPoint> rawPoints,
        double targetLengthMeters = 0,
        TrackPoint? startAnchor = null,
        double spacingMeters = DefaultSpacingMeters)
    {
        ArgumentNullException.ThrowIfNull(rawPoints);

        List<TrackPoint> points = SmoothAndClose(rawPoints);

        if (startAnchor is TrackPoint anchor)
        {
            points = RotateToNearest(points, anchor);
        }

        if (targetLengthMeters > 0)
        {
            points = ScaleToLength(points, targetLengthMeters);
        }

        return new TrackPath(Resample(points, Math.Clamp(spacingMeters, 2.0, 30.0)));
    }

    /// <summary>Suaviza os cantos e garante que o último ponto coincide com o primeiro.</summary>
    private static List<TrackPoint> SmoothAndClose(IReadOnlyList<TrackPoint> rawPoints)
    {
        var cleaned = new List<TrackPoint>(rawPoints.Count + 1);

        foreach (TrackPoint point in rawPoints)
        {
            if (cleaned.Count == 0 || cleaned[^1].DistanceTo(point) > 0.5)
            {
                cleaned.Add(point);
            }
        }

        if (cleaned.Count >= 2 && cleaned[0].DistanceTo(cleaned[^1]) <= 1.0)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        if (cleaned.Count < 3)
        {
            throw new ArgumentException("O traçado precisa de pelo menos três pontos distintos.", nameof(rawPoints));
        }

        int count = cleaned.Count;
        var smoothed = new List<TrackPoint>(count * SmoothingSubdivisions);

        for (var index = 0; index < count; index++)
        {
            TrackPoint previous = cleaned[(index - 1 + count) % count];
            TrackPoint start = cleaned[index];
            TrackPoint end = cleaned[(index + 1) % count];
            TrackPoint next = cleaned[(index + 2) % count];

            for (var step = 0; step < SmoothingSubdivisions; step++)
            {
                smoothed.Add(CatmullRom(previous, start, end, next, (double)step / SmoothingSubdivisions));
            }
        }

        return smoothed;
    }

    private static TrackPoint CatmullRom(TrackPoint p0, TrackPoint p1, TrackPoint p2, TrackPoint p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        double x = 0.5 * ((2 * p1.X) +
                          ((-p0.X + p2.X) * t) +
                          (((2 * p0.X) - (5 * p1.X) + (4 * p2.X) - p3.X) * t2) +
                          ((-p0.X + (3 * p1.X) - (3 * p2.X) + p3.X) * t3));

        double y = 0.5 * ((2 * p1.Y) +
                          ((-p0.Y + p2.Y) * t) +
                          (((2 * p0.Y) - (5 * p1.Y) + (4 * p2.Y) - p3.Y) * t2) +
                          ((-p0.Y + (3 * p1.Y) - (3 * p2.Y) + p3.Y) * t3));

        return new TrackPoint(x, y);
    }

    /// <summary>Gira a lista para começar no ponto mais próximo da âncora (linha de chegada).</summary>
    private static List<TrackPoint> RotateToNearest(List<TrackPoint> points, TrackPoint anchor)
    {
        int count = points.Count;
        int bestIndex = 0;
        double bestRatio = 0;
        double bestDistance = double.MaxValue;

        for (var index = 0; index < count; index++)
        {
            TrackPoint from = points[index];
            TrackPoint to = points[(index + 1) % count];

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double lengthSquared = (dx * dx) + (dy * dy);
            double ratio = 0;

            if (lengthSquared > 1e-9)
            {
                ratio = Math.Clamp(
                    (((anchor.X - from.X) * dx) + ((anchor.Y - from.Y) * dy)) / lengthSquared,
                    0,
                    1);
            }

            var closest = new TrackPoint(from.X + (dx * ratio), from.Y + (dy * ratio));
            double distance = anchor.DistanceTo(closest);

            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = index;
            bestRatio = ratio;
        }

        var rotated = new List<TrackPoint>(count + 1);

        if (bestRatio > 0.001)
        {
            rotated.Add(TrackGeometry.Lerp(points[bestIndex], points[(bestIndex + 1) % count], bestRatio));
        }

        for (var offset = 1; offset <= count; offset++)
        {
            rotated.Add(points[(bestIndex + offset) % count]);
        }

        return rotated;
    }

    private static List<TrackPoint> ScaleToLength(List<TrackPoint> points, double targetLengthMeters)
    {
        double current = ClosedLength(points);
        if (current <= 0)
        {
            return points;
        }

        double factor = targetLengthMeters / current;
        var scaled = new List<TrackPoint>(points.Count);

        foreach (TrackPoint point in points)
        {
            scaled.Add(point.Scaled(factor));
        }

        return scaled;
    }

    private static List<TrackPoint> Resample(List<TrackPoint> points, double spacingMeters)
    {
        double total = ClosedLength(points);

        if (total <= 0)
        {
            throw new ArgumentException("O traçado não tem comprimento positivo.", nameof(points));
        }

        int sampleCount = Math.Max(12, (int)Math.Round(total / spacingMeters));
        double step = total / sampleCount;

        var resampled = new List<TrackPoint>(sampleCount);
        int segment = 0;
        double segmentStart = 0;
        double segmentLength = points[0].DistanceTo(points[1]);

        for (var sample = 0; sample < sampleCount; sample++)
        {
            double target = sample * step;

            while (segment < points.Count - 1 && segmentStart + segmentLength < target)
            {
                segmentStart += segmentLength;
                segment++;
                segmentLength = points[segment].DistanceTo(points[(segment + 1) % points.Count]);
            }

            double ratio = segmentLength <= 1e-9 ? 0 : Math.Clamp((target - segmentStart) / segmentLength, 0, 1);
            resampled.Add(TrackGeometry.Lerp(points[segment], points[(segment + 1) % points.Count], ratio));
        }

        return resampled;
    }

    private static double ClosedLength(List<TrackPoint> points)
    {
        double total = 0;

        for (var index = 0; index < points.Count; index++)
        {
            total += points[index].DistanceTo(points[(index + 1) % points.Count]);
        }

        return total;
    }
}
