using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Core.Telemetry.LeMansUltimate;

/// <summary>
/// Deslocamentos dos blocos do plugin "rFactor 2 Shared Memory Map", que também é o caminho
/// de telemetria do Le Mans Ultimate (mesmas estruturas de <c>rF2State.h</c>, compiladas com
/// <c>#pragma pack(4)</c>). Cada bloco começa com o cabeçalho de versão
/// (<c>mVersionUpdateBegin</c>, <c>mVersionUpdateEnd</c>, <c>mBytesUpdatedHint</c>).
/// </summary>
public static class RFactorSharedMemoryLayout
{
    /// <summary>Bloco de telemetria do carro.</summary>
    public const string TelemetryMapName = "$rFactor2SMMP_Telemetry$";

    /// <summary>Bloco de classificação (posição na pista, voltas e tempo de sessão).</summary>
    public const string ScoringMapName = "$rFactor2SMMP_Scoring$";

    /// <summary>Bytes do cabeçalho de versão comum aos blocos mapeados.</summary>
    public const int BufferHeaderBytes = 12;

    /// <summary>Quantidade máxima de carros que o plugin publica por bloco.</summary>
    public const int MaxMappedVehicles = 128;

    // ---- Cabeçalho de versão ----
    public const int VersionBeginOffset = 0;
    public const int VersionEndOffset = 4;
    public const int BytesUpdatedHintOffset = 8;

    // ---- rF2Telemetry ----
    /// <summary>Quantidade de carros no bloco de telemetria.</summary>
    public static int TelemetryVehicleCountOffset { get; } = BufferHeaderBytes;

    /// <summary>Primeiro <c>rF2VehicleTelemetry</c> do bloco.</summary>
    public static int TelemetryVehiclesOffset { get; } = BufferHeaderBytes + sizeof(int);

    public static int TelemetryVehicleSize { get; } = BuildVehicleTelemetry().Size;

    public static int TelemetryElapsedTimeOffset { get; } = BuildVehicleTelemetry().ElapsedTime;

    public static int TelemetryLapNumberOffset { get; } = BuildVehicleTelemetry().LapNumber;

    public static int TelemetryLocalVelocityOffset { get; } = BuildVehicleTelemetry().LocalVelocity;

    public static int TelemetryFilteredThrottleOffset { get; } = BuildVehicleTelemetry().FilteredThrottle;

    public static int TelemetryFilteredBrakeOffset { get; } = BuildVehicleTelemetry().FilteredBrake;

    public static int TelemetryGearOffset { get; } = BuildVehicleTelemetry().Gear;

    // ---- rF2Scoring ----
    /// <summary>Primeiro <c>rF2VehicleScoring</c> do bloco (após o cabeçalho e o resumo).</summary>
    public static int ScoringVehiclesOffset { get; } = BufferHeaderBytes + BuildScoringInfo().Size;

    public static int ScoringVehicleSize { get; } = BuildVehicleScoring().Size;

    public static int ScoringTrackLengthOffset { get; } = BufferHeaderBytes + BuildScoringInfo().LapDist;

    public static int ScoringCurrentTimeOffset { get; } = BufferHeaderBytes + BuildScoringInfo().CurrentElapsedTime;

    public static int ScoringVehicleCountOffset { get; } = BufferHeaderBytes + BuildScoringInfo().NumVehicles;

    public static int ScoringVehicleLapDistanceOffset { get; } = BuildVehicleScoring().LapDist;

    public static int ScoringVehicleTotalLapsOffset { get; } = BuildVehicleScoring().TotalLaps;

    public static int ScoringVehicleIsPlayerOffset { get; } = BuildVehicleScoring().IsPlayer;

