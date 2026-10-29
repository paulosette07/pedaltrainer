namespace PedalTrainer.Core.Drills;

/// <summary>Pedal movimentado por uma etapa do programa de treino.</summary>
public enum DrillPedal
{
    /// <summary>Pedal de freio.</summary>
    Brake = 0,

    /// <summary>Pedal de acelerador.</summary>
    Throttle = 1
}

/// <summary>Nomes usados na interface para cada pedal.</summary>
public static class DrillPedalNames
{
    /// <summary>Nome curto, para etiquetas e barras.</summary>
    public static string Short(DrillPedal pedal) => pedal switch
    {
        DrillPedal.Throttle => "ACELERADOR",
        _ => "FREIO"
    };

    /// <summary>Nome em minúsculas, para frases de instrução.</summary>
    public static string Long(DrillPedal pedal) => pedal switch
    {
        DrillPedal.Throttle => "acelerador",
        _ => "freio"
    };
}
