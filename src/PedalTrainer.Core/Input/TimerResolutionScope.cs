using System;
using System.Runtime.InteropServices;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Eleva a resolução do timer do Windows para 1 ms enquanto o laço de aquisição roda.
/// Sem isso, Thread.Sleep(1) pode dormir 15 ms e derrubar a taxa de leitura.
/// </summary>
internal sealed class TimerResolutionScope : IDisposable
{
    private const uint TargetResolutionMs = 1;
    private bool _disposed;
    private readonly bool _acquired;

    private TimerResolutionScope(bool acquired) => _acquired = acquired;

    public static TimerResolutionScope Acquire()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new TimerResolutionScope(false);
        }

        try
        {
            return new TimerResolutionScope(TimeBeginPeriod(TargetResolutionMs) == 0);
        }
        catch (DllNotFoundException)
        {
            return new TimerResolutionScope(false);
        }
        catch (EntryPointNotFoundException)
        {
            return new TimerResolutionScope(false);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_acquired)
        {
            TimeEndPeriod(TargetResolutionMs);
        }
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", ExactSpelling = true)]
    private static extern uint TimeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", ExactSpelling = true)]
    private static extern uint TimeEndPeriod(uint milliseconds);
}
