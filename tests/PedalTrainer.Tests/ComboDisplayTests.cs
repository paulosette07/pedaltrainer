using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry;

namespace PedalTrainer.Tests;

/// <summary>
/// O template do combo mostra o item pelo ToString. Se um record cair no ToString padrão, a
/// lista exibe "SimulatorInfo { Id = Auto, ... }" em vez do nome — o piloto não entende nada.
/// </summary>
public sealed class ComboDisplayTests
{
    [Fact]
    public void SimulatorInfo_RendersItsDisplayName()
    {
        Assert.All(SimulatorCatalog.Selectable, simulator =>
            Assert.Equal(simulator.DisplayName, simulator.ToString()));
    }

    [Fact]
    public void PedalDeviceDescriptor_RendersItsDisplayName()
    {
        var device = new PedalDeviceDescriptor(
            InstanceGuid: Guid.NewGuid(),
            ProductGuid: Guid.NewGuid(),
            Name: "SIMSONN Plus X",
            ProductName: "SIMSONN Plus X",
            AxisCount: 3,
            AxisNames: new[] { "X", "Y", "Z" });

        Assert.Equal("SIMSONN Plus X", device.ToString());
    }

    [Fact]
    public void PedalDeviceDescriptor_KeepsBothNamesWhenTheyDiffer()
    {
        var device = new PedalDeviceDescriptor(
            InstanceGuid: Guid.NewGuid(),
            ProductGuid: Guid.NewGuid(),
            Name: "Arduino Micro",
            ProductName: "SIMSONN Load Cell",
            AxisCount: 3,
            AxisNames: new[] { "X", "Y", "Z" });

        Assert.Equal("Arduino Micro (SIMSONN Load Cell)", device.ToString());
    }
}
