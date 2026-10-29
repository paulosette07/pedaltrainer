using System;
using System.Collections.Generic;
using PedalTrainer.Core.Models;
using SharpDX;
using SharpDX.DirectInput;

namespace PedalTrainer.Core.Input;

/// <summary>
/// Enumera os controladores de jogo (DirectInput) e identifica o que tem mais cara de
/// caixa de pedais. A escolha final fica com <see cref="PedalDeviceScorer"/>.
/// </summary>
public static class PedalDeviceEnumerator
{
    private static readonly string[] FallbackAxisNames =
    {
        "Eixo X",
        "Eixo Y",
        "Eixo Z",
        "Rotação X",
        "Rotação Y",
        "Rotação Z",
        "Slider 1",
        "Slider 2",
        "Slider 3",
        "Slider 4",
        "Eixo 9",
        "Eixo 10"
    };

    /// <summary>Lista os controladores conectados que possuem ao menos um eixo analógico.</summary>
    public static IReadOnlyList<PedalDeviceDescriptor> Enumerate()
    {
        var found = new List<PedalDeviceDescriptor>();

        try
        {
            using var directInput = new DirectInput();
            IList<DeviceInstance> instances = directInput.GetDevices(
                DeviceClass.GameControl,
                DeviceEnumerationFlags.AttachedOnly);

            foreach (DeviceInstance instance in instances)
            {
                PedalDeviceDescriptor? descriptor = Describe(directInput, instance);
                if (descriptor is not null)
                {
                    found.Add(descriptor);
                }
            }
        }
        catch (SharpDXException)
        {
            // Sem DirectInput disponível: a UI cai no modo simulado.
        }
        catch (DllNotFoundException)
        {
        }

        found.Sort(static (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return found;
    }

    /// <summary>
    /// Escolhe o controlador mais provável de ser o conjunto de pedais. Uma base de
    /// volante só tem eixos de rotação e perde para a placa que expõe "Freio".
    /// </summary>
    public static PedalDeviceDescriptor? PickDefault(IReadOnlyList<PedalDeviceDescriptor> devices) =>
        PedalDeviceScorer.PickBest(devices);

    private static PedalDeviceDescriptor? Describe(DirectInput directInput, DeviceInstance instance)
    {
        string name = string.IsNullOrWhiteSpace(instance.InstanceName) ? instance.ProductName : instance.InstanceName;

        try
        {
            using var joystick = new Joystick(directInput, instance.InstanceGuid);
            IList<DeviceObjectInstance> axes = joystick.GetObjects(DeviceObjectTypeFlags.Axis);

            if (axes.Count == 0)
            {
                return null;
            }

            var names = new string[Math.Min(axes.Count, RawAxesSnapshot.MaxAxes)];
            for (int i = 0; i < names.Length; i++)
            {
                string axisName = axes[i].Name ?? string.Empty;
                names[i] = string.IsNullOrWhiteSpace(axisName) ? AxisFallback(i) : axisName;
            }

            return new PedalDeviceDescriptor(
                instance.InstanceGuid,
                instance.ProductGuid,
                name,
                instance.ProductName,
                axes.Count,
                names);
        }
        catch (SharpDXException)
        {
            return new PedalDeviceDescriptor(
                instance.InstanceGuid,
                instance.ProductGuid,
                name,
                instance.ProductName,
                RawAxesSnapshot.MaxAxes,
                FallbackAxisNames);
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private static string AxisFallback(int index) =>
        index >= 0 && index < FallbackAxisNames.Length ? FallbackAxisNames[index] : $"Eixo {index}";
}
