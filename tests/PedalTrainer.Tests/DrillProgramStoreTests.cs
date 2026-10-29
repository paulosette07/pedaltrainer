using System;
using System.IO;
using PedalTrainer.Core.Drills;

namespace PedalTrainer.Tests;

/// <summary>
/// Biblioteca em disco: grava os programas padrão na primeira execução, ordena a lista
/// e ignora arquivos inválidos sem derrubar a tela.
/// </summary>
public sealed class DrillProgramStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "pedaltrainer-drills-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadLibrary_WritesDefaultsAndPutsTheBrakeDrillFirst()
    {
        var store = new DrillProgramStore(_folder);

        IReadOnlyList<DrillProgramEntry> entries = store.LoadLibrary();

        Assert.Equal(2, entries.Count);
        Assert.Equal(DrillProgramCatalog.BrakeFileName, Path.GetFileName(entries[0].FilePath));
        Assert.Equal(DrillProgramCatalog.ThrottleFileName, Path.GetFileName(entries[1].FilePath));
        Assert.All(entries, entry => Assert.True(entry.IsValid));
        Assert.True(File.Exists(Path.Combine(_folder, DrillProgramCatalog.BrakeFileName)));
    }

    [Fact]
    public void LoadLibrary_KeepsEditedFileUntouched()
    {
        var store = new DrillProgramStore(_folder);
        store.EnsureDefaultFiles(overwrite: false, out string firstError);

        Assert.Equal(string.Empty, firstError);

        DrillProgram edited = DrillProgramCatalog.CreateBrakeProgressive();
        edited.Name = "Meu freio";
        edited.Steps[0].Repetitions = 3;

        string path = Path.Combine(_folder, DrillProgramCatalog.BrakeFileName);
        Assert.True(DrillProgramFile.Save(edited, path, out string saveError), saveError);

        IReadOnlyList<DrillProgramEntry> entries = store.LoadLibrary();

        DrillProgramEntry brake = entries[0];
        Assert.Equal("Meu freio", brake.DisplayName);
        Assert.Equal(3, brake.Program!.Steps[0].Repetitions);
    }

    [Fact]
    public void LoadLibrary_ReportsBrokenFilesInsteadOfThrowing()
    {
        var store = new DrillProgramStore(_folder);
        store.EnsureDefaultFiles(overwrite: false, out _);

        File.WriteAllText(Path.Combine(_folder, "quebrado.drill.json"), "{ nada }");

        IReadOnlyList<DrillProgramEntry> entries = store.LoadLibrary();

        Assert.Equal(3, entries.Count);
        DrillProgramEntry broken = entries[^1];
        Assert.False(broken.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(broken.Error));
        Assert.Contains("quebrado", broken.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SuggestPath_UsesTheDrillsFolder()
    {
        var store = new DrillProgramStore(_folder);
        DrillProgram program = DrillProgramCatalog.CreateThrottleProgressive();

        string path = store.SuggestPath(program);

        Assert.Equal(_folder, Path.GetDirectoryName(path)!);
        Assert.EndsWith(DrillProgramFile.Extension, path, StringComparison.Ordinal);
        Assert.Contains("acelerador", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
