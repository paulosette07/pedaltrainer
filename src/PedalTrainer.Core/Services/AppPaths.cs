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

    /// <summary>Raiz dos dados de pista: mapas importados e voltas de referência do piloto.</summary>
    public static string TracksDirectory => Path.Combine(RootDirectory, "tracks");

    /// <summary>Traçados importados pelo piloto (um JSON por pista).</summary>
    public static string ImportedTracksDirectory => Path.Combine(TracksDirectory, "maps");

    /// <summary>Pasta com as voltas de referência que o piloto gravou/importou para a pista.</summary>
    public static string TrackReferencesDirectory(string trackId) =>
        Path.Combine(TracksDirectory, "references", SafeTrackFolder(trackId));

    public static string DemoLapFile => Path.Combine(SamplesDirectory, "demo-lap.csv");

    /// <summary>Nome de pasta seguro a partir do identificador da pista.</summary>
    public static string SafeTrackFolder(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            return "sem-pista";
        }

        var builder = new System.Text.StringBuilder(trackId.Length);

        foreach (char character in trackId)
        {
            bool valid = char.IsLetterOrDigit(character) || character is '-' or '_';

            if (valid)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.Length == 0 ? "sem-pista" : builder.ToString();
    }

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
            Directory.CreateDirectory(TracksDirectory);
            Directory.CreateDirectory(ImportedTracksDirectory);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
