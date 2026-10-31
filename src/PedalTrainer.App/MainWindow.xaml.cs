using System;
using System.Windows;
using System.Windows.Interop;
using PedalTrainer.App.Services;
using PedalTrainer.App.ViewModels;

namespace PedalTrainer.App;

/// <summary>Janela principal: cria o shell, entrega o handle ao DirectInput e inicializa o motor.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        AppIconProvider.Apply(this);
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.AttachWindow(new WindowInteropHelper(this).Handle);
        _viewModel.Initialize();
    }

    private void OnClosed(object? sender, EventArgs e) => _viewModel.Shutdown();
}
