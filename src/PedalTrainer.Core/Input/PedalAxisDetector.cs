using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Descobre qual eixo pertence a qual pedal observando o movimento durante o assistente
/// de calibração. A Simsonn aparece como controlador genérico, então a ordem dos eixos
/// varia entre firmwares e adaptadores — detectar por movimento é o caminho confiável.
/// </summary>
public static class PedalAxisDetector
{
    /// <summary>Faixa mínima (em unidades brutas) para considerar que o eixo se moveu.</summary>
    public const int MinimumMovementSpan = 400;

    /// <summary>
    /// Retorna o índice do eixo que mais se moveu nas amostras, ignorando
    /// <paramref name="excludedAxis"/> (normalmente o eixo do outro pedal).
    /// </summary>
    public static int DetectDominantAxis(IReadOnlyList<RawAxesSnapshot> frames, int excludedAxis = -1)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count == 0)
        {
            return -1;
        }

        int axisCount = 0;
        foreach (RawAxesSnapshot frame in frames)
        {
            axisCount = Math.Max(axisCount, Math.Min(frame.Count, RawAxesSnapshot.MaxAxes));
        }

        int bestAxis = -1;
        int bestSpan = 0;

        for (int axis = 0; axis < axisCount; axis++)
        {
            if (axis == excludedAxis)
            {
                continue;
            }

            int span = MeasureSpan(frames, axis);
            if (span > bestSpan)
            {
                bestSpan = span;
                bestAxis = axis;
            }
        }

        return bestSpan >= MinimumMovementSpan ? bestAxis : -1;
    }

    /// <summary>Amplitude observada (máximo - mínimo) de um eixo nas amostras.</summary>
    public static int MeasureSpan(IReadOnlyList<RawAxesSnapshot> frames, int axis)
    {
        ArgumentNullException.ThrowIfNull(frames);

        int min = int.MaxValue;
        int max = int.MinValue;

        foreach (RawAxesSnapshot frame in frames)
        {
            if (axis < 0 || axis >= frame.Count)
            {
                continue;
            }

            int value = frame.Get(axis);
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        return min > max ? 0 : max - min;
    }

    /// <summary>Detecta o eixo do freio (o primeiro a ser movimentado no assistente).</summary>
    public static int DetectBrakeAxis(IReadOnlyList<RawAxesSnapshot> brakeFrames) =>
        DetectDominantAxis(brakeFrames);

    /// <summary>Detecta o eixo do acelerador ignorando o eixo já atribuído ao freio.</summary>
    public static int DetectThrottleAxis(IReadOnlyList<RawAxesSnapshot> throttleFrames, int brakeAxis) =>
        DetectDominantAxis(throttleFrames, brakeAxis);
}
