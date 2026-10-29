using System;
using System.Diagnostics;
using System.Threading;
using PedalTrainer.Core.Models;
using SharpDX;
using SharpDX.DirectInput;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Leitura dos pedais via DirectInput (SharpDX). Uma thread dedicada faz polling
/// em torno de 500 Hz e publica apenas o último instantâneo dos eixos.
/// </summary>
public sealed class DirectInputPedalDevice : IPedalDevice
{
    /// <summary>Intervalo de polling do laço de aquisição.</summary>
    public const int PollIntervalMilliseconds = 1;

    private const int HResultNotAcquired = unchecked((int)0x8007000C);
    private const int HResultInputLost = unchecked((int)0x8007001E);
    private const int HResultNotInitialized = unchecked((int)0x80070015);

    private static readonly string[] AxisLabels =
    {
        "Eixo X",
        "Eixo Y",
        "Eixo Z",
        "Rotação X",
        "Rotação Y",
        "Rotação Z",
        "Slider 1",
        "Slider 2"
    };

    private readonly PedalDeviceDescriptor _descriptor;
    private readonly IntPtr _windowHandle;
    private readonly SnapshotHolder _holder = new();
    private readonly int[] _axisBuffer = new int[RawAxesSnapshot.MaxAxes];
    private readonly CancellationTokenSource _cts = new();

    private PedalAxisMap? _axisMap;

    private Thread? _worker;
    private DirectInput? _directInput;
    private Joystick? _joystick;
    private volatile bool _isConnected;
    private volatile string? _lastError;
    private bool _disposed;

    public DirectInputPedalDevice(PedalDeviceDescriptor descriptor, IntPtr windowHandle)
    {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _windowHandle = windowHandle;
    }

    public PedalDeviceDescriptor Descriptor => _descriptor;

    public bool IsConnected => _isConnected;

    public double SampleRateHz => _holder.RateHz;

    public string? LastError => _lastError;

    /// <summary>Rótulo legível de um índice de eixo.</summary>
    public static string AxisLabel(int index) =>
        index >= 0 && index < AxisLabels.Length ? AxisLabels[index] : $"Eixo {index}";

    public void Start()
    {
        if (_worker is not null || _disposed)
        {
            return;
        }

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "PedalTrainer.Input",
            Priority = ThreadPriority.Highest
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

        while (!token.IsCancellationRequested)
        {
            if (_joystick is null && !TryOpenDevice())
            {
                token.WaitHandle.WaitOne(250);
                continue;
            }

            if (!TryReadOnce())
            {
                CloseDevice();
                _isConnected = false;
                token.WaitHandle.WaitOne(250);
                continue;
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }

        CloseDevice();
    }

    private bool TryReadOnce()
    {
        Joystick? joystick = _joystick;
        if (joystick is null)
        {
            return false;
        }

        try
        {
            joystick.Poll();
            JoystickState state = joystick.GetCurrentState();
            FillAxes(state);
            _holder.Publish(new RawAxesSnapshot(_axisBuffer, Stopwatch.GetTimestamp()));

            if (!_isConnected)
            {
                _isConnected = true;
                _lastError = null;
            }

            return true;
        }
        catch (SharpDXException ex) when (IsRecoverable(ex.HResult))
        {
            return TryReacquire(joystick, ex.HResult);
        }
        catch (SharpDXException ex)
        {
            _lastError = $"Falha na leitura do controlador (0x{ex.HResult:X8}).";
            _isConnected = false;
            return false;
        }
        catch (ObjectDisposedException)
        {
            _isConnected = false;
            return false;
        }
    }

    private bool TryReacquire(Joystick joystick, int hresult)
    {
        _isConnected = false;

        if (hresult != HResultInputLost && hresult != HResultNotAcquired)
        {
            _lastError = $"Controlador não inicializado (0x{hresult:X8}).";
            return false;
        }

        try
        {
            joystick.Acquire();
            _lastError = null;
            return true;
        }
        catch (SharpDXException ex)
        {
            _lastError = $"Reconexão do controlador falhou (0x{ex.HResult:X8}).";
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private bool TryOpenDevice()
    {
        try
        {
            _directInput ??= new DirectInput();
            var joystick = new Joystick(_directInput, _descriptor.InstanceGuid);
            Configure(joystick);
            _joystick = joystick;
            _lastError = null;
            return true;
        }
        catch (SharpDXException ex)
        {
            _lastError = $"Não foi possível abrir \"{_descriptor.DisplayName}\" (0x{ex.HResult:X8}).";
            DisposeDirectInput();
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ObjectDisposedException)
        {
            _lastError = $"Não foi possível abrir \"{_descriptor.DisplayName}\": {ex.Message}";
            DisposeDirectInput();
            return false;
        }
    }

    private void Configure(Joystick joystick)
    {
        try
        {
            CooperativeLevel level = CooperativeLevel.Background | CooperativeLevel.NonExclusive;
            joystick.SetCooperativeLevel(_windowHandle, level);
        }
        catch (SharpDXException)
        {
            // Alguns drivers genéricos (adaptadores HID) recusam Background|NonExclusive.
            joystick.SetCooperativeLevel(_windowHandle, CooperativeLevel.NonExclusive);
        }

        TrySetProperty(() => joystick.Properties.Range = new InputRange(0, 65535));
        TrySetProperty(() => joystick.Properties.AxisMode = DeviceAxisMode.Absolute);
        _axisMap = TryBuildAxisMap(joystick);
        joystick.Acquire();
    }

    private static void TrySetProperty(Action action)
    {
        try
        {
            action();
        }
        catch (SharpDXException)
        {
            // Propriedade não suportada pelo dispositivo: seguimos com o padrão do driver.
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static PedalAxisMap? TryBuildAxisMap(Joystick joystick)
    {
        try
        {
            return PedalAxisMap.From(joystick.GetObjects(DeviceObjectTypeFlags.Axis));
        }
        catch (SharpDXException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private void FillAxes(JoystickState state)
    {
        if (_axisMap is not null)
        {
            _axisMap.Fill(state, _axisBuffer);
            return;
        }

        _axisBuffer[0] = state.X;
        _axisBuffer[1] = state.Y;
        _axisBuffer[2] = state.Z;
        _axisBuffer[3] = state.RotationX;
        _axisBuffer[4] = state.RotationY;
        _axisBuffer[5] = state.RotationZ;

        int[]? sliders = state.Sliders;
        _axisBuffer[6] = sliders is { Length: > 0 } ? sliders[0] : 0;
        _axisBuffer[7] = sliders is { Length: > 1 } ? sliders[1] : 0;

        for (int i = 8; i < _axisBuffer.Length; i++)
        {
            _axisBuffer[i] = 0;
        }
    }

    private static bool IsRecoverable(int hresult) =>
        hresult == HResultInputLost || hresult == HResultNotAcquired || hresult == HResultNotInitialized;

    private void CloseDevice()
    {
        if (_joystick is null)
        {
            return;
        }

        try
        {
            _joystick.Unacquire();
        }
        catch (SharpDXException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _joystick.Dispose();
        _joystick = null;
    }

    private void DisposeDirectInput()
    {
        _directInput?.Dispose();
        _directInput = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        CloseDevice();
        DisposeDirectInput();
        _holder.Reset();
        _cts.Dispose();
    }
}