    /// <summary>
    /// <c>TelemWheelV01</c> (<c>rF2Wheel</c>): 4 pneus no fim da struct de telemetria.
    /// Transcrito para que o tamanho do carro saia do próprio código, sem número mágico.
    /// </summary>
    private static int BuildWheelSize()
    {
        var layout = new FieldLayout();

        layout.AddDouble();         // mSuspensionDeflection
        layout.AddDouble();         // mRideHeight
        layout.AddDouble();         // mSuspForce
        layout.AddDouble();         // mBrakeTemp
        layout.AddDouble();         // mBrakePressure
        layout.AddDouble();         // mRotation
        layout.AddDouble();         // mLateralPatchVel
        layout.AddDouble();         // mLongitudinalPatchVel
        layout.AddDouble();         // mLateralGroundVel
        layout.AddDouble();         // mLongitudinalGroundVel
        layout.AddDouble();         // mCamber
        layout.AddDouble();         // mLateralForce
        layout.AddDouble();         // mLongitudinalForce
        layout.AddDouble();         // mTireLoad
        layout.AddDouble();         // mGripFract
        layout.AddDouble();         // mPressure
        layout.AddDouble();         // mTemperature[0]
        layout.AddDouble();         // mTemperature[1]
        layout.AddDouble();         // mTemperature[2]
        layout.AddDouble();         // mWear
        layout.AddBytes(16);        // mTerrainName
        layout.AddByte();           // mSurfaceType
        layout.AddBool();           // mFlat
        layout.AddBool();           // mDetached
        layout.AddByte();           // mStaticUndeflectedRadius
        layout.AddDouble();         // mVerticalTireDeflection
        layout.AddDouble();         // mWheelYLocation
        layout.AddDouble();         // mToe
        layout.AddDouble();         // mTireCarcassTemperature
        layout.AddDouble();         // mTireInnerLayerTemperature[0]
        layout.AddDouble();         // mTireInnerLayerTemperature[1]
        layout.AddDouble();         // mTireInnerLayerTemperature[2]
        layout.AddBytes(24);        // mExpansion

        return layout.Size;
    }

    /// <summary><c>rF2VehicleTelemetry</c> / <c>TelemInfoV01</c>.</summary>
    private static VehicleTelemetryFields BuildVehicleTelemetry()
    {
        var layout = new FieldLayout();

        layout.AddInt();                // mID
        layout.AddDouble();             // mDeltaTime
        int elapsedTime = layout.AddDouble();
        int lapNumber = layout.AddInt();
        layout.AddDouble();             // mLapStartET
        layout.AddBytes(64);            // mVehicleName
        layout.AddBytes(64);            // mTrackName

        layout.AddDouble();             // mPos.x
        layout.AddDouble();             // mPos.y
        layout.AddDouble();             // mPos.z
        int localVelocity = layout.AddDouble();
        layout.AddDouble();             // mLocalVel.y
        layout.AddDouble();             // mLocalVel.z
        layout.AddDouble();             // mLocalAccel.x
        layout.AddDouble();             // mLocalAccel.y
        layout.AddDouble();             // mLocalAccel.z

        for (var i = 0; i < 9; i++)     // mOri[3] (matriz 3x3)
        {
            layout.AddDouble();
        }

        layout.AddDouble();             // mLocalRot.x
        layout.AddDouble();             // mLocalRot.y
        layout.AddDouble();             // mLocalRot.z
        layout.AddDouble();             // mLocalRotAccel.x
        layout.AddDouble();             // mLocalRotAccel.y
        layout.AddDouble();             // mLocalRotAccel.z

        int gear = layout.AddInt();
        layout.AddDouble();             // mEngineRPM
        layout.AddDouble();             // mEngineWaterTemp
        layout.AddDouble();             // mEngineOilTemp
        layout.AddDouble();             // mClutchRPM

        layout.AddDouble();             // mUnfilteredThrottle
        layout.AddDouble();             // mUnfilteredBrake
        layout.AddDouble();             // mUnfilteredSteering
        layout.AddDouble();             // mUnfilteredClutch
        int filteredThrottle = layout.AddDouble();
        int filteredBrake = layout.AddDouble();
        layout.AddDouble();             // mFilteredSteering
        layout.AddDouble();             // mFilteredClutch

        layout.AddDouble();             // mSteeringShaftTorque
        layout.AddDouble();             // mFront3rdDeflection
        layout.AddDouble();             // mRear3rdDeflection

        layout.AddDouble();             // mFrontWingHeight
        layout.AddDouble();             // mFrontRideHeight
        layout.AddDouble();             // mRearRideHeight
        layout.AddDouble();             // mDrag
        layout.AddDouble();             // mFrontDownforce
        layout.AddDouble();             // mRearDownforce

        layout.AddDouble();             // mFuel
        layout.AddDouble();             // mEngineMaxRPM
        layout.AddByte();               // mScheduledStops
        layout.AddBool();               // mOverheating
        layout.AddBool();               // mDetached
        layout.AddBool();               // mHeadlights
        layout.AddBytes(8);             // mDentSeverity
        layout.AddDouble();             // mLastImpactET
        layout.AddDouble();             // mLastImpactMagnitude
        layout.AddDouble();             // mLastImpactPos.x
        layout.AddDouble();             // mLastImpactPos.y
        layout.AddDouble();             // mLastImpactPos.z

        layout.AddDouble();             // mEngineTorque
        layout.AddInt();                // mCurrentSector
        layout.AddByte();               // mSpeedLimiter
        layout.AddByte();               // mMaxGears
        layout.AddByte();               // mFrontTireCompoundIndex
        layout.AddByte();               // mRearTireCompoundIndex
        layout.AddDouble();             // mFuelCapacity
        layout.AddByte();               // mFrontFlapActivated
        layout.AddByte();               // mRearFlapActivated
        layout.AddByte();               // mRearFlapLegalStatus
        layout.AddByte();               // mIgnitionStarter
        layout.AddBytes(18);            // mFrontTireCompoundName
        layout.AddBytes(18);            // mRearTireCompoundName
        layout.AddByte();               // mSpeedLimiterAvailable
        layout.AddByte();               // mAntiStallActivated
        layout.AddBytes(2);             // mUnused
        layout.AddFloat();              // mVisualSteeringWheelRange
        layout.AddDouble();             // mRearBrakeBias
        layout.AddDouble();             // mTurboBoostPressure
        layout.AddArray(3);             // mPhysicsToGraphicsOffset
        layout.AddFloat();              // mPhysicalSteeringWheelRange
        layout.AddDouble();             // mBatteryChargeFraction
        layout.AddDouble();             // mElectricBoostMotorTorque
        layout.AddDouble();             // mElectricBoostMotorRPM
        layout.AddDouble();             // mElectricBoostMotorTemperature
        layout.AddDouble();             // mElectricBoostWaterTemperature
        layout.AddByte();               // mElectricBoostMotorState
        layout.AddBytes(111);           // mExpansion
        layout.AddBytes(BuildWheelSize() * 4); // mWheels[4]

        return new VehicleTelemetryFields(
            elapsedTime,
            lapNumber,
            localVelocity,
            filteredThrottle,
            filteredBrake,
            gear,
            layout.Size);
    }

