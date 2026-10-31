using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PedalTrainer.App.Services;

/// <summary>
/// Entrega o icone da janela principal.
/// </summary>
/// <remarks>
/// O atributo <c>Icon</c> do XAML nao sobrevive ao <c>PublishSingleFile</c>: a resolucao do
/// pack URI devolve vazio e a janela fica sem icone na barra de titulo, na barra de tarefas
/// e no Alt+Tab. Por isso o icone e lido em tempo de execucao, preferindo o recurso que o
/// proprio executavel carrega (o mesmo que o Explorer mostra) e caindo para o recurso WPF
/// quando o processo nao tem icone proprio - caso de rodar via <c>dotnet PedalTrainer.dll</c>.
/// </remarks>
internal static class AppIconProvider
{
    private const int WmSetIcon = 0x0080;
    private const int IconSmallSlot = 0;
    private const int IconLargeSlot = 1;
    private const int FirstIconIndex = 0;
    private const uint SingleIcon = 1;

    private static IntPtr _smallIconHandle;
    private static IntPtr _largeIconHandle;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string filePath, int iconIndex, IntPtr[]? largeIcons, IntPtr[]? smallIcons, uint iconCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Define o icone da janela e entrega os tamanhos nativos 16 e 32 para o Windows,
    /// evitando que o titulo use uma versao reduzida por escala.
    /// </summary>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var source = LoadFromExecutable() ?? LoadFromApplicationResource();
        if (source is null) { return; }

        window.Icon = source;
        window.SourceInitialized += (_, _) => ApplyNativeSizes(window);
    }

    /// <summary>Le o icone embutido no executavel em execucao (o mesmo exibido no Explorer).</summary>
    private static ImageSource? LoadFromExecutable()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath)) { return null; }

        var largeIcons = new IntPtr[1];
        var smallIcons = new IntPtr[1];
        var extracted = ExtractIconEx(executablePath, FirstIconIndex, largeIcons, smallIcons, SingleIcon);
        if (extracted == 0) { return null; }

        _largeIconHandle = largeIcons[0];
        _smallIconHandle = smallIcons[0];

        return CreateImage(_largeIconHandle) ?? CreateImage(_smallIconHandle);
    }

    /// <summary>Le <c>Assets/pedaltrainer.ico</c> como recurso WPF (caminho usado no build normal).</summary>
    private static ImageSource? LoadFromApplicationResource()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("Assets/pedaltrainer.ico", UriKind.Relative));
            if (resource is null) { return null; }

            using var stream = resource.Stream;
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        catch (IOException)
        {
            // Sem o recurso no assembly nao ha o que carregar: a janela fica com o icone padrao.
            return null;
        }
    }

    private static ImageSource? CreateImage(IntPtr handle)
    {
        if (handle == IntPtr.Zero) { return null; }

        var bitmap = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        bitmap.Freeze();
        return bitmap;
    }

    private static void ApplyNativeSizes(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) { return; }

        if (_largeIconHandle != IntPtr.Zero)
        {
            SendMessage(handle, WmSetIcon, new IntPtr(IconLargeSlot), _largeIconHandle);
        }

        if (_smallIconHandle != IntPtr.Zero)
        {
            SendMessage(handle, WmSetIcon, new IntPtr(IconSmallSlot), _smallIconHandle);
        }
    }
}
