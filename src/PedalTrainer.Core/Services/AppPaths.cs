using System;
using System.IO;

namespace PedalTrainer.Core.Services;

/// <summary>Locais onde o aplicativo grava preferências, calibração e amostras.</summary>
public static class AppPaths
{
    /// <summary>Pasta raiz do usuário (por padrão %APPDATA%\PedalTrainer).</summary>
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PedalTrainer");

    public static string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public static string CalibrationFile => Path.Combine(RootDirectory, "calibration.json");

    public static string SamplesDirectory => Path.Combine(RootDirectory, "samples");

    /// <summary>Pasta com os arquivos de treino (programas de exercícios em JSON).</summary>
    public static string DrillsDirectory => Path.Combine(RootDirectory, "drills");

    public static string DemoLapFile => Path.Combine(SamplesDirectory, "demo-lap.csv");

    /// <summary>Caminho de uma volta gravada do iRacing, com carimbo de data e hora.</summary>
    public static string RecordedLapFile(DateTime timestamp) =>
        Path.Combine(SamplesDirectory, string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"volta-{timestamp:yyyyMMdd-HHmmss}.csv"));

    /// <summary>Cria as pastas necessárias. Não lança em caso de falha de permissão.</summary>
    public static void EnsureCreated()
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(SamplesDirectory);
            Directory.CreateDirectory(DrillsDirectory);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