    /// <summary><c>rF2ScoringInfo</c> / <c>ScoringInfoV01</c>.</summary>
    private static ScoringInfoFields BuildScoringInfo()
    {
        var layout = new FieldLayout();

        layout.AddBytes(64);            // mTrackName
        layout.AddInt();                // mSession
        int currentElapsedTime = layout.AddDouble();
        layout.AddDouble();             // mEndET
        layout.AddInt();                // mMaxLaps
        int lapDist = layout.AddDouble();
        layout.AddBytes(8);             // mResultsStream (ponteiro opaco em 64 bits)
        int numVehicles = layout.AddInt();
        layout.AddByte();               // mGamePhase
        layout.AddByte();               // mYellowFlagState
        layout.AddBytes(3);             // mSectorFlag
        layout.AddByte();               // mStartLight
        layout.AddByte();               // mNumRedLights
        layout.AddBool();               // mInRealtime
        layout.AddBytes(32);            // mPlayerName
        layout.AddBytes(64);            // mPlrFileName

        layout.AddDouble();             // mDarkCloud
        layout.AddDouble();             // mRaining
        layout.AddDouble();             // mAmbientTemp
        layout.AddDouble();             // mTrackTemp
        layout.AddDouble();             // mWind.x
        layout.AddDouble();             // mWind.y
        layout.AddDouble();             // mWind.z
        layout.AddDouble();             // mMinPathWetness
        layout.AddDouble();             // mMaxPathWetness

        layout.AddByte();               // mGameMode
        layout.AddBool();               // mIsPasswordProtected
        layout.AddShort();              // mServerPort
        layout.AddInt();                // mServerPublicIP
        layout.AddInt();                // mMaxPlayers
        layout.AddBytes(32);            // mServerName
        layout.AddFloat();              // mStartET
        layout.AddDouble();             // mAvgPathWetness
        layout.AddBytes(200);           // mExpansion
        layout.AddBytes(8);             // mVehicle (ponteiro opaco em 64 bits)

        return new ScoringInfoFields(currentElapsedTime, lapDist, numVehicles, layout.Size);
    }

