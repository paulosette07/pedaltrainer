using System;
using System.Collections.Generic;
using System.IO;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>Carrega voltas de referência em CSV/TSV (Time, Brake, Throttle[, Distance, Speed]).</summary>
public sealed class CsvReferenceLapLoader : IReferenceLapLoader
{
    private static readonly string[] SupportedExtensions = { "csv", "tsv", "txt" };

    public string FormatName => "CSV / TSV (Time, Brake, Throttle)";

    public IReadOnlyList<string> Extensions => SupportedExtensions;

    public bool CanLoad(string path) => MatchesExtension(path);

    public ReferenceLap Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ReferenceLapException("Informe o caminho do arquivo de referência.");
        }

        if (!File.Exists(path))
        {
            throw new ReferenceLapException($"Arquivo não encontrado: {path}");
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (IOException ex)
        {
            throw new ReferenceLapException($"Não foi possível ler \"{Path.GetFileName(path)}\": {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ReferenceLapException($"Sem permissão de leitura em \"{path}\".", ex);
        }

        string name = Path.GetFileNameWithoutExtension(path);
        return CsvReferenceParser.Parse(lines, name);
    }

    internal static bool MatchesExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return Array.IndexOf(SupportedExtensions, extension) >= 0;
    }
}
