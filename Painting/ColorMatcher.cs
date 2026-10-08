using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Autodraw.Painting;

public enum ColorSpace { Rgb, Lab }

public static class ColorMatcher
{
    public static int ClosestIndex(SKColor c, IReadOnlyList<SKColor> palette, ColorSpace space)
    {
        if (palette.Count == 0)
            throw new ArgumentException("Palette must not be empty.", nameof(palette));

        int best = 0;
        double bestDist = double.MaxValue;
        for (int i = 0; i < palette.Count; i++)
        {
            double d = space == ColorSpace.Lab
                ? DeltaE2000(c, palette[i])
                : RgbDistance(c, palette[i]);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    public static double DeltaE2000(SKColor a, SKColor b)
    {
        ToLab(a, out double L1, out double a1, out double b1);
        ToLab(b, out double L2, out double a2, out double b2);
        double dL = L1 - L2, da = a1 - a2, db = b1 - b2;
        return Math.Sqrt(dL * dL + da * da + db * db);
    }

    private static double RgbDistance(SKColor a, SKColor b)
    {
        int dr = a.Red - b.Red, dg = a.Green - b.Green, dbl = a.Blue - b.Blue;
        return Math.Sqrt(dr * dr + dg * dg + dbl * dbl);
    }

    private static void ToLab(SKColor c, out double L, out double a, out double b)
    {
        double r = PivotRgb(c.Red / 255.0);
        double g = PivotRgb(c.Green / 255.0);
        double bl = PivotRgb(c.Blue / 255.0);

        double x = PivotXyz((r * 0.4124564 + g * 0.3575761 + bl * 0.1804375) / 0.95047);
        double y = PivotXyz(r * 0.2126729 + g * 0.7151522 + bl * 0.0721750);
        double z = PivotXyz((r * 0.0193339 + g * 0.1191920 + bl * 0.9503041) / 1.08883);

        L = 116.0 * y - 16.0;
        a = 500.0 * (x - y);
        b = 200.0 * (y - z);
    }

    private static double PivotRgb(double n)
        => n > 0.04045 ? Math.Pow((n + 0.055) / 1.055, 2.4) : n / 12.92;

    private static double PivotXyz(double n)
        => n > 0.008856 ? Math.Cbrt(n) : 7.787 * n + 16.0 / 116.0;
}
