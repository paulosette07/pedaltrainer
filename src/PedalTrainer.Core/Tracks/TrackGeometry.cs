using System;

namespace PedalTrainer.Core.Tracks;

/// <summary>Contas de geometria plana usadas pelo traçado, pelo mapa e pela volta ideal.</summary>
public static class TrackGeometry
{
    private const double TwoPi = Math.PI * 2;

    /// <summary>
    /// Raio do círculo que passa por três pontos (raio de curva instantâneo).
    /// Devolve infinito quando os pontos são colineares.
    /// </summary>
    public static double Circumradius(TrackPoint a, TrackPoint b, TrackPoint c)
    {
        double sideA = b.DistanceTo(c);
        double sideB = c.DistanceTo(a);
        double sideC = a.DistanceTo(b);

        double cross = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
        double area = Math.Abs(cross) * 0.5;

        if (area < 1e-6)
        {
            return double.PositiveInfinity;
        }

        return sideA * sideB * sideC / (4 * area);
    }

    /// <summary>Distância de um ponto ao segmento AB (usada para achar a linha de chegada).</summary>
    public static double DistanceToSegment(TrackPoint point, TrackPoint a, TrackPoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared < 1e-9)
        {
            return point.DistanceTo(a);
        }

        double projection = (((point.X - a.X) * dx) + ((point.Y - a.Y) * dy)) / lengthSquared;
        projection = Math.Clamp(projection, 0, 1);

        var closest = new TrackPoint(a.X + (dx * projection), a.Y + (dy * projection));
        return point.DistanceTo(closest);
    }

    /// <summary>Normaliza um ângulo para o intervalo (-π, π].</summary>
    public static double NormalizeAngle(double radians)
    {
        double value = radians % TwoPi;

        if (value > Math.PI)
        {
            value -= TwoPi;
        }
        else if (value <= -Math.PI)
        {
            value += TwoPi;
        }

        return value;
    }

    /// <summary>Interpolação linear entre dois pontos.</summary>
    public static TrackPoint Lerp(TrackPoint from, TrackPoint to, double ratio) => new(
        from.X + ((to.X - from.X) * ratio),
        from.Y + ((to.Y - from.Y) * ratio));
}
