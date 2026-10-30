using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Telemetry;

/// <summary>Fonte inerte usada enquanto nenhum simulador está sendo lido.</summary>
public sealed class NullCarTelemetrySource : ICarTelemetrySource
{
    public SimulatorInfo Simulator => SimulatorCatalog.Auto;

    public string ConnectedSimulatorName => string.Empty;

    public bool IsConnected => false;

    public string StatusDescription => "Simulador: não conectado";

    public CarTelemetry Latest => CarTelemetry.Disconnected(0);

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
