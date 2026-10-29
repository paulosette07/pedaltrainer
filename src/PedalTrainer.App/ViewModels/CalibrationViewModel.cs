using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PedalTrainer.Core.Input;
using PedalTrainer.Core.Input.Pedals;
using PedalTrainer.Core.Models;

namespace PedalTrainer.App.ViewModels;

/// <summary>
/// Tela de calibração dos pedais: mostra o que o app descobriu sozinho, permite capturar
/// a faixa real de cada pedal e ajustar zona morta e curva de resposta.
/// </summary>
public sealed class CalibrationViewModel : ObservableObject
{
    private const double WizardSeconds = 4.0;

    private readonly IShellActions _shell;
    private readonly List<RawAxesSnapshot> _wizardFrames = new();

    private PedalCalibration _calibration;
    private bool _wizardRunning;
    private bool _wizardForBrake;
    private double _wizardEndsAt;

    private int _brakeAxis;
    private int _throttleAxis;
    private int _brakeMin;
    private int _brakeMax;
    private int _throttleMin;
    private int _throttleMax;
    private double _brakeDeadzonePercent;
    private double _throttleDeadzonePercent;
    private double _brakeExponent;
    private double _throttleExponent;
    private bool _invertBrake;
    private bool _invertThrottle;
    private double _smoothingAlpha;
    private bool _isCalibrated;
    private string _pedalModelId = string.Empty;

    private string _statusMessage = "Pise em um pedal para ver a leitura bruta.";
    private string _liveBrakeText = "0%";
    private string _liveThrottleText = "0%";
    private string _rawAxesText = "sem leitura";
    private string _wizardMessage = string.Empty;
    private string _detectedAxesText = "Pise no freio para o app descobrir o eixo.";
    private string _autoMapperText = string.Empty;

