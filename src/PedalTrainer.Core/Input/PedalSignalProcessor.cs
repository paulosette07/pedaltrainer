using System;
using System.Threading;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Converte eixos brutos em percentual de pedal: aplica a calibração salva ou, quando
/// ela ainda não existe, o mapa automático de eixos (o primeiro pedal a se mover é o freio).
/// Aplica também um filtro exponencial leve.
/// </summary>
public sealed class PedalSignalProcessor
{
    private readonly AutoAxisMapper _autoMapper = new();

    private PedalCalibration _calibration = new();
    private double _filteredBrake;
    private double _filteredThrottle;
    private bool _hasState;

    /// <summary>Calibração em uso. Nula nunca é aceita; volta ao perfil padrão.</summary>
    public PedalCalibration Calibration
    {
        get => Volatile.Read(ref _calibration);
        set => Volatile.Write(ref _calibration, value ?? new PedalCalibration());
    }

    /// <summary>Mapa automático de eixos (ativo enquanto não houver calibração salva).</summary>
    public AutoAxisMapper AutoMapper => _autoMapper;

    /// <summary>Verdadeiro quando os eixos estão sendo descobertos automaticamente.</summary>
    public bool IsAutoMapping => !Calibration.IsCalibrated;

    /// <summary>Faixa observada do freio (para o assistente de calibração).</summary>
    public AxisRangeTracker BrakeRange => _autoMapper.HasBrake
        ? _autoMapper.Tracker(_autoMapper.BrakeAxis)
        : _autoMapper.Tracker(Calibration.BrakeAxis);

    /// <summary>Faixa observada do acelerador (para o assistente de calibração).</summary>
    public AxisRangeTracker ThrottleRange => _autoMapper.HasThrottle
        ? _autoMapper.Tracker(_autoMapper.ThrottleAxis)
        : _autoMapper.Tracker(Calibration.ThrottleAxis);

    /// <summary>Últimos valores brutos por eixo, para diagnóstico na tela de calibração.</summary>
    public RawAxesSnapshot LastRaw { get; private set; } = RawAxesSnapshot.Empty;

    public PedalReading Process(in RawAxesSnapshot raw, double timestampSeconds)
    {
        PedalCalibration calibration = Calibration;
        LastRaw = raw;

        _autoMapper.Observe(raw);

        double brakeTarget;
        double throttleTarget;

        if (calibration.IsCalibrated)
        {
            brakeTarget = calibration.BrakePercent(raw);
            throttleTarget = calibration.ThrottlePercent(raw);
        }
        else
        {
            brakeTarget = _autoMapper.BrakePercent(raw);
            throttleTarget = _autoMapper.ThrottlePercent(raw);
        }

        ApplyFilter(calibration, brakeTarget, throttleTarget, out double brake, out double throttle);

        return new PedalReading(
            Math.Clamp(brake, 0, 100),
            Math.Clamp(throttle, 0, 100),
            timestampSeconds);
    }

    /// <summary>
    /// Monta uma calibração a partir dos eixos descobertos automaticamente.
    /// Devolve falso enquanto nenhum pedal tiver sido movido.
    /// </summary>
    public bool TryBuildLearnedCalibration(
        Guid deviceGuid,
        string deviceName,
        out PedalCalibration calibration) =>
        _autoMapper.TryBuildCalibration(deviceGuid, deviceName, Calibration, out calibration);

    public void Reset()
    {
        _autoMapper.Reset();
        _filteredBrake = 0;
        _filteredThrottle = 0;
        _hasState = false;
    }

    private void ApplyFilter(
        PedalCalibration calibration,
        double brakeTarget,
        double throttleTarget,
        out double brake,
        out double throttle)
    {
        if (!_hasState)
        {
            _filteredBrake = brakeTarget;
            _filteredThrottle = throttleTarget;
            _hasState = true;
            brake = brakeTarget;
            throttle = throttleTarget;
            return;
        }

        double alpha = Math.Clamp(calibration.SmoothingAlpha, 0.05, 1.0);
        _filteredBrake += (brakeTarget - _filteredBrake) * alpha;
        _filteredThrottle += (throttleTarget - _filteredThrottle) * alpha;

        brake = _filteredBrake;
        throttle = _filteredThrottle;
    }
}
