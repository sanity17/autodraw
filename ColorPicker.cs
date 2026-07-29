using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using SkiaSharp;

namespace Autodraw;

/// <summary>Calibration and color selection for the Custom hue wheel shown by the target game.</summary>
public static class ColorPicker
{
    private const string ModeEntry = "ColorPickerMode";

    public static bool UsesWheel => Config.GetEntry(ModeEntry) == "Wheel" && TryGetWheelCalibration(out _, out _, out _, out _);

    public static void SaveWheelCalibration(Vector2 center, Vector2 rim, Vector2 brightnessTop, Vector2 brightnessBottom)
    {
        Config.SetEntry(ModeEntry, "Wheel");
        Config.SetEntry("ColorWheelCenterX", center.X.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorWheelCenterY", center.Y.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorWheelRimX", rim.X.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorWheelRimY", rim.Y.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorBrightnessTopX", brightnessTop.X.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorBrightnessTopY", brightnessTop.Y.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorBrightnessBottomX", brightnessBottom.X.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorBrightnessBottomY", brightnessBottom.Y.ToString(CultureInfo.InvariantCulture));
    }

    public static async Task SelectWheelColor(string hex)
    {
        if (!TryGetWheelCalibration(out var center, out var rim, out var brightnessTop, out var brightnessBottom)) return;

        var color = SKColor.Parse(hex);
        color.ToHsv(out var hue, out var saturation, out var value);
        var radius = Vector2.Distance(center, rim);

        // This game's wheel is red at 9 o'clock, then yellow/green clockwise.
        var angle = (hue + 180f) * MathF.PI / 180f;
        var wheelPoint = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius * saturation / 100f);
        Input.MoveTo((short)MathF.Round(wheelPoint.X), (short)MathF.Round(wheelPoint.Y));
        Input.SendClick(Input.MouseTypes.MouseLeft);

        await Drawing.NOP(500_000);
        var brightnessPoint = Vector2.Lerp(brightnessTop, brightnessBottom, 1f - value / 100f);
        Input.MoveTo((short)MathF.Round(brightnessPoint.X), (short)MathF.Round(brightnessPoint.Y));
        Input.SendClick(Input.MouseTypes.MouseLeft);
        await Drawing.NOP(500_000);
    }

    private static bool TryGetWheelCalibration(out Vector2 center, out Vector2 rim, out Vector2 brightnessTop, out Vector2 brightnessBottom)
    {
        center = rim = brightnessTop = brightnessBottom = default;
        return TryRead("ColorWheelCenterX", out center.X) && TryRead("ColorWheelCenterY", out center.Y)
            && TryRead("ColorWheelRimX", out rim.X) && TryRead("ColorWheelRimY", out rim.Y)
            && TryRead("ColorBrightnessTopX", out brightnessTop.X) && TryRead("ColorBrightnessTopY", out brightnessTop.Y)
            && TryRead("ColorBrightnessBottomX", out brightnessBottom.X) && TryRead("ColorBrightnessBottomY", out brightnessBottom.Y)
            && Vector2.Distance(center, rim) > 5 && Vector2.Distance(brightnessTop, brightnessBottom) > 5;
    }

    private static bool TryRead(string key, out float value) => float.TryParse(Config.GetEntry(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
