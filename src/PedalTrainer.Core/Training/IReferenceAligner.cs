using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>
/// Traduz o instante atual do piloto em uma amostra da volta de referência.
/// Duas estratégias: por distância (precisa, exige iRacing) e por tempo (fallback).
/// </summary>
public interface IReferenceAligner
{
    /// <summary>Zona de frenagem que está sendo treinada.</summary>
    BrakingZone Zone { get; }

    /// <summary>Nome do modo de alinhamento, para exibir na interface.</summary>
    string ModeName { get; }

    /// <summary>Amostra de referência correspondente ao instante atual do piloto.</summary>
    ReferenceSample Resolve(double elapsedSeconds, double? distanceMeters);

    /// <summary>Progresso dentro da zona, de 0 a 1.</summary>
    double Progress(double elapsedSeconds, double? distanceMeters);

    /// <summary>Verdadeiro quando a janela de desafio pode ser encerrada.</summary>
    bool IsComplete(double elapsedSeconds, double? distanceMeters);
}
