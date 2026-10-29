using System;
using System.Collections.Generic;
using SharpDX.DirectInput;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Liga cada eixo enumerado do dispositivo ao campo correspondente do
/// <see cref="JoystickState"/>. Sem isso o índice mostrado na tela não é o mesmo
/// que o app lê, e o freio acaba mapeado no eixo errado.
/// </summary>
public sealed class PedalAxisMap
{
    private readonly AxisField[] _fields;

    private PedalAxisMap(AxisField[] fields)
    {
        _fields = fields;
    }

    /// <summary>Quantidade de eixos mapeados.</summary>
    public int Count => _fields.Length;

    /// <summary>Campo do JoystickState usado por cada índice enumerado.</summary>
    public AxisField FieldAt(int index) =>
        index >= 0 && index < _fields.Length ? _fields[index] : AxisField.None;

    /// <summary>
    /// Constrói o mapa a partir dos objetos de eixo do dispositivo, na mesma ordem em
    /// que o DirectInput os enumera (a mesma usada nos nomes exibidos na interface).
    /// </summary>
    public static PedalAxisMap From(IList<DeviceObjectInstance> axes)
    {
        int count = Math.Min(axes.Count, Models.RawAxesSnapshot.MaxAxes);
        var fields = new AxisField[count];
        var used = new HashSet<AxisField>();

        for (var i = 0; i < count; i++)
        {
            AxisField field = Resolve(axes[i]);

            if (field != AxisField.None && !used.Add(field))
            {
                field = AxisField.None;
            }

            fields[i] = field;
        }

        AssignLeftovers(fields, used);

        return new PedalAxisMap(fields);
    }

    /// <summary>Copia os valores dos eixos para o buffer do instantâneo.</summary>
    public void Fill(JoystickState state, int[] destination)
    {
        int count = Math.Min(_fields.Length, destination.Length);

        for (var i = 0; i < count; i++)
        {
            destination[i] = Read(state, _fields[i]);
        }

        for (int i = count; i < destination.Length; i++)
        {
            destination[i] = 0;
        }
    }

    private static AxisField Resolve(DeviceObjectInstance axis)
    {
        Guid objectType = axis.ObjectType;

        if (objectType == ObjectGuid.XAxis)
        {
            return AxisField.X;
        }

        if (objectType == ObjectGuid.YAxis)
        {
            return AxisField.Y;
        }

        if (objectType == ObjectGuid.ZAxis)
        {
            return AxisField.Z;
        }

        if (objectType == ObjectGuid.RxAxis)
        {
            return AxisField.RotationX;
        }

        if (objectType == ObjectGuid.RyAxis)
        {
            return AxisField.RotationY;
        }

        if (objectType == ObjectGuid.RzAxis)
        {
            return AxisField.RotationZ;
        }

        if (objectType == ObjectGuid.Slider)
        {
            return axis.ObjectId.InstanceNumber switch
            {
                0 => AxisField.Slider0,
                1 => AxisField.Slider1,
                2 => AxisField.Slider2,
                _ => AxisField.None
            };
        }

        return AxisField.None;
    }

    /// <summary>
    /// Eixos com identificador proprietário (comum em placas Arduino com nomes próprios,
    /// como "Direção" e "Freio") recebem, em ordem, os campos canônicos ainda livres.
    /// </summary>
    private static void AssignLeftovers(AxisField[] fields, HashSet<AxisField> used)
    {
        var pending = new Queue<AxisField>(new[]
        {
            AxisField.Slider0,
            AxisField.Slider1,
            AxisField.Slider2,
            AxisField.X,
            AxisField.Y,
            AxisField.Z,
            AxisField.RotationX,
            AxisField.RotationY,
            AxisField.RotationZ
        });

        for (var i = 0; i < fields.Length; i++)
        {
            if (fields[i] != AxisField.None)
            {
                continue;
            }

            while (pending.Count > 0 && !used.Add(pending.Peek()))
            {
                pending.Dequeue();
            }

            if (pending.Count == 0)
            {
                return;
            }

            fields[i] = pending.Dequeue();
        }
    }

    private static int Read(JoystickState state, AxisField field) => field switch
    {
        AxisField.X => state.X,
        AxisField.Y => state.Y,
        AxisField.Z => state.Z,
        AxisField.RotationX => state.RotationX,
        AxisField.RotationY => state.RotationY,
        AxisField.RotationZ => state.RotationZ,
        AxisField.Slider0 => ReadSlider(state, 0),
        AxisField.Slider1 => ReadSlider(state, 1),
        AxisField.Slider2 => ReadSlider(state, 2),
        _ => 0
    };

    private static int ReadSlider(JoystickState state, int index)
    {
        int[]? sliders = state.Sliders;
        return sliders is not null && index < sliders.Length ? sliders[index] : 0;
    }
}

/// <summary>Campo do JoystickState que alimenta um índice de eixo.</summary>
public enum AxisField
{
    None = 0,
    X,
    Y,
    Z,
    RotationX,
    RotationY,
    RotationZ,
    Slider0,
    Slider1,
    Slider2
}
