using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry.Trace;
using Xunit;

namespace PedalTrainer.Tests;

/// <summary>
/// Traço da volta, divisão em setores e livro de melhores trechos: o piloto precisa que o
/// melhor setor continue valendo depois de uma volta ruim e depois de fechar o programa.
/// </summary>
public sealed class LapTraceTests
{
    public LapTraceTests()
    {
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        CultureInfo.CurrentUICulture = new CultureInfo("pt-BR");
    }

    [Fact]
    public void RecorderFechaVoltaQuandoOJogoNumeraAVolta()
    {
        var recorder = new LapTraceRecorder();

        // Uma volta inteira: posição de 0 a 0,99 em 100 quadros de 0,1 s.
        for (int step = 0; step < 100; step++)
        {
            Assert.Null(recorder.Observe(Car(lapNumber: 1, percent: step / 100.0, seconds: step * 0.1), step * 0.1));
        }

        Assert.True(recorder.IsRunning);
        Assert.Equal(1, recorder.CurrentLapNumber);

        // A volta 2 começa: a 1 fecha.
        TelemetryLapTrace? lap = recorder.Observe(Car(lapNumber: 2, percent: 0.01, seconds: 100 * 0.1), 100 * 0.1);

        Assert.NotNull(lap);
        Assert.Equal(1, lap!.LapNumber);
        Assert.True(lap.CoversLap());
        Assert.True(lap.Samples.Count > 50);
        Assert.Equal(2, recorder.CurrentLapNumber);
    }

    [Fact]
    public void RecorderFechaVoltaPelaQuedaDePosicaoQuandoOJogoNaoNumera()
    {
        var recorder = new LapTraceRecorder();

        for (int step = 0; step < 100; step++)
        {
            recorder.Observe(Car(lapNumber: 0, percent: step / 100.0, seconds: step * 0.1), step * 0.1);
        }

        TelemetryLapTrace? lap = recorder.Observe(Car(lapNumber: 0, percent: 0.005, seconds: 10.2), 10.2);

        Assert.NotNull(lap);
        Assert.True(lap!.CoversLap());
    }

    [Fact]
    public void RecorderDescartaVoltaParcial()
    {
        var recorder = new LapTraceRecorder();

        // Só metade da pista antes de cruzar a linha.
        for (int step = 0; step < 50; step++)
        {
            recorder.Observe(Car(lapNumber: 3, percent: step / 100.0, seconds: step * 0.1), step * 0.1);
        }

        Assert.Null(recorder.Observe(Car(lapNumber: 4, percent: 0.01, seconds: 5.1), 5.1));
        Assert.Equal(4, recorder.CurrentLapNumber);
    }

    [Fact]
    public void RecorderZeraQuandoOJogoDesconecta()
    {
        var recorder = new LapTraceRecorder();

        recorder.Observe(Car(lapNumber: 1, percent: 0.3, seconds: 1), 1);
        Assert.True(recorder.IsRunning);

        Assert.Null(recorder.Observe(CarTelemetry.Disconnected(2), 2));
        Assert.False(recorder.IsRunning);
    }

    [Fact]
    public void SetoresDividemOTempoPelaPosicaoNaPista()
    {
        TelemetryLapTrace lap = BuildLap(1, 1.0, 2.0, 3.0, 4.0, 5.0);

        double[] times = LapSectorBook.SectorTimes(lap, sectorCount: 5);

        Assert.Equal(5, times.Length);
        Assert.Equal(1.0, times[0], 3);
        Assert.Equal(2.0, times[1], 3);
        Assert.Equal(3.0, times[2], 3);
        Assert.Equal(4.0, times[3], 3);
        Assert.Equal(5.0, times[4], 3);
        Assert.Equal(lap.DurationSeconds, Sum(times), 3);
    }

    [Fact]
    public void LivroGuardaOMelhorSetorMesmoNumaVoltaMaisLenta()
    {
        var book = new LapSectorBook(sectorCount: 5);

        book.Add(BuildLap(1, 5.0, 5.0, 5.0, 5.0, 5.0));

        // Volta 2 é mais lenta no total, mas acerta o setor 3.
        LapSectorOutcome outcome = book.Add(BuildLap(2, 6.0, 6.0, 3.0, 6.0, 6.0));

        Assert.Equal(1, outcome.BeatenSectors);
        Assert.True(outcome.BeatenMask[2]);
        Assert.False(outcome.BeatenMask[0]);
        Assert.Equal(3.0, book.BestSectorSeconds[2], 3);
        Assert.Equal(5.0, book.BestSectorSeconds[0], 3);

        // A volta composta usa o melhor de cada setor: 5+5+3+5+5.
        Assert.Equal(23.0, book.CompositeSeconds!.Value, 3);
        Assert.Equal(5, book.KnownSectorCount);
        Assert.False(outcome.IsFastestLap);
    }

    [Fact]
    public void LivroMarcaAVoltaMaisRapida()
    {
        var book = new LapSectorBook(sectorCount: 5);

        LapSectorOutcome first = book.Add(BuildLap(1, 5, 5, 5, 5, 5));
        LapSectorOutcome second = book.Add(BuildLap(2, 4, 4, 4, 4, 4));

        Assert.True(first.IsFastestLap);
        Assert.True(second.IsFastestLap);
        Assert.Equal(20.0, book.BestLapSeconds, 3);
        Assert.Equal(5, second.BeatenSectors);
    }

