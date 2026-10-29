using System;
using System.Windows;
using System.Windows.Controls;
using PedalTrainer.App.ViewModels;
using PedalTrainer.Core.Drills;

namespace PedalTrainer.App.Views;

/// <summary>
/// Tela de programas de treino. Os controles desenhados à mão (gráfico, medidor e barras)
/// são atualizados aqui a cada quadro, porque expõem propriedades simples.
/// </summary>
public partial class DrillView : UserControl
{
    private const double WaitingWindowSeconds = 4.0;

    private DrillViewModel? _viewModel;

    public DrillView()
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

        _viewModel = e.NewValue as DrillViewModel;

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
        DrillViewModel? viewModel = _viewModel;
        if (viewModel is null)
        {
            return;
        }

        Gauge.Value = viewModel.ScoreValue;
        Gauge.IsActive = viewModel.IsRunning;
        Gauge.Grade = viewModel.GradeText;

        BrakeBar.Value = viewModel.BrakePercent;
        ThrottleBar.Value = viewModel.ThrottlePercent;

        bool braking = viewModel.ActivePedal == DrillPedal.Brake;
        BrakeBar.ReferenceValue = braking ? viewModel.TargetMarkerPercent : -1;
        ThrottleBar.ReferenceValue = braking ? -1 : viewModel.TargetMarkerPercent;

        RepetitionProgressBar.Value = viewModel.Progress;
        ProgramProgressBar.Value = viewModel.ProgramProgress;

        Plot.SetWaiting(WaitingWindowSeconds);
        Plot.SetTarget(viewModel.PlotCurve, viewModel.TolerancePercent);
        Plot.SetLive(viewModel.PlotTrace, viewModel.CursorSeconds);
    }
}
