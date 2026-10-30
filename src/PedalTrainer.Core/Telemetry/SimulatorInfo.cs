namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Descrição de um simulador suportado: nome exibido, apelido curto e o que o piloto
/// precisa fazer no jogo para a telemetria aparecer.
/// </summary>
public sealed record SimulatorInfo(
    SimulatorId Id,
    string DisplayName,
    string ShortName,
    string SetupHint,
    bool RequiresPlugin)
{
    /// <summary>Verdadeiro para a entrada "detectar sozinho".</summary>
    public bool IsAuto => Id == SimulatorId.Auto;

    /// <summary>
    /// O template do combo mostra o item pelo ToString; devolver o nome exibido evita que a
    /// lista caia no record inteiro ("SimulatorInfo { Id = Auto, ... }").
    /// </summary>
    public override string ToString() => DisplayName;
}
