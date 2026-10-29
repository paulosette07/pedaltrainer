using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Programa de treino completo: uma sequência de etapas executada do começo ao fim
/// (por exemplo, "freio progressivo 20/50/80 + alvos sorteados").
/// </summary>
public sealed class DrillProgram
{
    /// <summary>Versão do formato gravada nos arquivos de treino.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Nome do programa, exibido na lista.</summary>
    public string Name { get; set; } = "Novo programa";

    /// <summary>Descrição curta do objetivo do programa.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Versão do formato do arquivo.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Percentual abaixo do qual o pedal é considerado solto.</summary>
    public double RestPercent { get; set; } = 4.0;

    /// <summary>Etapas na ordem de execução.</summary>
    public List<DrillStep> Steps { get; set; } = new();

    /// <summary>Total de repetições do programa.</summary>
    [JsonIgnore]
    public int TotalRepetitions
    {
        get
        {
            int total = 0;
            foreach (DrillStep step in Steps)
            {
                total += Math.Max(0, step.Repetitions);
            }

            return total;
        }
    }

    /// <summary>Duração estimada do programa, em segundos.</summary>
    [JsonIgnore]
    public double EstimatedSeconds
    {
        get
        {
            double total = 0;
            foreach (DrillStep step in Steps)
            {
                total += step.RepetitionSeconds * Math.Max(0, step.Repetitions);
            }

            return total;
        }
    }

    /// <summary>Pedal predominante do programa, para rótulos da interface.</summary>
    [JsonIgnore]
    public DrillPedal PrimaryPedal
    {
        get
        {
            int brake = 0;
            int throttle = 0;

            foreach (DrillStep step in Steps)
            {
                if (step.Pedal == DrillPedal.Throttle)
                {
                    throttle += Math.Max(1, step.Repetitions);
                }
                else
                {
                    brake += Math.Max(1, step.Repetitions);
                }
            }

            return throttle > brake ? DrillPedal.Throttle : DrillPedal.Brake;
        }
    }

    /// <summary>Valida o programa inteiro antes de iniciar o treino.</summary>
    public bool TryValidate(out string error)
    {
        if (Steps.Count == 0)
        {
            error = "O programa não tem nenhuma etapa.";
            return false;
        }

        if (RestPercent < 0 || RestPercent > 20)
        {
            error = "O percentual de repouso precisa ficar entre 0 e 20%.";
            return false;
        }

        for (var i = 0; i < Steps.Count; i++)
        {
            if (!Steps[i].TryValidate(out string stepError))
            {
                error = $"Etapa {i + 1}: {stepError}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public DrillProgram Clone()
    {
        var copy = new DrillProgram
        {
            Name = Name,
            Description = Description,
            Version = Version,
            RestPercent = RestPercent,
            Steps = new List<DrillStep>(Steps.Count)
        };

        foreach (DrillStep step in Steps)
        {
            copy.Steps.Add(step.Clone());
        }

        return copy;
    }
}
