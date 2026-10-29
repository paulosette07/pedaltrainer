using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Dá uma nota a cada controlador para escolher a caixa de pedais em vez da base do volante.
/// Placas como a Simsonn aparecem com nomes genéricos ("Arduino Micro"), então a decisão
/// olha principalmente os nomes dos eixos: "Freio" e "Acelerador" valem muito mais que
/// "Rotação Z".
/// </summary>
public static class PedalDeviceScorer
{
    private static readonly string[] PedalKeywords =
    {
        "pedal",
        "load cell",
        "simsonn",
        "simjack",
        "heusinkveld",
        "aseteck",
        "moza",
        "simagic",
        "fanatec",
        "arduino",
        "hid"
    };

    private static readonly string[] PedalAxisKeywords =
    {
        "freio",
        "acelerador",
        "embraiagem",
        "embreagem",
        "brake",
        "throttle",
        "clutch",
        "pedal"
    };

    private static readonly string[] WheelKeywords =
    {
        "wheel",
        "base",
        "volante",
        "steering",
        "direct drive",
        "force feedback"
    };

    /// <summary>Nota do controlador: quanto maior, mais provável ser a caixa de pedais.</summary>
    public static int Score(PedalDeviceDescriptor device)
    {
        ArgumentNullException.ThrowIfNull(device);

        string haystack = $"{device.Name} {device.ProductName}".ToLowerInvariant();
        int score = 0;

        foreach (string keyword in PedalKeywords)
        {
            if (haystack.Contains(keyword, StringComparison.Ordinal))
            {
                score += 40;
                break;
            }
        }

        foreach (string keyword in WheelKeywords)
        {
            if (haystack.Contains(keyword, StringComparison.Ordinal))
            {
                score -= 60;
                break;
            }
        }

        int namedPedalAxes = 0;
        foreach (string axisName in device.AxisNames)
        {
            string name = axisName.ToLowerInvariant();

            foreach (string keyword in PedalAxisKeywords)
            {
                if (name.Contains(keyword, StringComparison.Ordinal))
                {
                    score += 60;
                    namedPedalAxes++;
                    break;
                }
            }
        }

        if (namedPedalAxes >= 2)
        {
            score += 40;
        }

        if (device.AxisCount >= 2)
        {
            score += 10;
        }

        return score;
    }

    /// <summary>Escolhe o controlador com maior nota; empate fica com o de mais eixos.</summary>
    public static PedalDeviceDescriptor? PickBest(System.Collections.Generic.IReadOnlyList<PedalDeviceDescriptor> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        PedalDeviceDescriptor? best = null;
        int bestScore = int.MinValue;

        foreach (PedalDeviceDescriptor device in devices)
        {
            int score = Score(device);

            if (score > bestScore || (score == bestScore && best is not null && device.AxisCount > best.AxisCount))
            {
                best = device;
                bestScore = score;
            }
        }

        return best;
    }
}
