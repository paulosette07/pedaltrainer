using System.Diagnostics;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry;

namespace PedalTrainer.Tests;

/// <summary>
/// A fonte é o que o motor de treino consome: ela escolhe o leitor, mantém o relógio de
/// sessão dos simuladores que não publicam tempo e cai para o próximo simulador quando o
/// primeiro não está publicando.
/// </summary>
public sealed class SimTelemetrySourceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    [Fact]
    public void Source_PublishesWhatTheReaderReturns()
    {
        var reader = new FakeReader(
            SimulatorCatalog.IRacing,
            isAvailable: true,
            sample: new SimTelemetrySample(
                SpeedKmh: 132,
                BrakePercent: 62,
                ThrottlePercent: 0,
                LapDistancePercent: 0.25,
                LapNumber: 4,
                SessionTimeSeconds: 91.5));

        using var source = new SimTelemetrySource(SimulatorCatalog.IRacing, new[] { reader });
        source.Start();

        Assert.True(WaitForConnection(source));

        CarTelemetry latest = source.Latest;
        Assert.True(latest.IsConnected);
        Assert.Equal(132, latest.SpeedKmh, 1);
        Assert.Equal(62, latest.BrakePercent, 1);
        Assert.Equal(0.25, latest.LapDistancePercent, 3);
        Assert.Equal(4, latest.LapNumber);
        Assert.Equal(91.5, latest.SessionTimeSeconds, 3);
        Assert.Equal("iRacing", source.ConnectedSimulatorName);
        Assert.Contains("ao vivo", source.StatusDescription);
    }

    [Fact]
    public void Source_InventsASessionClockWhenTheSimHasNone()
    {
        var reader = new FakeReader(
            SimulatorCatalog.AssettoCorsaCompetizione,
            isAvailable: true,
            sample: new SimTelemetrySample(80, 40, 20, 0.5, 2, SessionTimeSeconds: null));

        using var source = new SimTelemetrySource(SimulatorCatalog.AssettoCorsaCompetizione, new[] { reader });
        source.Start();

        Assert.True(WaitForConnection(source));

        Assert.True(source.Latest.SessionTimeSeconds > 0);
        Assert.Equal(source.Latest.SessionTimeSeconds, source.Latest.TimestampSeconds, 3);
    }

    [Fact]
    public void Source_FallsBackToTheNextSimulatorWhenTheFirstIsClosed()
    {
        var closed = new FakeReader(SimulatorCatalog.IRacing, isAvailable: false, sample: null);
        var open = new FakeReader(
            SimulatorCatalog.AssettoCorsaEvo,
            isAvailable: true,
            sample: new SimTelemetrySample(50, 10, 90, 0.1, 1, null));

        using var source = new SimTelemetrySource(SimulatorCatalog.Auto, new[] { closed, open });
        source.Start();

        Assert.True(WaitForConnection(source));
        Assert.Equal("AC EVO", source.ConnectedSimulatorName);
        Assert.Equal(50, source.Latest.SpeedKmh, 1);
    }

    [Fact]
    public void Source_ReportsNoSimulatorWhenNothingIsPublishing()
    {
        var reader = new FakeReader(SimulatorCatalog.LeMansUltimate, isAvailable: false, sample: null);

        using var source = new SimTelemetrySource(SimulatorCatalog.LeMansUltimate, new[] { reader });
        source.Start();

        Assert.True(SpinWait.SpinUntil(
            () => source.StatusDescription.Contains("não encontrado", StringComparison.Ordinal),
            Timeout));

        Assert.False(source.IsConnected);
        Assert.True(source.Latest.IsConnected == false);
    }

    [Fact]
    public void Source_ClampsValuesThatComeOutOfRange()
    {
        var reader = new FakeReader(
            SimulatorCatalog.Automobilista2,
            isAvailable: true,
            sample: new SimTelemetrySample(-15, 180, -20, 3.4, -2, null));

        using var source = new SimTelemetrySource(SimulatorCatalog.Automobilista2, new[] { reader });
        source.Start();

        Assert.True(WaitForConnection(source));

        Assert.Equal(0, source.Latest.SpeedKmh);
        Assert.Equal(100, source.Latest.BrakePercent, 3);
        Assert.Equal(0, source.Latest.ThrottlePercent, 3);
        Assert.Equal(1, source.Latest.LapDistancePercent, 3);
        Assert.Equal(0, source.Latest.LapNumber);
    }

    [Fact]
    public void Source_DisposeStopsPollingAndClosesTheReader()
    {
        var reader = new FakeReader(
            SimulatorCatalog.LeMansUltimate,
            isAvailable: true,
            sample: new SimTelemetrySample(10, 0, 0, 0, 1, 1));

        var source = new SimTelemetrySource(SimulatorCatalog.LeMansUltimate, new[] { reader });
        source.Start();
        Assert.True(WaitForConnection(source));

        source.Dispose();

        Assert.True(reader.WasDisposed);
    }

    private static bool WaitForConnection(SimTelemetrySource source) =>
        SpinWait.SpinUntil(() => source.IsConnected, Timeout);

    /// <summary>Leitor de mentira: devolve sempre o mesmo instantâneo e registra o descarte.</summary>
    private sealed class FakeReader : ISimTelemetryReader
    {
        private readonly bool _isAvailable;
        private readonly SimTelemetrySample? _sample;

        public FakeReader(SimulatorInfo simulator, bool isAvailable, SimTelemetrySample? sample)
        {
            Simulator = simulator;
            _isAvailable = isAvailable;
            _sample = sample;
        }

        public SimulatorInfo Simulator { get; }

        public bool IsOpen { get; private set; }

        public bool WasDisposed { get; private set; }

        public bool TryOpen()
        {
            if (!_isAvailable)
            {
                return false;
            }

            IsOpen = true;
            return true;
        }

        public void Close() => IsOpen = false;

        public bool TryRead(out SimTelemetrySample sample, out string status)
        {
            if (!IsOpen || _sample is null)
            {
                sample = default;
                status = "sem dados";
                return false;
            }

            sample = _sample.Value;
            status = "ao vivo (teste)";
            return true;
        }

        public void Dispose()
        {
            WasDisposed = true;
            Close();
        }
    }
}
