using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Buffer circular dos últimos segundos de pedal, consumido pelo gráfico.
/// Escrita no motor de treino, leitura na thread de UI — protegido por lock curto.
/// </summary>
public sealed class TraceRingBuffer
{
    private readonly object _gate = new();
    private readonly TraceSample[] _samples;
    private int _nextIndex;
    private int _count;

    public TraceRingBuffer(double historySeconds = 30, double rateHz = 200)
    {
        double capacity = Math.Max(64, historySeconds * Math.Max(1, rateHz));
        _samples = new TraceSample[(int)Math.Min(capacity, 200_000)];
    }

    public double LatestTimeSeconds
    {
        get
        {
            lock (_gate)
            {
                return _count == 0 ? 0 : _samples[PreviousIndex()].TimeSeconds;
            }
        }
    }

    public void Add(in TraceSample sample)
    {
        lock (_gate)
        {
            _samples[_nextIndex] = sample;
            _nextIndex = (_nextIndex + 1) % _samples.Length;
            if (_count < _samples.Length)
            {
                _count++;
            }
        }
    }

    /// <summary>
    /// Copia para <paramref name="destination"/> as amostras com tempo maior ou igual a
    /// <paramref name="fromTimeSeconds"/>, em ordem cronológica. Devolve a quantidade copiada.
    /// </summary>
    public int CopySince(Span<TraceSample> destination, double fromTimeSeconds)
    {
        if (destination.Length == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            int available = _count;
            int startBack = 0;

            while (startBack < available)
            {
                int index = WrapIndex(PreviousIndex() - startBack);
                if (_samples[index].TimeSeconds < fromTimeSeconds)
                {
                    break;
                }

                startBack++;
            }

            int copyCount = Math.Min(startBack, destination.Length);
            if (copyCount == 0)
            {
                return 0;
            }

            // A cópia começa na amostra mais antiga da janela pedida e termina na mais nova,
            // para que o gráfico receba os pontos em ordem cronológica.
            for (int i = 0; i < copyCount; i++)
            {
                int index = WrapIndex(PreviousIndex() - (copyCount - 1 - i));
                destination[i] = _samples[index];
            }

            return copyCount;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _nextIndex = 0;
            _count = 0;
        }
    }

    private int PreviousIndex() => _nextIndex == 0 ? _samples.Length - 1 : _nextIndex - 1;

    private int WrapIndex(int index)
    {
        int modulo = index % _samples.Length;
        return modulo < 0 ? modulo + _samples.Length : modulo;
    }
}
