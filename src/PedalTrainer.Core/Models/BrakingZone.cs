namespace PedalTrainer.Core.Models;

/// <summary>
/// Trecho contínuo de frenagem da volta de referência — a unidade de treino
/// que o jogador precisa reproduzir.
/// </summary>
public sealed record BrakingZone(
    int Index,
    int StartSampleIndex,
    int EndSampleIndex,
    double StartTimeSeconds,
    double EndTimeSeconds,
    double StartDistanceMeters,
    double EndDistanceMeters,
    double PeakBrakePercent,
    string Name)
{
    /// <summary>Duração da zona de frenagem em segundos.</summary>
    public double DurationSeconds => EndTimeSeconds - StartTimeSeconds;

    /// <summary>Extensão da zona em metros (0 quando a referência não tem distância).</summary>
    public double DistanceLengthMeters => EndDistanceMeters - StartDistanceMeters;

    public bool HasDistance => EndDistanceMeters > StartDistanceMeters;

    internal BrakingZone WithName(string name) => this with { Name = name };
}
