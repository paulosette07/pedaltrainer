using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PedalTrainer.Core.Telemetry.Recording;

/// <summary>
/// Nome e caminho dos arquivos de telemetria. Um arquivo por gravação, com o apelido do
/// jogo e o carimbo de data e hora — dois arquivos nunca se sobrescrevem.
/// </summary>
public static class TelemetryLogFile
{
    /// <summary>Extensão dos arquivos gravados.</summary>
    public const string Extension = ".csv";

    /// <summary>Nome do arquivo a partir do perfil e do instante da gravação.</summary>
    public static string BuildFileName(TelemetryProfile profile, DateTime localTime)
    {
        ArgumentNullException.ThrowIfNull(profile);

        string slug = Slug(profile.IsAuto ? "auto" : profile.ShortName);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"telemetria-{slug}-{localTime:yyyyMMdd-HHmmss}{Extension}");
    }

    /// <summary>
    /// Caminho livre dentro da pasta: se o mesmo segundo já tiver arquivo, ganha um sufixo
    /// em vez de apagar a gravação anterior.
    /// </summary>
    public static string BuildUniquePath(string folder, TelemetryProfile profile, DateTime localTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        string fileName = BuildFileName(profile, localTime);
        string path = Path.Combine(folder, fileName);

        if (!File.Exists(path))
        {
            return path;
        }

        string stem = Path.GetFileNameWithoutExtension(fileName);

        for (var attempt = 2; attempt < 1000; attempt++)
        {
            string candidate = Path.Combine(
                folder,
                string.Create(CultureInfo.InvariantCulture, $"{stem}-{attempt}{Extension}"));

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(
            folder,
            string.Create(CultureInfo.InvariantCulture, $"{stem}-{Guid.NewGuid():N}{Extension}"));
    }

    /// <summary>Converte o apelido do jogo em parte segura do nome do arquivo.</summary>
    public static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "auto";
        }

        var builder = new StringBuilder(value.Length);

        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (character is ' ' or '-' or '_')
            {
                builder.Append('-');
            }
        }

        return builder.Length == 0 ? "auto" : builder.ToString();
    }
}
