using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Autodraw.Painting;

public enum QuantizeMethod { KMeansPlusPlus, LabColorSpace }
public enum KMeansAlgo { KMeansPlusPlus, MiniBatchKMeans }

public class LabParams
{
    public float LumWeight = 1;
    public float ChromaWeight = 1;
    public float HueWeight = 1;
    public float ContrastThresh = 10;
    public float SpatialSigma = 10;
    public int SpatialIter = 1;
    public float EdgePreservation = 50;
    public float RangeSigma = 20;
}

public static class PaletteExtractor
{
    public static IReadOnlyList<SKColor> Extract(
        SKBitmap source,
        int paletteSize,
        byte alphaThreshold,
        QuantizeMethod method,
        KMeansAlgo algo,
        LabParams lab)
    {
        return Array.Empty<SKColor>();
    }
}
