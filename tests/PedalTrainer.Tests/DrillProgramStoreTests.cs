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
    /// <summary>
    /// Programa de freio exatamente como a versão 1 do aplicativo gravava (pisada lenta,
    /// soltura rápida), para conferir a migração dos arquivos já existentes.
    /// </summary>
    private const string LegacyBrakeJson = """
        {
          "name": "Freio — 20 / 50 / 80 e alvos sorteados",
          "description": "Pise no freio até o alvo, segure e solte devagar. Começa leve (20%), sobe para 50% e 80% e termina com alvos sorteados para treinar a memória muscular.",
          "version": 1,
          "restPercent": 4,
          "steps": [
            {
              "label": "Freio até 20% e soltar devagar",
              "pedal": "brake",
              "targetPercent": 20,
              "tolerancePercent": 4,
              "repetitions": 10,
              "segments": [
                { "kind": "ramp", "seconds": 1 },
                { "kind": "hold", "seconds": 0.8 },
                { "kind": "release", "seconds": 2.5, "toPercent": 0 }
              ]
            },
            {
              "label": "Freio até 50% e soltar devagar",
              "pedal": "brake",
              "targetPercent": 50,
              "tolerancePercent": 5,
              "repetitions": 10,
              "segments": [
                { "kind": "ramp", "seconds": 1.6 },
                { "kind": "hold", "seconds": 1 },
                { "kind": "release", "seconds": 3, "toPercent": 0 }
              ]
            },
            {
              "label": "Freio até 80% e soltar devagar",
              "pedal": "brake",
              "targetPercent": 80,
              "tolerancePercent": 6,
              "repetitions": 10,
              "segments": [
                { "kind": "ramp", "seconds": 2 },
                { "kind": "hold", "seconds": 1.2 },
                { "kind": "release", "seconds": 3.5, "toPercent": 0 }
              ]
            },
            {
              "label": "Freio em alvo sorteado (20 a 85%)",
              "pedal": "brake",
              "targetPercent": 50,
              "tolerancePercent": 7,
              "repetitions": 12,
              "randomTargetMinPercent": 20,
              "randomTargetMaxPercent": 85,
              "segments": [
                { "kind": "ramp", "seconds": 1.5 },
                { "kind": "hold", "seconds": 1 },
                { "kind": "release", "seconds": 3, "toPercent": 0 }
              ]
            }
          ]
        }
        """;

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
    public void LoadLibrary_ReplacesTheLegacyDefaultWithTheFastPressProgram()
    {
        string path = WriteLegacyBrake();
        var store = new DrillProgramStore(_folder);

        IReadOnlyList<DrillProgramEntry> entries = store.LoadLibrary();

        DrillProgramEntry brake = entries[0];
        Assert.True(brake.IsValid, brake.Error);
        Assert.Equal(DrillProgram.CurrentVersion, brake.Program!.Version);
        Assert.Equal("rápido", DrillInstructor.PressPace(brake.Program.Steps[0]));
        Assert.Equal("gradualmente", DrillInstructor.ReleasePace(brake.Program.Steps[0]));
        Assert.True(brake.Program.Steps[0].Segments[0].Seconds <= 0.5);

        // O arquivo em disco também foi regravado, não só a lista em memória.
        Assert.True(DrillProgramFile.TryLoad(path, out DrillProgram? reloaded, out string error), error);
        Assert.True(reloaded!.Steps[0].Segments[0].Seconds <= 0.5);
    }

    [Fact]
    public void LoadLibrary_KeepsLegacyFileThatTheDriverEdited()
    {
        string path = WriteLegacyBrake();

        Assert.True(DrillProgramFile.TryLoad(path, out DrillProgram? edited, out string error), error);
        edited!.Steps[0].Repetitions = 3;
        File.WriteAllText(path, DrillProgramFile.ToJson(edited));

        var store = new DrillProgramStore(_folder);
        IReadOnlyList<DrillProgramEntry> entries = store.LoadLibrary();

        DrillProgramEntry brake = entries[0];
        Assert.True(brake.IsValid, brake.Error);
        Assert.Equal(3, brake.Program!.Steps[0].Repetitions);
        Assert.Equal(1.0, brake.Program.Steps[0].Segments[0].Seconds, 3);
    }

    /// <summary>Grava o programa de freio da versão 1 na pasta do teste.</summary>
    private string WriteLegacyBrake()
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, DrillProgramCatalog.BrakeFileName);
        File.WriteAllText(path, LegacyBrakeJson);
        return path;
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
