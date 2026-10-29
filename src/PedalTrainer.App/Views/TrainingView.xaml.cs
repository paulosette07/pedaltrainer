using System;
using System.Windows;
using System.Windows.Controls;
using PedalTrainer.App.ViewModels;

namespace PedalTrainer.App.Views;

/// <summary>
/// Tela de treino. Os controles desenhados à mão (gráfico, medidor e barras) são
/// atualizados aqui a cada quadro, porque expõem propriedades simples e não bindings.
/// </summary>
public partial class TrainingView : UserControl
{
    private TrainingViewModel? _viewModel;

    public TrainingView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PlotDataChanged -= OnPlotDataChanged;
        }

        _viewModel = e.NewValue as TrainingViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PlotDataChanged += OnPlotDataChanged;
        }

        OnPlotDataChanged(this, EventArgs.Empty);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PlotDataChanged -= OnPlotDataChanged;
        _viewModel = null;
    }

    private void OnPlotDataChanged(object? sender, EventArgs e)
    {
        TrainingViewModel? viewModel = _viewModel;
        if (viewModel is null)
        {
            return;
        }

        Gauge.Value = viewModel.ScoreValue;
        Gauge.IsActive = viewModel.IsChallengeActive;
        Gauge.Grade = viewModel.GradeText;

        BrakeBar.Value = viewModel.BrakePercent;
        BrakeBar.ReferenceValue = viewModel.ReferenceBrakePercent;
        ThrottleBar.Value = viewModel.ThrottlePercent;
        ThrottleBar.ReferenceValue = viewModel.ReferenceThrottlePercent;

        ChallengeProgressBar.Value = viewModel.ChallengeProgress;

        Plot.ShowThrottle = viewModel.ShowThrottle;
        Plot.SetGhost(viewModel.Ghost, viewModel.TolerancePercent);

        if (viewModel.Ghost is null)
        {
            Plot.SetTimeRange(-4, 0.2);
        }

        Plot.SetLive(viewModel.PlotTrace, viewModel.CursorSeconds);
    }
}
