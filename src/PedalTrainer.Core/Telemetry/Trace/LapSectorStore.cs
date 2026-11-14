using System;
using System.IO;
using System.Text.Json;
using PedalTrainer.Core.Services;

namespace PedalTrainer.Core.Telemetry.Trace;

/// <summary>
/// Leva os melhores trechos para o disco, um arquivo por pista: fechar o programa não apaga
/// o melhor setor que o piloto já fez. Nunca lança — um arquivo corrompido apenas recomeça
/// o livro.
/// </summary>
public static class LapSectorStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    /// <summary>Carrega os melhores trechos da pista (livro vazio quando ainda não há nada).</summary>
    public static LapSectorBook Load(string trackId) => LoadFrom(AppPaths.BestSectorsFile(trackId));

    /// <summary>Carrega os melhores trechos de um arquivo qualquer.</summary>
    public static LapSectorBook LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new LapSectorBook();
            }

            string json = File.ReadAllText(path);
            LapSectorFile? file = JsonSerializer.Deserialize<LapSectorFile>(json, Options);

            if (file?.Sectors is null || file.Sectors.Length == 0)
            {
                return new LapSectorBook();
            }

            return LapSectorBook.FromSaved(file.Sectors, file.BestLapSeconds);
        }
        catch (Exception exception) when (IsFileProblem(exception) || exception is JsonException)
        {
            return new LapSectorBook();
        }
    }

    /// <summary>Grava os melhores trechos da pista. Falha de disco é silenciosa.</summary>
    public static bool Save(string trackId, LapSectorBook book) =>
        SaveTo(AppPaths.BestSectorsFile(trackId), book);

    /// <summary>Grava os melhores trechos num arquivo qualquer.</summary>
    public static bool SaveTo(string path, LapSectorBook book)
    {
        ArgumentNullException.ThrowIfNull(book);

        try
        {
            string? folder = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var file = new LapSectorFile
            {
                TrackId = Path.GetFileNameWithoutExtension(path),
                SectorCount = book.SectorCount,
                BestLapSeconds = book.BestLapSeconds,
                Sectors = new double[book.SectorCount]
            };

            for (int sector = 0; sector < book.SectorCount; sector++)
            {
                file.Sectors[sector] = book.BestSectorSeconds[sector];
            }

            File.WriteAllText(path, JsonSerializer.Serialize(file, Options));
            return true;
        }
        catch (Exception exception) when (IsFileProblem(exception) || exception is JsonException)
        {
            return false;
        }
    }

    private static bool IsFileProblem(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or NotSupportedException;

    /// <summary>Formato do arquivo em disco.</summary>
    private sealed class LapSectorFile
    {
        public string TrackId { get; set; } = string.Empty;

        public int SectorCount { get; set; }

        public double BestLapSeconds { get; set; }

        public double[] Sectors { get; set; } = Array.Empty<double>();
    }
}
