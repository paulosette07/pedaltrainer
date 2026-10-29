using System;
using System.Threading;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.IRacing;

/// <summary>
/// Mantém uma thread lendo a telemetria do iRacing a 60 Hz e publica o último instantâneo.
/// Quando o simulador não está aberto, o estado fica "desconectado" sem gerar erro.
/// </summary>
public sealed class IRacingTelemetrySource : ICarTelemetrySource
{
    /// <summary>Taxa de leitura da telemetria do carro.</summary>
    public const double PollRateHz = 60.0;

    private readonly object _gate = new();
    private readonly IRacingSharedMemoryReader _reader = new();
    private readonly CancellationTokenSource _cts = new();

    private Thread? _worker;
    private CarTelemetry _latest = CarTelemetry.Disconnected(0);
    private string _status = "iRacing: procurando sim...";
    private bool _connected;
    private bool _disposed;

    public bool IsConnected
    {
        get
        {
            lock (_gate)
            {
                return _connected;
            }
        }
    }

    public string StatusDescription
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public CarTelemetry Latest
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    public void Start()
    {
        if (_worker is not null || _disposed)
        {
            return;
        }

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "PedalTrainer.IRacing",
            Priority = ThreadPriority.Normal
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
        _reader.Dispose();
    }

    private void WorkerLoop()
    {
        CancellationToken token = _cts.Token;
        int intervalMilliseconds = (int)Math.Round(1000 / PollRateHz);

        while (!token.IsCancellationRequested)
        {
            Poll();
            token.WaitHandle.WaitOne(intervalMilliseconds);
        }
    }

    private void Poll()
    {
        if (!_reader.IsOpen && !_reader.TryConnect())
        {
            PublishDisconnected("iRacing: procurando sim...");
            return;
        }

        if (!_reader.TryReadSample(out IRacingRawSample sample) || !sample.IsValid)
        {
            PublishDisconnected("iRacing: sim aberto, telemetria inativa");
            return;
        }

        var telemetry = new CarTelemetry(
            IsConnected: true,
            SpeedKmh: Math.Max(0, sample.SpeedMetersPerSecond) * 3.6,
            BrakePercent: Math.Clamp(sample.BrakeFraction, 0, 1) * 100,
            ThrottlePercent: Math.Clamp(sample.ThrottleFraction, 0, 1) * 100,
            LapDistancePercent: Math.Clamp(sample.LapDistancePercent, 0, 1),
            SessionTimeSeconds: sample.SessionTimeSeconds,
            LapNumber: sample.LapNumber,
            TimestampSeconds: 0);

        lock (_gate)
        {
            _latest = telemetry;
            _connected = true;
            _status = $"iRacing: conectado (volta {sample.LapNumber})";
        }
    }

    private void PublishDisconnected(string status)
    {
        lock (_gate)
        {
            _connected = false;
            _status = status;
            _latest = CarTelemetry.Disconnected(0);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _cts.Dispose();
    }
}
