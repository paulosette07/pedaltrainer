using System;
using System.Collections.Generic;
using System.Globalization;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Pista completa: nome, local, comprimento oficial, traçado fechado e curvas nomeadas.
/// É a peça que liga o mapa da tela, a volta de referência do sistema e a seleção do piloto.
/// </summary>
public sealed class TrackDefinition
{
    public TrackDefinition(
        string id,
        string name,
        string city,
        string country,
        string countryCode,
        double lengthMeters,
        TrackPath path,
        IReadOnlyList<TrackCorner> corners,
        TrackSource source = TrackSource.System)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(path);

        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        City = city ?? string.Empty;
        Country = country ?? string.Empty;
        CountryCode = countryCode ?? string.Empty;
        LengthMeters = lengthMeters > 0 ? lengthMeters : path.LengthMeters;
        Path = path;
        Corners = corners ?? Array.Empty<TrackCorner>();
        Source = source;
    }

    /// <summary>Identificador estável, usado nas preferências e nos arquivos.</summary>
    public string Id { get; }

    public string Name { get; }

    public string City { get; }

    public string Country { get; }

    /// <summary>Sigla do país (BR, IT, US...), usada nos agrupamentos da lista.</summary>
    public string CountryCode { get; }

    /// <summary>Comprimento oficial da volta em metros.</summary>
    public double LengthMeters { get; }

    /// <summary>Traçado fechado, com a linha de chegada na fração zero.</summary>
    public TrackPath Path { get; }

    /// <summary>Curvas nomeadas, em ordem de volta.</summary>
    public IReadOnlyList<TrackCorner> Corners { get; }

    public TrackSource Source { get; }

    /// <summary>"Interlagos · São Paulo, Brasil".</summary>
    public string LocationText => string.IsNullOrEmpty(City)
        ? Country
        : $"{City}, {Country}";

    public string CityAndCountry => string.IsNullOrEmpty(City) ? Country : $"{City} · {Country}";

    /// <summary>Comprimento no formato brasileiro ("4 309 m").</summary>
    public string LengthText => string.Create(
        CultureInfo.CurrentCulture,
        $"{LengthMeters / 1000.0:0.000} km");

    /// <summary>Nome curto para combos e listas: o autódromo costuma ter nome longo.</summary>
    public string ShortName => Name
        .Replace("Autódromo ", string.Empty, StringComparison.Ordinal)
        .Replace("Autodromo ", string.Empty, StringComparison.Ordinal)
        .Replace("International ", string.Empty, StringComparison.Ordinal)
        .Replace("Circuit ", string.Empty, StringComparison.Ordinal);

    public override string ToString() => $"{Name} ({City})";

    /// <summary>Curva mais próxima da fração informada (usada para nomear as zonas).</summary>
    public TrackCorner? CornerNear(double fraction)
    {
        TrackCorner? best = null;
        double bestDelta = double.MaxValue;

        foreach (TrackCorner corner in Corners)
        {
            if (!corner.Matches(fraction))
            {
                continue;
            }

            double delta = fraction - corner.Fraction;
            delta -= Math.Round(delta);

            if (Math.Abs(delta) < bestDelta)
            {
                bestDelta = Math.Abs(delta);
                best = corner;
            }
        }

        return best;
    }
}
