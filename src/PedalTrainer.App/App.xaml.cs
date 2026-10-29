using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PedalTrainer.App.ViewModels;
using PedalTrainer.Core.Drills;
using PedalTrainer.Core.Services;
using PedalTrainer.Core.Training;

namespace PedalTrainer.App;

/// <summary>
/// Ponto de entrada: monta o contêiner de dependências, cria a janela principal
/// e garante que o motor de telemetria seja encerrado ao sair.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        services.AddSingleton<TrainingEngine>();
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<CalibrationStore>();
        services.AddSingleton<DrillProgramStore>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        AppPaths.EnsureCreated();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _services = null;
        base.OnExit(e);
    }
}
