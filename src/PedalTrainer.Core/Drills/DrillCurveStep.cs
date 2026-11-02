using System.Globalization;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Um trecho do plano de uma repetição, já pronto para a tela: forma, percentuais, duração
/// e o texto que o piloto lê antes de começar ("suba rápido até 90%").
/// </summary>
public sealed record DrillCurveStep
{
    /// <summary>Forma do trecho (subida, espera ou soltura).</summary>
    public required DrillSegmentKind Kind { get; init; }

    /// <summary>Percentual de partida do trecho.</summary>
    public required double FromPercent { get; init; }

    /// <summary>Percentual de chegada do trecho.</summary>
    public required double ToPercent { get; init; }

    /// <summary>Duração do trecho, em segundos.</summary>
    public required double Seconds { get; init; }

    /// <summary>Instrução completa do trecho, a mesma lida durante a execução.</summary>
    public required string Text { get; init; }

    /// <summary>Verbo curto exibido como título do trecho na prévia da curva.</summary>
    public string Action => Kind switch
    {
        DrillSegmentKind.Hold => "SEGURAR",
        DrillSegmentKind.Release => "SOLTAR",
        _ => ToPercent >= FromPercent ? "SUBIR" : "DESCER"
    };

    /// <summary>Destino do trecho ("até 90%" ou, na espera, "em 90%").</summary>
    public string TargetText => Kind == DrillSegmentKind.Hold
        ? string.Create(CultureInfo.CurrentCulture, $"em {Format(ToPercent)}")
        : string.Create(CultureInfo.CurrentCulture, $"até {Format(ToPercent)}");

    /// <summary>Duração formatada ("0,8 s").</summary>
    public string TimeText => string.Create(CultureInfo.CurrentCulture, $"{Seconds:0.0} s");

    private static string Format(double percent) => string.Create(
        CultureInfo.CurrentCulture,
        $"{Math.Round(percent, 0, MidpointRounding.AwayFromZero):0}%");
}