    /// <summary><c>rF2VehicleScoring</c> / <c>VehicleScoringInfoV01</c>.</summary>
    private static VehicleScoringFields BuildVehicleScoring()
    {
        var layout = new FieldLayout();

        layout.AddInt();                // mID
        layout.AddBytes(32);            // mDriverName
        layout.AddBytes(64);            // mVehicleName
        int totalLaps = layout.AddShort();
        layout.AddByte();               // mSector
        layout.AddByte();               // mFinishStatus
        int lapDist = layout.AddDouble();
        layout.AddDouble();             // mPathLateral
        layout.AddDouble();             // mTrackEdge

        layout.AddDouble();             // mBestSector1
        layout.AddDouble();             // mBestSector2
        layout.AddDouble();             // mBestLapTime
        layout.AddDouble();             // mLastSector1
        layout.AddDouble();             // mLastSector2
        layout.AddDouble();             // mLastLapTime
        layout.AddDouble();             // mCurSector1
        layout.AddDouble();             // mCurSector2

        layout.AddShort();              // mNumPitstops
        layout.AddShort();              // mNumPenalties
        int isPlayer = layout.AddBool();
        layout.AddByte();               // mControl
        layout.AddBool();               // mInPits
        layout.AddByte();               // mPlace
        layout.AddBytes(32);            // mVehicleClass

        layout.AddDouble();             // mTimeBehindNext
        layout.AddInt();                // mLapsBehindNext
        layout.AddDouble();             // mTimeBehindLeader
        layout.AddInt();                // mLapsBehindLeader
        layout.AddDouble();             // mLapStartET

        layout.AddDouble();             // mPos.x
        layout.AddDouble();             // mPos.y
        layout.AddDouble();             // mPos.z
        layout.AddDouble();             // mLocalVel.x
        layout.AddDouble();             // mLocalVel.y
        layout.AddDouble();             // mLocalVel.z
        layout.AddDouble();             // mLocalAccel.x
        layout.AddDouble();             // mLocalAccel.y
        layout.AddDouble();             // mLocalAccel.z

        for (var i = 0; i < 9; i++)     // mOri[3]
        {
            layout.AddDouble();
        }

        layout.AddDouble();             // mLocalRot.x
        layout.AddDouble();             // mLocalRot.y
        layout.AddDouble();             // mLocalRot.z
        layout.AddDouble();             // mLocalRotAccel.x
        layout.AddDouble();             // mLocalRotAccel.y
        layout.AddDouble();             // mLocalRotAccel.z

        layout.AddByte();               // mHeadlights
        layout.AddByte();               // mPitState
        layout.AddByte();               // mServerScored
        layout.AddByte();               // mIndividualPhase
        layout.AddInt();                // mQualification
        layout.AddDouble();             // mTimeIntoLap
        layout.AddDouble();             // mEstimatedLapTime
        layout.AddBytes(24);            // mPitGroup
        layout.AddByte();               // mFlag
        layout.AddBool();               // mUnderYellow
        layout.AddByte();               // mCountLapFlag
        layout.AddBool();               // mInGarageStall
        layout.AddBytes(16);            // mUpgradePack
        layout.AddFloat();              // mPitLapDist
        layout.AddFloat();              // mBestLapSector1
        layout.AddFloat();              // mBestLapSector2
        layout.AddBytes(48);            // mExpansion

        return new VehicleScoringFields(lapDist, totalLaps, isPlayer, layout.Size);
    }

    private readonly record struct VehicleTelemetryFields(
        int ElapsedTime,
        int LapNumber,
        int LocalVelocity,
        int FilteredThrottle,
        int FilteredBrake,
        int Gear,
        int Size);

    private readonly record struct ScoringInfoFields(
        int CurrentElapsedTime,
        int LapDist,
        int NumVehicles,
        int Size);

    private readonly record struct VehicleScoringFields(
        int LapDist,
        int TotalLaps,
        int IsPlayer,
        int Size);
}
