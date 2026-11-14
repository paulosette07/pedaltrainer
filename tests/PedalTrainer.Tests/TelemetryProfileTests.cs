using PedalTrainer.Core.Telemetry;

namespace PedalTrainer.Tests;

/// <summary>
/// Configuração de telemetria de cada jogo: blocos lidos, canais publicados e o perfil
/// efetivo quando o simulador é detectado automaticamente.
/// </summary>
public sealed class TelemetryProfileTests
{
    [Fact]
    public void EverySimulatorHasAProfile()
    {
        foreach (SimulatorInfo simulator in SimulatorCatalog.All)
        {
            TelemetryProfile profile = TelemetryProfileCatalog.Find(simulator);

            Assert.Equal(simulator.Id, profile.Id);
            Assert.True(profile.RecordingRateHz > 0);
            Assert.NotEmpty(profile.SetupHint);
            Assert.NotEmpty(profile.ChannelSummary);
        }
    }

    [Fact]
    public void CatalogKeepsAutoFirst()
    {
        Assert.Equal(SimulatorId.Auto, TelemetryProfileCatalog.All[0].Id);
        Assert.Equal(SimulatorCatalog.Selectable.Count, TelemetryProfileCatalog.All.Count);
    }

    [Fact]
    public void IRacingReadsIrsdkAndPublishesSessionTime()
    {
        TelemetryProfile profile = TelemetryProfileCatalog.IRacing;

        Assert.Contains("IRSDKMemMapFileName", profile.MemoryMapSummary);
        Assert.True(profile.Provides(TelemetryChannel.SessionTime));
        Assert.True(TelemetryChannelNames.IsComplete(profile.Channels));
    }

    [Theory]
    [InlineData(SimulatorId.AssettoCorsaCompetizione, "acpmf_physics")]
    [InlineData(SimulatorId.AssettoCorsaEvo, "acevo_pmf_physics")]
    [InlineData(SimulatorId.Automobilista2, "$pcars2$")]
    [InlineData(SimulatorId.LeMansUltimate, "$rFactor2SMMP_Telemetry$")]
    public void GameProfilesMapTheSharedMemoryTheyRead(SimulatorId id, string mapName)
    {
        TelemetryProfile profile = TelemetryProfileCatalog.Find(id);

        Assert.Contains(mapName, profile.MemoryMapSummary);
    }

    [Fact]
    public void GamesWithoutSessionTimeSayWhichChannelIsMissing()
    {
        TelemetryProfile profile = TelemetryProfileCatalog.AssettoCorsaCompetizione;

        Assert.False(profile.Provides(TelemetryChannel.SessionTime));
        Assert.Contains("tempo de sessão", TelemetryChannelNames.Missing(profile.Channels));
        Assert.True(profile.Provides(TelemetryChannel.Brake));
    }

    [Fact]
    public void LeMansUltimateRequiresPluginAndPublishesSessionTime()
    {
        TelemetryProfile profile = TelemetryProfileCatalog.LeMansUltimate;

        Assert.True(profile.RequiresPlugin);
        Assert.True(profile.Provides(TelemetryChannel.SessionTime));
        Assert.Contains("$rFactor2SMMP_Scoring$", profile.MemoryMapSummary);
    }

    [Fact]
    public void AutoProfileFollowsTheConnectedGame()
    {
        TelemetryProfile withGame = TelemetryProfileCatalog.Resolve(
            SimulatorCatalog.Auto,
            SimulatorCatalog.Automobilista2);

        Assert.Equal(SimulatorId.Automobilista2, withGame.Id);

        TelemetryProfile withoutGame = TelemetryProfileCatalog.Resolve(SimulatorCatalog.Auto, null);
        Assert.True(withoutGame.IsAuto);
    }

    [Fact]
    public void ExplicitChoiceWinsOverConnectedGame()
    {
        TelemetryProfile profile = TelemetryProfileCatalog.Resolve(
            SimulatorCatalog.IRacing,
            SimulatorCatalog.AssettoCorsaEvo);

        Assert.Equal(SimulatorId.IRacing, profile.Id);
    }

    [Fact]
    public void ColumnsMatchTheChannelsOfTheGame()
    {
        Assert.Equal(
            new[]
            {
                "tempo_gravacao_s",
                "velocidade_kmh",
                "freio_pct",
                "acelerador_pct",
                "posicao_volta_pct",
                "volta"
            },
            TelemetryProfileCatalog.AssettoCorsaCompetizione.Columns.Select(column => column.Header));

        Assert.Equal(
            7,
            TelemetryProfileCatalog.LeMansUltimate.Columns.Count);
    }
}
