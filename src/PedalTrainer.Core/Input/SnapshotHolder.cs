using System;
using System.Diagnostics;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Guarda o último instantâneo bruto e mede a taxa de aquisição.
/// O lock é sempre não contendido na prática (escrita a 500 Hz, leitura a 200 Hz).
/// </summary>
internal sealed class SnapshotHolder
{
    private readonly object _gate = new();
    private readonly Stopwatch _rateClock = Stopwatch.StartNew();
    private RawAxesSnapshot _snapshot = RawAxesSnapshot.Empty;
    private bool _hasValue;
    private long _rateWindowStartTicks;
    private int _rateSamples;
    private double _rateHz;

    public double RateHz
    {
        get
        {
            lock (_gate)
            {
                return _rateHz;
            }
        }
    }

    public void Publish(in RawAxesSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
            _hasValue = true;
            _rateSamples++;

            long now = _rateClock.ElapsedTicks;
            if (_rateWindowStartTicks == 0)
            {
                _rateWindowStartTicks = now;
                return;
            }

            double elapsedSeconds = (now - _rateWindowStartTicks) / (double)Stopwatch.Frequency;
            if (elapsedSeconds >= 0.5)
            {
                _rateHz = _rateSamples / elapsedSeconds;
                _rateSamples = 0;
                _rateWindowStartTicks = now;
            }
        }
    }

    public bool TryRead(out RawAxesSnapshot snapshot)
    {
        lock (_gate)
        {
            snapshot = _snapshot;
            return _hasValue;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _snapshot = RawAxesSnapshot.Empty;
            _hasValue = false;
            _rateSamples = 0;
            _rateWindowStartTicks = 0;
            _rateHz = 0;
        }
    }
}
