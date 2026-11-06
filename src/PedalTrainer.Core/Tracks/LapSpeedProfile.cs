using System;
using System.Collections.Generic;

namespace PedalTrainer.Core.Tracks;

/// <summary>
/// Perfil de velocidade de uma volta ideal: parte do raio de cada curva, aplica os limites
/// de frenagem e de aceleração e devolve velocidade, tempo, distância e o que o piloto
/// estaria fazendo no pedal ponto a ponto. Serve de referência para qualquer pista.
/// </summary>
public sealed class LapSpeedProfile
{
    private const double MinimumRadiusMeters = 12.0;
    private const double MinimumSpeedMps = 3.0;

    // O raio é medido numa corda larga (uns 36 m): numa reta, o tremor de 10 cm do
    // levantamento viraria "curva de 45 m" e encheria a volta de frenagens falsas.
    private const double CurvatureWindowMeters = 18.0;

    // A linha de corrida é mais aberta que o eixo da pista (corta a curva): o raio usado
    // para a velocidade é o maior da vizinhança, com uma folga a mais.
    private const double RacingLineFactor = 1.25;
    private const int RadiusEnvelopePoints = 2;
    private const double MinimumRacingRadiusMeters = 20.0;
    private const double DragFactor = 0.00035;
    private const double PedalDeadband = 1.0;

    // Frenagens muito leves são ruído do cálculo (o piloto estaria aliviando o pedal):
    // abaixo disso o trecho não vira zona de treino.
    private const double BrakeFloorPercent = 6.0;

    private readonly double[] _speeds;
    private readonly double[] _brake;
    private readonly double[] _throttle;
    private readonly double[] _times;
    private readonly double[] _distances;

    private LapSpeedProfile(double[] speeds, double[] brake, double[] throttle, double[] times, double[] distances)
    {
        _speeds = speeds;
        _brake = brake;
        _throttle = throttle;
        _times = times;
        _distances = distances;
    }

    public int Count => _speeds.Length;

    public double LapTimeSeconds => _times[^1];

    public IReadOnlyList<double> Speeds => _speeds;

    public IReadOnlyList<double> BrakePercent => _brake;

    public IReadOnlyList<double> ThrottlePercent => _throttle;

    public IReadOnlyList<double> Times => _times;

    public IReadOnlyList<double> Distances => _distances;

    public static LapSpeedProfile Build(TrackPath path, LapPhysicsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        LapPhysicsOptions physics = (options ?? LapPhysicsOptions.Default).Validated();
        int count = path.Count;

        double[] steps = BuildSteps(path);
        double[] speeds = BuildCornerSpeeds(path, physics);

        ApplyBrakingPasses(speeds, steps, physics, count);
        ApplyAccelerationPasses(speeds, steps, physics, count);
        SmoothSpeeds(speeds);

        var brake = new double[count + 1];
        var throttle = new double[count + 1];
        var times = new double[count + 1];
        var distances = new double[count + 1];
        var closed = new double[count + 1];

        for (var index = 0; index < count; index++)
        {
            closed[index] = speeds[index];
        }

        closed[count] = speeds[0];
        ApplyPedals(closed, steps, physics, brake, throttle);

        for (var index = 0; index < count; index++)
        {
            double average = Math.Max(MinimumSpeedMps, (closed[index] + closed[index + 1]) * 0.5);
            times[index + 1] = times[index] + (steps[index] / average);
            distances[index + 1] = distances[index] + steps[index];
        }

        return new LapSpeedProfile(closed, brake, throttle, times, distances);
    }

    private static double[] BuildSteps(TrackPath path)
    {
        int count = path.Count;
        var steps = new double[count];

        for (var index = 0; index < count; index++)
        {
            double current = path.DistanceAt(index);
            double next = index + 1 >= count ? path.LengthMeters : path.DistanceAt(index + 1);
            steps[index] = Math.Max(0.5, next - current);
        }

        return steps;
    }

    private static double[] BuildCornerSpeeds(TrackPath path, LapPhysicsOptions physics)
    {
        int count = path.Count;
        var speeds = new double[count];
        double lateral = physics.LateralAcceleration * physics.LateralMargin;

        double step = Math.Max(1.0, path.StepMeters);
        int window = Math.Max(2, (int)Math.Round(CurvatureWindowMeters / step));

        var radii = new double[count];

        for (var index = 0; index < count; index++)
        {
            radii[index] = TrackGeometry.Circumradius(path[index - window], path[index], path[index + window]);
        }

        for (var index = 0; index < count; index++)
        {
            double best = radii[index];

            for (var offset = -RadiusEnvelopePoints; offset <= RadiusEnvelopePoints; offset++)
            {
                int neighbour = ((index + offset) % count + count) % count;
                best = Math.Max(best, radii[neighbour]);
            }

            double radius = Math.Max(
                Math.Max(MinimumRadiusMeters, MinimumRacingRadiusMeters),
                best * RacingLineFactor);

            double cornerSpeed = Math.Sqrt(lateral * radius);
            speeds[index] = Math.Min(cornerSpeed, physics.TopSpeedMetersPerSecond);
        }

        return speeds;
    }

    private static void ApplyBrakingPasses(double[] speeds, double[] steps, LapPhysicsOptions physics, int count)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            for (int index = count - 1; index >= 0; index--)
            {
                int next = (index + 1) % count;
                double limit = Math.Sqrt((speeds[next] * speeds[next]) + (2 * physics.BrakingDeceleration * steps[index]));

                if (speeds[index] > limit)
                {
                    speeds[index] = limit;
                }
            }
        }
    }

    private static void ApplyAccelerationPasses(double[] speeds, double[] steps, LapPhysicsOptions physics, int count)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            for (var index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                double limit = Math.Sqrt((speeds[index] * speeds[index]) + (2 * physics.AccelerationRate * steps[index]));

                if (speeds[next] > limit)
                {
                    speeds[next] = limit;
                }
            }
        }
    }

    /// <summary>
    /// Suaviza o perfil para o pedal não oscilar a cada ponto: sem isso o desenho da
    /// frenagem reference fica picotado e a detecção de zonas encontra dezenas de ruídos.
    /// </summary>
    private static void SmoothSpeeds(double[] speeds)
    {
        int count = speeds.Length;
        if (count < 3)
        {
            return;
        }

        var original = new double[count];
        Array.Copy(speeds, original, count);

        for (var index = 0; index < count; index++)
        {
            double previous = original[(index - 1 + count) % count];
            double next = original[(index + 1) % count];
            speeds[index] = (previous + (2 * original[index]) + next) * 0.25;
        }
    }

    private static void ApplyPedals(double[] speeds, double[] steps, LapPhysicsOptions physics, double[] brake, double[] throttle)
    {
        int count = speeds.Length - 1;

        for (var index = 0; index < count; index++)
        {
            double from = speeds[index];
            double to = speeds[index + 1];
            double acceleration = ((to * to) - (from * from)) / (2 * steps[index]);

            // A resistência do ar obriga a manter pedal mesmo em velocidade constante.
            double demand = acceleration + (DragFactor * from * from);

            if (demand < -PedalDeadband)
            {
                double pressure = Math.Clamp(-demand / physics.BrakingDeceleration * 100.0, 0, 100);

                brake[index] = pressure < BrakeFloorPercent ? 0 : pressure;
                throttle[index] = 0;
            }
            else
            {
                brake[index] = 0;
                throttle[index] = Math.Clamp(demand / physics.AccelerationRate * 100.0, 0, 100);
            }
        }

        brake[count] = brake[0];
        throttle[count] = throttle[0];
    }
}
