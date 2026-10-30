using System;

namespace PedalTrainer.Core.Telemetry.SharedMemory;

/// <summary>
/// Calcula o deslocamento de cada campo de uma struct C compilada com <c>#pragma pack(4)</c>.
/// Cada chamada reserva o campo e devolve a posição em bytes; a ordem das chamadas é a
/// ordem do cabeçalho oficial, então um campo esquecido aparece na revisão do código.
/// </summary>
public sealed class FieldLayout
{
    private const int FloatSize = 4;
    private const int IntSize = 4;
    private const int ShortSize = 2;
    private const int ByteSize = 1;
    private const int DoubleSize = 8;
    private const int WideCharSize = 2;

    private readonly int _pack;
    private int _offset;

    public FieldLayout(int pack = 4)
    {
        _pack = Math.Clamp(pack, 1, 8);
    }

    /// <summary>Posição (em bytes) onde o próximo campo será colocado.</summary>
    public int Offset => _offset;

    /// <summary>Tamanho total reservado até aqui, já alinhado ao pack da struct.</summary>
    public int Size => Align(_offset, _pack);

    public int AddFloat() => Reserve(FloatSize, FloatSize);

    public int AddInt() => Reserve(IntSize, IntSize);

    public int AddShort() => Reserve(ShortSize, ShortSize);

    public int AddByte() => Reserve(ByteSize, ByteSize);

    public int AddBool() => AddByte();

    public int AddDouble() => Reserve(DoubleSize, DoubleSize);

    /// <summary>Reserva <paramref name="count"/> elementos de 4 bytes (float ou int).</summary>
    public int AddArray(int count) => Reserve(checked(FloatSize * count), FloatSize);

    /// <summary>Reserva uma sequência de bytes (char C, vetor opaco, expansão futura).</summary>
    public int AddBytes(int count) => Reserve(count, ByteSize);

    /// <summary>Reserva uma string larga (wchar_t do Windows: 2 bytes por caractere).</summary>
    public int AddWideString(int characters) => Reserve(checked(WideCharSize * characters), WideCharSize);

    /// <summary>Avança sem devolver posição (útil para campos não usados).</summary>
    public void Skip(int bytes) => Reserve(bytes, ByteSize);

    /// <summary>Alinha o cursor ao pack da struct (equivale ao fechamento de um bloco).</summary>
    public void AlignToPack() => _offset = Align(_offset, _pack);

    /// <summary>Recomeça a contagem em <paramref name="baseOffset"/> bytes.</summary>
    public void StartAt(int baseOffset)
    {
        _offset = Math.Max(0, baseOffset);
    }

    private int Reserve(int size, int alignment)
    {
        _offset = Align(_offset, Math.Min(alignment, _pack));

        int position = _offset;
        _offset = checked(_offset + size);
        return position;
    }

    private static int Align(int value, int alignment)
    {
        if (alignment <= 1)
        {
            return value;
        }

        int remainder = value % alignment;
        return remainder == 0 ? value : checked(value + (alignment - remainder));
    }
}
