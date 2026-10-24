using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Autodraw.Configuration;
using Autodraw.Painting.Calibration;
using SkiaSharp;

namespace Autodraw.Painting.ColorInput;

/// <summary>
/// Palette Match color input: instead of driving a wheel or typing channel values, the
/// requested color is resolved to the nearest captured palette slot (ΔE2000) and that
/// slot's stored screen coordinate is clicked.  Slots come from the portable config key
/// <c>palette_slots</c> (<see cref="AutoDrawConfig.PaletteSlots"/>).
/// </summary>
public class PaletteGridInput : IColorInputMethod
{
    public string Name => "Palette Match";
    public bool IsCalibrated => DdConfigService.Current.PaletteSlots.Count > 0;

    /// <summary>Clicks the palette slot closest to <paramref name="hex"/>.  No-op when no
    /// slot parses as a colour, so a misconfigured palette never misclicks.</summary>
    public async Task ApplyColor(string hex, CancellationToken ct)
    {
        var slots = DdConfigService.Current.PaletteSlots;
        if (slots.Count == 0) return;
        if (!SKColor.TryParse(hex, out var target)) return;

        var bestSlot = slots[0];
        var bestDistance = double.MaxValue;
        var found = false;
        foreach (var slot in slots)
        {
            ct.ThrowIfCancellationRequested();
            if (!SKColor.TryParse(slot.Hex, out var slotColor)) continue;
            var distance = ColorMatcher.DeltaE2000(target, slotColor);
            if (!found || distance < bestDistance)
            {
                bestSlot = slot;
                bestDistance = distance;
                found = true;
            }
        }
        if (!found) return;

        Input.MoveTo((short)bestSlot.Coord.X, (short)bestSlot.Coord.Y);
        Input.SendClick(Input.MouseTypes.MouseLeft);
        await Drawing.NOP(500_000);
    }

    public Task CalibrateAsync(IEnumerable<Vector2> points) => Task.CompletedTask;
    public void Save(PickerProfile profile) { }
    public void Load(PickerProfile profile) { }
}
