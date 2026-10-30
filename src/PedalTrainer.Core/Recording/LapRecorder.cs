using System;
using System.Collections.Generic;
using System.Globalization;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Recording;

/// <summary>
/// Grava uma volta do simulador conectado (iRacing, ACC, AC EVO, AMS2 ou LMU) e a converte
/// em volta de referência. Arma na troca de volta e fecha na troca seguinte, então a
/// referência sempre cobre uma volta inteira. A distância em metros sai da integração da
/// velocidade, calibrada pelo percentual de pista efetivamente percorrido.
/// </summary>
public sealed class LapRecorder
{
    /// <summary>Fração da pista que precisa ter sido percorrida para aceitar a volta.</summary>
    public const double MinimumLapCoverage = 0.9;

    /// <summary>Duração máxima aceita para uma volta (10 minutos).</summary>
    public const double MaximumLapSeconds = 600.0;

    private const int MinimumSamples = 40;
    private const double MaximumFrameSeconds = 1.0;
    private const double MaximumFramePercent = 0.05;
    private const double WrapHighPercent = 0.9;
    private const double WrapLowPercent = 0.1;
    private const double TimeEpsilonSeconds = 1e-4;
    private const double MinimumTrackLengthMeters = 200.0;
    private const double MaximumTrackLengthMeters = 30000.0;

    private readonly object _gate = new();
    private readonly List<LapRecorderSample> _samples = new();

    private LapRecordingState _state = LapRecordingState.Idle;
    private ReferenceLap? _completedLap;
    private string _status = "Nenhuma gravação em andamento.";
    private int _startLapNumber;
    private int _lastLapNumber;
    private double _lastSessionTime;
    private double _lastLapPercent;
    private double _lapStartSessionTime;
    private double _lapPercentTravelled;
    private double _travelledMeters;
    private double _progress;
    private bool _hasSample;

    /// <summary>Nome do simulador em uso, usado nas mensagens de estado.</summary>
    public string SimulatorName { get; set; } = "simulador";

