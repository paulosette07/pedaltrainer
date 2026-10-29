using System;
using System.Collections.Generic;
using System.Globalization;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>
/// Interpreta arquivos de texto delimitados com colunas Time/Brake/Throttle
/// (aceita também Distance/LapDist e Speed). Detecta separador, cabeçalho e escala.
/// </summary>
internal static class CsvReferenceParser
{
    private static readonly string[] TimeKeys = { "time", "tempo", "sessiontime" };
    private static readonly string[] BrakeKeys = { "brake", "brakepct", "brakepercent", "brakepressure", "freio" };
    private static readonly string[] ThrottleKeys = { "throttle", "throttlepct", "throttlepercent", "gas", "acelerador" };
    private static readonly string[] DistanceKeys = { "distance", "lapdist", "lapdistance", "distancia", "dist" };
    private static readonly string[] SpeedKeys = { "speed", "speedkmh", "velocidade" };

    private sealed record ColumnMap(int Time, int Brake, int Throttle, int Distance, int Speed)
    {
        public static ColumnMap Positional(int columnCount) => new(
            Time: 0,
            Brake: 1,
            Throttle: columnCount > 2 ? 2 : -1,
            Distance: columnCount > 3 ? 3 : -1,
            Speed: columnCount > 4 ? 4 : -1);
    }

    public static ReferenceLap Parse(string[] lines, string name)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var rows = new List<string>(lines.Length);
        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                rows.Add(line);
            }
        }

        if (rows.Count < 2)
        {
            throw new ReferenceLapException("O arquivo não tem amostras suficientes (mínimo: cabeçalho + 1 linha).");
        }

        char separator = DetectSeparator(rows[0]);
        string[] firstCells = SplitRow(rows[0], separator);
        ColumnMap? headerMap = ColumnMapFromHeader(firstCells);
        ColumnMap map = headerMap ?? ColumnMap.Positional(firstCells.Length);
        int firstDataRow = headerMap is null ? 0 : 1;

        var raw = new List<double[]>(rows.Count);
        for (int i = firstDataRow; i < rows.Count; i++)
        {
            double[]? parsed = ParseRow(rows[i], separator, map);
            if (parsed is not null)
            {
                raw.Add(parsed);
            }
        }

        if (raw.Count < 2)
        {
            throw new ReferenceLapException(
                "Não foi possível interpretar as linhas. Verifique as colunas Time, Brake e Throttle.");
        }

        return BuildLap(raw, name);
    }

    private static ReferenceLap BuildLap(List<double[]> raw, string name)
    {
        double brakeScale = MaxOf(raw, 1) <= 1.5 ? 100.0 : 1.0;
        double throttleScale = MaxOf(raw, 2) <= 1.5 ? 100.0 : 1.0;
        double timeOrigin = raw[0][0];
        double distanceScale = MaxOf(raw, 3) <= 1.5 ? 0.0 : 1.0;

        var samples = new List<ReferenceSample>(raw.Count);
        foreach (double[] row in raw)
        {
            samples.Add(new ReferenceSample(
                TimeSeconds: row[0] - timeOrigin,
                BrakePercent: row[1] * brakeScale,
                ThrottlePercent: row[2] * throttleScale,
                DistanceMeters: Math.Max(0, row[3] * distanceScale),
                SpeedKmh: Math.Max(0, row[4])));
        }

        return ReferenceLap.Create(name, samples);
    }

    private static double MaxOf(List<double[]> rows, int column)
    {
        double max = 0;
        foreach (double[] row in rows)
        {
            if (row[column] > max)
            {
                max = row[column];
            }
        }

        return max;
    }

    private static double[]? ParseRow(string line, char separator, ColumnMap map)
    {
        string[] cells = SplitRow(line, separator);
        int highestIndex = Math.Max(map.Time, map.Brake);
        if (cells.Length <= highestIndex)
        {
            return null;
        }

        if (!TryParseNumber(cells[map.Time], out double time) ||
            !TryParseNumber(cells[map.Brake], out double brake))
        {
            return null;
        }

        double throttle = ReadOptional(cells, map.Throttle);
        double distance = ReadOptional(cells, map.Distance);
        double speed = ReadOptional(cells, map.Speed);

        return new[]
        {
            time,
            Math.Clamp(brake, 0, 100),
            Math.Clamp(throttle, 0, 100),
            Math.Max(0, distance),
            Math.Max(0, speed)
        };
    }

    private static double ReadOptional(string[] cells, int index)
    {
        if (index < 0 || index >= cells.Length)
        {
            return 0;
        }

        return TryParseNumber(cells[index], out double value) ? value : 0;
    }

    private static ColumnMap? ColumnMapFromHeader(string[] header)
    {
        if (header.Length < 2)
        {
            return null;
        }

        var normalized = new string[header.Length];
        for (int i = 0; i < header.Length; i++)
        {
            normalized[i] = header[i]
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("%", "pct")
                .Trim('"');
        }

        int time = IndexOfKey(normalized, TimeKeys);
        int brake = IndexOfKey(normalized, BrakeKeys);

        if (time < 0 || brake < 0)
        {
            return null;
        }

        return new ColumnMap(
            time,
            brake,
            IndexOfKey(normalized, ThrottleKeys),
            IndexOfKey(normalized, DistanceKeys),
            IndexOfKey(normalized, SpeedKeys));
    }

    private static int IndexOfKey(string[] normalizedHeader, string[] keys)
    {
        foreach (string key in keys)
        {
            for (int i = 0; i < normalizedHeader.Length; i++)
            {
                if (normalizedHeader[i].StartsWith(key, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static char DetectSeparator(string firstLine)
    {
        int commas = firstLine.Split(',').Length - 1;
        int semicolons = firstLine.Split(';').Length - 1;
        int tabs = firstLine.Split('\t').Length - 1;

        if (semicolons >= commas && semicolons >= tabs && semicolons > 0)
        {
            return ';';
        }

        if (tabs >= commas && tabs > 0)
        {
            return '\t';
        }

        return ',';
    }

    private static string[] SplitRow(string line, char separator) => line.Split(separator);

    /// <summary>Aceita ponto ou vírgula como separador decimal.</summary>
    internal static bool TryParseNumber(string text, out double value)
    {
        string trimmed = text.Trim().Trim('"');
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        string normalized = trimmed.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
