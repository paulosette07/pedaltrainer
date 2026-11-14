using System.Collections.Generic;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Coluna do arquivo de telemetria. A ordem da lista é a ordem do CSV; cada jogo pode
/// publicar um subconjunto, e a tela mostra exatamente as colunas que serão gravadas.
/// </summary>
public sealed record TelemetryColumn(TelemetryChannel Channel, string Header, string Description)
{
    private static readonly TelemetryColumn SpeedColumn = new(
        TelemetryChannel.Speed, "velocidade_kmh", "km/h");

    private static readonly TelemetryColumn BrakeColumn = new(
        TelemetryChannel.Brake, "freio_pct", "%");

    private static readonly TelemetryColumn ThrottleColumn = new(
        TelemetryChannel.Throttle, "acelerador_pct", "%");

    private static readonly TelemetryColumn LapDistanceColumn = new(
        TelemetryChannel.LapDistance, "posicao_volta_pct", "% da volta");

    private static readonly TelemetryColumn LapNumberColumn = new(
        TelemetryChannel.LapNumber, "volta", "número da volta");

    private static readonly TelemetryColumn SessionTimeColumn = new(
        TelemetryChannel.SessionTime, "tempo_sessao_s", "s");

    private static readonly TelemetryColumn ElapsedColumn = new(
        TelemetryChannel.None, "tempo_gravacao_s", "s desde o início da gravação");

    /// <summary>Coluna do relógio da própria gravação (sempre presente).</summary>
    public static TelemetryColumn Elapsed { get; } = ElapsedColumn;

    /// <summary>Colunas de um conjunto de canais, na ordem canônica do arquivo.</summary>
    public static IReadOnlyList<TelemetryColumn> For(TelemetryChannel channels)
    {
        var columns = new List<TelemetryColumn>(7) { Elapsed };

        if ((channels & TelemetryChannel.Speed) != 0)
        {
            columns.Add(SpeedColumn);
        }

        if ((channels & TelemetryChannel.Brake) != 0)
        {
            columns.Add(BrakeColumn);
        }

        if ((channels & TelemetryChannel.Throttle) != 0)
        {
            columns.Add(ThrottleColumn);
        }

        if ((channels & TelemetryChannel.LapDistance) != 0)
        {
            columns.Add(LapDistanceColumn);
        }

        if ((channels & TelemetryChannel.LapNumber) != 0)
        {
            columns.Add(LapNumberColumn);
        }

        if ((channels & TelemetryChannel.SessionTime) != 0)
        {
            columns.Add(SessionTimeColumn);
        }

        return columns;
    }
}
