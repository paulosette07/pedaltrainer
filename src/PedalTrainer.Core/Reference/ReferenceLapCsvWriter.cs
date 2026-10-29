using System;
using System.Globalization;
using System.IO;
using System.Text;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>
/// Serializa uma volta de referência no CSV que o próprio app relê
/// (Time, Brake, Throttle, Distance, Speed — separador vírgula, ponto decimal).
/// </summary>
public static class ReferenceLapCsvWriter
{
    /// <summary>Cabeçalho usado nas colunas gravadas.</summary>
    public const string Header = "Time,Brake,Throttle,Distance,Speed";

    /// <summary>Devolve a volta completa em texto CSV.</summary>
    public static string ToCsv(ReferenceLap lap)
    {
        ArgumentNullException.ThrowIfNull(lap);

        var builder = new StringBuilder();
        builder.AppendLine(Header);

        foreach (ReferenceSample sample in lap.Samples)
        {
            builder.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{sample.TimeSeconds:0.000},{sample.BrakePercent:0.00},{sample.ThrottlePercent:0.00},{sample.DistanceMeters:0.0},{sample.SpeedKmh:0.0}"));
        }

        return builder.ToString();
    }

    /// <summary>Grava a volta em disco, criando a pasta se necessário.</summary>
    public static void WriteFile(ReferenceLap lap, string path)
    {
        ArgumentNullException.ThrowIfNull(lap);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Informe o caminho do arquivo de destino.", nameof(path));
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToCsv(lap), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
