namespace PedalTrainer.Core.Models;

/// <summary>Ponto do gráfico em tempo real: instante relativo e pressão dos pedais.</summary>
public readonly record struct TraceSample(
    double TimeSeconds,
    double BrakePercent,
    double ThrottlePercent);
