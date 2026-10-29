using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Histórico da sessão: média, melhor nota, recorde por zona e sequência de acertos.
/// Uma execução é considerada "acerto" a partir de <see cref="StreakThreshold"/>.
/// </summary>
public sealed class SessionStats
{
    /// <summary>Nota mínima para manter a sequência de acertos.</summary>
    public const double StreakThreshold = 85.0;

    private readonly Dictionary<int, double> _bestByZone = new();
    private double _scoreSum;
    private int _completed;

    public int CompletedChallenges => _completed;

    public double AverageScore => _completed == 0 ? 0 : _scoreSum / _completed;

    public double BestScore { get; private set; }

    public int Streak { get; private set; }

    public int BestStreak { get; private set; }

    public CornerResult? LastResult { get; private set; }

    /// <summary>Melhor nota já obtida em uma zona específica.</summary>
    public double BestForZone(int zoneIndex) =>
        _bestByZone.TryGetValue(zoneIndex, out double best) ? best : 0;

    public void Register(CornerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _completed++;
        _scoreSum += result.Score;
        LastResult = result;

        if (result.Score > BestScore)
        {
            BestScore = result.Score;
        }

        if (result.Score >= StreakThreshold)
        {
            Streak++;
            BestStreak = Math.Max(BestStreak, Streak);
        }
        else
        {
            Streak = 0;
        }

        if (result.Score > BestForZone(result.ZoneIndex))
        {
            _bestByZone[result.ZoneIndex] = result.Score;
        }
    }

    public void Reset()
    {
        _bestByZone.Clear();
        _scoreSum = 0;
        _completed = 0;
        BestScore = 0;
        Streak = 0;
        BestStreak = 0;
        LastResult = null;
    }
}
