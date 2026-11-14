using System.Windows;
using System.Windows.Controls;
using PedalTrainer.App.Services;
using PedalTrainer.App.ViewModels;

namespace PedalTrainer.App.Views;

/// <summary>
/// Tela de telemetria. A lógica fica no <c>TelemetryViewModel</c>; aqui só o traço da volta é
/// empurrado para o desenho, que roda a cada quadro (o binding não serve para um controle
/// desenhado à mão). O ligador é o mesmo usado pela janela destacada.
/// </summary>
public partial class TelemetryView : UserControl
{
    private readonly TracePlotBinder _traceBinder;

    public TelemetryView()
    {
        InitializeComponent();

        _traceBinder = new TracePlotBinder(LapTrace);

        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => _traceBinder.Bind(null);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        _traceBinder.Bind(DataContext as TelemetryViewModel);
}
