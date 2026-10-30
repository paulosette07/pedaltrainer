using PedalTrainer.Core.Telemetry.SharedMemory;

namespace PedalTrainer.Core.Telemetry.AssettoCorsaEvo;

/// <summary>
/// Deslocamentos da memória compartilhada do Assetto Corsa EVO, transcritos de
/// <c>SPageFilePhysics</c> e <c>SPageFileGraphicEvo</c> (cabeçalho publicado pela Kunos,
/// compilado com <c>#pragma pack(4)</c>).
/// </summary>
public static class AcEvoSharedMemoryLayout
{
    /// <summary>Bloco de física, atualizado a cada passo da simulação.</summary>
    public const string PhysicsMapName = "Local\\acevo_pmf_physics";

    /// <summary>Bloco gráfico, atualizado a cada quadro renderizado.</summary>
    public const string GraphicsMapName = "Local\\acevo_pmf_graphics";

    /// <summary>Estado publicado quando há sessão de pilotagem ao vivo.</summary>
    public const int StatusLive = 2;

    /// <summary>Tamanho de <c>SMEvoTyreState</c> (verificado por static_assert no cabeçalho).</summary>
    public const int TyreStateSize = 256;

    /// <summary>Tamanho de <c>SMEvoDamageState</c>.</summary>
    public const int DamageStateSize = 128;

    /// <summary>Tamanho de <c>SMEvoInstrumentation</c>.</summary>
    public const int InstrumentationSize = 128;

    /// <summary>Tamanho de <c>SMEvoElectronics</c>.</summary>
    public const int ElectronicsSize = 128;

    /// <summary>Tamanho de <c>SMEvoPitInfo</c>.</summary>
    public const int PitInfoSize = 64;

    // ---- SPageFilePhysics ----
    public static int GasOffset { get; } = BuildPhysics().Gas;

    public static int BrakeOffset { get; } = BuildPhysics().Brake;

    public static int SpeedKmhOffset { get; } = BuildPhysics().SpeedKmh;

    // ---- SPageFileGraphicEvo ----
    public static int StatusOffset { get; } = BuildGraphics().Status;

    public static int BrakePercentOffset { get; } = BuildGraphics().BrakePercent;

    public static int GasPercentOffset { get; } = BuildGraphics().GasPercent;

    public static int NormalizedPositionOffset { get; } = BuildGraphics().NormalizedPosition;

    public static int TotalLapCountOffset { get; } = BuildGraphics().TotalLapCount;

    private static PhysicsFields BuildPhysics()
    {
        var layout = new FieldLayout();

        layout.AddInt();            // packetId
        int gas = layout.AddFloat();
        int brake = layout.AddFloat();
        layout.AddFloat();          // fuel
        layout.AddInt();            // gear
        layout.AddInt();            // rpms
        layout.AddFloat();          // steerAngle
        int speedKmh = layout.AddFloat();

        return new PhysicsFields(gas, brake, speedKmh);
    }

