using System;
using PedalTrainer.App.Views;

namespace PedalTrainer.App.Services;

/// <summary>
/// Dono da janela destacada da telemetria. A tela e a janela compartilham o mesmo
/// ViewModel, então o piloto vê os mesmos números enquanto joga, com a janela por cima do
/// simulador, arrastável e redimensionável.
/// </summary>
public sealed class TelemetryOverlayController : IDisposable
{
    /// <summary>Menor tamanho aceito para a janela destacada.</summary>
    public const double MinimumWidth = 320;
    public const double MinimumHeight = 380;

    /// <summary>Maior tamanho aceito para a janela destacada.</summary>
    public const double MaximumWidth = 960;
    public const double MaximumHeight = 900;

    private TelemetryOverlayWindow? _window;
    private bool _isPinned = true;
    private double _opacity = 0.92;
    private double _width = 380;
    private double _height = 420;
    private bool _disposed;

    /// <summary>Disparado quando a janela abre ou fecha.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Disparado quando o piloto redimensiona a janela (para guardar o tamanho).</summary>
    public event EventHandler? SizeChanged;

    /// <summary>Verdadeiro quando a janela destacada está aberta.</summary>
    public bool IsOpen => _window is not null;

    /// <summary>Verdadeiro quando a janela fica acima das demais (por cima do jogo).</summary>
    public bool IsPinned => _isPinned;

    /// <summary>Transparência do fundo do painel (0 = só os números, 1 = painel opaco).</summary>
    public double Opacity => _opacity;

    /// <summary>Largura atual da janela destacada.</summary>
    public double Width => _width;

    /// <summary>Altura atual da janela destacada.</summary>
    public double Height => _height;

    /// <summary>Aplica o modo "sempre no topo" e a transparência, com ou sem janela aberta.</summary>
    public void Configure(bool pinned, double opacity)
    {
        _isPinned = pinned;
        _opacity = Math.Clamp(opacity, 0.0, 1.0);

        if (_window is null)
        {
            return;
        }

        _window.ApplyPresentation(_isPinned, _opacity);
    }

    /// <summary>
    /// Guarda o tamanho escolhido pelo piloto (0 mantém o padrão) e aplica na janela aberta.
    /// </summary>
    public void ConfigureSize(double width, double height)
    {
        double clampedWidth = width > 0 ? Math.Clamp(width, MinimumWidth, MaximumWidth) : 380;
        double clampedHeight = height > 0 ? Math.Clamp(height, MinimumHeight, MaximumHeight) : 420;

        if (Math.Abs(clampedWidth - _width) < 0.5 && Math.Abs(clampedHeight - _height) < 0.5)
        {
            return;
        }

        _width = clampedWidth;
        _height = clampedHeight;

        _window?.ApplySize(_width, _height);
    }

    /// <summary>Abre a janela destacada (ou fecha, quando ela já está aberta).</summary>
    public void Toggle(object dataContext)
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        Show(dataContext);
    }

    /// <summary>Abre a janela destacada com o ViewModel informado.</summary>
    public void Show(object dataContext)
    {
        ArgumentNullException.ThrowIfNull(dataContext);

        if (_window is not null || _disposed)
        {
            return;
        }

        var window = new TelemetryOverlayWindow
        {
            DataContext = dataContext
        };

        window.Closed += OnWindowClosed;
        window.UserResized += OnWindowResized;
        _window = window;

        window.ApplySize(_width, _height);
        window.ApplyPresentation(_isPinned, _opacity);
        window.Show();

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Fecha a janela destacada.</summary>
    public void Close()
    {
        TelemetryOverlayWindow? window = _window;

        if (window is null)
        {
            return;
        }

        _window = null;
        window.Closed -= OnWindowClosed;
        window.UserResized -= OnWindowResized;
        window.Close();

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowResized(object? sender, EventArgs e)
    {
        if (_window is not { } window)
        {
            return;
        }

        _width = Math.Clamp(window.ActualWidth, MinimumWidth, MaximumWidth);
        _height = Math.Clamp(window.ActualHeight, MinimumHeight, MaximumHeight);

        SizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is TelemetryOverlayWindow window)
        {
            window.Closed -= OnWindowClosed;
            window.UserResized -= OnWindowResized;
        }

        _window = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        Close();
    }
}
