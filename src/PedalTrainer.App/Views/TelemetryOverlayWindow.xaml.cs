using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PedalTrainer.App.Services;
using PedalTrainer.App.ViewModels;

namespace PedalTrainer.App.Views;

/// <summary>
/// Janela destacada da telemetria: sem moldura, arrastável pelo cabeçalho, redimensionável
/// pelas bordas e sempre no topo quando o piloto pede, para ficar visível por cima do
/// simulador durante a pilotagem.
/// O fundo é translúcido de verdade (a janela é transparente e só o painel é pintado), então
/// os números e o traço da volta continuam opacos por cima do jogo.
/// </summary>
public partial class TelemetryOverlayWindow : Window
{
    private const int WmNcHitTest = 0x0084;

    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    /// <summary>Espessura (em DIP) da faixa da borda que pega o redimensionamento.</summary>
    private const double ResizeBorder = 7;

    private static readonly Color PanelColor = Color.FromRgb(0x10, 0x13, 0x17);
    private static readonly Color PanelBorderColor = Color.FromRgb(0x2A, 0x31, 0x3B);

    private readonly TracePlotBinder _traceBinder;
    private double _panelOpacity = 0.92;
    private HwndSource? _source;

    public TelemetryOverlayWindow()
    {
        InitializeComponent();

        _traceBinder = new TracePlotBinder(OverlayTrace);
        OverlayTrace.EmptyMessage = "Sem posição na pista nem volta de referência.";

        WindowStartupLocation = WindowStartupLocation.Manual;

        DataContextChanged += (_, _) => BindTrace();
        Closed += (_, _) => _traceBinder.Dispose();
        Loaded += OnFirstLoaded;

        ApplyPanelOpacity(_panelOpacity);
    }

    /// <summary>
    /// Disparado depois que o piloto redimensiona a janela pela borda, para o app guardar o
    /// tamanho escolhido.
    /// </summary>
    public event EventHandler? UserResized;

    /// <summary>Aplica o tamanho da janela vindo das preferências.</summary>
    public void ApplySize(double width, double height)
    {
        Width = Math.Clamp(width, MinWidth, MaxWidth);
        Height = Math.Clamp(height, MinHeight, MaxHeight);
    }

    /// <summary>
    /// Com <c>WindowStyle="None"</c> o Windows não cria a borda de redimensionamento, então a
    /// detecção é feita à mão: perto da borda a janela devolve o código de "redimensionar" em
    /// vez de repassar o clique para o conteúdo.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            _source = source;
            source.AddHook(OnWindowMessage);
        }
    }

    /// <summary>
    /// Só depois do layout a janela tem tamanho: aí ela é encostada no canto superior
    /// direito, onde costuma sobrar espaço na tela do jogo.
    /// </summary>
    private void OnFirstLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnFirstLoaded;

        double width = ActualWidth > 0 ? ActualWidth : 320;

        Left = Math.Max(0, SystemParameters.WorkArea.Right - width - 24);
        Top = SystemParameters.WorkArea.Top + 24;

        BindTrace();
    }

    /// <summary>Aplica o modo "sempre no topo" e a transparência do fundo pedidos pela tela.</summary>
    public void ApplyPresentation(bool pinned, double panelOpacity)
    {
        Topmost = pinned;
        ApplyPanelOpacity(panelOpacity);

        PinButton.Content = pinned ? "Fixo" : "Livre";
        PinButton.Foreground = pinned
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextSecondaryBrush");
    }

    /// <summary>
    /// Pinta só o fundo do painel com a transparência escolhida (0 = só os números sobre o
    /// jogo). O texto e o gráfico ficam sempre opacos: quem some é o painel, não a informação.
    /// </summary>
    private void ApplyPanelOpacity(double value)
    {
        _panelOpacity = Math.Clamp(value, 0, 1);

        Panel.Background = CreateBrush(PanelColor, _panelOpacity);
        Panel.BorderBrush = CreateBrush(PanelBorderColor, Math.Clamp(_panelOpacity, 0.18, 0.6));

        // O gráfico acompanha o fundo, um pouco mais denso: o traço precisa continuar legível
        // por cima da imagem do jogo.
        OverlayTrace.BackgroundOpacity = Math.Clamp(_panelOpacity + 0.15, 0, 1);
    }

    private static SolidColorBrush CreateBrush(Color color, double alpha)
    {
        byte level = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
        var brush = new SolidColorBrush(Color.FromArgb(level, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private void BindTrace() => _traceBinder.Bind(DataContext as TelemetryViewModel);

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmNcHitTest || !IsLoaded)
        {
            return IntPtr.Zero;
        }

        int packed = lParam.ToInt32();
        var screenPoint = new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF));

        if (!TryGetResizeHit(screenPoint, out int hitTest))
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    /// <summary>
    /// Traduz a posição do mouse nos oito códigos de redimensionamento do Windows. Fora da
    /// faixa da borda devolve falso e a janela trata o clique normalmente (arrastar, botões).
    /// </summary>
    private bool TryGetResizeHit(Point screenPoint, out int hitTest)
    {
        hitTest = 0;

        Point local = PointFromScreen(screenPoint);
        double width = ActualWidth;
        double height = ActualHeight;

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        bool left = local.X <= ResizeBorder;
        bool right = local.X >= width - ResizeBorder;
        bool top = local.Y <= ResizeBorder;
        bool bottom = local.Y >= height - ResizeBorder;

        if (top && left)
        {
            hitTest = HtTopLeft;
        }
        else if (top && right)
        {
            hitTest = HtTopRight;
        }
        else if (bottom && left)
        {
            hitTest = HtBottomLeft;
        }
        else if (bottom && right)
        {
            hitTest = HtBottomRight;
        }
        else if (left)
        {
            hitTest = HtLeft;
        }
        else if (right)
        {
            hitTest = HtRight;
        }
        else if (top)
        {
            hitTest = HtTop;
        }
        else if (bottom)
        {
            hitTest = HtBottom;
        }

        return hitTest != 0;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (!IsLoaded || (!sizeInfo.WidthChanged && !sizeInfo.HeightChanged))
        {
            return;
        }

        UserResized?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (_source is not null)
        {
            _source.RemoveHook(OnWindowMessage);
            _source = null;
        }
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is TelemetryViewModel viewModel)
        {
            viewModel.OverlayPinned = !viewModel.OverlayPinned;
            return;
        }

        ApplyPresentation(!Topmost, _panelOpacity);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
