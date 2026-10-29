using System;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Fonte de leitura dos pedais. A implementação roda em thread própria e apenas
/// publica o último instantâneo bruto; o consumidor faz polling sem bloquear.
/// </summary>
public interface IPedalDevice : IDisposable
{
    /// <summary>Identificação do controlador em uso.</summary>
    PedalDeviceDescriptor Descriptor { get; }

    bool IsConnected { get; }

    /// <summary>Taxa efetiva de aquisição medida em Hz.</summary>
    double SampleRateHz { get; }

    /// <summary>Última mensagem de erro (aquisição, conexão), quando houver.</summary>
    string? LastError { get; }

    /// <summary>Inicia a thread de aquisição.</summary>
    void Start();

    /// <summary>Encerra a thread de aquisição.</summary>
    void Stop();

    /// <summary>Copia o último instantâneo lido. Retorna falso se ainda não houve leitura.</summary>
    bool TryReadLatest(out RawAxesSnapshot snapshot);
}
