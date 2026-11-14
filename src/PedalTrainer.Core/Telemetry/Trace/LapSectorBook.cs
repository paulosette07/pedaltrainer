using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Telemetry.Trace;

/// <summary>
/// Guarda o melhor tempo de cada setor da pista entre todas as voltas já feitas. Um setor que
/// o piloto acertou uma vez continua sendo o melhor dali em diante, mesmo numa volta mais lenta:
/// é isso que permite montar a "volta dos melhores trechos".
/// </summary>
public sealed class LapSectorBook
{
    /// <summary>Setores por volta. Vinte dá trechos de ~215 m em Interlagos.</summary>
    public const int DefaultSectorCount = 20;

    /// <summary>Setor com menos tempo que isso é tratado como não percorrido.</summary>
    public const double MinimumSectorSeconds = 0.05;

    private readonly double[] _bestSectorSeconds;
    private double _bestLapSeconds;

    public LapSectorBook(int sectorCount = DefaultSectorCount)
    {
        if (sectorCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorCount));
        }

        SectorCount = sectorCount;
        _bestSectorSeconds = new double[sectorCount];
    }

    private LapSectorBook(double[] bestSectorSeconds, double bestLapSeconds)
    {
        SectorCount = bestSectorSeconds.Length;
        _bestSectorSeconds = bestSectorSeconds;
        _bestLapSeconds = bestLapSeconds;
    }

    /// <summary>
    /// Restaura um livro salvo em disco. Valores inválidos (negativos, NaN, infinito) viram
    /// setor desconhecido, para um arquivo antigo ou editado à mão não quebrar a tela.
    /// </summary>
    public static LapSectorBook FromSaved(IReadOnlyList<double>? sectors, double bestLapSeconds)
    {
        if (sectors is null || sectors.Count == 0)
        {
            return new LapSectorBook();
        }

        var restored = new double[sectors.Count];

        for (int sector = 0; sector < sectors.Count; sector++)
        {
            double seconds = sectors[sector];
            restored[sector] = IsUsable(seconds) ? seconds : 0;
        }

        return new LapSectorBook(restored, IsUsable(bestLapSeconds) ? bestLapSeconds : 0);
    }

    private static bool IsUsable(double seconds) =>
        seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds);

    /// <summary>Quantos setores a volta tem.</summary>
    public int SectorCount { get; }

    /// <summary>Melhor volta completa do piloto na pista, em segundos (0 = nenhuma ainda).</summary>
    public double BestLapSeconds => _bestLapSeconds;

    /// <summary>Melhor tempo de cada setor; 0 quando o setor nunca foi percorrido.</summary>
    public IReadOnlyList<double> BestSectorSeconds => _bestSectorSeconds;

    /// <summary>Quantos setores já têm tempo conhecido.</summary>
    public int KnownSectorCount
    {
        get
        {
            int known = 0;

            foreach (double seconds in _bestSectorSeconds)
            {
                if (seconds > 0)
                {
                    known++;
                }
            }

            return known;
        }
    }

    /// <summary>
    /// Volta composta: soma dos melhores setores já feitos. Nula enquanto nenhum setor tiver
    /// tempo; <see cref="LapSectorOutcome.HasFullComposite"/> diz se ela cobre a volta inteira.
    /// </summary>
    public double? CompositeSeconds
    {
        get
        {
            double total = 0;
            int known = 0;

            foreach (double seconds in _bestSectorSeconds)
            {
                if (seconds > 0)
                {
                    total += seconds;
                    known++;
                }
            }

            return known == 0 ? null : total;
        }
    }

    /// <summary>
    /// Registra uma volta, atualiza os melhores setores e devolve o que ela melhorou.
    /// Volta sem cobertura da pista inteira deve ser filtrada antes de chamar isto.
    /// </summary>
    public LapSectorOutcome Add(TelemetryLapTrace lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        double[] sectorTimes = SectorTimes(lap, SectorCount);
        var beaten = new bool[SectorCount];
        int beatenCount = 0;

        for (int sector = 0; sector < SectorCount; sector++)
        {
            double seconds = sectorTimes[sector];

            if (seconds <= 0)
            {
                continue;
            }

            double best = _bestSectorSeconds[sector];

            if (best <= 0 || seconds < best)
            {
                _bestSectorSeconds[sector] = seconds;
                beaten[sector] = true;
                beatenCount++;
            }
        }

        bool fastest = lap.DurationSeconds > 0 && (_bestLapSeconds <= 0 || lap.DurationSeconds < _bestLapSeconds);

        if (fastest)
        {
            _bestLapSeconds = lap.DurationSeconds;
        }

        return new LapSectorOutcome(
            LapNumber: lap.LapNumber,
            LapSeconds: lap.DurationSeconds,
            CompositeSeconds: CompositeSeconds,
            BeatenSectors: beatenCount,
            KnownSectors: KnownSectorCount,
            SectorCount: SectorCount,
            IsFastestLap: fastest,
            BeatenMask: beaten);
    }

    /// <summary>Zera o livro (troca de pista).</summary>
    public void Reset()
    {
        Array.Clear(_bestSectorSeconds);
        _bestLapSeconds = 0;
    }

    /// <summary>
    /// Divide o tempo da volta entre os setores, posicionando cada intervalo na pista (pelo
    /// meio do trecho) em vez de usar o relógio: dois traçados com taxas diferentes dão a
    /// mesma divisão.
    /// </summary>
    public static double[] SectorTimes(TelemetryLapTrace lap, int sectorCount)
    {
        ArgumentNullException.ThrowIfNull(lap);

        if (sectorCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorCount));
        }

        var times = new double[sectorCount];
        IReadOnlyList<TelemetryTraceSample> samples = lap.Samples;

        for (int index = 1; index < samples.Count; index++)
        {
            TelemetryTraceSample previous = samples[index - 1];
            TelemetryTraceSample current = samples[index];

            double seconds = current.TimeSeconds - previous.TimeSeconds;

            if (seconds <= 0)
            {
                continue;
            }

            double middle = (previous.LapPercent + current.LapPercent) / 2;
            times[IndexOf(middle, sectorCount)] += seconds;
        }

        for (int sector = 0; sector < sectorCount; sector++)
        {
            if (times[sector] < MinimumSectorSeconds)
            {
                times[sector] = 0;
            }
        }

        return times;
    }

    /// <summary>Setor onde uma posição da pista (0 a 1) cai.</summary>
    public static int IndexOf(double lapPercent, int sectorCount)
    {
        if (sectorCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorCount));
        }

        int sector = (int)Math.Floor(Math.Clamp(lapPercent, 0, 1) * sectorCount);
        return Math.Clamp(sector, 0, sectorCount - 1);
    }

    /// <summary>Setor onde uma posição da pista cai, neste livro.</summary>
    public int IndexOf(double lapPercent) => IndexOf(lapPercent, SectorCount);
}
