using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Gera a polilinha da curva-alvo de uma repetição, para o gráfico do exercício.
/// Os segmentos são retos, então basta amostrar dentro de cada um.
/// </summary>
public static class DrillTargetCurveBuilder
{
    /// <summary>Amostras por segmento na curva desenhada.</summary>
    public const int SamplesPerSegment = 8;

    /// <summary>Constrói a curva do perfil informado.</summary>
    public static IReadOnlyList<DrillCurvePoint> Build(DrillProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var points = new List<DrillCurvePoint>((profile.SegmentCount * SamplesPerSegment) + 2)
        {
            new(0, profile.TargetAt(0))
        };

        for (var index = 0; index < profile.SegmentCount; index++)
        {
            double start = profile.SegmentStartSeconds(index);
            double seconds = profile.SegmentSeconds(index);

            for (var sample = 1; sample <= SamplesPerSegment; sample++)
            {
                double time = start + (seconds * sample / SamplesPerSegment);
                points.Add(new DrillCurvePoint(time, profile.TargetAt(time)));
            }
        }

        return points;
    }
}
