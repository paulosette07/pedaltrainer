using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Traçado fechado já reamostrado em passos iguais: sabe a posição, a direção e a fração
/// de volta de qualquer ponto. É a base do mapa da pista e da volta ideal.
/// </summary>
public sealed class TrackPath
{
    private readonly TrackPoint[] _points;
    private readonly double[] _cumulative;

    /// <param name="points">Volta fechada, sem repetir o primeiro ponto no fim.</param>
    public TrackPath(IReadOnlyList<TrackPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 3)
        {
            throw new ArgumentException("O traçado precisa de pelo menos três pontos.", nameof(points));
        }

        _points = new TrackPoint[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            _points[index] = points[index];
        }

        _cumulative = new double[_points.Length + 1];

        for (var index = 1; index <= _points.Length; index++)
        {
            TrackPoint from = _points[index - 1];
            TrackPoint to = _points[index % _points.Length];
            _cumulative[index] = _cumulative[index - 1] + from.DistanceTo(to);
        }
    }

    /// <summary>Pontos do traçado, em metros, com a linha de chegada no índice zero.</summary>
    public IReadOnlyList<TrackPoint> Points => _points;

    public int Count => _points.Length;

    /// <summary>Comprimento total da volta em metros.</summary>
    public double LengthMeters => _cumulative[^1];

    /// <summary>Distância média entre dois pontos consecutivos (metros).</summary>
    public double StepMeters => LengthMeters / Count;

    public TrackPoint this[int index] => _points[NormalizeIndex(index)];

    public double DistanceAt(int index) => _cumulative[Math.Clamp(index, 0, _points.Length)];

    public double FractionAt(int index) => LengthMeters <= 0 ? 0 : DistanceAt(index) / LengthMeters;

    /// <summary>Ponto do traçado na fração informada (0 = chegada, 0,5 = metade da volta).</summary>
    public TrackPoint PointAtFraction(double fraction)
    {
        double distance = WrapFraction(fraction) * LengthMeters;
        int index = SegmentIndexAtDistance(distance);

        double segmentStart = _cumulative[index];
        double segmentLength = _cumulative[index + 1] - segmentStart;
        double ratio = segmentLength <= 1e-9 ? 0 : (distance - segmentStart) / segmentLength;

        return TrackGeometry.Lerp(_points[index], _points[(index + 1) % _points.Length], ratio);
    }

    /// <summary>Direção da pista na fração informada, em radianos.</summary>
    public double HeadingAtFraction(double fraction)
    {
        const double ProbeFraction = 0.0025;
        TrackPoint behind = PointAtFraction(fraction - ProbeFraction);
        TrackPoint ahead = PointAtFraction(fraction + ProbeFraction);
        return behind.HeadingTo(ahead);
    }

    /// <summary>Ponto mais próximo do traçado e a distância até ele (usado ao ancorar pontos).</summary>
    public double NearestFraction(TrackPoint probe, out double distanceMeters)
    {
        double bestFraction = 0;
        double bestDistance = double.MaxValue;

        for (var index = 0; index < _points.Length; index++)
        {
            TrackPoint from = _points[index];
            TrackPoint to = _points[(index + 1) % _points.Length];

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double lengthSquared = (dx * dx) + (dy * dy);
            double ratio = 0;

            if (lengthSquared > 1e-9)
            {
                ratio = Math.Clamp(
                    (((probe.X - from.X) * dx) + ((probe.Y - from.Y) * dy)) / lengthSquared,
                    0,
                    1);
            }

            var closest = new TrackPoint(from.X + (dx * ratio), from.Y + (dy * ratio));
            double distance = probe.DistanceTo(closest);

            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            double segmentLength = _cumulative[index + 1] - _cumulative[index];
            bestFraction = (_cumulative[index] + (segmentLength * ratio)) / LengthMeters;
        }

        distanceMeters = bestDistance;
        return bestFraction;
    }

    /// <summary>Converte a fração em metros percorridos.</summary>
    public double DistanceAtFraction(double fraction) => WrapFraction(fraction) * LengthMeters;

    private int SegmentIndexAtDistance(double distance)
    {
        int low = 0;
        int high = _points.Length - 1;

        while (low < high)
        {
            int middle = (low + high + 1) / 2;

            if (_cumulative[middle] <= distance)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    private int NormalizeIndex(int index)
    {
        int wrapped = index % _points.Length;
        return wrapped < 0 ? wrapped + _points.Length : wrapped;
    }

    private static double WrapFraction(double fraction)
    {
        double wrapped = fraction % 1.0;
        return wrapped < 0 ? wrapped + 1.0 : wrapped;
    }
}
