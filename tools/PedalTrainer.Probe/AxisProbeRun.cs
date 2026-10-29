using System.Diagnostics;
using System.Globalization;
using PedalTrainer.Core.Input;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Probe;

/// <summary>
/// Corre o mesmo caminho de leitura usado pelo app (DirectInputPedalDevice) durante alguns
/// segundos, guarda o mínimo e o máximo de cada eixo e diz qual eixo parece ser o freio.
/// </summary>
internal sealed class AxisProbeRun
{
    private readonly List<int>[] _samples;
    private readonly int[] _firstValues;
    private readonly int _axisCount;

    private AxisProbeRun(int axisCount)
    {
        _axisCount = axisCount;
        _samples = new List<int>[axisCount];
        _firstValues = new int[axisCount];

        for (var axis = 0; axis < axisCount; axis++)
        {
            _samples[axis] = new List<int>();
            _firstValues[axis] = -1;
        }
    }

    /// <summary>Verdadeiro quando ao menos um eixo se moveu durante a captura.</summary>
    public bool HasMovement { get; private set; }

    /// <summary>Eixo com maior curso observado, ou -1 se nada se moveu.</summary>
    public int DominantAxis { get; private set; } = -1;

    public static AxisProbeRun Execute(PedalDeviceDescriptor device, int seconds, int printInterval)
    {
        var run = new AxisProbeRun(Math.Min(device.AxisCount, RawAxesSnapshot.MaxAxes));

        using var pedalDevice = new DirectInputPedalDevice(device, IntPtr.Zero);
        pedalDevice.Start();

        var clock = Stopwatch.StartNew();
        long nextPrint = 0;

        while (clock.Elapsed.TotalSeconds < seconds)
        {
            if (pedalDevice.TryReadLatest(out RawAxesSnapshot raw) && raw.Count > 0)
            {
                run.Capture(raw);
            }

            if (clock.ElapsedMilliseconds >= nextPrint)
            {
                nextPrint = clock.ElapsedMilliseconds + printInterval;
                run.PrintLine(pedalDevice, clock.Elapsed.TotalSeconds);
            }

            Thread.Sleep(5);
        }

        pedalDevice.Stop();
        return run;
    }

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("Resumo da captura:");

        for (var axis = 0; axis < _axisCount; axis++)
        {
            Console.WriteLine(
                $"  eixo {axis}: mín {Minimum(axis),6}  máx {Maximum(axis),6}  curso {Span(axis),6}" +
                (IsInverted(axis) ? "  (invertido: repousa no topo)" : string.Empty));
        }

        Console.WriteLine();

        if (DominantAxis < 0)
        {
            Console.WriteLine("NENHUM eixo se moveu. Pise no freio durante a captura.");
            Console.WriteLine("Se o pedal tem software próprio, verifique se ele está em modo DirectInput/PC.");
            return;
        }

        Console.WriteLine($"Eixo do freio detectado: {DominantAxis} (curso {Span(DominantAxis)}).");
        Console.WriteLine("O app usa exatamente essa regra: o primeiro eixo que se move vira o freio.");
    }

    public void WriteCsv(string path)
    {
        var lines = new List<string> { "axis,min,max,span,first" };

        for (var axis = 0; axis < _axisCount; axis++)
        {
            lines.Add(string.Join(
                ',',
                axis.ToString(CultureInfo.InvariantCulture),
                Minimum(axis).ToString(CultureInfo.InvariantCulture),
                Maximum(axis).ToString(CultureInfo.InvariantCulture),
                Span(axis).ToString(CultureInfo.InvariantCulture),
                _firstValues[axis].ToString(CultureInfo.InvariantCulture)));
        }

        try
        {
            File.WriteAllLines(path, lines);
            Console.WriteLine($"CSV salvo em {path}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Não foi possível salvar o CSV: {ex.Message}");
        }
    }

    private void Capture(in RawAxesSnapshot raw)
    {
        int count = Math.Min(raw.Count, _axisCount);

        for (var axis = 0; axis < count; axis++)
        {
            int value = raw.Get(axis);

            if (_firstValues[axis] < 0)
            {
                _firstValues[axis] = value;
            }

            _samples[axis].Add(value);
        }

        int dominant = DominantAxis;
        if (dominant < 0)
        {
            return;
        }

        if (!HasMovement && Span(dominant) >= AutoAxisMapper.MinimumAxisSpan)
        {
            HasMovement = true;
        }
    }

    private void PrintLine(DirectInputPedalDevice device, double seconds)
    {
        if (!device.TryReadLatest(out RawAxesSnapshot raw) || raw.Count == 0)
        {
            Console.WriteLine($"[{seconds,5:0.0}s] sem leitura ({device.LastError ?? "aguardando"})");
            return;
        }

        UpdateDominantAxis();

        var parts = new List<string>(Math.Min(raw.Count, _axisCount));
        for (var axis = 0; axis < Math.Min(raw.Count, _axisCount); axis++)
        {
            parts.Add($"{axis}:{raw.Get(axis),6}");
        }

        Console.WriteLine($"[{seconds,5:0.0}s] {string.Join("  ", parts)}");
    }

    private void UpdateDominantAxis()
    {
        int best = -1;
        int bestSpan = 0;

        for (var axis = 0; axis < _axisCount; axis++)
        {
            int span = Span(axis);
            if (span > bestSpan)
            {
                bestSpan = span;
                best = axis;
            }
        }

        DominantAxis = best;
        HasMovement = bestSpan >= AutoAxisMapper.MinimumAxisSpan;
    }

    private int Minimum(int axis) => _samples[axis].Count == 0 ? 0 : _samples[axis].Min();

    private int Maximum(int axis) => _samples[axis].Count == 0 ? 0 : _samples[axis].Max();

    private int Span(int axis) => Maximum(axis) - Minimum(axis);

    private bool IsInverted(int axis)
    {
        if (_samples[axis].Count == 0 || Span(axis) < AutoAxisMapper.MinimumAxisSpan)
        {
            return false;
        }

        int resting = _firstValues[axis];
        return Math.Abs((long)resting - Maximum(axis)) < Math.Abs((long)resting - Minimum(axis));
    }
}
