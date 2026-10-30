using System;

namespace PedalTrainer.Core.Models;

/// <summary>Preferências do aplicativo, persistidas em JSON no perfil do usuário.</summary>
public sealed class AppSettings
{
    /// <summary>Pressão de freio (%) que dispara o início de uma janela de desafio.</summary>
    public double BrakeTriggerPercent { get; set; } = 5.0;

    /// <summary>Banda de tolerância (%) em que a diferença para a referência não penaliza.</summary>
    public double ToleranceBandPercent { get; set; } = 8.0;

    /// <summary>Duração visível do gráfico em tempo real.</summary>
    public double ChartWindowSeconds { get; set; } = 6.0;

    /// <summary>Histórico retido pelo motor (segundos) para desenhar o gráfico.</summary>
    public double ChartHistorySeconds { get; set; } = 30.0;

    /// <summary>Usa a posição na pista (LapDistPct) para alinhar referência e piloto.</summary>
    public bool UseIRacingAlignment { get; set; } = true;

    /// <summary>Exibe a curva do acelerador junto do freio.</summary>
    public bool ShowThrottleTrace { get; set; } = true;

    /// <summary>Último controlador usado, para reconectar automaticamente.</summary>
    public Guid LastDeviceGuid { get; set; } = Guid.Empty;

    /// <summary>Modelo de pedal escolhido no catálogo (vazio = não listado).</summary>
    public string PedalModelId { get; set; } = string.Empty;

    /// <summary>
    /// Simulador escolhido para a telemetria, guardado como texto
    /// (<c>Auto</c>, <c>IRacing</c>, <c>AssettoCorsaCompetizione</c>, <c>AssettoCorsaEvo</c>,
    /// <c>Automobilista2</c>, <c>LeMansUltimate</c>).
    /// </summary>
    public string SimulatorId { get; set; } = string.Empty;

    /// <summary>Caminho da última volta de referência carregada.</summary>
    public string LastReferencePath { get; set; } = string.Empty;

    /// <summary>Quando verdadeiro, o app roda com pedais sintéticos.</summary>
    public bool UseSimulatedPedals { get; set; }

    /// <summary>Mostra a taxa real de aquisição do controlador na barra superior.</summary>
    public bool ShowInputRate { get; set; } = true;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
