using System;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Perfil sintético usado quando não há hardware nem referência carregada.
/// Um laço de 12 s com duas zonas de frenagem, suficiente para exercitar o app.
/// </summary>
internal static class SyntheticPedalWaveform
{
    private const double LoopSeconds = 12.0;

    private static readonly (double Time, double Value)[] BrakeKeyframes =
    {
        (0.00, 0),
        (3.00, 0),
        (3.28, 92),
        (3.60, 88),
        (4.40, 22),
        (4.75, 0),
        (8.00, 0),
        (8.20, 58),
        (8.60, 12),
        (9.00, 0),
        (12.00, 0)
    };

    private static readonly (double Time, double Value)[] ThrottleKeyframes =
    {
        (0.00, 100),
        (3.00, 65),
        (3.30, 0),
        (4.75, 0),
        (6.60, 100),
        (8.00, 78),
        (8.25, 0),
        (9.00, 0),
        (10.60, 100),
        (12.00, 100)
    };

    public static double BrakeAt(double timeSeconds) => Sample(BrakeKeyframes, Wrap(timeSeconds));

    public static double ThrottleAt(double timeSeconds) => Sample(ThrottleKeyframes, Wrap(timeSeconds));

    private static double Wrap(double timeSeconds)
    {
        double wrapped = timeSeconds % LoopSeconds;
        return wrapped < 0 ? wrapped + LoopSeconds : wrapped;
    }

    private static double Sample((double Time, double Value)[] keyframes, double timeSeconds)
    {
        for (int i = 1; i < keyframes.Length; i++)
        {
            if (timeSeconds > keyframes[i].Time)
            {
                continue;
            }

            (double Time, double Value) previous = keyframes[i - 1];
            (double Time, double Value) current = keyframes[i];
            double span = current.Time - previous.Time;
            if (span <= 0)
            {
                return current.Value;
            }

            double ratio = (timeSeconds - previous.Time) / span;
            return previous.Value + ((current.Value - previous.Value) * Math.Clamp(ratio, 0, 1));
        }

        return keyframes[^1].Value;
    }
}
