namespace PedalTrainer.Core.Drills;

/// <summary>Um programa da biblioteca: pode ter vindo do disco ou ser um padrão em memória.</summary>
public sealed class DrillProgramEntry
{
    public required string FilePath { get; init; }

    /// <summary>Programa carregado; nulo quando o arquivo está inválido.</summary>
    public required DrillProgram? Program { get; init; }

    /// <summary>Motivo da falha de leitura, quando houver.</summary>
    public required string Error { get; init; }

    /// <summary>Verdadeiro quando o programa pode ser executado.</summary>
    public bool IsValid => Program is not null;

    /// <summary>Nome exibido na lista.</summary>
    public string DisplayName => Program?.Name ?? System.IO.Path.GetFileName(FilePath);

    /// <summary>Resumo de uma linha com pedal, etapas e repetições.</summary>
    public string Summary
    {
        get
        {
            if (Program is not { } program)
            {
                return Error;
            }

            int minutes = (int)System.Math.Round(program.EstimatedSeconds / 60.0);
            string pedal = DrillPedalNames.Short(program.PrimaryPedal);
            return $"{pedal} · {program.Steps.Count} etapas · {program.TotalRepetitions} repetições · ~{minutes} min";
        }
    }
}
