using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;

namespace PedalTrainer.Core.Telemetry.SharedMemory;

/// <summary>
/// Acesso somente-leitura a um bloco de memória compartilhada nomeado do Windows.
/// Abre por uma lista de nomes candidatos (o mesmo jogo pode publicar com ou sem o
/// prefixo de sessão) e expõe leituras tipadas com verificação de limites.
/// </summary>
public sealed class SharedMemoryMap : IDisposable
{
    /// <summary>Quantidade mínima de bytes para considerar o bloco utilizável.</summary>
    public const int MinimumUsefulBytes = 64;

    private MemoryMappedFile? _file;
    private MemoryMappedViewAccessor? _view;
    private bool _disposed;

    /// <summary>Nome com que o bloco foi efetivamente aberto.</summary>
    public string? OpenName { get; private set; }

    /// <summary>Verdadeiro quando o bloco está aberto e pronto para leitura.</summary>
    public bool IsOpen => _view is not null;

    /// <summary>Tamanho do bloco aberto, em bytes (0 quando fechado).</summary>
    public long CapacityBytes => _view?.Capacity ?? 0;

    /// <summary>
    /// Tenta abrir o primeiro nome que existir. Devolve falso sem lançar exceção
    /// quando o jogo não está publicando o bloco.
    /// </summary>
    public bool TryOpen(params string[] candidateNames)
    {
        if (_disposed || !OperatingSystem.IsWindows() || candidateNames is null || candidateNames.Length == 0)
        {
            return false;
        }

        if (IsOpen)
        {
            return true;
        }

        Close();

        foreach (string name in candidateNames)
        {
            if (!OpenByName(name))
            {
                continue;
            }

            if (CapacityBytes >= MinimumUsefulBytes)
            {
                return true;
            }

            Close();
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private bool OpenByName(string name)
    {
        try
        {
            MemoryMappedFile file = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            MemoryMappedViewAccessor view = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            _file = file;
            _view = view;
            OpenName = name;
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Lê um float; devolve zero quando a posição está fora do bloco.</summary>
    public float ReadFloat(int offset) =>
        CanRead(offset, sizeof(float)) ? _view!.ReadSingle(offset) : 0f;

    /// <summary>Lê um double; devolve zero quando a posição está fora do bloco.</summary>
    public double ReadDouble(int offset) =>
        CanRead(offset, sizeof(double)) ? _view!.ReadDouble(offset) : 0d;

    /// <summary>Lê um inteiro de 32 bits; devolve zero quando a posição está fora do bloco.</summary>
    public int ReadInt32(int offset) =>
        CanRead(offset, sizeof(int)) ? _view!.ReadInt32(offset) : 0;

    /// <summary>Lê um inteiro sem sinal de 64 bits; devolve zero quando fora do bloco.</summary>
    public long ReadInt64(int offset) =>
        CanRead(offset, sizeof(long)) ? _view!.ReadInt64(offset) : 0L;

    /// <summary>Lê um inteiro de 16 bits sem sinal; devolve zero quando fora do bloco.</summary>
    public ushort ReadUInt16(int offset) =>
        CanRead(offset, sizeof(ushort)) ? _view!.ReadUInt16(offset) : (ushort)0;

    /// <summary>Lê um byte; devolve zero quando a posição está fora do bloco.</summary>
    public byte ReadByte(int offset) =>
        CanRead(offset, 1) ? _view!.ReadByte(offset) : (byte)0;

    /// <summary>Verdadeiro quando há <paramref name="length"/> bytes a partir do deslocamento.</summary>
    public bool CanRead(int offset, int length) =>
        _view is not null && offset >= 0 && offset + (long)length <= CapacityBytes;

    /// <summary>Fecha o bloco aberto, se houver.</summary>
    public void Close()
    {
        _view?.Dispose();
        _view = null;

        _file?.Dispose();
        _file = null;

        OpenName = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Close();
    }
}
