using System.Collections.Generic;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Programas de treino que vêm com o aplicativo. São gravados como arquivos JSON na
/// primeira execução, então o piloto pode copiá-los, editar os percentuais e recarregar.
/// </summary>
public static class DrillProgramCatalog
{
    /// <summary>Nome do arquivo do programa de freio.</summary>
    public const string BrakeFileName = "freio-progressivo.drill.json";

    /// <summary>Nome do arquivo do programa de acelerador.</summary>
    public const string ThrottleFileName = "aceleracao-progressiva.drill.json";

    /// <summary>Programa de freio: 20%, 50%, 80% e depois alvos sorteados.</summary>
    public static DrillProgram CreateBrakeProgressive() => new()
    {
        Name = "Freio — 20 / 50 / 80 e alvos sorteados",
        Description =
            "Pise no freio até o alvo, segure e solte devagar. Começa leve (20%), " +
            "sobe para 50% e 80% e termina com alvos sorteados para treinar a memória muscular.",
        RestPercent = 4,
        Steps = new List<DrillStep>
        {
            new()
            {
                Label = "Freio até 20% e soltar devagar",
                Pedal = DrillPedal.Brake,
                TargetPercent = 20,
                TolerancePercent = 4,
                Repetitions = 10,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(1.0),
                    DrillSegment.Hold(0.8),
                    DrillSegment.Release(2.5)
                }
            },
            new()
            {
                Label = "Freio até 50% e soltar devagar",
                Pedal = DrillPedal.Brake,
                TargetPercent = 50,
                TolerancePercent = 5,
                Repetitions = 10,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(1.6),
                    DrillSegment.Hold(1.0),
                    DrillSegment.Release(3.0)
                }
            },
            new()
            {
                Label = "Freio até 80% e soltar devagar",
                Pedal = DrillPedal.Brake,
                TargetPercent = 80,
                TolerancePercent = 6,
                Repetitions = 10,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(2.0),
                    DrillSegment.Hold(1.2),
                    DrillSegment.Release(3.5)
                }
            },
            new()
            {
                Label = "Freio em alvo sorteado (20 a 85%)",
                Pedal = DrillPedal.Brake,
                TargetPercent = 50,
                TolerancePercent = 7,
                Repetitions = 12,
                RandomTargetMinPercent = 20,
                RandomTargetMaxPercent = 85,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(1.5),
                    DrillSegment.Hold(1.0),
                    DrillSegment.Release(3.0)
                }
            }
        }
    };

    /// <summary>
    /// Programa de acelerador: subida rápida até 20%, subida gradual, 50% até 100%
    /// progressivo e, por fim, 80% com segura.
    /// </summary>
    public static DrillProgram CreateThrottleProgressive() => new()
    {
        Name = "Acelerador — rápido, gradual, progressivo e 80%",
        Description =
            "Treina a modulação do acelerador: toque rápido até 20%, subida gradual, " +
            "50% até 100% progressivo e 80% sustentado. Repete várias vezes cada padrão.",
        RestPercent = 4,
        Steps = new List<DrillStep>
        {
            new()
            {
                Label = "Acelerador rápido até 20%",
                Pedal = DrillPedal.Throttle,
                TargetPercent = 20,
                TolerancePercent = 4,
                Repetitions = 8,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(0.35),
                    DrillSegment.Hold(0.6),
                    DrillSegment.Release(1.5)
                }
            },
            new()
            {
                Label = "Acelerador gradualmente até 20%",
                Pedal = DrillPedal.Throttle,
                TargetPercent = 20,
                TolerancePercent = 4,
                Repetitions = 8,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(2.0),
                    DrillSegment.Hold(0.8),
                    DrillSegment.Release(2.0)
                }
            },
            new()
            {
                Label = "50% e subida progressiva até 100%",
                Pedal = DrillPedal.Throttle,
                TargetPercent = 100,
                TolerancePercent = 5,
                Repetitions = 6,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(1.0, 50),
                    DrillSegment.Hold(0.8),
                    DrillSegment.Ramp(5.0),
                    DrillSegment.Hold(1.2),
                    DrillSegment.Release(2.5)
                }
            },
            new()
            {
                Label = "Acelerador rápido até 80% e sustentar",
                Pedal = DrillPedal.Throttle,
                TargetPercent = 80,
                TolerancePercent = 5,
                Repetitions = 6,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(1.0),
                    DrillSegment.Hold(2.0),
                    DrillSegment.Release(2.5)
                }
            }
        }
    };

    /// <summary>Todos os programas embutidos, com o nome de arquivo sugerido.</summary>
    public static IReadOnlyList<(string FileName, DrillProgram Program)> All() => new[]
    {
        (BrakeFileName, CreateBrakeProgressive()),
        (ThrottleFileName, CreateThrottleProgressive())
    };
}
