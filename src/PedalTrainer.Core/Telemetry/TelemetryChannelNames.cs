using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Nomes legíveis dos canais de telemetria. Ficam num só lugar porque a tela, o arquivo
/// gravado e as mensagens de status precisam falar exatamente a mesma língua.
/// </summary>
public static class TelemetryChannelNames
{
    private static readonly (TelemetryChannel Channel, string Name)[] Ordered =
    {
        (TelemetryChannel.Speed, "velocidade"),
        (TelemetryChannel.Brake, "freio"),
        (TelemetryChannel.Throttle, "acelerador"),
        (TelemetryChannel.LapDistance, "posição na volta"),
        (TelemetryChannel.LapNumber, "volta"),
        (TelemetryChannel.SessionTime, "tempo de sessão")
    };

    /// <summary>Lista os canais do conjunto, separados por vírgula.</summary>
    public static string Describe(TelemetryChannel channels)
    {
        var names = new List<string>(Ordered.Length);

        foreach ((TelemetryChannel channel, string name) in Ordered)
        {
            if ((channels & channel) == channel)
            {
                names.Add(name);
            }
        }

        return names.Count == 0 ? "nenhum canal" : string.Join(", ", names);
    }

    /// <summary>Nome de um canal isolado.</summary>
    public static string Of(TelemetryChannel channel)
    {
        foreach ((TelemetryChannel candidate, string name) in Ordered)
        {
            if (candidate == channel)
            {
                return name;
            }
        }

        return "canal";
    }

    /// <summary>
    /// Canais do aplicativo que o jogo não publica, em texto — a tela mostra isso como aviso
    /// em vez de deixar o piloto achando que o campo apenas não atualizou.
    /// </summary>
    public static string Missing(TelemetryChannel supported)
    {
        var missing = new List<string>(Ordered.Length);

        foreach ((TelemetryChannel channel, string name) in Ordered)
        {
            if ((supported & channel) != channel)
            {
                missing.Add(name);
            }
        }

        return missing.Count == 0 ? string.Empty : string.Join(", ", missing);
    }

    /// <summary>Verdadeiro quando o jogo entrega todos os canais do aplicativo.</summary>
    public static bool IsComplete(TelemetryChannel channels) => (channels & TelemetryChannel.All) == TelemetryChannel.All;
}
