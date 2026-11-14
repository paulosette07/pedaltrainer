using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PedalTrainer.Core.Telemetry.Recording;

/// <summary>
/// Texto do arquivo de telemetria: cabeçalho com a configuração do jogo em uso e uma linha
/// por amostra. As colunas seguem o perfil, então o arquivo só tem o que o jogo publica.
/// </summary>
public static class TelemetryLogFormatter
{
    private const string Separator = ",";
    private const string HeaderPrefix = "# ";
    private const string NumberFormat = "0.###";

    /// <summary>
    /// Linhas de cabeçalho (comentários) que documentam a gravação, mais a linha de colunas.
    /// </summary>
    public static IReadOnlyList<string> BuildHeader(
        TelemetryProfile profile,
        string simulatorName,
        DateTime startedAtLocal)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var lines = new List<string>(10)
        {
            HeaderPrefix + "Pedal Trainer - telemetria",
            HeaderPrefix + "simulador: " + simulatorName,
            HeaderPrefix + "perfil: " + profile.DisplayName,
            HeaderPrefix + "blocos: " + DescribeBlocks(profile),
            HeaderPrefix + "canais: " + profile.ChannelSummary,
            HeaderPrefix + "taxa: " + profile.RecordingRateHz.ToString("0.#", CultureInfo.InvariantCulture) + " Hz",
            HeaderPrefix + "inicio: " + startedAtLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            HeaderPrefix + "ajuste no jogo: " + profile.SetupHint,
            BuildColumnLine(profile)
        };

        return lines;
    }

    private static string DescribeBlocks(TelemetryProfile profile) =>
        profile.MemoryMapNames.Count == 0
            ? "detectado ao conectar"
            : string.Join(" | ", profile.MemoryMapNames);

    /// <summary>Linha com o nome das colunas.</summary>
    public static string BuildColumnLine(TelemetryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        IReadOnlyList<TelemetryColumn> columns = profile.Columns;
        var builder = new StringBuilder(columns.Count * 16);

        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(Separator);
            }

            builder.Append(columns[index].Header);
        }

        return builder.ToString();
    }

    /// <summary>Uma linha de dados, com apenas as colunas que o jogo publica.</summary>
    public static string BuildRow(TelemetryProfile profile, in TelemetryLogSample sample)
    {
        ArgumentNullException.ThrowIfNull(profile);

        IReadOnlyList<TelemetryColumn> columns = profile.Columns;
        var builder = new StringBuilder(columns.Count * 10);

        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(Separator);
            }

            builder.Append(FormatValue(columns[index].Channel, sample));
        }

        return builder.ToString();
    }

    private static string FormatValue(TelemetryChannel channel, in TelemetryLogSample sample) => channel switch
    {
        TelemetryChannel.Speed => sample.SpeedKmh.ToString(NumberFormat, CultureInfo.InvariantCulture),
        TelemetryChannel.Brake => sample.BrakePercent.ToString(NumberFormat, CultureInfo.InvariantCulture),
        TelemetryChannel.Throttle => sample.ThrottlePercent.ToString(NumberFormat, CultureInfo.InvariantCulture),
        TelemetryChannel.LapDistance =>
            (sample.LapDistancePercent * 100).ToString(NumberFormat, CultureInfo.InvariantCulture),
        TelemetryChannel.LapNumber => sample.LapNumber.ToString(CultureInfo.InvariantCulture),
        TelemetryChannel.SessionTime =>
            sample.SessionTimeSeconds.ToString(NumberFormat, CultureInfo.InvariantCulture),
        _ => sample.ElapsedSeconds.ToString(NumberFormat, CultureInfo.InvariantCulture)
    };
}
