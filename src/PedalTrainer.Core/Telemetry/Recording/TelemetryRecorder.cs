using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Telemetry.Recording;

/// <summary>
/// Gravador de telemetria do jogo em uso: transforma os instantâneos do carro em um arquivo
/// CSV dentro da pasta <c>telemetria</c>, na taxa definida pelo perfil do simulador.
/// A amostragem acontece na thread de quem chama <see cref="Observe"/>; a escrita em disco
/// fica na thread do <see cref="TelemetryLogWriter"/>.
/// </summary>
public sealed class TelemetryRecorder : IDisposable
{
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private TelemetryLogWriter? _writer;
    private TelemetryProfile _profile = TelemetryProfileCatalog.Auto;
    private string _simulatorName = string.Empty;
    private string _status = "Pronto para gravar.";
    private string _lastFilePath = string.Empty;
    private double _nextSampleSeconds;
    private double _startedAtSeconds;
    private bool _disposed;

    /// <summary>Pasta onde os arquivos são gravados (raiz do programa, ou a de reserva).</summary>
    public string FolderPath { get; private set; } = TelemetryFolder.NextToProgram;

    /// <summary>Verdadeiro quando a pasta da raiz do programa não aceitou escrita.</summary>
    public bool IsUsingFallbackFolder { get; private set; }

    /// <summary>Arquivo em gravação; vazio quando nada está sendo gravado.</summary>
    public string FilePath
    {
        get
        {
            lock (_gate)
            {
                return _writer?.FilePath ?? string.Empty;
            }
        }
    }

    /// <summary>Último arquivo fechado, para a tela oferecer "abrir o arquivo".</summary>
    public string LastFilePath
    {
        get
        {
            lock (_gate)
            {
                return _lastFilePath;
            }
        }
    }

    /// <summary>Verdadeiro enquanto o arquivo está aberto.</summary>
    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _writer is not null;
            }
        }
    }

    /// <summary>Amostras já gravadas no arquivo em uso.</summary>
    public int SampleCount
    {
        get
        {
            lock (_gate)
            {
                return _writer?.WrittenCount ?? 0;
            }
        }
    }

    /// <summary>Tempo desde o início da gravação, em segundos.</summary>
    public double ElapsedSeconds
    {
        get
        {
            lock (_gate)
            {
                return _writer is null ? 0 : Math.Max(0, _clock.Elapsed.TotalSeconds - _startedAtSeconds);
            }
        }
    }

    /// <summary>Perfil em uso (o do jogo em gravação).</summary>
    public TelemetryProfile Profile
    {
        get
        {
            lock (_gate)
            {
                return _profile;
            }
        }
    }

    /// <summary>Mensagem curta do estado, exibida na tela de telemetria.</summary>
    public string StatusMessage
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    /// <summary>
    /// Começa um arquivo novo para o jogo em uso. Devolve falso (e explica em
    /// <see cref="StatusMessage"/>) quando a pasta ou o arquivo não podem ser criados.
    /// </summary>
    public bool Start(TelemetryProfile profile, string simulatorName)
    {
        ArgumentNullException.ThrowIfNull(profile);

        TelemetryFolderLocation folder = TelemetryFolder.Ensure();

        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            if (_writer is not null)
            {
                _status = "Já existe uma gravação em andamento.";
                return false;
            }

            FolderPath = folder.Path;
            IsUsingFallbackFolder = folder.IsFallback;
            _profile = profile;
            _simulatorName = string.IsNullOrWhiteSpace(simulatorName) ? profile.DisplayName : simulatorName;

            string path;

            try
            {
                path = TelemetryLogFile.BuildUniquePath(folder.Path, profile, DateTime.Now);
                _writer = TelemetryLogWriter.Open(path, profile, _simulatorName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _writer = null;
                _status = "Não foi possível criar o arquivo: " + exception.Message;
                return false;
            }

            _startedAtSeconds = _clock.Elapsed.TotalSeconds;
            _nextSampleSeconds = _startedAtSeconds;
            _lastFilePath = path;

            _status = profile.IsAuto
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"Gravando a {profile.RecordingRateHz:0.#} Hz — aguardando o jogo.")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"Gravando {profile.DisplayName} a {profile.RecordingRateHz:0.#} Hz.");

            return true;
        }
    }

    /// <summary>
    /// Enfileira um instantâneo do carro, respeitando a taxa do perfil. Chamado a 60 Hz pelo
    /// laço da interface; sem jogo conectado nada é gravado.
    /// </summary>
    public void Observe(CarTelemetry car)
    {
        if (!car.IsConnected)
        {
            return;
        }

        TelemetryLogWriter? writer;
        TelemetryLogSample sample;

        lock (_gate)
        {
            if (_writer is null || _disposed)
            {
                return;
            }

            double now = _clock.Elapsed.TotalSeconds;

            if (now < _nextSampleSeconds)
            {
                return;
            }

            double interval = _profile.RecordingRateHz > 0 ? 1.0 / _profile.RecordingRateHz : 0;

            // Se a interface atrasou, não acumula dívida: a próxima amostra sai agora + intervalo.
            _nextSampleSeconds = now + interval;

            writer = _writer;
            sample = TelemetryLogSample.From(car, now - _startedAtSeconds);
        }

        writer.Append(sample);
    }

    /// <summary>Fecha o arquivo e devolve quantas amostras foram gravadas.</summary>
    public int Stop()
    {
        TelemetryLogWriter? writer;

        lock (_gate)
        {
            writer = _writer;
            _writer = null;
        }

        if (writer is null)
        {
            return 0;
        }

        int written = writer.Close();
        string path = writer.FilePath;
        string fault = writer.Fault;
        writer.Dispose();

        lock (_gate)
        {
            _lastFilePath = path;

            _status = fault.Length > 0
                ? "Gravação interrompida: " + fault
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"Gravado: {written} amostras em {Path.GetFileName(path)}.");
        }

        return written;
    }

    /// <summary>Zera a mensagem de estado (ao trocar de jogo, por exemplo).</summary>
    public void ClearStatus()
    {
        lock (_gate)
        {
            if (_writer is null)
            {
                _status = "Pronto para gravar.";
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
    }
}
