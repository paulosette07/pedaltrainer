using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Descobre sozinho em que eixo cada pedal está: o primeiro eixo que se mexe é o freio,
/// o segundo é o acelerador. Assim a Simsonn funciona no primeiro toque, sem o usuário
/// ter de adivinhar índices de eixo.
/// </summary>
public sealed class AutoAxisMapper
{
    /// <summary>Movimento mínimo (unidades brutas) para considerar que um eixo é um pedal.</summary>
    public const int MinimumAxisSpan = PedalAxisDetector.MinimumMovementSpan;

    private readonly AxisRangeTracker[] _trackers = new AxisRangeTracker[RawAxesSnapshot.MaxAxes];
    private readonly int[] _restingValues = new int[RawAxesSnapshot.MaxAxes];
    private readonly bool[] _hasRestingValue = new bool[RawAxesSnapshot.MaxAxes];

    private int _brakeAxis = -1;
    private int _throttleAxis = -1;

    public AutoAxisMapper()
    {
        for (var axis = 0; axis < _trackers.Length; axis++)
        {
            _trackers[axis] = new AxisRangeTracker(MinimumAxisSpan);
        }
    }

    /// <summary>Eixo atribuído ao freio, ou -1 enquanto nenhum eixo se moveu.</summary>
    public int BrakeAxis => _brakeAxis;

    /// <summary>Eixo atribuído ao acelerador, ou -1 enquanto não houver um segundo eixo.</summary>
    public int ThrottleAxis => _throttleAxis;

    public bool HasBrake => _brakeAxis >= 0;

    public bool HasThrottle => _throttleAxis >= 0;

    /// <summary>Faixa observada de um eixo (para diagnóstico na interface).</summary>
    public AxisRangeTracker Tracker(int axis) =>
        axis >= 0 && axis < _trackers.Length ? _trackers[axis] : new AxisRangeTracker();

    public void Reset()
    {
        for (var axis = 0; axis < _trackers.Length; axis++)
        {
            _trackers[axis].Reset();
            _restingValues[axis] = 0;
            _hasRestingValue[axis] = false;
        }

        _brakeAxis = -1;
        _throttleAxis = -1;
    }

    /// <summary>Observa um instantâneo e atribui os eixos assim que eles se movem.</summary>
    public void Observe(in RawAxesSnapshot raw)
    {
        int count = Math.Min(raw.Count, RawAxesSnapshot.MaxAxes);

        for (var axis = 0; axis < count; axis++)
        {
            int value = raw.Get(axis);

            if (!_hasRestingValue[axis])
            {
                _restingValues[axis] = value;
                _hasRestingValue[axis] = true;
            }

            _trackers[axis].Observe(value);
        }

        if (_brakeAxis < 0)
        {
            int candidate = FindMovedAxis(exclude: -1);
            if (candidate >= 0)
            {
                _brakeAxis = candidate;
            }

            return;
        }

        if (_throttleAxis < 0)
        {
            int candidate = FindMovedAxis(exclude: _brakeAxis);
            if (candidate >= 0)
            {
                _throttleAxis = candidate;
            }
        }
    }

    /// <summary>Curso do freio (0 a 100) a partir da faixa observada.</summary>
    public double BrakePercent(in RawAxesSnapshot raw) => PercentOf(_brakeAxis, raw);

    /// <summary>Curso do acelerador (0 a 100) a partir da faixa observada.</summary>
    public double ThrottlePercent(in RawAxesSnapshot raw) => PercentOf(_throttleAxis, raw);

    /// <summary>
    /// Monta uma calibração a partir do que foi aprendido, para que a próxima execução
    /// já comece com os eixos certos.
    /// </summary>
    public bool TryBuildCalibration(
        Guid deviceGuid,
        string deviceName,
        PedalCalibration template,
        out PedalCalibration calibration)
    {
        calibration = template.Clone();

        if (!HasBrake)
        {
            return false;
        }

        AxisRangeTracker brake = _trackers[_brakeAxis];
        if (!brake.HasUsableRange)
        {
            return false;
        }

        (int brakeMin, int brakeMax) = brake.ToCalibratedRange(2);

        calibration.DeviceGuid = deviceGuid;
        calibration.DeviceName = deviceName;
        calibration.BrakeAxis = _brakeAxis;
        calibration.BrakeMin = brakeMin;
        calibration.BrakeMax = brakeMax;
        calibration.InvertBrake = IsInverted(_brakeAxis);
        calibration.BrakeDeadzonePercent = template.BrakeDeadzonePercent;
        calibration.BrakeExponent = template.BrakeExponent;

        if (HasThrottle)
        {
            AxisRangeTracker throttle = _trackers[_throttleAxis];
            if (throttle.HasUsableRange)
            {
                (int throttleMin, int throttleMax) = throttle.ToCalibratedRange(2);
                calibration.ThrottleAxis = _throttleAxis;
                calibration.ThrottleMin = throttleMin;
                calibration.ThrottleMax = throttleMax;
                calibration.InvertThrottle = IsInverted(_throttleAxis);
            }
        }

        calibration.IsCalibrated = true;
        return true;
    }

    private int FindMovedAxis(int exclude)
    {
        int bestAxis = -1;
        int bestSpan = 0;

        for (var axis = 0; axis < _trackers.Length; axis++)
        {
            if (axis == exclude)
            {
                continue;
            }

            AxisRangeTracker tracker = _trackers[axis];
            if (!tracker.HasUsableRange)
            {
                continue;
            }

            if (tracker.Span > bestSpan)
            {
                bestSpan = tracker.Span;
                bestAxis = axis;
            }
        }

        return bestAxis;
    }

    private double PercentOf(int axis, in RawAxesSnapshot raw)
    {
        if (axis < 0)
        {
            return 0;
        }

        AxisRangeTracker tracker = _trackers[axis];
        if (!tracker.HasUsableRange)
        {
            return 0;
        }

        (int min, int max) = tracker.ToCalibratedRange(2);

        return PedalCalibration.AxisToPercent(
            raw.Get(axis),
            min,
            max,
            IsInverted(axis),
            deadzonePercent: 0,
            exponent: 1.0);
    }

    /// <summary>
    /// Um pedal invertido repousa no topo da faixa observada. Sem isso, uma Load Cell
    /// ligada ao contrário ficaria travada em 100%.
    /// </summary>
    public bool IsInverted(int axis)
    {
        if (!_hasRestingValue[axis])
        {
            return false;
        }

        AxisRangeTracker tracker = _trackers[axis];
        if (!tracker.HasUsableRange)
        {
            return false;
        }

        int resting = _restingValues[axis];

        return Math.Abs((long)resting - tracker.Max) < Math.Abs((long)resting - tracker.Min);
    }
}
