using System.Collections.Generic;
using System.Globalization;

namespace PedalTrainer.Core.Input.Pedals;

/// <summary>
/// Um conjunto de pedais real do mercado. Reúne o que muda de modelo para modelo na
/// hora de ler o sinal: tecnologia do freio, quantidade de pedais e os ajustes que
/// costumam funcionar melhor naquele hardware.
/// </summary>
public sealed record PedalModel(
    string Id,
    string Brand,
    string Name,
    PedalSensorType BrakeSensor,
    int PedalCount,
    double RecommendedBrakeExponent,
    double RecommendedThrottleExponent,
    double RecommendedBrakeDeadzonePercent,
    double RecommendedThrottleDeadzonePercent,
    double RecommendedSmoothingAlpha,
    IReadOnlyList<string> MatchKeywords,
    string Notes)
{
    /// <summary>Nome completo exibido nas listas (marca + modelo).</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Brand) ? Name : $"{Brand} {Name}";

    /// <summary>Rótulo curto do sensor do freio.</summary>
    public string SensorLabel => BrakeSensor switch
    {
        PedalSensorType.LoadCell => "Load Cell",
        PedalSensorType.HallEffect => "Hall",
        _ => "Potenciômetro"
    };

    /// <summary>Resumo do hardware, mostrado abaixo do seletor.</summary>
    public string HardwareLabel => IsCatchAll
        ? "Sem perfil de fábrica"
        : string.Create(CultureInfo.InvariantCulture, $"{PedalCount} pedais · freio {SensorLabel}");

    /// <summary>Resumo dos ajustes que o modelo aplica na calibração.</summary>
    public string TuningLabel => IsCatchAll
        ? "mantém a curva e o filtro atuais"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"curva {RecommendedBrakeExponent:0.00} · zona morta {RecommendedBrakeDeadzonePercent:0.#}% · filtro {RecommendedSmoothingAlpha:0.00}");

    /// <summary>Verdadeiro para o item "não listado", que não mexe em nada.</summary>
    public bool IsCatchAll => Id == PedalModelCatalog.CatchAllId;

    /// <summary>Usado pelo ComboBox; evita depender do ToString padrão do record.</summary>
    public override string ToString() => DisplayName;
}
