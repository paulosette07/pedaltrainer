using System;
using System.Diagnostics;
using System.Threading;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Pedais sintéticos: reproduzem a volta de referência em tempo real (com atraso e
/// ruído configuráveis) ou um perfil fixo. Permite demonstrar e testar o app sem hardware.
/// </summary>
public sealed class SimulatedPedalDevice : IPedalDevice
{
    /// <summary>Taxa de publicação dos pedais simulados.</summary>
    public const double OutputRateHz = 250;

    private const int MaxRaw = 65535;
    private const double ThrottleAxisIndex = 0;
    private const double BrakeAxisIndex = 1;

    private readonly SnapshotHolder _holder = new();
    private readonly int[] _axes = new int[3];
    private readonly CancellationTokenSource _cts = new();
    private readonly Random _noise = new(20240517);
    private readonly PedalDeviceDescriptor _descriptor = PedalDeviceDescriptor.Simulated();

    private Thread? _worker;
    private volatile ReferenceLap? _lap;
    private volatile int _skillPercent = 92;
    private bool _disposed;

    public PedalDeviceDescriptor Descriptor => _descriptor;

    public bool IsConnected => _worker is not null;

    public double SampleRateHz => _holder.RateHz;

    public string? LastError => null;

    /// <summary>Fidelidade do piloto simulado em relação à referência (0 a 100).</summary>
    public int SkillPercent
    {
        get => _skillPercent;
        set => _skillPercent = Math.Clamp(value, 0, 100);
    }

    /// <summary>Faz o piloto simulado seguir esta volta. Nulo usa o perfil fixo.</summary>
    public void SetReferenceLap(ReferenceLap? lap) => _lap = lap;

    public void Start()
    {
        if (_worker is not null || _disposed)
        {
            return;
        }

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "PedalTrainer.SimulatedInput",
            Priority = ThreadPriority.AboveNormal
        };
        _worker.Start();
    }

    public void Stop()
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        _cts.Cancel();
        _worker?.Join(TimeSpan.FromSeconds(2));
        _worker = null;
    }

    public bool TryReadLatest(out RawAxesSnapshot snapshot) => _holder.TryRead(out snapshot);

    private void WorkerLoop()
    {
        using var timerResolution = TimerResolutionScope.Acquire();

        CancellationToken token = _cts.Token;
        long startTicks = Stopwatch.GetTimestamp();
        long tickInterval = (long)(Stopwatch.Frequency / OutputRateHz);
        long nextTick = startTicks;

        while (!token.IsCancellationRequested)
        {
            double elapsedSeconds = (Stopwatch.GetTimestamp() - startTicks) / (double)Stopwatch.Frequency;
            Publish(elapsedSeconds);

            nextTick += tickInterval;
            double waitMilliseconds = (nextTick - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;

            if (waitMilliseconds > 2)
            {
                token.WaitHandle.WaitOne((int)waitMilliseconds);
            }
            else
            {
                Thread.SpinWait(400);
            }
        }
    }

    private void Publish(double elapsedSeconds)
    {
        ReferenceLap? lap = _lap;
        double brake;
        double throttle;

        if (lap is not null && lap.DurationSeconds > 0.5)
        {
            double fidelity = _skillPercent / 100.0;
            double lagSeconds = (1.0 - fidelity) * 0.35;
            double position = lap.StartTimeSeconds + Wrap(elapsedSeconds, lap.DurationSeconds);
            double sampledTime = Math.Max(lap.StartTimeSeconds, position - lagSeconds);
            ReferenceSample reference = lap.SampleAtTime(sampledTime);

            brake = (reference.BrakePercent * fidelity) + Noise(1.4);
            throttle = (reference.ThrottlePercent * fidelity) + Noise(1.4);
        }
        else
        {
            brake = SyntheticPedalWaveform.BrakeAt(elapsedSeconds);
            throttle = SyntheticPedalWaveform.ThrottleAt(elapsedSeconds);
        }

        _axes[(int)ThrottleAxisIndex] = Encode(throttle);
        _axes[(int)BrakeAxisIndex] = Encode(brake);
        _axes[2] = MaxRaw / 2;

        _holder.Publish(new RawAxesSnapshot(_axes, Stopwatch.GetTimestamp()));
    }

    private static double Wrap(double value, double period)
    {
        double wrapped = value % period;
        return wrapped < 0 ? wrapped + period : wrapped;
    }

    private double Noise(double amplitude) => ((_noise.NextDouble() * 2) - 1) * amplitude;

    private static int Encode(double percent) =>
        (int)Math.Round(Math.Clamp(percent, 0, 100) / 100.0 * MaxRaw);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _holder.Reset();
        _cts.Dispose();
    }
}
