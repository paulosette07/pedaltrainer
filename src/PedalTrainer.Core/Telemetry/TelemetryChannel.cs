using System;

namespace PedalTrainer.Core.Telemetry;

/// <summary>
/// Canais que um simulador publica. A tela de telemetria mostra apenas o que o jogo
/// escolhido entrega de verdade, e o arquivo gravado repete essa mesma lista de colunas.
/// </summary>
[Flags]
public enum TelemetryChannel
{
    /// <summary>Nenhum canal (usado só para comparações).</summary>
    None = 0,

    /// <summary>Velocidade do carro.</summary>
    Speed = 1 << 0,

    /// <summary>Pressão do freio.</summary>
    Brake = 1 << 1,

    /// <summary>Posição do acelerador.</summary>
    Throttle = 1 << 2,

    /// <summary>Fração da volta percorrida (0 a 1).</summary>
    LapDistance = 1 << 3,

    /// <summary>Número da volta atual.</summary>
    LapNumber = 1 << 4,

    /// <summary>Tempo de sessão publicado pelo jogo (só o iRacing informa).</summary>
    SessionTime = 1 << 5,

    /// <summary>Todos os canais do aplicativo.</summary>
    All = Speed | Brake | Throttle | LapDistance | LapNumber | SessionTime
}
