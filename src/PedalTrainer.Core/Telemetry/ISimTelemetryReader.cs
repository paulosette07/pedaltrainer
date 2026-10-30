using System;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Leitor de um simulador: abre a memória compartilhada do jogo e devolve um instantâneo
/// do carro do jogador. Não conhece threads nem o motor de treino; isso é papel da fonte.
/// </summary>
public interface ISimTelemetryReader : IDisposable
{
    /// <summary>Simulador lido por este leitor.</summary>
    SimulatorInfo Simulator { get; }

    /// <summary>Verdadeiro quando a memória compartilhada já está aberta.</summary>
    bool IsOpen { get; }

    /// <summary>Tenta abrir a memória compartilhada. Não lança exceção.</summary>
    bool TryOpen();

    /// <summary>Fecha a memória compartilhada (o jogo pode estar fechando).</summary>
    void Close();

    /// <summary>
    /// Lê o instante atual. Quando devolve falso, <paramref name="status"/> explica o motivo
    /// (jogo em menu, telemetria inativa, sessão pausada).
    /// </summary>
    bool TryRead(out SimTelemetrySample sample, out string status);
}
