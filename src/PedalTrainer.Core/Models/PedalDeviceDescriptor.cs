using System;

namespace PedalTrainer.Core.Models;

/// <summary>Identificação de um controlador de jogo encontrado no DirectInput.</summary>
public sealed record PedalDeviceDescriptor(
    Guid InstanceGuid,
    Guid ProductGuid,
    string Name,
    string ProductName,
    int AxisCount,
    string[] AxisNames,
    bool IsSimulated = false)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(ProductName) || ProductName == Name
            ? Name
            : $"{Name} ({ProductName})";

    /// <summary>
    /// O template do combo mostra o item pelo ToString; sem isto a lista exibiria o record
    /// inteiro, com GUIDs, em vez do nome do controlador.
    /// </summary>
    public override string ToString() => DisplayName;

    public static PedalDeviceDescriptor Simulated(string name = "Simulador interno (sem hardware)")
        => new(
            Guid.Parse("00000000-0000-0000-0000-0000000000FF"),
            Guid.Empty,
            name,
            name,
            AxisCount: 3,
            AxisNames: new[] { "X (Acelerador)", "Y (Freio)", "Z" },
            IsSimulated: true);
}
