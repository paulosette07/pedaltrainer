using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Constrói a polilinha do "fantasma" (piloto de referência) para uma zona de frenagem,
/// com o tempo relativo ao início da zona — o mesmo eixo usado pelo gráfico ao vivo.
/// </summary>
public static class GhostCurveBuilder
{
    /// <summary>Passo padrão entre pontos da curva fantasma, em segundos.</summary>
    public const double DefaultStepSeconds = 0.02;

    /// <summary>Quanto antes do início da zona a curva começa (dá contexto visual).</summary>
    public const double LeadInSeconds = 0.8;

    /// <summary>Quanto depois do fim da zona a curva termina.</summary>
    public const double TailSeconds = 0.8;

    public static IReadOnlyList<TraceSample> Build(
        ReferenceLap lap,
        BrakingZone zone,
        double stepSeconds = DefaultStepSeconds)
    {
        ArgumentNullException.ThrowIfNull(lap);
        ArgumentNullException.ThrowIfNull(zone);

        stepSeconds = Math.Clamp(stepSeconds, 0.005, 0.2);
        var points = new List<TraceSample>();
        double endSeconds = zone.DurationSeconds + TailSeconds;

        for (double relative = -LeadInSeconds; relative <= endSeconds; relative += stepSeconds)
        {
            ReferenceSample sample = lap.SampleAtTime(zone.StartTimeSeconds + relative);
            points.Add(new TraceSample(
                TimeSeconds: relative,
                BrakePercent: sample.BrakePercent,
                ThrottlePercent: sample.ThrottlePercent));
        }

        return points;
    }

    /// <summary>Faixa de tempo coberta pela curva fantasma, usada para escalar o gráfico.</summary>
    public static (double Min, double Max) GetTimeRange(BrakingZone zone) =>
        (-LeadInSeconds, zone.DurationSeconds + TailSeconds);
}
