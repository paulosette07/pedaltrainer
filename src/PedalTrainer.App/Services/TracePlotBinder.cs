using System;
using PedalTrainer.App.Controls;
using PedalTrainer.App.ViewModels;

namespace PedalTrainer.App.Services;

/// <summary>
/// Liga um <see cref="TelemetryTracePlot"/> ao <see cref="TelemetryViewModel"/>. O traço muda a
/// cada quadro da telemetria, então o desenho é empurrado por evento em vez de binding: a tela
/// de telemetria e a janela destacada usam o mesmo ligador.
/// </summary>
public sealed class TracePlotBinder : IDisposable
{
    private readonly TelemetryTracePlot _plot;
    private TelemetryViewModel? _viewModel;

    public TracePlotBinder(TelemetryTracePlot plot)
    {
        _plot = plot ?? throw new ArgumentNullException(nameof(plot));
    }

    /// <summary>Aponta o desenho para um ViewModel (ou para nenhum, ao sair da tela).</summary>
    public void Bind(TelemetryViewModel? viewModel)
    {
        if (ReferenceEquals(viewModel, _viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.TraceChanged -= OnTraceChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.TraceChanged += OnTraceChanged;
        }

        Refresh();
    }

    /// <inheritdoc />
    public void Dispose() => Bind(null);

    private void OnTraceChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_viewModel is null)
        {
            _plot.SetTrace(null, 0, 0);
            _plot.SetBestSectors(null, null);
            return;
        }

        _plot.SetTrace(
            _viewModel.TraceSamples,
            _viewModel.TrackLengthMeters,
            _viewModel.LapPositionPercent);
        _plot.SetBestSectors(_viewModel.BestSectors, _viewModel.BeatenSectors);
    }
}
