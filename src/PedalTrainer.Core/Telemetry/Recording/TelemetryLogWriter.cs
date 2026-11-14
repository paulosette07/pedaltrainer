using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace PedalTrainer.Core.Telemetry.Recording;

/// <summary>
/// Grava as amostras em disco numa thread própria: o laço de 60 Hz só enfileira, então um
/// disco lento nunca trava a interface. O arquivo já sai com o cabeçalho do jogo em uso.
/// </summary>
public sealed class TelemetryLogWriter : IDisposable
{
    private const int FlushIntervalMilliseconds = 200;
    private const int JoinTimeoutMilliseconds = 3000;

    private readonly object _gate = new();
    private readonly List<TelemetryLogSample> _queue = new(512);
    private readonly AutoResetEvent _wake = new(false);
    private readonly TelemetryProfile _profile;

    private StreamWriter? _stream;
    private Thread? _worker;
    private bool _stopRequested;
    private int _writtenCount;
    private string _fault = string.Empty;

    private TelemetryLogWriter(string filePath, TelemetryProfile profile, StreamWriter stream)
    {
        FilePath = filePath;
        _profile = profile;
        _stream = stream;
    }

    /// <summary>Caminho do arquivo em gravação.</summary>
    public string FilePath { get; }

    /// <summary>Amostras já escritas no disco.</summary>
    public int WrittenCount => Volatile.Read(ref _writtenCount);

    /// <summary>Mensagem de erro quando a gravação falhou (vazio quando tudo certo).</summary>
    public string Fault
    {
        get
        {
            lock (_gate)
            {
                return _fault;
            }
        }
    }

    /// <summary>Abriu uma gravação nova; lança quando o arquivo não pode ser criado.</summary>
    public static TelemetryLogWriter Open(string filePath, TelemetryProfile profile, string simulatorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(profile);

        // BOM: o Excel abre o CSV com os acentos do cabeçalho no lugar certo.
        var stream = new StreamWriter(filePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            AutoFlush = false
        };

        foreach (string line in TelemetryLogFormatter.BuildHeader(profile, simulatorName, DateTime.Now))
        {
            stream.WriteLine(line);
        }

        stream.Flush();

        var writer = new TelemetryLogWriter(filePath, profile, stream);
        writer.StartWorker();
        return writer;
    }

    /// <summary>Enfileira uma amostra; não bloqueia e ignora depois de um erro de disco.</summary>
    public void Append(in TelemetryLogSample sample)
    {
        lock (_gate)
        {
            if (_stopRequested || _stream is null || _fault.Length > 0)
            {
                return;
            }

            _queue.Add(sample);
        }

        _wake.Set();
    }

    /// <summary>Fecha o arquivo e devolve quantas amostras foram gravadas.</summary>
    public int Close()
    {
        Thread? worker;

        lock (_gate)
        {
            if (_stream is null)
            {
                return _writtenCount;
            }

            _stopRequested = true;
            worker = _worker;
            _worker = null;
        }

        _wake.Set();
        worker?.Join(JoinTimeoutMilliseconds);

        lock (_gate)
        {
            _stream?.Flush();
            _stream?.Dispose();
            _stream = null;
        }

        return _writtenCount;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Close();
        _wake.Dispose();
    }

    private void StartWorker()
    {
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "PedalTrainer.TelemetriaArquivo"
        };

        _worker.Start();
    }

    private void WorkerLoop()
    {
        var batch = new List<TelemetryLogSample>(512);

        while (true)
        {
            batch.Clear();
            bool stopping;

            lock (_gate)
            {
                stopping = _stopRequested;
                batch.AddRange(_queue);
                _queue.Clear();
            }

            if (batch.Count > 0)
            {
                WriteBatch(batch);
            }
            else if (stopping)
            {
                return;
            }

            if (batch.Count == 0)
            {
                _wake.WaitOne(FlushIntervalMilliseconds);
            }
        }
    }

    private void WriteBatch(List<TelemetryLogSample> batch)
    {
        try
        {
            StreamWriter? stream;

            lock (_gate)
            {
                stream = _stream;
            }

            if (stream is null)
            {
                return;
            }

            foreach (TelemetryLogSample sample in batch)
            {
                stream.WriteLine(TelemetryLogFormatter.BuildRow(_profile, sample));
            }

            stream.Flush();
            Interlocked.Add(ref _writtenCount, batch.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            lock (_gate)
            {
                _fault = exception.Message;
                _stopRequested = true;
            }
        }
    }
}
