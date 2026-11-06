namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Curva nomeada do traçado. A fração indica onde a curva começa na volta (0 = chegada),
/// o que permite nomear as zonas de frenagem e rotular o mapa.
/// </summary>
public sealed record TrackCorner(string Name, double Fraction)
{
    /// <summary>Distância, em fração de volta, para considerar que a zona é "esta curva".</summary>
    public const double MatchTolerance = 0.02;

    public bool Matches(double fraction)
    {
        double delta = fraction - Fraction;
        delta -= System.Math.Round(delta);
        return System.Math.Abs(delta) <= MatchTolerance;
    }
}
