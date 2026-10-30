using PedalTrainer.Core.Telemetry;
using PedalTrainer.Core.Telemetry.AssettoCorsaCompetizione;
using PedalTrainer.Core.Telemetry.AssettoCorsaEvo;
using PedalTrainer.Core.Telemetry.Automobilista2;
using PedalTrainer.Core.Telemetry.LeMansUltimate;
using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Tests;

/// <summary>
/// Trava os deslocamentos das structs de memória compartilhada. Cada valor aqui foi conferido
/// contra o cabeçalho oficial do jogo; se alguém pular um campo, o teste acusa na hora.
/// </summary>
public sealed class TelemetryLayoutTests
{
    [Fact]
    public void FieldLayout_AlignsEachFieldToItsOwnSize()
    {
        var layout = new FieldLayout();

        Assert.Equal(0, layout.AddInt());
        Assert.Equal(4, layout.AddDouble());
        Assert.Equal(12, layout.Offset);
        Assert.Equal(12, layout.Size);
    }

    [Fact]
    public void FieldLayout_PackFourStopsDoubleFromPaddingEight()
    {
        var layout = new FieldLayout();
        layout.AddInt();

        int offset = layout.AddDouble();

        Assert.Equal(4, offset);
        Assert.Equal(12, layout.Size);
    }

    [Fact]
    public void FieldLayout_CharFollowedByFloatLeavesThreeBytesOfPadding()
    {
        var layout = new FieldLayout();
        layout.AddBytes(2);

        Assert.Equal(4, layout.AddFloat());
    }

    [Fact]
    public void Acc_PhysicsKeepsPedalsInTheSecondAndThirdFields()
    {
        Assert.Equal(4, AccSharedMemoryLayout.GasOffset);
        Assert.Equal(8, AccSharedMemoryLayout.BrakeOffset);
        Assert.Equal(28, AccSharedMemoryLayout.SpeedKmhOffset);
    }

    [Fact]
    public void Acc_GraphicsPutsLapAndPositionAfterTheTimeStrings()
    {
        Assert.Equal(4, AccSharedMemoryLayout.StatusOffset);
        Assert.Equal(132, AccSharedMemoryLayout.CompletedLapsOffset);
        Assert.Equal(248, AccSharedMemoryLayout.NormalizedPositionOffset);
    }

    [Fact]
    public void AcEvo_PhysicsSharesTheAccPrefix()
    {
        Assert.Equal(4, AcEvoSharedMemoryLayout.GasOffset);
        Assert.Equal(8, AcEvoSharedMemoryLayout.BrakeOffset);
        Assert.Equal(28, AcEvoSharedMemoryLayout.SpeedKmhOffset);
    }

    [Fact]
    public void AcEvo_GraphicsSkipsFourTyreBlocksBeforeTheLapPosition()
    {
        Assert.Equal(4, AcEvoSharedMemoryLayout.StatusOffset);
        Assert.Equal(1244, AcEvoSharedMemoryLayout.NormalizedPositionOffset);
        Assert.Equal(2384, AcEvoSharedMemoryLayout.TotalLapCountOffset);
    }

    [Fact]
    public void AcEvo_TotalLapCountSitsAfterAllTheStateBlocks()
    {
        int expected = AcEvoSharedMemoryLayout.NormalizedPositionOffset
            + (4 * sizeof(float))                    // posição, ERS (2) e trava de volante
            + AcEvoSharedMemoryLayout.DamageStateSize
            + sizeof(int)                            // car_location
            + AcEvoSharedMemoryLayout.PitInfoSize
            + (8 * sizeof(float))                    // combustível, bateria e consumo
            + (3 * AcEvoSharedMemoryLayout.InstrumentationSize)
            + (4 * AcEvoSharedMemoryLayout.ElectronicsSize);

        Assert.Equal(expected, AcEvoSharedMemoryLayout.TotalLapCountOffset);
    }

    [Fact]
    public void AcEvo_NestedStructSizesMatchTheHeaderAsserts()
    {
        Assert.Equal(256, AcEvoSharedMemoryLayout.TyreStateSize);
        Assert.Equal(128, AcEvoSharedMemoryLayout.DamageStateSize);
        Assert.Equal(128, AcEvoSharedMemoryLayout.InstrumentationSize);
        Assert.Equal(128, AcEvoSharedMemoryLayout.ElectronicsSize);
        Assert.Equal(64, AcEvoSharedMemoryLayout.PitInfoSize);
    }

