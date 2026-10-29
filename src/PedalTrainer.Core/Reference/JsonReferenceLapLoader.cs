using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PedalTrainer.Core.Models;

namespace PedalTrainer.Core.Reference;

/// <summary>
/// Carrega voltas de referência em JSON. Aceita tanto chaves curtas (t, b, th, d, v)
/// quanto os nomes completos (time, brake, throttle, distance, speed).
/// </summary>
public sealed class JsonReferenceLapLoader : IReferenceLapLoader
{
    private static readonly string[] SupportedExtensions = { "json" };

    public string FormatName => "JSON (samples: time, brake, throttle)";

    public IReadOnlyList<string> Extensions => SupportedExtensions;

    public bool CanLoad(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);
    }

    public ReferenceLap Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ReferenceLapException("Informe o caminho do arquivo de referência.");
        }

        if (!File.Exists(path))
        {
            throw new ReferenceLapException($"Arquivo não encontrado: {path}");
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            return ReadLap(document.RootElement, Path.GetFileNameWithoutExtension(path));
        }
        catch (JsonException ex)
        {
            throw new ReferenceLapException($"JSON inválido em \"{Path.GetFileName(path)}\": {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new ReferenceLapException($"Não foi possível ler \"{Path.GetFileName(path)}\": {ex.Message}", ex);
        }
    }

    private static ReferenceLap ReadLap(JsonElement root, string fallbackName)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return ReferenceLap.Create(fallbackName, ReadSamples(root));
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ReferenceLapException("O JSON deve ser um objeto com \"samples\" ou um array de amostras.");
        }

        string name = ReadString(root, "name") ?? fallbackName;
        double trackLength = ReadDouble(root, "trackLengthMeters") ?? ReadDouble(root, "trackLength") ?? 0;

        if (!TryGetProperty(root, "samples", out JsonElement samplesElement) ||
            samplesElement.ValueKind != JsonValueKind.Array)
        {
            throw new ReferenceLapException("O JSON precisa conter um array \"samples\".");
        }

        List<ReferenceSample> samples = ReadSamples(samplesElement);
        return samples.Count == 0
            ? throw new ReferenceLapException("O array \"samples\" está vazio.")
            : ReferenceLap.Create(name, samples, trackLength);
    }

    private static List<ReferenceSample> ReadSamples(JsonElement array)
    {
        var samples = new List<ReferenceSample>(array.GetArrayLength());
        double timeOrigin = double.NaN;

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            double? time = ReadDouble(item, "t") ?? ReadDouble(item, "time");
            double? brake = ReadDouble(item, "b") ?? ReadDouble(item, "brake");
            if (time is null || brake is null)
            {
                continue;
            }

            double? throttle = ReadDouble(item, "th") ?? ReadDouble(item, "throttle");
            double? distance = ReadDouble(item, "d") ?? ReadDouble(item, "distance");
            double? speed = ReadDouble(item, "v") ?? ReadDouble(item, "speed");

            if (double.IsNaN(timeOrigin))
            {
                timeOrigin = time.Value;
            }

            samples.Add(new ReferenceSample(
                time.Value - timeOrigin,
                Math.Clamp(brake.Value, 0, 100),
                Math.Clamp(throttle ?? 0, 0, 100),
                Math.Max(0, distance ?? 0),
                Math.Max(0, speed ?? 0)));
        }

        return samples;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out double number) => number,
            JsonValueKind.String when double.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double parsed) => parsed,
            _ => null
        };
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
