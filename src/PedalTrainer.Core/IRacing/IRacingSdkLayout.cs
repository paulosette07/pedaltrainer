namespace PedalTrainer.Core.IRacing;

/// <summary>
/// Layout binário da API de telemetria do iRacing (arquivos irsdk_defines.h / irsdk_py.
/// Os deslocamentos são fixos e documentados — é isso que permite ler a telemetria sem
/// depender do pacote oficial, que só existe para .NET Framework.
/// </summary>
public static class IRacingSdkLayout
{
    /// <summary>Nome do mapeamento de memória criado pelo simulador.</summary>
    public const string MemoryMapName = "Local\\IRSDKMemMapFileName";

    /// <summary>Nome alternativo usado por algumas versões.</summary>
    public const string FallbackMemoryMapName = "IRSDKMemMapFileName";

    // ---- irsdk_header (112 bytes) ----
    public const int HeaderVersionOffset = 0;
    public const int HeaderStatusOffset = 4;
    public const int HeaderTickRateOffset = 8;
    public const int HeaderNumVarsOffset = 24;
    public const int HeaderVarHeaderOffset = 28;
    public const int HeaderNumBufOffset = 32;
    public const int HeaderBufLenOffset = 36;
    public const int HeaderVarBufOffset = 48;

    /// <summary>Tamanho de cada irsdk_varBuf (tickCount, bufOffset, 2 de padding).</summary>
    public const int VarBufSize = 16;

    /// <summary>Quantidade máxima de buffers publicados pelo simulador.</summary>
    public const int MaxBuffers = 4;

    /// <summary>Bit de <c>status</c> que indica sim conectado.</summary>
    public const int StatusConnected = 1;

    // ---- irsdk_varHeader (144 bytes) ----
    public const int VarHeaderSize = 144;
    public const int VarTypeOffset = 0;
    public const int VarOffsetOffset = 4;
    public const int VarCountOffset = 8;
    public const int VarNameOffset = 16;
    public const int VarNameLength = 32;
    public const int VarDescOffset = 48;
    public const int VarDescLength = 64;
    public const int VarUnitOffset = 112;
    public const int VarUnitLength = 32;

    // ---- Tipos de variável (irsdk_VarType) ----
    public const int TypeChar = 0;
    public const int TypeBool = 1;
    public const int TypeInt = 2;
    public const int TypeBitField = 3;
    public const int TypeFloat = 4;
    public const int TypeDouble = 5;

    // ---- Canais usados pelo PedalTrainer ----
    public const string ChannelSpeed = "Speed";
    public const string ChannelBrake = "Brake";
    public const string ChannelThrottle = "Throttle";
    public const string ChannelLapDistPct = "LapDistPct";
    public const string ChannelSessionTime = "SessionTime";
    public const string ChannelLap = "Lap";
}
