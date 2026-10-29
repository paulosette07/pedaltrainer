using System.IO;
using PedalTrainer.Core.Input.Pedals;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Services;

namespace PedalTrainer.Tests;

/// <summary>
/// O modelo de pedal escolhido precisa sobreviver ao fechamento do app: sem isso o
/// piloto reajustaria a curva toda vez que abrisse o programa.
/// </summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "pedaltrainer-settings-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void SaveAndLoad_KeepsTheSelectedPedalModel()
    {
        var store = new SettingsStore(FilePath);
        AppSettings settings = store.Load();

        settings.PedalModelId = "fanatec-clubsport-v3";
        settings.LastDeviceGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.True(store.Save(settings));

        AppSettings reloaded = new SettingsStore(FilePath).Load();

        Assert.Equal("fanatec-clubsport-v3", reloaded.PedalModelId);
        Assert.Equal(settings.LastDeviceGuid, reloaded.LastDeviceGuid);
        Assert.Equal("fanatec-clubsport-v3", PedalModelCatalog.Find(reloaded.PedalModelId).Id);
    }

    [Fact]
    public void Load_WithoutAFile_FallsBackToTheCatchAllModel()
    {
        AppSettings settings = new SettingsStore(FilePath).Load();

        Assert.True(PedalModelCatalog.Find(settings.PedalModelId).IsCatchAll);
        Assert.Equal(string.Empty, settings.PedalModelId);
    }

    [Fact]
    public void Load_WithAnUnknownModelId_DoesNotThrow()
    {
        var store = new SettingsStore(FilePath);
        AppSettings settings = store.Load();

        settings.PedalModelId = "modelo-que-nao-existe";
        store.Save(settings);

        AppSettings reloaded = store.Load();

        Assert.Equal("modelo-que-nao-existe", reloaded.PedalModelId);
        Assert.True(PedalModelCatalog.Find(reloaded.PedalModelId).IsCatchAll);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
