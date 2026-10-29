using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Input.Pedals;

/// <summary>
/// Copia os ajustes recomendados de um modelo de pedal para a calibração em uso.
/// Mexe apenas em zona morta, curva e filtro: a faixa bruta de cada eixo continua
/// sendo a que foi medida naquele exemplar de hardware.
/// </summary>
public static class PedalProfileApplier
{
    /// <summary>
    /// Aplica o perfil do modelo sobre a calibração atual e devolve uma nova instância.
    /// O item "não listado" só limpa a marca do modelo, sem alterar ajustes.
    /// </summary>
    public static PedalCalibration Apply(PedalModel model, PedalCalibration? current)
    {
        ArgumentNullException.ThrowIfNull(model);

        PedalCalibration calibration = current?.Clone() ?? new PedalCalibration();

        if (model.IsCatchAll)
        {
            calibration.PedalModelId = string.Empty;
            return calibration;
        }

        calibration.BrakeExponent = model.RecommendedBrakeExponent;
        calibration.ThrottleExponent = model.RecommendedThrottleExponent;
        calibration.BrakeDeadzonePercent = model.RecommendedBrakeDeadzonePercent;
        calibration.ThrottleDeadzonePercent = model.RecommendedThrottleDeadzonePercent;
        calibration.SmoothingAlpha = model.RecommendedSmoothingAlpha;
        calibration.PedalModelId = model.Id;

        return calibration;
    }

    /// <summary>
    /// Indica se a calibração já está com os ajustes do modelo (usado para não
    /// reaplicar por cima de um ajuste fino feito à mão pelo piloto).
    /// </summary>
    public static bool Matches(PedalModel model, PedalCalibration? calibration)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (calibration is null || model.IsCatchAll)
        {
            return false;
        }

        const double tolerance = 0.001;

        return string.Equals(calibration.PedalModelId, model.Id, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(calibration.BrakeExponent - model.RecommendedBrakeExponent) < tolerance
            && Math.Abs(calibration.ThrottleExponent - model.RecommendedThrottleExponent) < tolerance
            && Math.Abs(calibration.BrakeDeadzonePercent - model.RecommendedBrakeDeadzonePercent) < tolerance
            && Math.Abs(calibration.ThrottleDeadzonePercent - model.RecommendedThrottleDeadzonePercent) < tolerance
            && Math.Abs(calibration.SmoothingAlpha - model.RecommendedSmoothingAlpha) < tolerance;
    }
}
