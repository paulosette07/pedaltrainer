using System;
using System.IO;
using System.Text.Json;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Services;

/// <summary>Persistência da calibração dos pedais em JSON.</summary>
public sealed class CalibrationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public CalibrationStore(string? filePath = null)
    {
        FilePath = filePath ?? AppPaths.CalibrationFile;
    }

    public string FilePath { get; }

    /// <summary>Carrega a calibração salva ou devolve um perfil vazio.</summary>
    public PedalCalibration Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new PedalCalibration();
            }

            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PedalCalibration();
            }

            PedalCalibration? calibration = JsonSerializer.Deserialize<PedalCalibration>(json, SerializerOptions);
            return calibration ?? new PedalCalibration();
        }
        catch (JsonException)
        {
            return new PedalCalibration();
        }
        catch (IOException)
        {
            return new PedalCalibration();
        }
        catch (UnauthorizedAccessException)
        {
            return new PedalCalibration();
        }
    }

    /// <summary>Grava a calibração. Devolve falso quando não foi possível escrever.</summary>
    public bool Save(PedalCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);

        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(calibration, SerializerOptions));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
