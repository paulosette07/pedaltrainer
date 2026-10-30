using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry;

namespace PedalTrainer.Tests;

/// <summary>
/// O catálogo é o que a tela de configurações mostra e o que a fábrica usa para escolher o
/// leitor; um simulador fora da lista simplesmente não pode ser escolhido.
/// </summary>
public sealed class SimulatorCatalogTests
{
    [Fact]
    public void Selectable_StartsWithAutomaticAndListsEverySupportedSim()
    {
        Assert.Equal(6, SimulatorCatalog.Selectable.Count);
        Assert.True(SimulatorCatalog.Selectable[0].IsAuto);

        Assert.Contains(SimulatorCatalog.Selectable, s => s.Id == SimulatorId.IRacing);
        Assert.Contains(SimulatorCatalog.Selectable, s => s.Id == SimulatorId.AssettoCorsaCompetizione);
        Assert.Contains(SimulatorCatalog.Selectable, s => s.Id == SimulatorId.AssettoCorsaEvo);
        Assert.Contains(SimulatorCatalog.Selectable, s => s.Id == SimulatorId.Automobilista2);
        Assert.Contains(SimulatorCatalog.Selectable, s => s.Id == SimulatorId.LeMansUltimate);
    }

    [Fact]
    public void EverySimulatorCarriesANonEmptySetupHint()
    {
        Assert.All(SimulatorCatalog.Selectable, simulator =>
        {
            Assert.False(string.IsNullOrWhiteSpace(simulator.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(simulator.ShortName));
            Assert.False(string.IsNullOrWhiteSpace(simulator.SetupHint));
        });
    }

    [Fact]
    public void Parse_FallsBackToAutomaticForEmptyOrUnknownValues()
    {
        Assert.True(SimulatorCatalog.Parse(null).IsAuto);
        Assert.True(SimulatorCatalog.Parse(string.Empty).IsAuto);
        Assert.True(SimulatorCatalog.Parse("   ").IsAuto);
        Assert.True(SimulatorCatalog.Parse("ProjectCars").IsAuto);
    }

    [Theory]
    [InlineData("IRacing", SimulatorId.IRacing)]
    [InlineData("assettocorsacompetizione", SimulatorId.AssettoCorsaCompetizione)]
    [InlineData("AssettoCorsaEvo", SimulatorId.AssettoCorsaEvo)]
    [InlineData("Automobilista2", SimulatorId.Automobilista2)]
    [InlineData("LeMansUltimate", SimulatorId.LeMansUltimate)]
    public void Parse_ReadsTheStoredIdentifierCaseInsensitively(string stored, SimulatorId expected)
    {
        Assert.Equal(expected, SimulatorCatalog.Parse(stored).Id);
    }

    [Fact]
    public void Settings_RoundTripKeepsTheChosenSimulator()
    {
        var settings = new AppSettings { SimulatorId = SimulatorId.Automobilista2.ToString() };

        AppSettings copy = settings.Clone();

        Assert.Equal(SimulatorId.Automobilista2, SimulatorCatalog.Parse(copy.SimulatorId).Id);
    }

    [Fact]
    public void Factory_AutomaticModeBuildsOneReaderPerSimulator()
    {
        IReadOnlyList<ISimTelemetryReader> readers =
            SimTelemetrySourceFactory.CreateReaders(SimulatorCatalog.Auto);

        Assert.Equal(5, readers.Count);
        Assert.Equal(5, readers.Select(reader => reader.Simulator.Id).Distinct().Count());
    }

    [Fact]
    public void Factory_SpecificSimulatorBuildsOnlyItsOwnReader()
    {
        IReadOnlyList<ISimTelemetryReader> readers =
            SimTelemetrySourceFactory.CreateReaders(SimulatorCatalog.AssettoCorsaEvo);

        ISimTelemetryReader reader = Assert.Single(readers);
        Assert.Equal(SimulatorId.AssettoCorsaEvo, reader.Simulator.Id);
    }

    [Fact]
    public void Factory_KeepsIracingInTheAutomaticPriorityList()
    {
        IReadOnlyList<ISimTelemetryReader> readers =
            SimTelemetrySourceFactory.CreateReaders(SimulatorCatalog.Auto);

        Assert.Equal(SimulatorId.IRacing, readers[0].Simulator.Id);
    }

    [Fact]
    public void Factory_DisposingTheSourceAlsoDisposesTheReaders()
    {
        ICarTelemetrySource source = SimTelemetrySourceFactory.Create(SimulatorCatalog.LeMansUltimate);

        source.Dispose();

        Assert.False(source.IsConnected);
    }
}
