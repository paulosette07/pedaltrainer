using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Core.Telemetry.AssettoCorsaCompetizione;

/// <summary>
/// Deslocamentos das páginas de memória compartilhada do Assetto Corsa Competizione
/// (estruturas <c>SPageFilePhysics</c> e <c>SPageFileGraphic</c> do SDK oficial).
/// Só os campos usados pelo treino são mapeados, na ordem exata do cabeçalho.
/// </summary>
public static class AccSharedMemoryLayout
{
    /// <summary>Nome do bloco de física (atualizado a cada passo da simulação).</summary>
    public const string PhysicsMapName = "Local\\acpmf_physics";

    /// <summary>Nome do bloco gráfico (atualizado a cada quadro).</summary>
    public const string GraphicsMapName = "Local\\acpmf_graphics";

    /// <summary>Estado publicado quando o carro está na pista.</summary>
    public const int StatusLive = 2;

    // ---- SPageFilePhysics (prefixo usado) ----
    public static int PacketIdOffset { get; } = 0;

    public static int GasOffset { get; } = BuildPhysics().Gas;

    public static int BrakeOffset { get; } = BuildPhysics().Brake;

    public static int SpeedKmhOffset { get; } = BuildPhysics().SpeedKmh;

    // ---- SPageFileGraphic (prefixo usado) ----
    public static int StatusOffset { get; } = BuildGraphics().Status;

    public static int CompletedLapsOffset { get; } = BuildGraphics().CompletedLaps;

    public static int NormalizedPositionOffset { get; } = BuildGraphics().NormalizedCarPosition;

    private static PhysicsFields BuildPhysics()
    {
        var layout = new FieldLayout();

        int packetId = layout.AddInt();
        int gas = layout.AddFloat();
        int brake = layout.AddFloat();
        layout.AddFloat();          // fuel
        layout.AddInt();            // gear
        layout.AddInt();            // rpms
        layout.AddFloat();          // steerAngle
        int speedKmh = layout.AddFloat();

        return new PhysicsFields(packetId, gas, brake, speedKmh);
    }

    /// <summary>
    /// A página gráfica guarda textos largos (wchar_t de 2 bytes) antes dos números;
    /// por isso os deslocamentos de <c>completedLaps</c> e <c>normalizedCarPosition</c>
    /// dependem da transcrição integral desse prefixo.
    /// </summary>
    private static GraphicsFields BuildGraphics()
    {
        var layout = new FieldLayout();

        int packetId = layout.AddInt();
        int status = layout.AddInt();
        layout.AddInt();            // session
        layout.AddWideString(15);   // currentTime
        layout.AddWideString(15);   // lastTime
        layout.AddWideString(15);   // bestTime
        layout.AddWideString(15);   // split
        int completedLaps = layout.AddInt();
        layout.AddInt();            // position
        layout.AddInt();            // iCurrentTime
        layout.AddInt();            // iLastTime
        layout.AddInt();            // iBestTime
        layout.AddFloat();          // sessionTimeLeft
        layout.AddFloat();          // distanceTraveled
        layout.AddInt();            // isInPit
        layout.AddInt();            // currentSectorIndex
        layout.AddInt();            // lastSectorTime
        layout.AddInt();            // numberOfLaps
        layout.AddWideString(33);   // tyreCompound
        layout.AddFloat();          // replayTimeMultiplier
        int normalizedCarPosition = layout.AddFloat();

        return new GraphicsFields(packetId, status, completedLaps, normalizedCarPosition);
    }

    private readonly record struct PhysicsFields(int PacketId, int Gas, int Brake, int SpeedKmh);

    private readonly record struct GraphicsFields(int PacketId, int Status, int CompletedLaps, int NormalizedCarPosition);
}
