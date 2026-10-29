using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Training;

/// <summary>Resultado de um passo de avaliação da sessão de treino.</summary>
public readonly record struct SessionUpdate(
    TrainingPhase Phase,
    BrakingZone? ActiveZone,
    double LiveScore,
    double InstantErrorPercent,
    double RmsePercent,
    double Progress,
    double ElapsedSeconds,
    double ReferenceBrakePercent,
    double ReferenceThrottlePercent,
    double ChallengeStartSeconds,
    string StatusMessage,
    CornerResult? CompletedResult)
{
    public static SessionUpdate Idle(TrainingPhase phase, string status) => new(
        Phase: phase,
        ActiveZone: null,
        LiveScore: 0,
        InstantErrorPercent: 0,
        RmsePercent: 0,
        Progress: 0,
        ElapsedSeconds: 0,
        ReferenceBrakePercent: 0,
        ReferenceThrottlePercent: 0,
        ChallengeStartSeconds: 0,
        StatusMessage: status,
        CompletedResult: null);
}
