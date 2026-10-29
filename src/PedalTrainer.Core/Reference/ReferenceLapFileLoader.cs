using System;
using System.Collections.Generic;
using System.IO;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>
/// Ponto único de entrada para carregar uma volta de referência,
/// escolhendo o carregador pela extensão do arquivo.
/// </summary>
public sealed class ReferenceLapFileLoader
{
    private readonly List<IReferenceLapLoader> _loaders;

    public ReferenceLapFileLoader()
        : this(new IReferenceLapLoader[]
        {
            new JsonReferenceLapLoader(),
            new CsvReferenceLapLoader()
        })
    {
    }

    public ReferenceLapFileLoader(IEnumerable<IReferenceLapLoader> loaders)
    {
        _loaders = new List<IReferenceLapLoader>(
            loaders ?? throw new ArgumentNullException(nameof(loaders)));
    }

    /// <summary>Formatos disponíveis, na ordem em que são testados.</summary>
    public IReadOnlyList<IReferenceLapLoader> Loaders => _loaders;

    /// <summary>Filtro pronto para o OpenFileDialog do WPF.</summary>
    public string BuildFileDialogFilter()
    {
        var parts = new List<string>();
        var allPatterns = new List<string>();

        foreach (IReferenceLapLoader loader in _loaders)
        {
            var patterns = new List<string>();
            foreach (string extension in loader.Extensions)
            {
                string pattern = $"*.{extension}";
                patterns.Add(pattern);
                if (!allPatterns.Contains(pattern))
                {
                    allPatterns.Add(pattern);
                }
            }

            parts.Add($"{loader.FormatName}|{string.Join(";", patterns)}");
        }

        parts.Insert(0, $"Telemetria de referência|{string.Join(";", allPatterns)}");
        parts.Add("Todos os arquivos|*.*");
        return string.Join("|", parts);
    }

    public ReferenceLap Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ReferenceLapException("Informe o caminho do arquivo de referência.");
        }

        foreach (IReferenceLapLoader loader in _loaders)
        {
            if (loader.CanLoad(path))
            {
                return loader.Load(path);
            }
        }

        throw new ReferenceLapException(
            $"Formato não suportado ({Path.GetExtension(path)}). Use CSV, TSV ou JSON.");
    }

    public bool TryLoad(string path, out ReferenceLap? lap, out string? error)
    {
        try
        {
            lap = Load(path);
            error = null;
            return true;
        }
        catch (ReferenceLapException ex)
        {
            lap = null;
            error = ex.Message;
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lap = null;
            error = ex.Message;
            return false;
        }
    }
}
