using System;
using System.IO;
using PedalTrainer.Core.Services;

namespace PedalTrainer.Core.Telemetry.Recording;

/// <summary>Pasta escolhida para os arquivos de telemetria.</summary>
public readonly record struct TelemetryFolderLocation(string Path, bool IsFallback)
{
    /// <summary>Verdadeiro quando a pasta fica ao lado do executável (o caso normal).</summary>
    public bool IsNextToProgram => !IsFallback;
}

/// <summary>
/// Resolve a pasta <c>telemetria</c> na raiz do programa. Quando o executável está num
/// diretório somente leitura (Program Files, por exemplo), cai para a pasta do usuário em
/// vez de perder a gravação — a tela avisa qual das duas está em uso.
/// </summary>
public static class TelemetryFolder
{
    /// <summary>Nome da pasta criada na raiz do programa.</summary>
    public const string FolderName = "telemetria";

    /// <summary>Pasta ao lado do executável: a raiz do programa.</summary>
    public static string NextToProgram => System.IO.Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>Pasta de reserva, no perfil do usuário.</summary>
    public static string InUserProfile => System.IO.Path.Combine(AppPaths.RootDirectory, FolderName);

    /// <summary>
    /// Cria (se preciso) e devolve a pasta de gravação. Nunca lança: devolve a pasta do
    /// usuário quando a raiz do programa não aceita escrita.
    /// </summary>
    public static TelemetryFolderLocation Ensure()
    {
        if (TryCreate(NextToProgram))
        {
            return new TelemetryFolderLocation(NextToProgram, IsFallback: false);
        }

        TryCreate(InUserProfile);
        return new TelemetryFolderLocation(InUserProfile, IsFallback: true);
    }

    private static bool TryCreate(string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            // CreateDirectory sozinho não prova escrita quando a pasta já existe.
            string probe = System.IO.Path.Combine(path, ".pedaltrainer-escrita");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