    /// <summary>Estado atual do gravador.</summary>
    public LapRecordingState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _state == LapRecordingState.Recording;
            }
        }
    }

    /// <summary>Verdadeiro entre <see cref="Start"/> e o fim da gravação.</summary>
    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _state is LapRecordingState.WaitingForLapStart or LapRecordingState.Recording;
            }
        }
    }

    /// <summary>Fração da pista já percorrida na volta em gravação (0 a 1).</summary>
    public double Progress
    {
        get
        {
            lock (_gate)
            {
                return _progress;
            }
        }
    }

    /// <summary>Quantidade de amostras já capturadas na volta atual.</summary>
    public int SampleCount
    {
        get
        {
            lock (_gate)
            {
                return _samples.Count;
            }
        }
    }

    /// <summary>Mensagem legível sobre o estado da gravação.</summary>
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

    /// <summary>Arma o gravador. A captura começa quando o carro cruzar a linha.</summary>
    public void Start()
    {
        lock (_gate)
        {
            _samples.Clear();
            _completedLap = null;
            _hasSample = false;
            _lastSessionTime = 0;
            _lastLapNumber = 0;
            _lastLapPercent = 0;
            _lapStartSessionTime = 0;
            _lapPercentTravelled = 0;
            _travelledMeters = 0;
            _progress = 0;
            _state = LapRecordingState.WaitingForLapStart;
            _status = "Aguardando a linha: complete a volta atual para gravar a próxima.";
        }
    }

    /// <summary>Cancela a gravação em andamento, se houver.</summary>
    public void Cancel(string? message = null)
    {
        lock (_gate)
        {
            if (_state is not (LapRecordingState.WaitingForLapStart or LapRecordingState.Recording))
            {
                return;
            }

            Abort(message ?? "Gravação cancelada.");
        }
    }

    /// <summary>
    /// Consome um instante da telemetria do carro. Chamado pelo laço do motor;
    /// leituras repetidas do mesmo instante são ignoradas.
    /// </summary>
    public void Observe(in CarTelemetry car)
    {
        lock (_gate)
        {
            if (_state is not (LapRecordingState.WaitingForLapStart or LapRecordingState.Recording))
            {
                return;
            }

            if (!car.IsConnected)
            {
                Abort($"Gravação cancelada: o {SimulatorName} não está publicando telemetria.");
                return;
            }

            if (_hasSample && car.SessionTimeSeconds <= _lastSessionTime + TimeEpsilonSeconds)
            {
                return;
            }

            if (!_hasSample)
            {
                RememberSample(car);
                _status = DescribeWaiting(car);
                return;
            }

            bool sessionRestarted = car.LapNumber < _lastLapNumber;
            bool crossedLine = HasCrossedStartLine(
                _lastLapNumber,
                car.LapNumber,
                _lastLapPercent,
                car.LapDistancePercent);

            double previousSessionTime = _lastSessionTime;
            double previousLapPercent = _lastLapPercent;

            RememberSample(car);

            if (sessionRestarted)
            {
                Abort($"Gravação cancelada: a sessão do {SimulatorName} foi reiniciada.");
                return;
            }

            if (crossedLine)
            {
                if (_state == LapRecordingState.WaitingForLapStart)
                {
                    BeginLap(car);
                }
                else
                {
                    CompleteLap();
                }

                return;
            }

            if (_state == LapRecordingState.Recording)
            {
                AppendSample(car, previousSessionTime, previousLapPercent);
            }
            else
            {
                _status = DescribeWaiting(car);
            }
        }
    }

    /// <summary>Retira a volta gravada, se houver uma pronta.</summary>
    public bool TryTakeCompletedLap(out ReferenceLap lap)
    {
        lock (_gate)
        {
            if (_completedLap is null)
            {
                lap = null!;
                return false;
            }

            lap = _completedLap;
            _completedLap = null;
            _state = LapRecordingState.Idle;
            return true;
        }
    }

    private void BeginLap(in CarTelemetry car)
    {
        _samples.Clear();
        _startLapNumber = car.LapNumber;
        _lapStartSessionTime = car.SessionTimeSeconds;
        _lapPercentTravelled = 0;
        _travelledMeters = 0;
        _progress = 0;
        _state = LapRecordingState.Recording;
        _status = string.Create(CultureInfo.InvariantCulture, $"Gravando volta {car.LapNumber}...");
        _samples.Add(LapRecorderSample.From(in car));
    }

    private void AppendSample(in CarTelemetry car, double previousSessionTime, double previousLapPercent)
    {
        double elapsed = car.SessionTimeSeconds - _lapStartSessionTime;
        if (elapsed > MaximumLapSeconds)
        {
            Abort("Gravação cancelada: a volta passou de 10 minutos.");
            return;
        }

        double deltaSeconds = Math.Clamp(car.SessionTimeSeconds - previousSessionTime, 0, MaximumFrameSeconds);
        _travelledMeters += car.SpeedKmh / 3.6 * deltaSeconds;

        double deltaPercent = PercentDelta(previousLapPercent, car.LapDistancePercent);
        if (deltaPercent is > 0 and < MaximumFramePercent)
        {
            _lapPercentTravelled += deltaPercent;
        }

        _samples.Add(LapRecorderSample.From(in car));
        _progress = Math.Clamp(_lapPercentTravelled, 0, 1);
        _status = string.Create(
            CultureInfo.InvariantCulture,
            $"Gravando volta {_startLapNumber}: {_progress * 100:0}% ({_samples.Count} amostras).");
    }

    private void CompleteLap()
    {
        if (_samples.Count < MinimumSamples || _lapPercentTravelled < MinimumLapCoverage)
        {
            Abort(string.Create(
                CultureInfo.InvariantCulture,
                $"Volta descartada: apenas {_lapPercentTravelled * 100:0}% da pista foi coberta."));
            return;
        }

        double trackLength = TrackLengthMeters();

        double firstPercent = _samples[0].LapPercent;
        double timeOrigin = _samples[0].SessionTimeSeconds;
        var samples = new List<ReferenceSample>(_samples.Count);

        foreach (LapRecorderSample raw in _samples)
        {
            double covered = raw.LapPercent - firstPercent;
            if (covered < -0.5)
            {
                covered += 1.0;
            }

            covered = Math.Clamp(covered, 0, 1);

            samples.Add(new ReferenceSample(
                TimeSeconds: raw.SessionTimeSeconds - timeOrigin,
                BrakePercent: Math.Clamp(raw.BrakePercent, 0, 100),
                ThrottlePercent: Math.Clamp(raw.ThrottlePercent, 0, 100),
                DistanceMeters: covered * trackLength,
                SpeedKmh: Math.Max(0, raw.SpeedKmh)));
        }

        ReferenceLap lap = ReferenceLap.Create($"Volta gravada {_startLapNumber}", samples, trackLength);

        _completedLap = lap;
        _state = LapRecordingState.Completed;
        _progress = 1;
        _samples.Clear();
        _status = string.Create(
            CultureInfo.InvariantCulture,
            $"Volta gravada: {lap.DurationSeconds:0.0} s, {samples.Count} amostras, {trackLength:0} m de pista.");
    }

    private double TrackLengthMeters()
    {
        if (_travelledMeters <= 0 || _lapPercentTravelled <= 0)
        {
            return 0;
        }

        double length = _travelledMeters / _lapPercentTravelled;
        return length is < MinimumTrackLengthMeters or > MaximumTrackLengthMeters ? 0 : length;
    }

    private void Abort(string message)
    {
        _samples.Clear();
        _state = LapRecordingState.Idle;
        _progress = 0;
        _status = message;
    }

    private void RememberSample(in CarTelemetry car)
    {
        _hasSample = true;
        _lastSessionTime = car.SessionTimeSeconds;
        _lastLapNumber = car.LapNumber;
        _lastLapPercent = car.LapDistancePercent;
    }

    private static string DescribeWaiting(in CarTelemetry car) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Aguardando a linha (volta {car.LapNumber}, {car.LapDistancePercent * 100:0}% da pista).");

    /// <summary>
    /// A linha é detectada pela troca de número de volta (o iRacing incrementa no S/F)
    /// ou, como reserva, pelo salto de 99% para 0% no percentual de pista.
    /// </summary>
    private static bool HasCrossedStartLine(
        int previousLapNumber,
        int currentLapNumber,
        double previousLapPercent,
        double currentLapPercent)
    {
        if (currentLapNumber > previousLapNumber)
        {
            return true;
        }

        return previousLapPercent >= WrapHighPercent && currentLapPercent <= WrapLowPercent;
    }

    private static double PercentDelta(double from, double to)
    {
        double delta = to - from;

        if (delta < -0.5)
        {
            return delta + 1.0;
        }

        if (delta > 0.5)
        {
            return delta - 1.0;
        }

        return delta;
    }

    /// <summary>Amostra crua da volta gravada, antes da conversão para metros.</summary>
    private readonly record struct LapRecorderSample(
        double SessionTimeSeconds,
        double LapPercent,
        double BrakePercent,
        double ThrottlePercent,
        double SpeedKmh)
    {
        public static LapRecorderSample From(in CarTelemetry car) => new(
            car.SessionTimeSeconds,
            car.LapDistancePercent,
            car.BrakePercent,
            car.ThrottlePercent,
            car.SpeedKmh);
    }
}
