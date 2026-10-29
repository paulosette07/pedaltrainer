using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PedalTrainer.Core.Drills;

/// <summary>
/// Lê e grava programas de treino em JSON legível — o "arquivo de treino" que o piloto
/// pode editar à mão e reutilizar entre sessões.
/// </summary>
public static class DrillProgramFile
{
    /// <summary>Extensão usada nos arquivos de treino.</summary>
    public const string Extension = ".drill.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // Campos opcionais não aparecem no arquivo: o piloto lê e edita só o que importa.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Filtro para as caixas de diálogo de arquivo.</summary>
    public static string BuildFileDialogFilter() =>
        $"Programa de treino (*{Extension})|*{Extension}|JSON (*.json)|*.json|Todos os arquivos (*.*)|*.*";

    /// <summary>Lê um programa de treino do disco.</summary>
    public static bool TryLoad(string path, out DrillProgram? program, out string error)
    {
        program = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Caminho vazio.";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "O arquivo está vazio.";
                return false;
            }

            DrillProgram? loaded = JsonSerializer.Deserialize<DrillProgram>(json, SerializerOptions);

            if (loaded is null)
            {
                error = "O arquivo não contém um programa de treino.";
                return false;
            }

            loaded.Steps ??= new();

            if (!loaded.TryValidate(out string validationError))
            {
                error = validationError;
                return false;
            }

            if (string.IsNullOrWhiteSpace(loaded.Name))
            {
                loaded.Name = Path.GetFileNameWithoutExtension(path);
            }

            program = loaded;
            error = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON inválido: {ex.Message}";
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"Não foi possível ler o arquivo: {ex.Message}";
            return false;
        }
    }

    /// <summary>Grava o programa de treino no disco.</summary>
    public static bool Save(DrillProgram program, string path, out string error)
    {
        ArgumentNullException.ThrowIfNull(program);

        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            program.Version = DrillProgram.CurrentVersion;
            File.WriteAllText(path, JsonSerializer.Serialize(program, SerializerOptions));
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"Não foi possível gravar o arquivo: {ex.Message}";
            return false;
        }
    }

    /// <summary>Converte o programa em JSON (usado nos testes e na exportação).</summary>
    public static string ToJson(DrillProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return JsonSerializer.Serialize(program, SerializerOptions);
    }

    /// <summary>Lê o programa a partir de texto JSON.</summary>
    public static bool TryParse(string json, out DrillProgram? program, out string error)
    {
        program = null;

        try
        {
            DrillProgram? loaded = JsonSerializer.Deserialize<DrillProgram>(json, SerializerOptions);

            if (loaded is null)
            {
                error = "O texto não contém um programa de treino.";
                return false;
            }

            loaded.Steps ??= new();

            if (!loaded.TryValidate(out string validationError))
            {
                error = validationError;
                return false;
            }

            program = loaded;
            error = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON inválido: {ex.Message}";
            return false;
        }
    }
}
