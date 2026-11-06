using System;

namespace PedalTrainer.Core.Tracks;

/// <summary>Origem de uma volta de referência oferecida para a pista selecionada.</summary>
public enum TrackReferenceKind
{
    /// <summary>Volta calculada pelo aplicativo a partir do traçado.</summary>
    System,

    /// <summary>Arquivo do piloto: volta gravada no simulador ou telemetria importada.</summary>
    File
}

/// <summary>
/// Uma opção da lista "referência": a volta ideal do sistema ou um arquivo do piloto.
/// </summary>
public sealed record TrackReferenceSource(
    TrackReferenceKind Kind,
    string Label,
    string Detail,
    string? Path)
{
    public bool IsSystem => Kind == TrackReferenceKind.System;

    public static TrackReferenceSource System(string label, string detail) =>
        new(TrackReferenceKind.System, label, detail, null);

    public static TrackReferenceSource File(string label, string detail, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new(TrackReferenceKind.File, label, detail, path);
    }

    public override string ToString() => Label;
}
