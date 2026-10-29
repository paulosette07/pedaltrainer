using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PedalTrainer.Core.Services;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Biblioteca de programas de treino em disco: garante os programas padrão na primeira
/// execução e recarrega tudo o que estiver na pasta de treinos.
/// </summary>
public sealed class DrillProgramStore
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public DrillProgramStore(string? directory = null)
    {
        Directory = string.IsNullOrWhiteSpace(directory) ? AppPaths.DrillsDirectory : directory;
    }

    /// <summary>Pasta onde ficam os arquivos de treino.</summary>
    public string Directory { get; }

    /// <summary>Carrega a biblioteca, criando os programas padrão quando a pasta está vazia.</summary>
    public IReadOnlyList<DrillProgramEntry> LoadLibrary()
    {
        EnsureDefaultFiles(overwrite: false, out _);

        var entries = new List<DrillProgramEntry>();
        string[] files;

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            files = System.IO.Directory.GetFiles(Directory, "*" + DrillProgramFile.Extension);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return BuiltInEntries();
        }

        foreach (string file in files)
        {
            if (DrillProgramFile.TryLoad(file, out DrillProgram? program, out string error))
            {
                entries.Add(new DrillProgramEntry
                {
                    FilePath = file,
                    Program = program,
                    Error = string.Empty
                });
            }
            else
            {
                entries.Add(new DrillProgramEntry
                {
                    FilePath = file,
                    Program = null,
                    Error = error
                });
            }
        }

        if (entries.Count == 0)
        {
            return BuiltInEntries();
        }

        OrderByDefaults(entries);
        return entries;
    }

    /// <summary>
    /// Deixa os programas padrão no topo, na ordem em que são gerados, e o restante
    /// em ordem alfabética — o treino de freio aparece primeiro para quem abre a tela.
    /// </summary>
    private static void OrderByDefaults(List<DrillProgramEntry> entries)
    {
        IReadOnlyList<(string FileName, DrillProgram Program)> defaults = DrillProgramCatalog.All();
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < defaults.Count; index++)
        {
            order[defaults[index].FileName] = index;
        }

        entries.Sort((left, right) =>
        {
            int leftRank = RankOf(left.FilePath, order);
            int rightRank = RankOf(right.FilePath, order);

            if (leftRank != rightRank)
            {
                return leftRank.CompareTo(rightRank);
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        });
    }

    private static int RankOf(string filePath, IReadOnlyDictionary<string, int> order) =>
        order.TryGetValue(Path.GetFileName(filePath), out int rank) ? rank : int.MaxValue;

    /// <summary>
    /// Grava os programas padrão na pasta. Com <paramref name="overwrite"/> falso, respeita
    /// arquivos já existentes (o piloto pode ter editado os percentuais).
    /// </summary>
    public bool EnsureDefaultFiles(bool overwrite, out string error)
    {
        error = string.Empty;

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"Não foi possível criar a pasta de treinos: {ex.Message}";
            return false;
        }

        var ok = true;

        foreach ((string fileName, DrillProgram program) in DrillProgramCatalog.All())
        {
            string path = Path.Combine(Directory, fileName);

            if (!overwrite && File.Exists(path))
            {
                continue;
            }

            if (!DrillProgramFile.Save(program, path, out string saveError))
            {
                error = saveError;
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>Caminho sugerido para um programa dentro da pasta de treinos.</summary>
    public string SuggestPath(DrillProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        string baseName = Slug(program.Name);
        return Path.Combine(Directory, baseName + DrillProgramFile.Extension);
    }

    /// <summary>Programas padrão mantidos em memória, quando o disco não está disponível.</summary>
    public static IReadOnlyList<DrillProgramEntry> BuiltInEntries()
    {
        var entries = new List<DrillProgramEntry>();

        foreach ((string fileName, DrillProgram program) in DrillProgramCatalog.All())
        {
            entries.Add(new DrillProgramEntry
            {
                FilePath = Path.Combine(AppPaths.DrillsDirectory, fileName),
                Program = program,
                Error = string.Empty
            });
        }

        return entries;
    }

    /// <summary>
    /// Converte o nome do programa em um nome de arquivo seguro: minúsculas, sem acentos
    /// e com hífens no lugar dos separadores.
    /// </summary>
    public static string Slug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "programa";
        }

        string decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            char normalized = char.ToLowerInvariant(character);

            if (normalized is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(normalized);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.Length == 0 ? "programa" : builder.ToString();
    }

    /// <summary>Compara dois caminhos ignorando maiúsculas no Windows.</summary>
    public static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);
}
