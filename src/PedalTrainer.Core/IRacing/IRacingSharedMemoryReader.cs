using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Text;

namespace PedalTrainer.Core.IRacing;

/// <summary>Leitura crua de um instante da telemetria do iRacing.</summary>
public readonly record struct IRacingRawSample(
    bool IsValid,
    double SpeedMetersPerSecond,
    double BrakeFraction,
    double ThrottleFraction,
    double LapDistancePercent,
    double SessionTimeSeconds,
    int LapNumber);

/// <summary>
/// Lê a memória compartilhada publicada pelo iRacing. Abre o mapeamento uma vez,
/// resolve os deslocamentos das variáveis no cabeçalho e depois só copia os valores.
/// </summary>
public sealed class IRacingSharedMemoryReader : IDisposable
{
    private readonly Dictionary<string, VariableBinding> _bindings = new(StringComparer.Ordinal);
    private MemoryMappedFile? _mappedFile;
    private MemoryMappedViewAccessor? _view;
    private int _bufferCount;
    private bool _disposed;

    /// <summary>Verdadeiro quando o mapeamento está aberto e pronto para leitura.</summary>
    public bool IsOpen => _view is not null;

    /// <summary>Quantidade de canais resolvidos no cabeçalho.</summary>
    public int BoundChannelCount => _bindings.Count;

    /// <summary>Tenta abrir o mapeamento e resolver os canais. Não lança exceção.</summary>
    public bool TryConnect()
    {
        if (_disposed || !OperatingSystem.IsWindows())
        {
            return false;
        }

        if (IsOpen)
        {
            return true;
        }

        CloseResources();

        try
        {
            MemoryMappedFile? mapped = OpenMapping();
            _mappedFile = mapped;

            if (mapped is null)
            {
                return false;
            }

            _view = mapped.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            _bufferCount = Math.Clamp(_view.ReadInt32(IRacingSdkLayout.HeaderNumBufOffset), 1, IRacingSdkLayout.MaxBuffers);
            return ResolveBindings();
        }
        catch (FileNotFoundException)
        {
            CloseResources();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            CloseResources();
            return false;
        }
        catch (IOException)
        {
            CloseResources();
            return false;
        }
    }

    /// <summary>Lê o buffer mais recente do simulador.</summary>
    public bool TryReadSample(out IRacingRawSample sample)
    {
        sample = default;

        MemoryMappedViewAccessor? view = _view;
        if (view is null)
        {
            return false;
        }

        try
        {
            int status = view.ReadInt32(IRacingSdkLayout.HeaderStatusOffset);
            if ((status & IRacingSdkLayout.StatusConnected) == 0)
            {
                return false;
            }

            int bufferOffset = FindNewestBufferOffset(view);
            if (bufferOffset <= 0)
            {
                return false;
            }

            sample = new IRacingRawSample(
                IsValid: true,
                SpeedMetersPerSecond: ReadDouble(view, IRacingSdkLayout.ChannelSpeed, bufferOffset),
                BrakeFraction: ReadDouble(view, IRacingSdkLayout.ChannelBrake, bufferOffset),
                ThrottleFraction: ReadDouble(view, IRacingSdkLayout.ChannelThrottle, bufferOffset),
                LapDistancePercent: ReadDouble(view, IRacingSdkLayout.ChannelLapDistPct, bufferOffset),
                SessionTimeSeconds: ReadDouble(view, IRacingSdkLayout.ChannelSessionTime, bufferOffset),
                LapNumber: (int)ReadDouble(view, IRacingSdkLayout.ChannelLap, bufferOffset));

            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (AccessViolationException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static MemoryMappedFile? OpenMapping()
    {
        foreach (string name in new[] { IRacingSdkLayout.MemoryMapName, IRacingSdkLayout.FallbackMemoryMapName })
        {
            try
            {
                return MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            }
            catch (FileNotFoundException)
            {
            }
        }

        return null;
    }

    private static int FindNewestBufferOffset(MemoryMappedViewAccessor view)
    {
        int count = Math.Clamp(view.ReadInt32(IRacingSdkLayout.HeaderNumBufOffset), 1, IRacingSdkLayout.MaxBuffers);
        int newestTick = int.MinValue;
        int newestOffset = 0;

        for (int i = 0; i < count; i++)
        {
            int entry = IRacingSdkLayout.HeaderVarBufOffset + (i * IRacingSdkLayout.VarBufSize);
            int tickCount = view.ReadInt32(entry);
            int bufferOffset = view.ReadInt32(entry + 4);

            if (bufferOffset > 0 && tickCount > newestTick)
            {
                newestTick = tickCount;
                newestOffset = bufferOffset;
            }
        }

        return newestOffset;
    }

    private bool ResolveBindings()
    {
        MemoryMappedViewAccessor? view = _view;
        if (view is null)
        {
            return false;
        }

        _bindings.Clear();
        int numVars = view.ReadInt32(IRacingSdkLayout.HeaderNumVarsOffset);
        int varHeaderOffset = view.ReadInt32(IRacingSdkLayout.HeaderVarHeaderOffset);

        if (numVars <= 0 || varHeaderOffset <= 0)
        {
            return false;
        }

        var wanted = new HashSet<string>(StringComparer.Ordinal)
        {
            IRacingSdkLayout.ChannelSpeed,
            IRacingSdkLayout.ChannelBrake,
            IRacingSdkLayout.ChannelThrottle,
            IRacingSdkLayout.ChannelLapDistPct,
            IRacingSdkLayout.ChannelSessionTime,
            IRacingSdkLayout.ChannelLap
        };

        for (int i = 0; i < numVars; i++)
        {
            int entry = varHeaderOffset + (i * IRacingSdkLayout.VarHeaderSize);
            string name = ReadFixedString(view, entry + IRacingSdkLayout.VarNameOffset, IRacingSdkLayout.VarNameLength);

            if (name.Length == 0 || !wanted.Contains(name) || _bindings.ContainsKey(name))
            {
                continue;
            }

            int type = view.ReadInt32(entry + IRacingSdkLayout.VarTypeOffset);
            int offset = view.ReadInt32(entry + IRacingSdkLayout.VarOffsetOffset);
            _bindings[name] = new VariableBinding(type, offset);
        }

        return _bindings.Count > 0;
    }

    private double ReadDouble(MemoryMappedViewAccessor view, string channel, int bufferOffset)
    {
        if (!_bindings.TryGetValue(channel, out VariableBinding binding))
        {
            return 0;
        }

        int position = bufferOffset + binding.Offset;

        return binding.Type switch
        {
            IRacingSdkLayout.TypeFloat => view.ReadSingle(position),
            IRacingSdkLayout.TypeDouble => view.ReadDouble(position),
            IRacingSdkLayout.TypeInt => view.ReadInt32(position),
            IRacingSdkLayout.TypeBool => view.ReadByte(position),
            IRacingSdkLayout.TypeBitField => view.ReadInt32(position),
            _ => 0
        };
    }

    private static string ReadFixedString(MemoryMappedViewAccessor view, int position, int length)
    {
        var buffer = new byte[length];
        view.ReadArray(position, buffer, 0, length);

        int end = Array.IndexOf(buffer, (byte)0);
        if (end < 0)
        {
            end = length;
        }

        return end == 0 ? string.Empty : Encoding.ASCII.GetString(buffer, 0, end);
    }

    private void CloseResources()
    {
        _bindings.Clear();
        _bufferCount = 0;
        _view?.Dispose();
        _view = null;
        _mappedFile?.Dispose();
        _mappedFile = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseResources();
    }

    private readonly record struct VariableBinding(int Type, int Offset);
}