    public CalibrationViewModel(IShellActions shell)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));

        AxisOptions = new ObservableCollection<string>();
        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(ResetToObserved);
        AutoDetectBrakeCommand = new RelayCommand(() => StartWizard(brake: true));
        AutoDetectThrottleCommand = new RelayCommand(() => StartWizard(brake: false));
        UseDetectedCommand = new RelayCommand(UseDetectedAxes);

        _calibration = new PedalCalibration();
        LoadFromCalibration(_calibration);
    }

    public ObservableCollection<string> AxisOptions { get; }

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand ResetCommand { get; }

    public IRelayCommand AutoDetectBrakeCommand { get; }

    public IRelayCommand AutoDetectThrottleCommand { get; }

    /// <summary>Copia os eixos descobertos automaticamente para os campos da tela.</summary>
    public IRelayCommand UseDetectedCommand { get; }

    public string DeviceName => _shell.Engine.Device?.Descriptor.DisplayName ?? "Modo simulado";

    /// <summary>Modelo de pedal em que a curva e o filtro atuais se baseiam.</summary>
    public string PedalModelName => PedalModelCatalog.Find(_pedalModelId).DisplayName;

    public int BrakeAxis
    {
        get => _brakeAxis;
        set => SetProperty(ref _brakeAxis, Math.Max(0, value));
    }

    public int ThrottleAxis
    {
        get => _throttleAxis;
        set => SetProperty(ref _throttleAxis, Math.Max(0, value));
    }

    public int BrakeMin
    {
        get => _brakeMin;
        set => SetProperty(ref _brakeMin, value);
    }

    public int BrakeMax
    {
        get => _brakeMax;
        set => SetProperty(ref _brakeMax, value);
    }

    public int ThrottleMin
    {
        get => _throttleMin;
        set => SetProperty(ref _throttleMin, value);
    }

    public int ThrottleMax
    {
        get => _throttleMax;
        set => SetProperty(ref _throttleMax, value);
    }

    public double BrakeDeadzonePercent
    {
        get => _brakeDeadzonePercent;
        set => SetProperty(ref _brakeDeadzonePercent, Math.Clamp(value, 0, 40));
    }

    public double ThrottleDeadzonePercent
    {
        get => _throttleDeadzonePercent;
        set => SetProperty(ref _throttleDeadzonePercent, Math.Clamp(value, 0, 40));
    }

    public double BrakeExponent
    {
        get => _brakeExponent;
        set => SetProperty(ref _brakeExponent, Math.Clamp(value, 0.3, 3));
    }

    public double ThrottleExponent
    {
        get => _throttleExponent;
        set => SetProperty(ref _throttleExponent, Math.Clamp(value, 0.3, 3));
    }

    public bool InvertBrake
    {
        get => _invertBrake;
        set => SetProperty(ref _invertBrake, value);
    }

    public bool InvertThrottle
    {
        get => _invertThrottle;
        set => SetProperty(ref _invertThrottle, value);
    }

    public double SmoothingAlpha
    {
        get => _smoothingAlpha;
        set => SetProperty(ref _smoothingAlpha, Math.Clamp(value, 0.1, 1));
    }

    public bool IsCalibrated
    {
        get => _isCalibrated;
        private set => SetProperty(ref _isCalibrated, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LiveBrakeText
    {
        get => _liveBrakeText;
        private set => SetProperty(ref _liveBrakeText, value);
    }

    public string LiveThrottleText
    {
        get => _liveThrottleText;
        private set => SetProperty(ref _liveThrottleText, value);
    }

    public string RawAxesText
    {
        get => _rawAxesText;
        private set => SetProperty(ref _rawAxesText, value);
    }

    public string WizardMessage
    {
        get => _wizardMessage;
        private set => SetProperty(ref _wizardMessage, value);
    }

    /// <summary>O que o app descobriu sozinho sobre os eixos.</summary>
    public string DetectedAxesText
    {
        get => _detectedAxesText;
        private set => SetProperty(ref _detectedAxesText, value);
    }

    /// <summary>Indica se os eixos vêm do modo automático ou de uma calibração salva.</summary>
    public string AutoMapperText
    {
        get => _autoMapperText;
        private set => SetProperty(ref _autoMapperText, value);
    }

    /// <summary>Recarrega a calibração salva (chamado quando a tela é aberta).</summary>
    public void ReloadFromStore(PedalCalibration calibration)
    {
        _calibration = calibration?.Clone() ?? new PedalCalibration();
        LoadFromCalibration(_calibration);
        RebuildAxisOptions();
        Tick();
    }

    /// <summary>Chamado a cada quadro pela janela principal.</summary>
    public void Tick()
    {
        TrainingFrame frame = _shell.Engine.Latest;
        RawAxesSnapshot raw = _shell.Engine.LastRawAxes;

        LiveBrakeText = FormatPercent(frame.Pedals.BrakePercent);
        LiveThrottleText = FormatPercent(frame.Pedals.ThrottlePercent);
        RawAxesText = DescribeRaw(raw);
        UpdateDetectedAxesText();

        if (!_wizardRunning)
        {
            return;
        }

        if (raw.Count > 0)
        {
            _wizardFrames.Add(raw);
        }

        if (_shell.Engine.NowSeconds < _wizardEndsAt)
        {
            return;
        }

        _wizardRunning = false;
        FinishWizard();
    }

    private void UpdateDetectedAxesText()
    {
        AutoAxisMapper mapper = _shell.Engine.AutoMapper;

        AutoMapperText = _shell.Engine.IsAutoMapping
            ? "Modo automático: o app escolhe os eixos pelo movimento."
            : "Usando a calibração salva.";

        if (!mapper.HasBrake)
        {
            DetectedAxesText = "Pise no freio para o app descobrir o eixo.";
            return;
        }

        AxisRangeTracker brakeTracker = mapper.Tracker(mapper.BrakeAxis);
        string brake = $"freio: eixo {mapper.BrakeAxis} ({brakeTracker.Min}–{brakeTracker.Max})";

        string throttle;
        if (mapper.HasThrottle)
        {
            AxisRangeTracker throttleTracker = mapper.Tracker(mapper.ThrottleAxis);
            throttle = $"acelerador: eixo {mapper.ThrottleAxis} ({throttleTracker.Min}–{throttleTracker.Max})";
        }
        else
        {
            throttle = "acelerador: ainda não movido";
        }

        DetectedAxesText = $"{brake} · {throttle}";
    }

    private void UseDetectedAxes()
    {
        AutoAxisMapper mapper = _shell.Engine.AutoMapper;

        if (!mapper.HasBrake)
        {
            WizardMessage = "Nenhum eixo se moveu ainda — pise no freio e tente de novo.";
            return;
        }

        BrakeAxis = mapper.BrakeAxis;
        (int brakeMin, int brakeMax) = mapper.Tracker(mapper.BrakeAxis).ToCalibratedRange(2);
        BrakeMin = brakeMin;
        BrakeMax = brakeMax;
        InvertBrake = mapper.IsInverted(mapper.BrakeAxis);

        if (mapper.HasThrottle)
        {
            ThrottleAxis = mapper.ThrottleAxis;
            (int throttleMin, int throttleMax) = mapper.Tracker(mapper.ThrottleAxis).ToCalibratedRange(2);
            ThrottleMin = throttleMin;
            ThrottleMax = throttleMax;
            InvertThrottle = mapper.IsInverted(mapper.ThrottleAxis);
        }

        StatusMessage = "Eixos detectados aplicados — clique em Salvar calibração para manter.";
    }

    private void StartWizard(bool brake)
    {
        if (_shell.Engine.Device is null)
        {
            WizardMessage = "Conecte um controlador real para calibrar (o modo simulado já vem calibrado).";
            return;
        }

        _wizardFrames.Clear();
        _wizardRunning = true;
        _wizardForBrake = brake;
        _wizardEndsAt = _shell.Engine.NowSeconds + WizardSeconds;

        WizardMessage = brake
            ? $"Pressione o FREIO até o fundo, 3 vezes, durante {WizardSeconds:0} s..."
            : $"Pressione o ACELERADOR até o fundo, 3 vezes, durante {WizardSeconds:0} s...";
    }

    private void FinishWizard()
    {
        int excluded = _wizardForBrake ? -1 : BrakeAxis;
        int axis = PedalAxisDetector.DetectDominantAxis(_wizardFrames, excluded);

        if (axis < 0)
        {
            WizardMessage = "Não foi possível detectar movimento no pedal. Tente novamente com mais curso.";
            return;
        }

        var tracker = new AxisRangeTracker();
        foreach (RawAxesSnapshot frame in _wizardFrames)
        {
            tracker.Observe(frame.Get(axis));
        }

        (int min, int max) = tracker.ToCalibratedRange(1);

        if (_wizardForBrake)
        {
            BrakeAxis = axis;
            BrakeMin = min;
            BrakeMax = max;
        }
        else
        {
            ThrottleAxis = axis;
            ThrottleMin = min;
            ThrottleMax = max;
        }

        WizardMessage = _wizardForBrake
            ? $"Freio no eixo {axis} ({min} a {max}). Confira os valores e salve."
            : $"Acelerador no eixo {axis} ({min} a {max}). Confira os valores e salve.";
    }

    private void Save()
    {
        _calibration = BuildCalibration();
        _shell.ApplyCalibration(_calibration, persist: true);
        IsCalibrated = _calibration.IsCalibrated;
        StatusMessage = "Calibração aplicada e salva.";
    }

    private void ResetToObserved()
    {
        var calibration = new PedalCalibration
        {
            DeviceGuid = _shell.SelectedDeviceGuid ?? Guid.Empty,
            DeviceName = DeviceName,
            PedalModelId = _pedalModelId,
            BrakeAxis = BrakeAxis,
            ThrottleAxis = ThrottleAxis
        };

        AxisRangeTracker brakeRange = _shell.Engine.BrakeRange;
        AxisRangeTracker throttleRange = _shell.Engine.ThrottleRange;

        if (brakeRange.HasUsableRange)
        {
            (int min, int max) = brakeRange.ToCalibratedRange(2);
            calibration.BrakeMin = min;
            calibration.BrakeMax = max;
        }

        if (throttleRange.HasUsableRange)
        {
            (int min, int max) = throttleRange.ToCalibratedRange(2);
            calibration.ThrottleMin = min;
            calibration.ThrottleMax = max;
        }

        calibration.IsCalibrated = brakeRange.HasUsableRange || throttleRange.HasUsableRange;
        LoadFromCalibration(calibration);
        StatusMessage = calibration.IsCalibrated
            ? "Faixa observada aplicada. Salve para manter."
            : "Ainda não há curso suficiente observado. Pressione os pedais e tente de novo.";
    }

    private PedalCalibration BuildCalibration() => new()
    {
        DeviceGuid = _shell.SelectedDeviceGuid ?? Guid.Empty,
        DeviceName = DeviceName,
        BrakeAxis = BrakeAxis,
        ThrottleAxis = ThrottleAxis,
        BrakeMin = Math.Min(BrakeMin, BrakeMax),
        BrakeMax = Math.Max(BrakeMin, BrakeMax),
        ThrottleMin = Math.Min(ThrottleMin, ThrottleMax),
        ThrottleMax = Math.Max(ThrottleMin, ThrottleMax),
        BrakeDeadzonePercent = BrakeDeadzonePercent,
        ThrottleDeadzonePercent = ThrottleDeadzonePercent,
        BrakeExponent = BrakeExponent,
        ThrottleExponent = ThrottleExponent,
        InvertBrake = InvertBrake,
        InvertThrottle = InvertThrottle,
        SmoothingAlpha = SmoothingAlpha,
        PedalModelId = _pedalModelId,
        IsCalibrated = true
    };

    private void LoadFromCalibration(PedalCalibration calibration)
    {
        BrakeAxis = calibration.BrakeAxis;
        ThrottleAxis = calibration.ThrottleAxis;
        BrakeMin = calibration.BrakeMin;
        BrakeMax = calibration.BrakeMax;
        ThrottleMin = calibration.ThrottleMin;
        ThrottleMax = calibration.ThrottleMax;
        BrakeDeadzonePercent = calibration.BrakeDeadzonePercent;
        ThrottleDeadzonePercent = calibration.ThrottleDeadzonePercent;
        BrakeExponent = calibration.BrakeExponent;
        ThrottleExponent = calibration.ThrottleExponent;
        InvertBrake = calibration.InvertBrake;
        InvertThrottle = calibration.InvertThrottle;
        SmoothingAlpha = calibration.SmoothingAlpha;
        IsCalibrated = calibration.IsCalibrated;

        _pedalModelId = calibration.PedalModelId;
        OnPropertyChanged(nameof(PedalModelName));
    }

    private void RebuildAxisOptions()
    {
        AxisOptions.Clear();
        PedalDeviceDescriptor? descriptor = _shell.Engine.Device?.Descriptor;

        if (descriptor is null)
        {
            for (var i = 0; i < 3; i++)
            {
                AxisOptions.Add($"Eixo {i}");
            }

            return;
        }

        for (var i = 0; i < descriptor.AxisNames.Length; i++)
        {
            AxisOptions.Add($"{i}: {descriptor.AxisNames[i]}");
        }
    }

    private static string FormatPercent(double value) =>
        value.ToString("0", CultureInfo.CurrentCulture) + "%";

    private static string DescribeRaw(RawAxesSnapshot raw)
    {
        if (raw.Count == 0)
        {
            return "sem leitura";
        }

        var parts = new List<string>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            parts.Add($"{i}:{raw.Get(i)}");
        }

        return string.Join("  ", parts);
    }
}
