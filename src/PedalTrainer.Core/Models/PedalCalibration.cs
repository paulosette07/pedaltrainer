using System;

namespace PedalTrainer.Core.Models;

/// <summary>
/// Calibração de um pedal: faixa bruta do eixo, zona morta, curva de resposta e inversão.
/// Contém apenas matemática pura, o que a torna testável sem hardware.
/// </summary>
public sealed class PedalCalibration
{
    public const int DefaultAxisMin = 0;
    public const int DefaultAxisMax = 65535;
    private const int MinimumSpan = 16;

    public Guid DeviceGuid { get; set; } = Guid.Empty;

    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Modelo de pedal do catálogo em que a calibração se baseia (vazio quando o
    /// piloto não escolheu nenhum). Serve para saber de onde vieram curva e filtro.
    /// </summary>
    public string PedalModelId { get; set; } = string.Empty;

    public int BrakeAxis { get; set; } = 1;

    public int ThrottleAxis { get; set; } = 0;

    public int BrakeMin { get; set; } = DefaultAxisMin;

    public int BrakeMax { get; set; } = DefaultAxisMax;

    public int ThrottleMin { get; set; } = DefaultAxisMin;

    public int ThrottleMax { get; set; } = DefaultAxisMax;

    public double BrakeDeadzonePercent { get; set; }

    public double ThrottleDeadzonePercent { get; set; } = 1.0;

    public double BrakeExponent { get; set; } = 1.0;

    public double ThrottleExponent { get; set; } = 1.0;

    public bool InvertBrake { get; set; }

    public bool InvertThrottle { get; set; }

    /// <summary>Peso do filtro exponencial (0 = travado, 1 = sem filtro).</summary>
    public double SmoothingAlpha { get; set; } = 0.45;

    public bool IsCalibrated { get; set; }

    public double BrakePercent(in RawAxesSnapshot raw) => AxisToPercent(
        raw.Get(BrakeAxis), BrakeMin, BrakeMax, InvertBrake, BrakeDeadzonePercent, BrakeExponent);

    public double ThrottlePercent(in RawAxesSnapshot raw) => AxisToPercent(
        raw.Get(ThrottleAxis), ThrottleMin, ThrottleMax, InvertThrottle, ThrottleDeadzonePercent, ThrottleExponent);

    /// <summary>Converte um valor bruto de eixo em percentual de curso do pedal.</summary>
    public static double AxisToPercent(
        int raw,
        int min,
        int max,
        bool invert,
        double deadzonePercent,
        double exponent)
    {
        if (max - min < MinimumSpan)
        {
            return 0;
        }

        double normalized = Math.Clamp((raw - min) / (double)(max - min), 0, 1);

        if (invert)
        {
            normalized = 1 - normalized;
        }

        double deadzone = Math.Clamp(deadzonePercent, 0, 40) / 100.0;
        if (deadzone > 0)
        {
            normalized = Math.Clamp((normalized - deadzone) / (1 - deadzone), 0, 1);
        }

        double safeExponent = Math.Clamp(exponent, 0.2, 3.0);
        if (Math.Abs(safeExponent - 1.0) > 1e-6)
        {
            normalized = Math.Pow(normalized, safeExponent);
        }

        return normalized * 100.0;
    }

    public PedalCalibration Clone() => (PedalCalibration)MemberwiseClone();

    public static PedalCalibration CreateDefault(Guid deviceGuid, string deviceName) => new()
    {
        DeviceGuid = deviceGuid,
        DeviceName = deviceName,
        IsCalibrated = false
    };
}
