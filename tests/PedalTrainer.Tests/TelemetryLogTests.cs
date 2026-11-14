using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry;
using PedalTrainer.Core.Telemetry.Recording;

namespace PedalTrainer.Tests;

/// <summary>
/// Gravação de telemetria em arquivo: nome, cabeçalho com a configuração do jogo e as
/// colunas realmente publicadas.
/// </summary>
public sealed class TelemetryLogTests : IDisposable
{
    private readonly string _folder;

    public TelemetryLogTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pedaltrainer-telemetria-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void FileNameUsesSimulatorSlugAndTimestamp()
    {
        var time = new DateTime(2026, 12, 11, 9, 47, 3, DateTimeKind.Local);

        string name = TelemetryLogFile.BuildFileName(TelemetryProfileCatalog.AssettoCorsaCompetizione, time);

        Assert.Equal("telemetria-acc-20261211-094703.csv", name);
    }

    [Fact]
    public void UniquePathDoesNotOverwriteExistingFile()
    {
        TelemetryProfile profile = TelemetryProfileCatalog.IRacing;
        var time = new DateTime(2026, 12, 11, 9, 47, 3, DateTimeKind.Local);

        string first = TelemetryLogFile.BuildUniquePath(_folder, profile, time);
        File.WriteAllText(first, "x");

        string second = TelemetryLogFile.BuildUniquePath(_folder, profile, time);

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.False(File.Exists(second));
    }

    [Fact]
    public void HeaderCarriesProfileConfiguration()
    {
        TelemetryProfile profile = TelemetryProfileCatalog.Automobilista2;

        IReadOnlyList<string> header = TelemetryLogFormatter.BuildHeader(
            profile,
            "Automobilista 2",
            new DateTime(2026, 12, 11, 9, 47, 3, DateTimeKind.Local));

        string text = string.Join("\n", header);

        Assert.Contains("$pcars2$", text);
        Assert.Contains("simulador: Automobilista 2", text);
        Assert.Contains("taxa: 60 Hz", text);
        Assert.Contains("Shared Memory", text);
        Assert.EndsWith("tempo_gravacao_s,velocidade_kmh,freio_pct,acelerador_pct,posicao_volta_pct,volta", text);
    }

    [Fact]
    public void RowSkipsSessionTimeWhenGameDoesNotPublishIt()
    {
        TelemetryLogSample sample = new(2.5, 120.5, 55.25, 10, 0.4, 3, 12.5);

        string row = TelemetryLogFormatter.BuildRow(TelemetryProfileCatalog.AssettoCorsaCompetizione, sample);

        Assert.Equal("2.5,120.5,55.25,10,40,3", row);
    }

    [Fact]
    public void RowIncludesSessionTimeForIRacing()
    {
        TelemetryLogSample sample = new(1, 200, 0, 100, 0.5, 7, 98.75);

        string row = TelemetryLogFormatter.BuildRow(TelemetryProfileCatalog.IRacing, sample);

        Assert.Equal("1,200,0,100,50,7,98.75", row);
    }

    [Fact]
    public void WriterCreatesFileWithHeaderAndSamples()
    {
        string path = Path.Combine(_folder, "telemetria-teste.csv");

        using (TelemetryLogWriter writer = TelemetryLogWriter.Open(
            path,
            TelemetryProfileCatalog.IRacing,
            "iRacing"))
        {
            for (var index = 0; index < 20; index++)
            {
                writer.Append(new TelemetryLogSample(
                    ElapsedSeconds: index * 0.016,
                    SpeedKmh: 100 + index,
                    BrakePercent: index,
                    ThrottlePercent: 100 - index,
                    LapDistancePercent: index / 100.0,
                    LapNumber: 1,
                    SessionTimeSeconds: 10 + index));
            }
        }

        string[] lines = File.ReadAllLines(path);
        var dataLines = new List<string>();

        foreach (string line in lines)
        {
            if (!line.StartsWith('#'))
            {
                dataLines.Add(line);
            }
        }

        // Cabeçalho de colunas + 20 amostras.
        Assert.Equal(21, dataLines.Count);
        Assert.Equal(
            "tempo_gravacao_s,velocidade_kmh,freio_pct,acelerador_pct,posicao_volta_pct,volta,tempo_sessao_s",
            dataLines[0]);
        Assert.StartsWith("0,100,0,100,0,1,10", dataLines[1]);
        Assert.StartsWith("0.304,119,19,81,19,1,29", dataLines[20]);
    }

    [Fact]
    public void RecorderObservesRateAndStopsCleanly()
    {
        using var recorder = new TelemetryRecorder();
        var car = new CarTelemetry(true, 90, 20, 80, 0.25, 30, 1, 30);

        Assert.False(recorder.IsRecording);
        Assert.Equal(0, recorder.Stop());

        // Sem gravação em andamento, observar não lança e não cria arquivo.
        recorder.Observe(car);
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void SampleFromCarKeepsValues()
    {
        var car = new CarTelemetry(true, 132.5, 44, 66, 0.75, 12.5, 4, 12.5);

        TelemetryLogSample sample = TelemetryLogSample.From(car, 3.25);

        Assert.Equal(3.25, sample.ElapsedSeconds);
        Assert.Equal(132.5, sample.SpeedKmh);
        Assert.Equal(44, sample.BrakePercent);
        Assert.Equal(66, sample.ThrottlePercent);
        Assert.Equal(0.75, sample.LapDistancePercent);
        Assert.Equal(4, sample.LapNumber);
        Assert.Equal(12.5, sample.SessionTimeSeconds);
    }
}
