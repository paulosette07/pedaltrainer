using System;
using System.Globalization;
using System.Text;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Assinatura textual de um programa: descreve tudo o que define o treino (nome, descrição,
/// repouso, etapas e segmentos) num único texto comparável.
/// </summary>
/// <remarks>
/// O aplicativo usa a assinatura para reconhecer um arquivo padrão que o piloto nunca
/// editou: se a assinatura do arquivo é igual à de um programa antigo embutido, ele pode ser
/// regravado com a versão nova sem apagar ajuste nenhum de quem personalizou o treino.
/// </remarks>
internal static class DrillProgramSignature
{
    private const char FieldSeparator = '\u001f';

    /// <summary>Assinatura do programa informado.</summary>
    public static string Of(DrillProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        var builder = new StringBuilder();

        Append(builder, program.Name);
        Append(builder, program.Description);
        Append(builder, program.RestPercent);

        foreach (DrillStep step in program.Steps)
        {
            Append(builder, step.Label);
            Append(builder, step.Pedal.ToString());
            Append(builder, step.TargetPercent);
            Append(builder, step.TolerancePercent);
            Append(builder, step.Repetitions);
            Append(builder, step.RandomTargetMinPercent ?? double.NaN);
            Append(builder, step.RandomTargetMaxPercent ?? double.NaN);

            foreach (DrillSegment segment in step.Segments)
            {
                Append(builder, segment.Kind.ToString());
                Append(builder, segment.Seconds);
                Append(builder, segment.ToPercent ?? double.NaN);
            }
        }

        return builder.ToString();
    }

    /// <summary>Verdadeiro quando os dois programas são o mesmo treino, campo a campo.</summary>
    public static bool AreEqual(DrillProgram left, DrillProgram right) =>
        string.Equals(Of(left), Of(right), StringComparison.Ordinal);

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value).Append(FieldSeparator);

    private static void Append(StringBuilder builder, double value) =>
        builder.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(FieldSeparator);

    private static void Append(StringBuilder builder, int value) => Append(builder, (double)value);
}
