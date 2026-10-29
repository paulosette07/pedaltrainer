using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.IRacing;

/// <summary>Fonte inerte usada quando o iRacing não está disponível.</summary>
public sealed class NullCarTelemetrySource : ICarTelemetrySource
{
    public bool IsConnected => false;

    public string StatusDescription => "iRacing: não conectado";

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
