using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using SkiaSharp;

namespace Autodraw;

/// <summary>
/// Calibration and color selection for the custom color picker shown by the target game.
/// Two modes are supported:
///   - "Wheel":  a hue/saturation wheel plus a separate brightness ramp (existing behavior).
///   - "Slider": three independent horizontal sliders for Hue, Saturation, and Value.
/// The active mode is persisted in the config entry <c>ColorPickerMode</c>. Existing wheel
/// keys (<c>ColorWheel*</c>, <c>ColorBrightness*</c>) are preserved untouched so older
/// configurations continue to work.
/// </summary>
public static class ColorPicker
{
    private const string ModeEntry = "ColorPickerMode";
    private const string WheelMode = "Wheel";
    private const string SliderMode = "Slider";

    public static string GetUsageInstructions() => string.Join(Environment.NewLine, new[]
    {
        "Color picker calibration",
        "",
        "Wheel (F5): click 4 points in order:",
        "1. Hue/saturation wheel center",
        "2. Wheel rim at center's hue",
        "3. Brightness/value ramp top",
        "4. Brightness/value ramp bottom",
        "",
        "Sliders (F6): click 6 points in order:",
        "1. Hue slider: left, then right",
        "2. Saturation slider: left, then right",
        "3. Value slider: left, then right",
        "",
        "AutoDraw minimizes during capture.",
        "It restores after the final click.",
        "Your selected mode is saved between launches.",
        "Use Set Color Hex in an action to select a color."
    });

    // ---------------------------------------------------------------------
    // Wheel mode
    // ---------------------------------------------------------------------

    public static bool UsesWheel =>
        Config.GetEntry(ModeEntry) == WheelMode && TryGetWheelCalibration(out _, out _, out _, out _);

    public static void SaveWheelCalibration(Vector2 center, Vector2 rim, Vector2 brightnessTop, Vector2 brightnessBottom)
    {
        Config.SetEntry(ModeEntry, WheelMode);
        Config.SetEntry("ColorWheelCenterX", center.X.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorWheelCenterY", center.Y.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorWheelRimX", rim.X.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorWheelRimY", rim.Y.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorBrightnessTopX", brightnessTop.X.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorBrightnessTopY", brightnessTop.Y.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorBrightnessBottomX", brightnessBottom.X.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry("ColorBrightnessBottomY", brightnessBottom.Y.ToString(CultureInfo.InvariantCulture));
    }

    // ---------------------------------------------------------------------
    // Slider mode
    // ---------------------------------------------------------------------

    public static bool UsesSlider =>
        Config.GetEntry(ModeEntry) == SliderMode && TryGetSliderCalibration(out _);

    /// <summary>True when at least one calibration mode is available for dispatching color selection.</summary>
    public static bool IsCalibrated => UsesWheel || UsesSlider;

    /// <summary>
    /// Persists the slider calibration. Each HSV channel is described by a min X, a max X, and a
    /// row Y; the caller supplies two samples per channel (left/right ends) and the midpoint of
    /// their Y values becomes the row's click height.
    /// </summary>
    public static void SaveSliderCalibration(
        Vector2 hueMin, Vector2 hueMax,
        Vector2 satMin, Vector2 satMax,
        Vector2 valMin, Vector2 valMax)
    {
        Config.SetEntry(ModeEntry, SliderMode);
        WriteChannelSlider("ColorSliderHue", hueMin, hueMax);
        WriteChannelSlider("ColorSliderSat", satMin, satMax);
        WriteChannelSlider("ColorSliderVal", valMin, valMax);
    }

    private static void WriteChannelSlider(string prefix, Vector2 a, Vector2 b)
    {
        var minX = MathF.Min(a.X, b.X);
        var maxX = MathF.Max(a.X, b.X);
        var midY = (a.Y + b.Y) / 2f;
        Config.SetEntry($"{prefix}MinX", minX.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry($"{prefix}MaxX", maxX.ToString(CultureInfo.InvariantCulture));
        Config.SetEntry($"{prefix}Y", midY.ToString(CultureInfo.InvariantCulture));
    }

    // ---------------------------------------------------------------------
    // Dispatch
    // ---------------------------------------------------------------------

    /// <summary>
    /// Selects a color in the target game's picker. Dispatches on the persisted
    /// <c>ColorPickerMode</c>; wheel mode is the default and keeps every existing
    /// wheel config key unchanged.
    /// </summary>
    public static async Task SelectWheelColor(string hex)
    {
        if (Config.GetEntry(ModeEntry) == SliderMode)
        {
            await SelectSliderColor(hex);
            return;
        }

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

    /// <summary>Selects a color by clicking each HSV slider at the position mapped from <paramref name="hex"/>.</summary>
    public static async Task SelectSliderColor(string hex)
    {
        if (!TryGetSliderCalibration(out var calibration)) return;

        var color = SKColor.Parse(hex);
        color.ToHsv(out var hue, out var saturation, out var value);

        await ClickChannelSlider(calibration.Hue, hue / 360f);
        await ClickChannelSlider(calibration.Sat, saturation / 100f);
        await ClickChannelSlider(calibration.Val, value / 100f);
    }

    private static async Task ClickChannelSlider(ChannelSlider slider, float t)
    {
        var x = slider.At(t);
        Input.MoveTo((short)MathF.Round(x), (short)MathF.Round(slider.Y));
        Input.SendClick(Input.MouseTypes.MouseLeft);
        await Drawing.NOP(500_000);
    }

    // ---------------------------------------------------------------------
    // Calibration readers
    // ---------------------------------------------------------------------

    /// <summary>A single horizontal slider described by its min X, max X, and click row Y.</summary>
    public readonly record struct ChannelSlider(float MinX, float MaxX, float Y)
    {
        public float At(float t) => MinX + Math.Clamp(t, 0f, 1f) * (MaxX - MinX);
        public bool IsValid => MathF.Abs(MaxX - MinX) > 5;
    }

    /// <summary>The complete slider calibration for all three HSV channels.</summary>
    public readonly record struct SliderCalibration(ChannelSlider Hue, ChannelSlider Sat, ChannelSlider Val)
    {
        public bool IsValid => Hue.IsValid && Sat.IsValid && Val.IsValid;
    }

    public static bool TryGetSliderCalibration(out SliderCalibration calibration)
    {
        calibration = default;
        if (!TryReadChannelSlider("ColorSliderHue", out var hue)) return false;
        if (!TryReadChannelSlider("ColorSliderSat", out var sat)) return false;
        if (!TryReadChannelSlider("ColorSliderVal", out var val)) return false;
        calibration = new SliderCalibration(hue, sat, val);
        return calibration.IsValid;
    }

    private static bool TryReadChannelSlider(string prefix, out ChannelSlider slider)
    {
        slider = default;
        if (!TryRead($"{prefix}MinX", out var minX)) return false;
        if (!TryRead($"{prefix}MaxX", out var maxX)) return false;
        if (!TryRead($"{prefix}Y", out var y)) return false;
        slider = new ChannelSlider(minX, maxX, y);
        return slider.IsValid;
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
