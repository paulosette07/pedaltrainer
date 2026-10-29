using System.IO;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Reference;

namespace PedalTrainer.Tests;

/// <summary>
/// Os arquivos de referência vêm de fontes variadas (planilhas, exports do iRacing,
/// JSON de terceiros), então o carregador precisa ser tolerante com formato.
/// </summary>
public sealed class ReferenceLapLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "pedaltrainer-tests-" + Guid.NewGuid().ToString("N"));

    private readonly ReferenceLapFileLoader _loader = new();

    public ReferenceLapLoaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void LoadCsv_WithHeader_ReadsEveryRow()
    {
        string path = WriteFile("volta.csv",
            "Time,Brake,Throttle\n" +
            "0.0,0,100\n" +
            "0.5,50,40\n" +
            "1.0,100,0\n");

        ReferenceLap lap = _loader.Load(path);

        Assert.Equal(3, lap.Samples.Count);
        Assert.Equal(0.5, lap.Samples[1].TimeSeconds, 3);
        Assert.Equal(50, lap.Samples[1].BrakePercent, 3);
        Assert.Equal(100, lap.Samples[2].BrakePercent, 3);
        Assert.Equal(1.0, lap.DurationSeconds, 3);
    }

    [Fact]
    public void LoadCsv_AcceptsSemicolonAndDecimalComma()
    {
        string path = WriteFile("volta-locale.csv",
            "Tempo;Freio;Acelerador\n" +
            "0,0;0;100\n" +
            "0,5;70;30\n" +
            "1,0;0;100\n");

        ReferenceLap lap = _loader.Load(path);

        Assert.Equal(3, lap.Samples.Count);
        Assert.Equal(70, lap.Samples[1].BrakePercent, 1);
        Assert.Equal(0.5, lap.Samples[1].TimeSeconds, 3);
    }

    [Fact]
    public void LoadCsv_ScalesFractionalValuesToPercent()
    {
        string path = WriteFile("fracao.csv",
            "Time,Brake,Throttle\n" +
            "0,0,1\n" +
            "1,0.8,0.2\n");

        ReferenceLap lap = _loader.Load(path);

        Assert.Equal(80, lap.Samples[1].BrakePercent, 1);
        Assert.Equal(20, lap.Samples[1].ThrottlePercent, 1);
    }

    [Fact]
    public void LoadCsv_ThrowsOnEmptyFile()
    {
        string path = WriteFile("vazio.csv", string.Empty);

        Assert.Throws<ReferenceLapException>(() => _loader.Load(path));
    }

    [Fact]
    public void LoadJson_AcceptsShortKeys()
    {
        string path = WriteFile("volta.json",
            "[\n" +
            "  { \"t\": 0, \"b\": 0, \"th\": 100 },\n" +
            "  { \"t\": 1, \"b\": 85, \"th\": 15 },\n" +
            "  { \"t\": 2, \"b\": 0, \"th\": 100 }\n" +
            "]\n");

        ReferenceLap lap = _loader.Load(path);

        Assert.Equal(3, lap.Samples.Count);
        Assert.Equal(85, lap.Samples[1].BrakePercent, 1);
    }

    [Fact]
    public void TryLoad_ReturnsFalseForUnsupportedExtension()
    {
        string path = WriteFile("telemetria.ibt", "binario");

        bool ok = _loader.TryLoad(path, out ReferenceLap? lap, out string? error);

        Assert.False(ok);
        Assert.Null(lap);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryLoad_ReturnsFalseForMissingFile()
    {
        bool ok = _loader.TryLoad(Path.Combine(_directory, "nao-existe.csv"), out _, out string? error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void DemoLap_SurvivesCsvRoundTrip()
    {
        ReferenceLap original = DemoReferenceLapFactory.Create();
        string csv = DemoReferenceLapFactory.ToCsv(original);
        string path = WriteFile("demo.csv", csv);

        ReferenceLap reloaded = _loader.Load(path);

        IReadOnlyList<BrakingZone> originalZones = BrakingZoneExtractor.Extract(original);
        IReadOnlyList<BrakingZone> reloadedZones = BrakingZoneExtractor.Extract(reloaded);

        Assert.Equal(original.Samples.Count, reloaded.Samples.Count);
        Assert.Equal(originalZones.Count, reloadedZones.Count);
        Assert.True(reloaded.HasDistanceData);
    }

    [Fact]
    public void DialogFilter_ListsSupportedFormats()
    {
        string filter = _loader.BuildFileDialogFilter();

        Assert.Contains("*.csv", filter);
        Assert.Contains("*.json", filter);
    }

    private string WriteFile(string name, string content)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
