namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Formato em disco de um traçado (JSON). <see cref="Points"/> é uma lista achatada
/// [x0, y0, x1, y1, ...] em metros; <see cref="Corners"/> guarda as curvas nomeadas.
/// </summary>
internal sealed class TrackFileDocument
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? CountryCode { get; set; }

    public double LengthMeters { get; set; }

    public double[]? Points { get; set; }

    public TrackFileCorner[]? Corners { get; set; }
}

/// <summary>Curva nomeada dentro do arquivo de traçado.</summary>
internal sealed class TrackFileCorner
{
    public string? Name { get; set; }

    public double Fraction { get; set; }
}
