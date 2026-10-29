namespace PedalTrainer.Core.Recording;

/// <summary>Estados do gravador de volta de referência.</summary>
public enum LapRecordingState
{
    /// <summary>Nada sendo gravado.</summary>
    Idle,

    /// <summary>Gravador armado: esperando o carro cruzar a linha para começar a volta.</summary>
    WaitingForLapStart,

    /// <summary>Capturando a volta em andamento.</summary>
    Recording,

    /// <summary>Uma volta completa está disponível para retirada.</summary>
    Completed
}
