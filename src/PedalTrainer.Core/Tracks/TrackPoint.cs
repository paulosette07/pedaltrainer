using System;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Ponto do traçado em metros: X aponta para leste e Y para norte, com a linha de
/// chegada na origem (a interface inverte o eixo Y para desenhar na tela).
/// </summary>
public readonly record struct TrackPoint(double X, double Y)
{
    public static TrackPoint Origin { get; } = new(0, 0);

    public double DistanceTo(TrackPoint other) => Math.Sqrt(SquaredDistanceTo(other));

    public double SquaredDistanceTo(TrackPoint other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return (dx * dx) + (dy * dy);
    }

    public TrackPoint Offset(double dx, double dy) => new(X + dx, Y + dy);

    public TrackPoint Scaled(double factor) => new(X * factor, Y * factor);

    public TrackPoint Mirrored() => new(-X, -Y);

    /// <summary>Ângulo da direção que sai deste ponto para o outro (radianos).</summary>
    public double HeadingTo(TrackPoint other) => Math.Atan2(other.Y - Y, other.X - X);

    public override string ToString() => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"({X:0.0}, {Y:0.0})");
}
