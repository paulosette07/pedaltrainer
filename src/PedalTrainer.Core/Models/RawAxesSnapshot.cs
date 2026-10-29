using System;

namespace PedalTrainer.Core.Models;

/// <summary>
/// Instantâneo dos eixos brutos reportados pelo DirectInput.
/// Estrutura de tamanho fixo (12 eixos) para não gerar alocação no laço de 500 Hz.
/// A ordem dos eixos é a mesma da enumeração do dispositivo, então o índice aqui
/// corresponde ao nome mostrado na tela de calibração.
/// </summary>
public readonly struct RawAxesSnapshot
{
    /// <summary>Quantidade máxima de eixos suportada por instantâneo.</summary>
    public const int MaxAxes = 12;

    private readonly int _a0;
    private readonly int _a1;
    private readonly int _a2;
    private readonly int _a3;
    private readonly int _a4;
    private readonly int _a5;
    private readonly int _a6;
    private readonly int _a7;
    private readonly int _a8;
    private readonly int _a9;
    private readonly int _a10;
    private readonly int _a11;

    public RawAxesSnapshot(ReadOnlySpan<int> axes, long timestampTicks)
    {
        Count = Math.Min(axes.Length, MaxAxes);
        TimestampTicks = timestampTicks;
        _a0 = Count > 0 ? axes[0] : 0;
        _a1 = Count > 1 ? axes[1] : 0;
        _a2 = Count > 2 ? axes[2] : 0;
        _a3 = Count > 3 ? axes[3] : 0;
        _a4 = Count > 4 ? axes[4] : 0;
        _a5 = Count > 5 ? axes[5] : 0;
        _a6 = Count > 6 ? axes[6] : 0;
        _a7 = Count > 7 ? axes[7] : 0;
        _a8 = Count > 8 ? axes[8] : 0;
        _a9 = Count > 9 ? axes[9] : 0;
        _a10 = Count > 10 ? axes[10] : 0;
        _a11 = Count > 11 ? axes[11] : 0;
    }

    /// <summary>Quantidade de eixos preenchidos.</summary>
    public int Count { get; }

    /// <summary>Momento da leitura, em ticks de <see cref="System.Diagnostics.Stopwatch"/>.</summary>
    public long TimestampTicks { get; }

    public int Get(int index) => index switch
    {
        0 => _a0,
        1 => _a1,
        2 => _a2,
        3 => _a3,
        4 => _a4,
        5 => _a5,
        6 => _a6,
        7 => _a7,
        8 => _a8,
        9 => _a9,
        10 => _a10,
        11 => _a11,
        _ => 0
    };

    public static RawAxesSnapshot Empty => default;
}