    /// <summary>
    /// A página gráfica é longa: quatro blocos de pneu, dano, eletrônica e instrumentação vêm
    /// antes de <c>npos</c> e de <c>total_lap_count</c>. Cada campo do cabeçalho é reservado
    /// aqui, na ordem original, para que os deslocamentos batam com o jogo.
    /// </summary>
    private static GraphicsFields BuildGraphics()
    {
        var layout = new FieldLayout();

        layout.AddInt();            // packetId
        int status = layout.AddInt();
        layout.AddBytes(8);         // focused_car_id_a
        layout.AddBytes(8);         // focused_car_id_b
        layout.AddBytes(8);         // player_car_id_a
        layout.AddBytes(8);         // player_car_id_b
        layout.AddShort();          // rpm

        // 15 flags booleanas: limitador, janelas de troca, TC, ABS, ESC, launch, ignição,
        // motor ligado, ERS (carga e limites) e DRS disponível.
        for (var i = 0; i < 15; i++)
        {
            layout.AddBool();
        }

        layout.AddShort();          // display_speed_kmh
        layout.AddShort();          // display_speed_mph
        layout.AddShort();          // display_speed_ms
        layout.AddFloat();          // pitspeeding_delta
        layout.AddShort();          // gear_int
        layout.AddFloat();          // rpm_percent
        int gasPercent = layout.AddFloat();
        int brakePercent = layout.AddFloat();
        layout.AddFloat();          // handbrake_percent
        layout.AddFloat();          // clutch_percent
        layout.AddFloat();          // steering_percent
        layout.AddFloat();          // ffb_strength
        layout.AddFloat();          // car_ffb_mupliplier
        layout.AddFloat();          // water_temperature_percent
        layout.AddFloat();          // water_pressure_bar
        layout.AddFloat();          // fuel_pressure_bar
        layout.AddByte();           // water_temperature_c
        layout.AddByte();           // air_temperature_c
        layout.AddFloat();          // oil_temperature_c
        layout.AddFloat();          // oil_pressure_bar
        layout.AddFloat();          // exhaust_temperature_c
        layout.AddFloat();          // g_forces_x
        layout.AddFloat();          // g_forces_y
        layout.AddFloat();          // g_forces_z
        layout.AddFloat();          // turbo_boost
        layout.AddFloat();          // turbo_boost_level
        layout.AddFloat();          // turbo_boost_perc
        layout.AddInt();            // steer_degrees
        layout.AddFloat();          // current_km
        layout.AddInt();            // total_km
        layout.AddInt();            // total_driving_time_s
        layout.AddInt();            // time_of_day_hours
        layout.AddInt();            // time_of_day_minutes
        layout.AddInt();            // time_of_day_seconds
        layout.AddInt();            // delta_time_ms
        layout.AddInt();            // current_lap_time_ms
        layout.AddInt();            // predicted_lap_time_ms
        layout.AddFloat();          // fuel_liter_current_quantity
        layout.AddFloat();          // fuel_liter_current_quantity_percent
        layout.AddFloat();          // fuel_liter_per_km
        layout.AddFloat();          // km_per_fuel_liter
        layout.AddFloat();          // current_torque
        layout.AddInt();            // current_bhp

        layout.AlignToPack();
        layout.AddBytes(TyreStateSize);  // tyre_lf
        layout.AddBytes(TyreStateSize);  // tyre_rf
        layout.AddBytes(TyreStateSize);  // tyre_lr
        layout.AddBytes(TyreStateSize);  // tyre_rr

        int normalizedPosition = layout.AddFloat();
        layout.AddFloat();          // kers_charge_perc
        layout.AddFloat();          // kers_current_perc
        layout.AddFloat();          // control_lock_time

        layout.AlignToPack();
        layout.AddBytes(DamageStateSize); // car_damage
        layout.AddInt();                  // car_location
        layout.AddBytes(PitInfoSize);     // pit_info

        layout.AddFloat();          // fuel_liter_used
        layout.AddFloat();          // fuel_liter_per_lap
        layout.AddFloat();          // laps_possible_with_fuel
        layout.AddFloat();          // battery_temperature
        layout.AddFloat();          // battery_voltage
        layout.AddFloat();          // instantaneous_fuel_liter_per_km
        layout.AddFloat();          // instantaneous_km_per_fuel_liter
        layout.AddFloat();          // gear_rpm_window

        layout.AlignToPack();
        layout.AddBytes(InstrumentationSize); // instrumentation
        layout.AddBytes(InstrumentationSize); // instrumentation_min_limit
        layout.AddBytes(InstrumentationSize); // instrumentation_max_limit
        layout.AddBytes(ElectronicsSize);     // electronics
        layout.AddBytes(ElectronicsSize);     // electronics_min_limit
        layout.AddBytes(ElectronicsSize);     // electronics_max_limit
        layout.AddBytes(ElectronicsSize);     // electronics_is_modifiable

        int totalLapCount = layout.AddInt();

        return new GraphicsFields(status, gasPercent, brakePercent, normalizedPosition, totalLapCount);
    }

    private readonly record struct PhysicsFields(int Gas, int Brake, int SpeedKmh);

    private readonly record struct GraphicsFields(
        int Status,
        int GasPercent,
        int BrakePercent,
        int NormalizedPosition,
        int TotalLapCount);
}
