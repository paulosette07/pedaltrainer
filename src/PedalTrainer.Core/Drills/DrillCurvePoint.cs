namespace PedalTrainer.Core.Drills;

/// <summary>Ponto da curva-alvo desenhada no gráfico do exercício.</summary>
public readonly record struct DrillCurvePoint(double TimeSeconds, double Percent);