    [Fact]
    public void LivroVazioNaoTemComposta()
    {
        var book = new LapSectorBook();

        Assert.Null(book.CompositeSeconds);
        Assert.Equal(0, book.KnownSectorCount);
        Assert.Equal(LapSectorBook.DefaultSectorCount, book.SectorCount);
    }

    [Fact]
    public void LivroRestauradoDeValoresInvalidosFicaVazio()
    {
        LapSectorBook book = LapSectorBook.FromSaved(new double[] { double.NaN, -1, 0 }, double.NaN);

        Assert.Null(book.CompositeSeconds);
        Assert.Equal(0, book.BestLapSeconds);
        Assert.Equal(3, book.SectorCount);
    }

    [Fact]
    public void StoreGravaELeOsMelhoresTrechos()
    {
        string path = Path.Combine(Path.GetTempPath(), "pedaltrainer-setores-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var book = new LapSectorBook(sectorCount: 4);
            book.Add(BuildLap(7, 1.5, 2.5, 3.5, 4.5));

            Assert.True(LapSectorStore.SaveTo(path, book));

            LapSectorBook loaded = LapSectorStore.LoadFrom(path);

            Assert.Equal(4, loaded.SectorCount);
            Assert.Equal(1.5, loaded.BestSectorSeconds[0], 3);
            Assert.Equal(4.5, loaded.BestSectorSeconds[3], 3);
            Assert.Equal(book.CompositeSeconds!.Value, loaded.CompositeSeconds!.Value, 3);
            Assert.Equal(12.0, loaded.BestLapSeconds, 3);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void StoreDeArquivoInexistenteDevolveLivroVazio()
    {
        string path = Path.Combine(Path.GetTempPath(), "pedaltrainer-inexistente-" + Guid.NewGuid().ToString("N") + ".json");

        LapSectorBook book = LapSectorStore.LoadFrom(path);

        Assert.Equal(LapSectorBook.DefaultSectorCount, book.SectorCount);
        Assert.Null(book.CompositeSeconds);
    }

    [Fact]
    public void StoreDeArquivoCorrompidoDevolveLivroVazio()
    {
        string path = Path.Combine(Path.GetTempPath(), "pedaltrainer-corrompido-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            File.WriteAllText(path, "{ isso não é json }");

            LapSectorBook book = LapSectorStore.LoadFrom(path);

            Assert.Null(book.CompositeSeconds);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void PreviaDaReferenciaUsaADistanciaDaPista()
    {
        var lap = ReferenceLap.Create(
            "ideal",
            new[]
            {
                new ReferenceSample(0.0, 0, 100, 0, 120),
                new ReferenceSample(1.0, 90, 0, 500, 80),
                new ReferenceSample(2.0, 0, 100, 1000, 120)
            },
            trackLengthMeters: 1000);

        IReadOnlyList<TelemetryTraceSample> trace = ReferenceTraceBuilder.Build(lap);

        Assert.Equal(3, trace.Count);
        Assert.Equal(0.0, trace[0].LapPercent, 3);
        Assert.Equal(0.5, trace[1].LapPercent, 3);
        Assert.Equal(1.0, trace[2].LapPercent, 3);
        Assert.Equal(90, trace[1].BrakePercent, 3);
    }

    [Fact]
    public void PreviaSemDistanciaUsaAOrdemDasAmostras()
    {
        var lap = ReferenceLap.Create(
            "sem-distancia",
            new[]
            {
                new ReferenceSample(0.0, 0, 100, 0, 120),
                new ReferenceSample(1.0, 50, 50, 0, 100),
                new ReferenceSample(2.0, 0, 100, 0, 120)
            });

        IReadOnlyList<TelemetryTraceSample> trace = ReferenceTraceBuilder.Build(lap);

        Assert.Equal(0.0, trace[0].LapPercent, 3);
        Assert.Equal(0.5, trace[1].LapPercent, 3);
        Assert.Equal(1.0, trace[2].LapPercent, 3);
    }

    private static double Sum(double[] values)
    {
        double total = 0;

        foreach (double value in values)
        {
            total += value;
        }

        return total;
    }

    private static CarTelemetry Car(int lapNumber, double percent, double seconds) =>
        new(
            IsConnected: true,
            SpeedKmh: 120,
            BrakePercent: 0,
            ThrottlePercent: 100,
            LapDistancePercent: percent,
            SessionTimeSeconds: seconds,
            LapNumber: lapNumber,
            TimestampSeconds: seconds);

    /// <summary>Volta sintética: cada setor leva exatamente o tempo pedido.</summary>
    private static TelemetryLapTrace BuildLap(int lapNumber, params double[] sectorSeconds)
    {
        int sectorCount = sectorSeconds.Length;
        double step = 1.0 / sectorCount;
        double percent = 0;
        double time = 0;

        var samples = new List<TelemetryTraceSample>
        {
            new(0, 0, 100, 120, 0)
        };

        for (int sector = 0; sector < sectorCount; sector++)
        {
            double half = sectorSeconds[sector] / 2;

            percent += step / 2;
            time += half;
            samples.Add(new TelemetryTraceSample(percent, 0, 100, 120, time));

            percent += step / 2;
            time += half;
            samples.Add(new TelemetryTraceSample(percent, 0, 100, 120, time));
        }

        return new TelemetryLapTrace(lapNumber, time, samples);
    }
}
