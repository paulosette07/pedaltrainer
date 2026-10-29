using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.IRacing;

/// <summary>
/// Fonte de dados do carro (velocidade e posição na pista). A implementação real
/// lê a memória compartilhada do iRacing; existe também uma versão nula.
/// </summary>
public interface ICarTelemetrySource : IDisposable
{
    /// <summary>Verdadeiro quando o iRacing está aberto e publicando telemetria.</summary>
    bool IsConnected { get; }

    /// <summary>Descrição curta do estado, exibida na barra superior.</summary>
    string StatusDescription { get; }

    /// <summary>Último instantâneo lido (imutável, seguro entre threads).</summary>
    CarTelemetry Latest { get; }

    void Start();

    void Stop();
}
