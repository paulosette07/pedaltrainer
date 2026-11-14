using System;
using System.Globalization;
using PedalTrainer.Core.Models;
using PedalTrainer.Core.Telemetry.Comparison;
using PedalTrainer.Core.Tracks;
using Xunit;

namespace PedalTrainer.Tests;

/// <summary>
/// Textos que o piloto lê na comparação com a melhor volta. Ficam no Core para que o sinal,
/// as casas decimais e a unidade sejam verificados aqui, e não só na tela.
/// </summary>
public sealed class BestLapComparisonTextTests
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public BestLapComparisonTextTests()
    {
        CultureInfo.CurrentCulture = PtBr;
        CultureInfo.CurrentUICulture = PtBr;
    }

    [Fact]
    public void Delta_AFrente_UsaSinalNegativo()
    {
        Assert.Equal("-0,42 s", BestLapComparisonText.Delta(-0.424));
    }

    [Fact]
    public void Delta_Atras_UsaSinalPositivo()
    {
        Assert.Equal("+1,35 s", BestLapComparisonText.Delta(1.349));
    }

    [Fact]
    public void Delta_QuaseZero_ViraEmpate()
    {
        Assert.Equal("0,00 s", BestLapComparisonText.Delta(0.004));
        Assert.Equal("0,00 s", BestLapComparisonText.Delta(-0.004));
        Assert.Equal("0,00 s", BestLapComparisonText.Delta(0));
    }

    [Fact]
    public void Delta_SemAmostra_MostraMarcador()
    {
        Assert.Equal("--", BestLapComparisonText.DeltaOrPlaceholder(BestLapComparison.None));
        Assert.Equal("--", BestLapComparisonText.BestDeltaOrPlaceholder(BestLapComparison.None));
        Assert.Equal("--", BestLapComparisonText.LastLapDeltaOrPlaceholder(BestLapComparison.None));
    }

    [Fact]
    public void Delta_ComAmostra_UsaOMesmoFormato()
    {
        var comparison = new BestLapComparison
        {
            HasDelta = true,
            DeltaSeconds = -1.5,
            HasBestDelta = true,
            BestDeltaSeconds = -2.25,
            HasLastLapDelta = true,
            LastLapDeltaSeconds = 0.8
        };

        Assert.Equal("-1,50 s", BestLapComparisonText.DeltaOrPlaceholder(comparison));
        Assert.Equal("-2,25 s", BestLapComparisonText.BestDeltaOrPlaceholder(comparison));
        Assert.Equal("+0,80 s", BestLapComparisonText.LastLapDeltaOrPlaceholder(comparison));
    }

    [Fact]
    public void Titulo_MostraNomeEDuracao()
    {
        var comparison = new BestLapComparison
        {
            ReferenceName = "Volta ideal",
            ReferenceDurationSeconds = 94.16
        };

        Assert.Equal("Volta ideal · 01:34.1", BestLapComparisonText.Title(comparison));
    }

    [Fact]
    public void Titulo_SemReferencia_Avisa()
    {
        Assert.Equal("Nenhuma volta carregada", BestLapComparisonText.Title(BestLapComparison.None));

        var semDuracao = new BestLapComparison { ReferenceName = "Minha volta" };

        Assert.Equal("Minha volta", BestLapComparisonText.Title(semDuracao));
    }

    [Fact]
    public void Duracao_AcimaDeUmaHora_UsaHoras()
    {
        Assert.Equal("01:05:00", BestLapComparisonText.Duration(3900));
    }

    [Fact]
    public void Comparativo_MostraValorEDeReferencia()
    {
        Assert.Equal("182 · ref 187 km/h", BestLapComparisonText.Compare(182.4, 187.2, "km/h"));
        Assert.Equal("0 · ref 60 %", BestLapComparisonText.Compare(0, 60, "%"));
    }

    [Fact]
    public void Progresso_UsaPorcentagemInteira()
    {
        Assert.Equal("42% da volta", BestLapComparisonText.Progress(0.424));
        Assert.Equal("100% da volta", BestLapComparisonText.Progress(1));
    }

    /// <summary>
    /// Seguindo a volta ideal do catálogo no ritmo dela, o delta tem de ficar em zero: é o
    /// caso que o piloto vê como "andando no tempo da melhor volta".
    /// </summary>
    [Fact]
    public void SeguindoAVoltaIdeal_DeltaFicaEmZero()
    {
        TrackDefinition track = TrackCatalog.Default;
        ReferenceLap lap = IdealLapFactory.Build(track);

        Assert.True(lap.HasDistanceData);

        var comparer = new BestLapComparer();
        BestLapComparison comparison = DriveFollowing(lap, comparer);

        Assert.True(comparison.CanCompare);
        Assert.True(comparison.HasDelta);
        Assert.InRange(comparison.LapProgress, 0.90, 1.0);

        // Não é zero bit a zero: a coluna de tempo da volta ideal e a velocidade dela são
        // integradas em passos distintos. O que o piloto vê é um empate (menos de 0,1 s).
        Assert.InRange(comparison.DeltaSeconds, -0.1, 0.1);
        Assert.False(comparison.IsAhead);
        Assert.False(
            BestLapComparisonText.Delta(comparison.DeltaSeconds).StartsWith("-", StringComparison.Ordinal));
    }

    /// <summary>Um pouco mais rápido que a volta ideal a volta inteira: delta negativo.</summary>
    [Fact]
    public void UmPoucoMaisRapidoQueAIdeal_DeltaNegativo()
    {
        TrackDefinition track = TrackCatalog.Default;
        ReferenceLap lap = IdealLapFactory.Build(track);

        var comparer = new BestLapComparer();
        BestLapComparison comparison = DriveFollowing(lap, comparer, speedFactor: 1.05);

        Assert.True(comparison.HasDelta);
        Assert.True(comparison.IsAhead);
        Assert.True(comparison.DeltaSeconds < -0.5);
        Assert.StartsWith("-", BestLapComparisonText.Delta(comparison.DeltaSeconds), StringComparison.Ordinal);
    }

    /// <summary>
    /// Percorre a volta de referência em passos pequenos (a 100% ou a <paramref name="speedFactor"/>
    /// da velocidade dela) e devolve a última comparação.
    /// </summary>
    private static BestLapComparison DriveFollowing(
        ReferenceLap lap,
        BestLapComparer comparer,
        double speedFactor = 1.0)
    {
        const double stepMeters = 15;

        double totalMeters = lap.TrackLengthMeters;
        double time = 0;
        double previousDistance = 0;

        BestLapComparison last = BestLapComparison.None;

        double previousSpeedMps = SpeedAt(lap, 0) * speedFactor;

        for (double distance = 0; distance <= totalMeters; distance += stepMeters)
        {
            ReferenceSample reference = lap.SampleAtDistance(distance);
            double carSpeedMps = Math.Max(reference.SpeedKmh, 1) / 3.6 * speedFactor;

            // Tempo do trecho pela média harmônica das pontas: é como o tempo se acumula
            // quando a velocidade varia dentro de um passo.
            double averageMps = 2 * previousSpeedMps * carSpeedMps / (previousSpeedMps + carSpeedMps);

            time += (distance - previousDistance) / averageMps;
            previousDistance = distance;
            previousSpeedMps = carSpeedMps;

            double lapPercent = Math.Clamp(distance / totalMeters, 0, 1);

            var car = new CarTelemetry(
                true,
                carSpeedMps * 3.6,
                reference.BrakePercent,
                reference.ThrottlePercent,
                lapPercent,
                time,
                1,
                time);

            last = comparer.Update(lap, car, time);
        }

        return last;
    }

    /// <summary>Velocidade da volta ideal (m/s) na distância pedida.</summary>
    private static double SpeedAt(ReferenceLap lap, double distanceMeters) =>
        Math.Max(lap.SampleAtDistance(distanceMeters).SpeedKmh, 1) / 3.6;
}