    [Fact]
    public void Pcars2_ParticipantBlockKeepsTheHeaderLayout()
    {
        Assert.Equal(8, Pcars2SharedMemoryLayout.GameStateOffset);
        Assert.Equal(20, Pcars2SharedMemoryLayout.ViewedParticipantOffset);
        Assert.Equal(28, Pcars2SharedMemoryLayout.ParticipantsOffset);
        Assert.Equal(100, Pcars2SharedMemoryLayout.ParticipantSize);
    }

    [Fact]
    public void Pcars2_ParticipantFieldsSitWhereTheHeaderPutsThem()
    {
        Assert.Equal(80, Pcars2SharedMemoryLayout.ParticipantLapDistanceOffset);
        Assert.Equal(88, Pcars2SharedMemoryLayout.ParticipantLapsCompletedOffset);
        Assert.Equal(92, Pcars2SharedMemoryLayout.ParticipantCurrentLapOffset);
    }

    [Fact]
    public void Pcars2_CarFieldsComeAfterTheWholeParticipantArray()
    {
        Assert.Equal(6704, Pcars2SharedMemoryLayout.TrackLengthOffset);
        Assert.Equal(6848, Pcars2SharedMemoryLayout.SpeedOffset);
        Assert.Equal(6860, Pcars2SharedMemoryLayout.BrakeOffset);
        Assert.Equal(6864, Pcars2SharedMemoryLayout.ThrottleOffset);
    }

    [Fact]
    public void Rfactor_TelemetryVehicleMatchesTheDoubleHeavyStruct()
    {
        Assert.Equal(12, RFactorSharedMemoryLayout.TelemetryElapsedTimeOffset);
        Assert.Equal(20, RFactorSharedMemoryLayout.TelemetryLapNumberOffset);
        Assert.Equal(184, RFactorSharedMemoryLayout.TelemetryLocalVelocityOffset);
        Assert.Equal(352, RFactorSharedMemoryLayout.TelemetryGearOffset);
        Assert.Equal(420, RFactorSharedMemoryLayout.TelemetryFilteredThrottleOffset);
        Assert.Equal(428, RFactorSharedMemoryLayout.TelemetryFilteredBrakeOffset);
        Assert.Equal(1888, RFactorSharedMemoryLayout.TelemetryVehicleSize);
    }

    [Fact]
    public void Rfactor_ScoringInfoCarriesTrackLengthAndVehicleCount()
    {
        Assert.Equal(12 + 68, RFactorSharedMemoryLayout.ScoringCurrentTimeOffset);
        Assert.Equal(12 + 88, RFactorSharedMemoryLayout.ScoringTrackLengthOffset);
        Assert.Equal(12 + 104, RFactorSharedMemoryLayout.ScoringVehicleCountOffset);
        Assert.Equal(12 + 548, RFactorSharedMemoryLayout.ScoringVehiclesOffset);
    }

    [Fact]
    public void Rfactor_VehicleScoringSizeAndPlayerFlag()
    {
        Assert.Equal(100, RFactorSharedMemoryLayout.ScoringVehicleTotalLapsOffset);
        Assert.Equal(104, RFactorSharedMemoryLayout.ScoringVehicleLapDistanceOffset);
        Assert.Equal(196, RFactorSharedMemoryLayout.ScoringVehicleIsPlayerOffset);
        Assert.Equal(584, RFactorSharedMemoryLayout.ScoringVehicleSize);
    }

    [Theory]
    [InlineData(SimulatorId.Auto)]
    [InlineData(SimulatorId.IRacing)]
    [InlineData(SimulatorId.AssettoCorsaCompetizione)]
    [InlineData(SimulatorId.AssettoCorsaEvo)]
    [InlineData(SimulatorId.Automobilista2)]
    [InlineData(SimulatorId.LeMansUltimate)]
    public void Factory_AlwaysProducesAnIdleAppSource(SimulatorId simulatorId)
    {
        using ICarTelemetrySource source = SimTelemetrySourceFactory.Create(simulatorId);

        Assert.NotNull(source.Simulator);
        Assert.False(string.IsNullOrWhiteSpace(source.StatusDescription));
        Assert.False(source.Latest.IsConnected);
    }
}
