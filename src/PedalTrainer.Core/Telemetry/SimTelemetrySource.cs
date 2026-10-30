using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Lê a telemetria de um ou mais simuladores numa thread dedicada a 60 Hz e publica o último
/// instantâneo. Quando o simulador preferido não está publicando, tenta os demais da lista;
/// assim a opção "detectar automaticamente" funciona sem nenhuma escolha manual.
/// </summary>
public sealed class SimTelemetrySource : ICarTelemetrySource
{
    /// <summary>Taxa de leitura da telemetria do carro.</summary>
    public const double PollRateHz = 60.0;

    private readonly object _gate = new();
    private readonly List<ISimTelemetryReader> _readers;
    private readonly CancellationTokenSource _cts = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Thread? _worker;
    private CarTelemetry _latest = CarTelemetry.Disconnected(0);
    private string _status;
    private string _connectedName = string.Empty;
    private int _activeReaderIndex = -1;
    private bool _connected;
    private bool _disposed;

    public SimTelemetrySource(SimulatorInfo simulator, IEnumerable<ISimTelemetryReader> readers)
    {
        Simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _readers = new List<ISimTelemetryReader>(readers ?? throw new ArgumentNullException(nameof(readers)));
        _status = $"{DescribeTarget()}: procurando simulador...";
    }

    public SimulatorInfo Simulator { get; }

    public string ConnectedSimulatorName
    {
        get
        {
            lock (_gate)
            {
                return _connectedName;
            }
        }
    }

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
            Name = "PedalTrainer.Telemetry",
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

        foreach (ISimTelemetryReader reader in _readers)
        {
            reader.Close();
        }
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
        if (TryPollActiveReader(out SimTelemetrySample sample, out string status) ||
            TryPollOtherReaders(out sample, out status))
        {
            Publish(sample, status);
            return;
        }

        PublishDisconnected(_readers.Count == 0
            ? $"{DescribeTarget()}: nenhum leitor disponível"
            : $"{DescribeTarget()}: simulador não encontrado");
    }

    private bool TryPollActiveReader(out SimTelemetrySample sample, out string status)
    {
        sample = default;
        status = string.Empty;

        if (_activeReaderIndex < 0 || _activeReaderIndex >= _readers.Count)
        {
            return false;
        }

        ISimTelemetryReader reader = _readers[_activeReaderIndex];

        if (!reader.IsOpen && !reader.TryOpen())
        {
            _activeReaderIndex = -1;
            return false;
        }

        if (reader.TryRead(out sample, out status))
        {
            return true;
        }

        // O simulador continua aberto, mas parou de publicar (menu, replay, pausa):
        // fecha e deixa a próxima volta do laço tentar reabrir.
        reader.Close();
        _activeReaderIndex = -1;
        return false;
    }

    private bool TryPollOtherReaders(out SimTelemetrySample sample, out string status)
    {
        sample = default;
        status = string.Empty;

        for (var index = 0; index < _readers.Count; index++)
        {
            if (index == _activeReaderIndex)
            {
                continue;
            }

            ISimTelemetryReader reader = _readers[index];

            if (!reader.TryOpen())
            {
                continue;
            }

            if (reader.TryRead(out sample, out status))
            {
                _activeReaderIndex = index;
                return true;
            }

            reader.Close();
        }

        return false;
    }

    private void Publish(in SimTelemetrySample sample, string status)
    {
        double sessionTime = sample.SessionTimeSeconds ?? _clock.Elapsed.TotalSeconds;

        var telemetry = new CarTelemetry(
            IsConnected: true,
            SpeedKmh: Math.Max(0, sample.SpeedKmh),
            BrakePercent: Math.Clamp(sample.BrakePercent, 0, 100),
            ThrottlePercent: Math.Clamp(sample.ThrottlePercent, 0, 100),
            LapDistancePercent: Math.Clamp(sample.LapDistancePercent, 0, 1),
            SessionTimeSeconds: sessionTime,
            LapNumber: Math.Max(0, sample.LapNumber),
            TimestampSeconds: sessionTime);

        string readerName = _activeReaderIndex >= 0 && _activeReaderIndex < _readers.Count
            ? _readers[_activeReaderIndex].Simulator.ShortName
            : Simulator.ShortName;

        lock (_gate)
        {
            _latest = telemetry;
            _connected = true;
            _connectedName = readerName;
            _status = $"{readerName}: {status}";
        }
    }

    private void PublishDisconnected(string status)
    {
        lock (_gate)
        {
            _connected = false;
            _connectedName = string.Empty;
            _status = status;
            _latest = CarTelemetry.Disconnected(0);
        }
    }

    private string DescribeTarget() => Simulator.IsAuto ? "Automático" : Simulator.ShortName;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();

        foreach (ISimTelemetryReader reader in _readers)
        {
            reader.Dispose();
        }

        _readers.Clear();
        _cts.Dispose();
    }
}
