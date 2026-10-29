namespace PedalTrainer.Core.Drills;

/// <summary>Forma de um trecho da repetição.</summary>
public enum DrillSegmentKind
{
    /// <summary>Subir (ou descer) o pedal até o percentual alvo em um tempo definido.</summary>
    Ramp = 0,

    /// <summary>Manter o pedal no percentual atual por um tempo.</summary>
    Hold = 1,

    /// <summary>Soltar o pedal até o percentual alvo em um tempo definido.</summary>
    Release = 2
}

/// <summary>
/// Um trecho da curva-alvo de uma repetição. A repetição é a sequência de segmentos
/// (por exemplo: subir até 20% em 0,4 s, segurar 1 s, soltar em 2,5 s).
/// </summary>
public sealed class DrillSegment
{
    /// <summary>Duração do trecho em segundos.</summary>
    public double Seconds { get; set; } = 1.0;

    /// <summary>Forma do trecho.</summary>
    public DrillSegmentKind Kind { get; set; } = DrillSegmentKind.Ramp;

    /// <summary>
    /// Percentual ao final do trecho. Quando nulo, usa o alvo da etapa —
    /// útil para etapas com alvo sorteado a cada repetição.
    /// </summary>
    public double? ToPercent { get; set; }

    public static DrillSegment Ramp(double seconds, double? toPercent = null) => new()
    {
        Kind = DrillSegmentKind.Ramp,
        Seconds = seconds,
        ToPercent = toPercent
    };

    public static DrillSegment Hold(double seconds) => new()
    {
        Kind = DrillSegmentKind.Hold,
        Seconds = seconds
    };

    public static DrillSegment Release(double seconds, double toPercent = 0) => new()
    {
        Kind = DrillSegmentKind.Release,
        Seconds = seconds,
        ToPercent = toPercent
    };

    public DrillSegment Clone() => new()
    {
        Kind = Kind,
        Seconds = Seconds,
        ToPercent = ToPercent
    };
}
