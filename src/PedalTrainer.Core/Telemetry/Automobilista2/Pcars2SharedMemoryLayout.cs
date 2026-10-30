using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Core.Telemetry.Automobilista2;

/// <summary>
/// Deslocamentos da memória compartilhada que o Automobilista 2 publica no formato
/// "Project CARS 2" (<c>struct pcars2APIStruct</c>, compilado com <c>#pragma pack(4)</c>).
/// O jogo só publica nesse formato quando Options > System > Shared Memory está em
/// "Project CARS 2".
/// </summary>
public static class Pcars2SharedMemoryLayout
{
    /// <summary>Nome do bloco publicado pelo jogo.</summary>
    public const string MemoryMapName = "$pcars2$";

    /// <summary>Jogo rodando e com o carro na pista (<c>GAME_INGAME_PLAYING</c>).</summary>
    public const int GameStateInGamePlaying = 2;

    /// <summary>Jogo pausado, mas ainda dentro da sessão (<c>GAME_INGAME_PAUSED</c>).</summary>
    public const int GameStateInGamePaused = 3;

    /// <summary>Quantidade máxima de pilotos que o jogo mantém no bloco.</summary>
    public const int StoredParticipantsMax = 64;

    /// <summary>Tamanho de <c>ParticipantInfo</c> em bytes.</summary>
    public static int ParticipantSize { get; } = BuildParticipant().Size;

    /// <summary>Posição do vetor de pilotos dentro da struct principal.</summary>
    public static int ParticipantsOffset { get; } = BuildMain().Participants;

    public static int ViewedParticipantOffset { get; } = BuildMain().ViewedParticipant;

    public static int GameStateOffset { get; } = BuildMain().GameState;

    public static int TrackLengthOffset { get; } = BuildMain().TrackLength;

    public static int SpeedOffset { get; } = BuildMain().Speed;

    public static int BrakeOffset { get; } = BuildMain().Brake;

    public static int ThrottleOffset { get; } = BuildMain().Throttle;

    // ---- ParticipantInfo ----
    public static int ParticipantLapDistanceOffset { get; } = BuildParticipant().LapDistance;

    public static int ParticipantLapsCompletedOffset { get; } = BuildParticipant().LapsCompleted;

    public static int ParticipantCurrentLapOffset { get; } = BuildParticipant().CurrentLap;

    private static ParticipantFields BuildParticipant()
    {
        var layout = new FieldLayout();

        layout.AddBool();           // mIsActive
        layout.AddBytes(64);        // mName
        layout.AddArray(3);         // mWorldPosition[3]
        int lapDistance = layout.AddFloat();
        layout.AddInt();            // mRacePosition
        int lapsCompleted = layout.AddInt();
        int currentLap = layout.AddInt();
        layout.AddInt();            // mCurrentSector

        return new ParticipantFields(lapDistance, lapsCompleted, currentLap, layout.Size);
    }

    /// <summary>
    /// Transcreve o prefixo de <c>pcars2APIStruct</c> até os pedais: a lista de pilotos
    /// (64 × <c>ParticipantInfo</c>) vem antes dos campos do carro, então qualquer campo
    /// esquecido aqui deslocaria velocidade e freio.
    /// </summary>
    private static MainFields BuildMain()
    {
        var layout = new FieldLayout();

        layout.AddInt();            // mVersion
        layout.AddInt();            // mBuildVersionNumber
        int gameState = layout.AddInt();
        layout.AddInt();            // mSessionState
        layout.AddInt();            // mRaceState
        int viewedParticipant = layout.AddInt();
        layout.AddInt();            // mNumParticipants

        layout.AlignToPack();
        int participants = layout.Offset;
        layout.AddBytes(ParticipantSize * StoredParticipantsMax);

        layout.AddFloat();          // mUnfilteredThrottle
        layout.AddFloat();          // mUnfilteredBrake
        layout.AddFloat();          // mUnfilteredSteering
        layout.AddFloat();          // mUnfilteredClutch
        layout.AddBytes(64);        // mCarName
        layout.AddBytes(64);        // mCarClassName
        layout.AddInt();            // mLapsInEvent
        layout.AddBytes(64);        // mTrackLocation
        layout.AddBytes(64);        // mTrackVariation
        int trackLength = layout.AddFloat();
        layout.AddInt();            // mNumSectors
        layout.AddBool();           // mLapInvalidated

        // Tempos: volta atual, setores e recordes pessoais/mundiais.
        layout.AddFloat();          // mBestLapTime
        layout.AddFloat();          // mLastLapTime
        layout.AddFloat();          // mCurrentTime
        layout.AddFloat();          // mSplitTimeAhead
        layout.AddFloat();          // mSplitTimeBehind
        layout.AddFloat();          // mSplitTime
        layout.AddFloat();          // mEventTimeRemaining
        layout.AddFloat();          // mPersonalFastestLapTime
        layout.AddFloat();          // mWorldFastestLapTime
        layout.AddFloat();          // mCurrentSector1Time
        layout.AddFloat();          // mCurrentSector2Time
        layout.AddFloat();          // mCurrentSector3Time
        layout.AddFloat();          // mFastestSector1Time
        layout.AddFloat();          // mFastestSector2Time
        layout.AddFloat();          // mFastestSector3Time
        layout.AddFloat();          // mPersonalFastestSector1Time
        layout.AddFloat();          // mPersonalFastestSector2Time
        layout.AddFloat();          // mPersonalFastestSector3Time
        layout.AddFloat();          // mWorldFastestSector1Time
        layout.AddFloat();          // mWorldFastestSector2Time
        layout.AddFloat();          // mWorldFastestSector3Time

        layout.AddInt();            // mHighestFlagColour
        layout.AddInt();            // mHighestFlagReason
        layout.AddInt();            // mPitMode
        layout.AddInt();            // mPitSchedule
        layout.AddInt();            // mCarFlags

        layout.AddFloat();          // mOilTempCelsius
        layout.AddFloat();          // mOilPressureKPa
        layout.AddFloat();          // mWaterTempCelsius
        layout.AddFloat();          // mWaterPressureKPa
        layout.AddFloat();          // mFuelPressureKPa
        layout.AddFloat();          // mFuelLevel
        layout.AddFloat();          // mFuelCapacity

        int speed = layout.AddFloat();
        layout.AddFloat();          // mRpm
        layout.AddFloat();          // mMaxRPM
        int brake = layout.AddFloat();
        int throttle = layout.AddFloat();

        return new MainFields(gameState, viewedParticipant, participants, trackLength, speed, brake, throttle);
    }

    private readonly record struct ParticipantFields(
        int LapDistance,
        int LapsCompleted,
        int CurrentLap,
        int Size);

    private readonly record struct MainFields(
        int GameState,
        int ViewedParticipant,
        int Participants,
        int TrackLength,
        int Speed,
        int Brake,
        int Throttle);
}
