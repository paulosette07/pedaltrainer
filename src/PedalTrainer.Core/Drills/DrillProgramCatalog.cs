using System.Collections.Generic;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Programas de treino que vêm com o aplicativo. São gravados como arquivos JSON na
/// primeira execução, então o piloto pode copiá-los, editar os percentuais e recarregar.
/// </summary>
/// <remarks>
/// Os dois programas seguem a mesma regra de pegada: a <b>pisada é rápida</b> — vai do
/// repouso até o alvo em menos de meio segundo, como num carro de verdade — e o que se
/// treina é a <b>soltura gradual</b>, que é onde o pé precisa de modulação.
/// </remarks>
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
            "Pise rápido até o alvo, segure e solte devagar. Começa leve (20%), " +
            "sobe para 50% e 80% e termina com alvos sorteados para treinar a memória muscular. " +
            "A pisada é sempre rápida: o que o exercício cobra é a soltura progressiva.",
        RestPercent = 4,
        Steps = new List<DrillStep>
        {
            new()
            {
                Label = "Freio: pisar rápido em 20% e soltar devagar",
                Pedal = DrillPedal.Brake,
                TargetPercent = 20,
                TolerancePercent = 4,
                Repetitions = 10,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(0.30),
                    DrillSegment.Hold(1.0),
                    DrillSegment.Release(2.0)
                }
            },
            new()
            {
                Label = "Freio: pisar rápido em 50% e soltar devagar",
                Pedal = DrillPedal.Brake,
                TargetPercent = 50,
                TolerancePercent = 5,
                Repetitions = 10,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(0.40),
                    DrillSegment.Hold(1.0),
                    DrillSegment.Release(3.0)
                }
            },
            new()
            {
                Label = "Freio: pisar rápido em 80% e soltar devagar",
                Pedal = DrillPedal.Brake,
                TargetPercent = 80,
                TolerancePercent = 6,
                Repetitions = 10,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(0.50),
                    DrillSegment.Hold(1.2),
                    DrillSegment.Release(4.5)
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
                    DrillSegment.Ramp(0.40),
                    DrillSegment.Hold(1.0),
                    DrillSegment.Release(5.0)
                }
            }
        }
    };

    /// <summary>
    /// Programa de acelerador: pisada rápida em 20%, 50% e 80%, mais a subida progressiva
    /// de 50% até 100% (o único trecho em que a subida é gradual, porque é isso que o
    /// acelerador pede na saída de curva).
    /// </summary>
    public static DrillProgram CreateThrottleProgressive() => new()
    {
        Name = "Acelerador — rápido em 20 / 50 / 80 e progressivo até 100%",
        Description =
            "Pise rápido até o alvo e solte devagar: 20%, 50%, a subida progressiva de 50% " +
            "até 100% e 80% sustentado. Repete várias vezes cada padrão.",
        RestPercent = 4,
        Steps = new List<DrillStep>
        {
            new()
            {
                Label = "Acelerador: pisar rápido em 20%",
                Pedal = DrillPedal.Throttle,
                TargetPercent = 20,
                TolerancePercent = 4,
                Repetitions = 8,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(0.30),
                    DrillSegment.Hold(0.8),
                    DrillSegment.Release(1.5)
                }
            },
            new()
            {
                Label = "Acelerador: pisar rápido em 50%",
                Pedal = DrillPedal.Throttle,
                TargetPercent = 50,
                TolerancePercent = 4,
                Repetitions = 8,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(0.40),
                    DrillSegment.Hold(1.0),
                    DrillSegment.Release(3.0)
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
                    DrillSegment.Ramp(0.40, 50),
                    DrillSegment.Hold(0.8),
                    DrillSegment.Ramp(5.0),
                    DrillSegment.Hold(1.2),
                    DrillSegment.Release(6.0)
                }
            },
            new()
            {
                Label = "Acelerador: pisar rápido em 80% e sustentar",
                Pedal = DrillPedal.Throttle,
                TargetPercent = 80,
                TolerancePercent = 5,
                Repetitions = 6,
                Segments = new List<DrillSegment>
                {
                    DrillSegment.Ramp(0.45),
                    DrillSegment.Hold(2.0),
                    DrillSegment.Release(4.5)
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
