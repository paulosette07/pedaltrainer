using System.Globalization;
using PedalTrainer.Core.Input;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Probe;

/// <summary>
/// Ferramenta de teste dos pedais: mostra ao vivo o que cada eixo do controlador está
/// enviando e diz qual eixo o app escolheria para o freio. Serve para provar o pedal
/// sem depender da interface do treino.
/// </summary>
internal static class Program
{
    private const int DefaultSeconds = 20;
    private const int PrintIntervalMilliseconds = 500;

    private static int Main(string[] args)
    {
        int seconds = ReadInt(args, 0, DefaultSeconds);
        int? deviceIndex = ReadOptionalInt(args, 1);

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Pedal Trainer · teste dos pedais");
        Console.WriteLine("Pressione o freio (e o acelerador) enquanto a leitura corre.");
        Console.WriteLine();

        IReadOnlyList<PedalDeviceDescriptor> devices = PedalDeviceEnumerator.Enumerate();
        if (devices.Count == 0)
        {
            Console.WriteLine("Nenhum controlador DirectInput encontrado. Verifique o cabo USB do pedal.");
            return 2;
        }

        PrintDevices(devices);

        PedalDeviceDescriptor? device = deviceIndex is int index && index >= 0 && index < devices.Count
            ? devices[index]
            : PedalDeviceEnumerator.PickDefault(devices);

        if (device is null)
        {
            Console.WriteLine("Não foi possível escolher um controlador.");
            return 3;
        }

        Console.WriteLine($"Lendo: {device.DisplayName} ({device.AxisCount} eixos) por {seconds} s");
        Console.WriteLine();

        AxisProbeRun run = AxisProbeRun.Execute(device, seconds, PrintIntervalMilliseconds);
        run.PrintSummary();
        run.WriteCsv(Path.Combine(Environment.CurrentDirectory, "pedal-probe.csv"));

        return run.HasMovement ? 0 : 1;
    }

    private static void PrintDevices(IReadOnlyList<PedalDeviceDescriptor> devices)
    {
        Console.WriteLine("Controladores encontrados:");
        for (var i = 0; i < devices.Count; i++)
        {
            PedalDeviceDescriptor device = devices[i];
            Console.WriteLine($"  [{i}] {device.DisplayName} — {device.AxisCount} eixos");

            for (var axis = 0; axis < device.AxisNames.Length; axis++)
            {
                Console.WriteLine($"        eixo {axis}: {device.AxisNames[axis]}");
            }
        }

        Console.WriteLine();
    }

    private static int ReadInt(string[] args, int position, int fallback) =>
        ReadOptionalInt(args, position) ?? fallback;

    private static int? ReadOptionalInt(string[] args, int position)
    {
        if (args.Length <= position)
        {
            return null;
        }

        return int.TryParse(args[position], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;
    }
}
