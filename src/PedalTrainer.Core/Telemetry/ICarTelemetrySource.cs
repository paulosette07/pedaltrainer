using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Fonte de dados do carro (velocidade, pedais e posição na pista) usada pelo treino e pela
/// gravação de voltas. Cada simulador tem o seu leitor; o motor conversa só com esta interface.
/// </summary>
public interface ICarTelemetrySource : IDisposable
{
    /// <summary>Simulador que esta fonte lê (ou "automático" quando ela mesma escolhe).</summary>
    SimulatorInfo Simulator { get; }

    /// <summary>Nome curto do simulador realmente conectado (vazio quando nenhum).</summary>
    string ConnectedSimulatorName { get; }

    /// <summary>Verdadeiro quando o simulador está aberto e publicando telemetria.</summary>
    bool IsConnected { get; }

    /// <summary>Descrição curta do estado, exibida na barra superior.</summary>
    string StatusDescription { get; }

    /// <summary>Último instantâneo lido (imutável, seguro entre threads).</summary>
    CarTelemetry Latest { get; }

    void Start();

    void Stop();
}
