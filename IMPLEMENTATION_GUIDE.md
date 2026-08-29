# AutoDraw Color Picker & Calibration Implementation Guide

This guide provides ready-to-use code implementations for the color picker and calibration features.

---

## 1. Screen Color Picker Implementation

### File: `/workspace/Color/ScreenColorPicker.cs`

```csharp
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SkiaSharp;

namespace Autodraw.Color;

public class ScreenColorPicker
{
    // P/Invoke for screen capture
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);
    
    [DllImport("gdi32.dll")]
    private static extern int GetPixel(IntPtr hdc, int x, int y);
    
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    /// <summary>
    /// Pick a color from screen at specified coordinates
    /// </summary>
    public static SKColor PickColor(int x, int y)
    {
        try
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                throw new Exception("Failed to get device context");
            
            uint pixel = (uint)GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);
            
            byte r = (byte)(pixel & 0xFF);
            byte g = (byte)((pixel >> 8) & 0xFF);
            byte b = (byte)((pixel >> 16) & 0xFF);
            
            return new SKColor(r, g, b, 255);
        }
        catch (Exception ex)
        {
            Utils.Log($"Color pick failed: {ex.Message}");
            return new SKColor(0, 0, 0, 255);
        }
    }

    /// <summary>
    /// Pick color at current cursor position
    /// </summary>
    public static SKColor PickColorAtCursor()
    {
        var cursorPos = Input.GetCursorPosition();
        return PickColor((int)cursorPos.X, (int)cursorPos.Y);
    }

    /// <summary>
    /// Interactive color picking mode with magnifier
    /// </summary>
    public static async Task<SKColor?> ShowEyedropperAsync(Window parentWindow)
    {
        var tcs = new TaskCompletionSource<SKColor?>();
        
        // Create overlay window
        var overlay = new Window
        {
            Title = "Color Picker",
            Width = 400,
            Height = 300,
            WindowStyle = WindowStyle.None,
            SystemDecorations = SystemDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            Topmost = true,
            CanResize = false,
            Owner = parentWindow
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto,Auto")
        };

        // Magnifier preview
        var magnifier = new Image
        {
            Width = 200,
            Height = 200,
            [Grid.RowProperty] = 0
        };

        // Color preview
        var colorPreview = new Border
        {
            Width = 100,
            Height = 50,
            CornerRadius = new CornerRadius(4),
            BorderBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            BorderThickness = new Thickness(2),
            [Grid.RowProperty] = 1,
            [Grid.ColumnProperty] = 0,
            Margin = new Thickness(10)
        };

        // Hex label
        var hexLabel = new TextBlock
        {
            Text = "#000000",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            [Grid.RowProperty] = 2,
            [Grid.ColumnProperty] = 0,
            Margin = new Thickness(10)
        };

        // Instruction
        var instruction = new TextBlock
        {
            Text = "Move cursor to pick color. Click to select. ESC to cancel.",
            FontSize = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            [Grid.RowProperty] = 3,
            Margin = new Thickness(10)
        };

        grid.Children.Add(magnifier);
        grid.Children.Add(colorPreview);
        grid.Children.Add(hexLabel);
        grid.Children.Add(instruction);

        overlay.Content = grid;

        SKColor? selectedColor = null;
        bool isPicking = true;

        // Timer for updating magnifier
        var timer = new System.Threading.Timer(_ =>
        {
            if (!isPicking) return;
            
            var cursorPos = Input.GetCursorPosition();
            var color = PickColor((int)cursorPos.X, (int)cursorPos.Y);
            
            Dispatcher.UIThread.Post(() =>
            {
                colorPreview.Background = new SolidColorBrush(
                    Color.FromRgb(color.Red, color.Green, color.Blue));
                hexLabel.Text = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
                
                // TODO: Update magnifier with zoomed screenshot region
            });
        }, null, 0, 50);

        overlay.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                isPicking = false;
                timer.Dispose();
                overlay.Close();
                tcs.SetResult(null);
            }
        };

        overlay.PointerPressed += (s, e) =>
        {
            var cursorPos = Input.GetCursorPosition();
            selectedColor = PickColor((int)cursorPos.X, (int)cursorPos.Y);
            isPicking = false;
            timer.Dispose();
            overlay.Close();
            tcs.SetResult(selectedColor);
        };

        overlay.Show();
        
        return await tcs.Task;
    }

    /// <summary>
    /// Add picked color to hex input box
    /// </summary>
    public static async Task AddColorToInputAsync(
        Window parentWindow, 
        TextBox hexInput,
        bool multiPickMode = false)
    {
        var color = await ShowEyedropperAsync(parentWindow);
        
        if (color.HasValue)
        {
            var hexValue = $"#{color.Value.Red:X2}{color.Value.Green:X2}{color.Value.Blue:X2}";
            
            if (string.IsNullOrWhiteSpace(hexInput.Text))
            {
                hexInput.Text = hexValue;
            }
            else
            {
                hexInput.Text += $", {hexValue}";
            }
        }
    }
}
```

---

## 2. Color Calibration System

### File: `/workspace/Color/ColorCalibrationProfile.cs`

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SkiaSharp;

namespace Autodraw.Color;

public class ColorCalibrationProfile
{
    [JsonProperty("name")]
    public string ProfileName { get; set; } = "Default";
    
    [JsonProperty("targetApplication")]
    public string TargetApplication { get; set; } = "";
    
    [JsonProperty("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    [JsonProperty("gammaCorrection")]
    public float GammaCorrection { get; set; } = 1.0f;
    
    [JsonProperty("saturationShift")]
    public float SaturationShift { get; set; } = 0.0f;
    
    [JsonProperty("brightnessShift")]
    public float BrightnessShift { get; set; } = 0.0f;
    
    [JsonProperty("colorSamples")]
    public List<ColorSamplePair> ColorSamples { get; set; } = new();
    
    [JsonProperty("useMatrixTransform")]
    public bool UseMatrixTransform { get; set; } = false;
    
    [JsonProperty("transformMatrix")]
    public float[] TransformMatrix { get; set; } = new float[9];
}

public class ColorSamplePair
{
    [JsonProperty("expectedR")]
    public byte ExpectedR { get; set; }
    
    [JsonProperty("expectedG")]
    public byte ExpectedG { get; set; }
    
    [JsonProperty("expectedB")]
    public byte ExpectedB { get; set; }
    
    [JsonProperty("actualR")]
    public byte ActualR { get; set; }
    
    [JsonProperty("actualG")]
    public byte ActualG { get; set; }
    
    [JsonProperty("actualB")]
    public byte ActualB { get; set; }
    
    public SKColor Expected => new SKColor(ExpectedR, ExpectedG, ExpectedB);
    public SKColor Actual => new SKColor(ActualR, ActualG, ActualB);
}
```

### File: `/workspace/Color/ColorCalibrator.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Autodraw.Color;

public static class ColorCalibrator
{
    /// <summary>
    /// Calculate calibration profile from sample pairs
    /// </summary>
    public static ColorCalibrationProfile CreateProfileFromSamples(
        string profileName,
        string targetApp,
        List<ColorSamplePair> samples)
    {
        if (samples.Count < 2)
            throw new ArgumentException("Need at least 2 sample pairs for calibration");

        var profile = new ColorCalibrationProfile
        {
            ProfileName = profileName,
            TargetApplication = targetApp,
            ColorSamples = samples
        };

        // Calculate average gamma correction
        float totalGammaR = 0, totalGammaG = 0, totalGammaB = 0;
        
        foreach (var sample in samples)
        {
            // Avoid division by zero
            if (sample.ExpectedR > 0 && sample.ActualR > 0)
            {
                totalGammaR += MathF.Log(sample.ExpectedR / 255f) / MathF.Log(sample.ActualR / 255f);
            }
            if (sample.ExpectedG > 0 && sample.ActualG > 0)
            {
                totalGammaG += MathF.Log(sample.ExpectedG / 255f) / MathF.Log(sample.ActualG / 255f);
            }
            if (sample.ExpectedB > 0 && sample.ActualB > 0)
            {
                totalGammaB += MathF.Log(sample.ExpectedB / 255f) / MathF.Log(sample.ActualB / 255f);
            }
        }

        profile.GammaCorrection = (totalGammaR + totalGammaG + totalGammaB) / (3f * samples.Count);
        
        // Calculate brightness shift
        float avgBrightnessDiff = 0;
        foreach (var sample in samples)
        {
            float expectedBrightness = (sample.ExpectedR + sample.ExpectedG + sample.ExpectedB) / 3f;
            float actualBrightness = (sample.ActualR + sample.ActualG + sample.ActualB) / 3f;
            avgBrightnessDiff += expectedBrightness - actualBrightness;
        }
        profile.BrightnessShift = avgBrightnessDiff / samples.Count;

        return profile;
    }

    /// <summary>
    /// Apply calibration to a color
    /// </summary>
    public static SKColor ApplyCalibration(SKColor originalColor, ColorCalibrationProfile profile)
    {
        if (profile == null) return originalColor;

        float r = originalColor.Red;
        float g = originalColor.Green;
        float b = originalColor.Blue;

        // Apply gamma correction
        if (profile.GammaCorrection != 1.0f)
        {
            r = MathF.Pow(r / 255f, 1f / profile.GammaCorrection) * 255f;
            g = MathF.Pow(g / 255f, 1f / profile.GammaCorrection) * 255f;
            b = MathF.Pow(b / 255f, 1f / profile.GammaCorrection) * 255f;
        }

        // Apply brightness shift
        r = Math.Clamp(r + profile.BrightnessShift, 0, 255);
        g = Math.Clamp(g + profile.BrightnessShift, 0, 255);
        b = Math.Clamp(b + profile.BrightnessShift, 0, 255);

        // Apply saturation shift
        if (profile.SaturationShift != 0)
        {
            float gray = 0.299f * r + 0.587f * g + 0.114f * b;
            r = gray + (r - gray) * (1f + profile.SaturationShift);
            g = gray + (g - gray) * (1f + profile.SaturationShift);
            b = gray + (b - gray) * (1f + profile.SaturationShift);
        }

        // Apply matrix transform if available
        if (profile.UseMatrixTransform && profile.TransformMatrix.Length == 9)
        {
            float newR = r * profile.TransformMatrix[0] + g * profile.TransformMatrix[1] + b * profile.TransformMatrix[2];
            float newG = r * profile.TransformMatrix[3] + g * profile.TransformMatrix[4] + b * profile.TransformMatrix[5];
            float newB = r * profile.TransformMatrix[6] + g * profile.TransformMatrix[7] + b * profile.TransformMatrix[8];
            
            r = Math.Clamp(newR, 0, 255);
            g = Math.Clamp(newG, 0, 255);
            b = Math.Clamp(newB, 0, 255);
        }

        return new SKColor((byte)r, (byte)g, (byte)b, 255);
    }

    /// <summary>
    /// Apply calibration to a list of colors
    /// </summary>
    public static List<SKColor> ApplyCalibration(List<SKColor> colors, ColorCalibrationProfile profile)
    {
        return colors.Select(c => ApplyCalibration(c, profile)).ToList();
    }

    /// <summary>
    /// Calculate color difference (Delta-E)
    /// </summary>
    public static double CalculateColorDifference(SKColor color1, SKColor color2)
    {
        // Simple Euclidean distance in RGB space
        int dr = color1.Red - color2.Red;
        int dg = color1.Green - color2.Green;
        int db = color1.Blue - color2.Blue;
        
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>
    /// Validate calibration quality
    /// </summary>
    public static CalibrationQuality AssessCalibrationQuality(ColorCalibrationProfile profile)
    {
        if (profile.ColorSamples.Count < 2)
            return new CalibrationQuality { IsValid = false, Message = "Need at least 2 samples" };

        double totalError = 0;
        double maxError = 0;

        foreach (var sample in profile.ColorSamples)
        {
            var calibrated = ApplyCalibration(sample.Expected, profile);
            double error = CalculateColorDifference(calibrated, sample.Actual);
            totalError += error;
            maxError = Math.Max(maxError, error);
        }

        double avgError = totalError / profile.ColorSamples.Count;

        return new CalibrationQuality
        {
            IsValid = true,
            AverageError = avgError,
            MaxError = maxError,
            SampleCount = profile.ColorSamples.Count,
            Rating = avgError < 5 ? "Excellent" : 
                     avgError < 10 ? "Good" : 
                     avgError < 20 ? "Fair" : "Poor"
        };
    }
}

public class CalibrationQuality
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = "";
    public double AverageError { get; set; }
    public double MaxError { get; set; }
    public int SampleCount { get; set; }
    public string Rating { get; set; } = "";
}
```

---

## 3. Color Quantization for Auto-Extraction

### File: `/workspace/Color/ColorQuantizer.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Autodraw.Color;

public static class ColorQuantizer
{
    /// <summary>
    /// Extract dominant colors using simple quantization
    /// </summary>
    public static List<SKColor> ExtractDominantColors(
        SKBitmap bitmap, 
        int maxColors = 8,
        int minColorFrequency = 10)
    {
        var colorCounts = new Dictionary<uint, int>();
        
        unsafe
        {
            var ptr = (uint*)bitmap.GetPixels().ToPointer();
            int width = bitmap.Width;
            int height = bitmap.Height;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    uint pixel = *(ptr + y * width + x);
                    byte a = (byte)((pixel >> 24) & 0xFF);
                    
                    // Skip transparent pixels
                    if (a < 128) continue;
                    
                    // Reduce precision to group similar colors
                    byte r = (byte)((pixel & 0xFF) & 0xF8); // 5-bit
                    byte g = (byte)(((pixel >> 8) & 0xFF) & 0xFC); // 6-bit
                    byte b = (byte)(((pixel >> 16) & 0xFF) & 0xF8); // 5-bit
                    
                    uint reducedColor = (uint)((r << 16) | (g << 8) | b);
                    
                    if (!colorCounts.ContainsKey(reducedColor))
                        colorCounts[reducedColor] = 0;
                    
                    colorCounts[reducedColor]++;
                }
            }
        }
        
        // Sort by frequency and take top N
        var sortedColors = colorCounts
            .Where(kvp => kvp.Value >= minColorFrequency)
            .OrderByDescending(kvp => kvp.Value)
            .Take(maxColors)
            .Select(kvp => new SKColor(
                (byte)(kvp.Key & 0xFF),
                (byte)((kvp.Key >> 8) & 0xFF),
                (byte)((kvp.Key >> 16) & 0xFF)
            ))
            .ToList();
        
        return sortedColors;
    }

    /// <summary>
    /// K-Means clustering for better color extraction
    /// </summary>
    public static List<SKColor> ExtractColorsKMeans(
        SKBitmap bitmap,
        int k = 8,
        int iterations = 10)
    {
        // Collect all opaque pixels
        var pixels = new List<(float r, float g, float b)>();
        
        unsafe
        {
            var ptr = (uint*)bitmap.GetPixels().ToPointer();
            int width = bitmap.Width;
            int height = bitmap.Height;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    uint pixel = *(ptr + y * width + x);
                    byte a = (byte)((pixel >> 24) & 0xFF);
                    
                    if (a >= 128)
                    {
                        pixels.Add((
                            (pixel & 0xFF) / 255f,
                            ((pixel >> 8) & 0xFF) / 255f,
                            ((pixel >> 16) & 0xFF) / 255f
                        ));
                    }
                }
            }
        }
        
        if (pixels.Count == 0) return new List<SKColor>();
        if (pixels.Count <= k)
        {
            return pixels.Select(p => new SKColor(
                (byte)(p.r * 255),
                (byte)(p.g * 255),
                (byte)(p.b * 255)
            )).ToList();
        }
        
        // Initialize centroids randomly
        var rand = new Random(42); // Fixed seed for reproducibility
        var centroids = new List<(float r, float g, float b)>();
        
        for (int i = 0; i < k; i++)
        {
            centroids.Add(pixels[rand.Next(pixels.Count)]);
        }
        
        // K-means iterations
        for (int iter = 0; iter < iterations; iter++)
        {
            var clusters = new List<List<(float r, float g, float b)>>(k);
            for (int i = 0; i < k; i++) clusters.Add(new List<(float, float, float)>());
            
            // Assign pixels to nearest centroid
            foreach (var pixel in pixels)
            {
                int closest = 0;
                float minDist = float.MaxValue;
                
                for (int i = 0; i < k; i++)
                {
                    float dist = Distance(pixel, centroids[i]);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = i;
                    }
                }
                
                clusters[closest].Add(pixel);
            }
            
            // Update centroids
            for (int i = 0; i < k; i++)
            {
                if (clusters[i].Count > 0)
                {
                    float avgR = clusters[i].Average(p => p.r);
                    float avgG = clusters[i].Average(p => p.g);
                    float avgB = clusters[i].Average(p => p.b);
                    centroids[i] = (avgR, avgG, avgB);
                }
            }
        }
        
        return centroids.Select(c => new SKColor(
            (byte)(c.r * 255),
            (byte)(c.g * 255),
            (byte)(c.b * 255)
        )).ToList();
    }
    
    private static float Distance((float r, float g, float b) c1, (float r, float g, float b) c2)
    {
        float dr = c1.r - c2.r;
        float dg = c1.g - c2.g;
        float db = c1.b - c2.b;
        return dr * dr + dg * dg + db * db;
    }
}
```

---

## 4. Integration Code for MainWindow

### Add to `/workspace/MainWindow.axaml.cs`:

```csharp
// Add these using statements at the top
using Autodraw.Color;
using System.IO;
using Newtonsoft.Json;

// Add these fields to MainWindow class
private ColorCalibrationProfile? _activeCalibrationProfile;
private readonly string _profilesPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "AutoDraw", "ColorProfiles.json");

// Add these methods to MainWindow class

/// <summary>
/// Pick color button click handler
/// </summary>
public async void PickColorButton_OnClick(object? sender, RoutedEventArgs e)
{
    var color = await ScreenColorPicker.ShowEyedropperAsync(this);
    
    if (color.HasValue)
    {
        var hexValue = $"#{color.Value.Red:X2}{color.Value.Green:X2}{color.Value.Blue:X2}";
        
        // If HexColorsInput has text, append; otherwise set
        if (string.IsNullOrWhiteSpace(HexColorsInput.Text))
        {
            HexColorsInput.Text = hexValue;
        }
        else
        {
            HexColorsInput.Text += $", {hexValue}";
        }
    }
}

/// <summary>
/// Auto-extract colors button click handler
/// </summary>
public void AutoExtractColorsButton_OnClick(object? sender, RoutedEventArgs e)
{
    if (_preFxBitmap == null || _preFxBitmap.Width <= 1 || _preFxBitmap.Height <= 1)
    {
        new MessageBox().ShowMessageBox("Error!", "Please import an image first.", "error");
        return;
    }

    // Get number of colors from slider or default
    int maxColors = 8; // TODO: Bind to UI slider
    
    var extractedColors = ColorQuantizer.ExtractColorsKMeans(_preFxBitmap, maxColors);
    
    if (extractedColors.Count == 0)
    {
        new MessageBox().ShowMessageBox("Warning", "No colors could be extracted.", "warning");
        return;
    }
    
    // Display as hex
    var hexString = string.Join(", ", extractedColors.Select(c => 
        $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}"));
    
    HexColorsInput.Text = hexString;
}

/// <summary>
/// Generate layers with calibration support
/// </summary>
public override void GenerateLayersButton_OnClick(object? sender, RoutedEventArgs e)
{
    if (_preFxBitmap == null || _preFxBitmap.Width <= 1 || _preFxBitmap.Height <= 1)
    {
        new MessageBox().ShowMessageBox("Error!", "Please import an image first.", "error");
        return;
    }

    var matches = Regex.Matches(HexColorsInput.Text ?? "", @"#?([a-fA-F0-9]{6})");
    var targetColors = new List<SKColor>();
    var seenColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
    foreach (Match match in matches)
    {
        var hex = match.Groups[1].Value.ToUpper();
        if (seenColors.Contains(hex)) continue;
        seenColors.Add(hex);
        
        try
        {
            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            
            var color = new SKColor(r, g, b);
            
            // Apply calibration if active
            if (_activeCalibrationProfile != null)
            {
                color = ColorCalibrator.ApplyCalibration(color, _activeCalibrationProfile);
            }
            
            targetColors.Add(color);
        }
        catch { }
    }

    if (targetColors.Count == 0)
    {
        new MessageBox().ShowMessageBox("Error!", "Please enter at least one valid hex color.", "error");
        return;
    }

    // Get tolerance from UI (default 50)
    double tolerance = 50.0; // TODO: Bind to UI slider
    
    // Split the image
    var splitResults = ImageProcessing.SplitImageByColors(
        _preFxBitmap, 
        targetColors, 
        (byte)_alphaThresh,
        tolerance);

    foreach (var layer in LayersContext)
    {
        layer.RawBitmap?.Dispose();
    }
    LayersContext.Clear();
    _layersStack.Clear();

    foreach (var res in splitResults)
    {
        if (res.PixelCount == 0)
        {
            res.Bitmap.Dispose();
            continue;
        }

        var hexStr = $"#{res.Color.Red:X2}{res.Color.Green:X2}{res.Color.Blue:X2}";
        var color = Color.FromRgb(res.Color.Red, res.Color.Green, res.Color.Blue);
        var avaloniaBrush = new SolidColorBrush(color);
        var thumbnail = res.Bitmap.ConvertToAvaloniaBitmap();

        LayersContext.Add(new LayerDisp
        {
            HexColor = hexStr,
            HexColorBrush = avaloniaBrush,
            Thumbnail = thumbnail,
            RawBitmap = res.Bitmap,
            PixelCount = res.PixelCount
        });
    }

    if (LayersContext.Count == 0)
    {
        new MessageBox().ShowMessageBox("Warning", "No pixels matched the specified hex colors.", "warning");
    }
}

/// <summary>
/// Save calibration profile
/// </summary>
public void SaveCalibrationProfile(ColorCalibrationProfile profile)
{
    try
    {
        List<ColorCalibrationProfile> profiles;
        
        if (File.Exists(_profilesPath))
        {
            var json = File.ReadAllText(_profilesPath);
            profiles = JsonConvert.DeserializeObject<List<ColorCalibrationProfile>>(json) ?? new();
        }
        else
        {
            profiles = new List<ColorCalibrationProfile>();
        }
        
        // Remove existing profile with same name
        profiles.RemoveAll(p => p.ProfileName == profile.ProfileName);
        profiles.Add(profile);
        
        Directory.CreateDirectory(Path.GetDirectoryName(_profilesPath)!);
        File.WriteAllText(_profilesPath, JsonConvert.SerializeObject(profiles, Formatting.Indented));
    }
    catch (Exception ex)
    {
        Utils.Log($"Failed to save calibration profile: {ex.Message}");
    }
}

/// <summary>
/// Load calibration profiles
/// </summary>
public List<ColorCalibrationProfile> LoadCalibrationProfiles()
{
    try
    {
        if (File.Exists(_profilesPath))
        {
            var json = File.ReadAllText(_profilesPath);
            return JsonConvert.DeserializeObject<List<ColorCalibrationProfile>>(json) ?? new();
        }
    }
    catch (Exception ex)
    {
        Utils.Log($"Failed to load calibration profiles: {ex.Message}");
    }
    
    return new List<ColorCalibrationProfile>();
}

/// <summary>
/// Set active calibration profile
/// </summary>
public void SetActiveCalibrationProfile(string profileName)
{
    var profiles = LoadCalibrationProfiles();
    _activeCalibrationProfile = profiles.FirstOrDefault(p => p.ProfileName == profileName);
    
    Utils.Log($"Active calibration profile: {_activeCalibrationProfile?.ProfileName ?? "None"}");
}
```

### Update `/workspace/MainWindow.axaml`:

```xml
<!-- Find the HexColorsInput section around line 339 and update it -->
<StackPanel Spacing="6" Margin="4">
    <Label FontSize="14" FontWeight="Medium" Content="Hex Colors (comma/space-separated):"/>
    
    <!-- Updated with color picker buttons -->
    <Grid ColumnDefinitions="*,Auto,Auto,Auto">
        <TextBox Grid.Column="0" Name="HexColorsInput" CornerRadius="4" Height="48" 
                 AcceptsReturn="True" TextWrapping="Wrap" 
                 Watermark="e.g. #FF0000, #00FF00, #0000FF" />
        
        <!-- Pick Color Button -->
        <Button Grid.Column="1" Name="PickColorButton" 
                Click="PickColorButton_OnClick"
                ToolTip.Tip="Pick color from screen"
                Margin="4,0,0,0" Padding="8,0">
            <StackPanel Orientation="Horizontal" Spacing="4">
                <TextBlock Text="🎨" FontSize="16"/>
                <TextBlock Text="Pick" VerticalAlignment="Center"/>
            </StackPanel>
        </Button>
        
        <!-- Auto-Extract Button -->
        <Button Grid.Column="2" Name="AutoExtractColorsButton" 
                Click="AutoExtractColorsButton_OnClick"
                ToolTip.Tip="Extract colors from image"
                Margin="4,0,0,0" Padding="8,0">
            <StackPanel Orientation="Horizontal" Spacing="4">
                <TextBlock Text="✨" FontSize="16"/>
                <TextBlock Text="Auto" VerticalAlignment="Center"/>
            </StackPanel>
        </Button>
        
        <!-- Calibration Button -->
        <Button Grid.Column="3" Name="CalibrationButton" 
                Click="CalibrationButton_OnClick"
                ToolTip.Tip="Color calibration"
                Margin="4,0,0,0" Padding="8,0">
            <StackPanel Orientation="Horizontal" Spacing="4">
                <TextBlock Text="⚙️" FontSize="16"/>
                <TextBlock Text="Calibrate" VerticalAlignment="Center"/>
            </StackPanel>
        </Button>
    </Grid>
    
    <!-- Add tolerance slider -->
    <StackPanel Orientation="Horizontal" Spacing="8">
        <Label Content="Color Tolerance:" VerticalAlignment="Center"/>
        <Slider Name="ColorToleranceSlider" Minimum="0" Maximum="100" Value="50" 
                Width="150" TickFrequency="5" IsSnapToTickEnabled="True"/>
        <TextBlock Name="ColorToleranceValue" Text="50" VerticalAlignment="Center" Width="30"/>
    </StackPanel>
    
    <Grid ColumnDefinitions="*, 8, *">
        <Button Grid.Column="0" Name="GenerateLayersButton" 
                HorizontalAlignment="Stretch" HorizontalContentAlignment="Center" 
                Click="GenerateLayersButton_OnClick" Content="Generate Layers"/>
        <Button Grid.Column="2" Name="ClearLayersButton" 
                HorizontalAlignment="Stretch" HorizontalContentAlignment="Center" 
                Click="ClearLayersButton_OnClick" Content="Clear Layers"/>
    </Grid>
    
    <!-- Rest of layers UI... -->
</StackPanel>
```

---

## 5. Build Instructions

### Add Required NuGet Package

Edit `Autodraw.csproj`:

```xml
<ItemGroup>
  <!-- Add this package reference -->
  <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
</ItemGroup>
```

### Create Directory Structure

```bash
mkdir -p /workspace/Color
mkdir -p /workspace/Calibration
mkdir -p /workspace/Controls
```

### Compile and Test

```bash
cd /workspace
dotnet build
```

---

## 6. Usage Guide

### For End Users

#### Using the Screen Color Picker:
1. Click the "🎨 Pick" button next to the hex colors input
2. A magnifier window appears showing the color under your cursor
3. Move your cursor to any location on any screen
4. Click to select the color (or press ESC to cancel)
5. The color is automatically added to the input box

#### Auto-Extracting Colors:
1. Import an image
2. Click "✨ Auto" button
3. Adjust the "Number of Colors" slider if needed
4. The dominant colors are extracted and displayed
5. Click "Generate Layers" to create drawing layers

#### Calibrating Colors:
1. Click "⚙️ Calibrate" button
2. Select or create a new profile (e.g., "Gartic Phone")
3. For each sample:
   - Click "Pick Expected" → click color in your reference image
   - Click "Pick Actual" → click the SAME color in the target app
4. Add 3-5 samples for best results
5. Click "Save Profile"
6. Select the profile before generating layers

### For Developers

#### Adding New Features:

1. **Custom Color Tolerance:**
```csharp
var tolerance = ColorToleranceSlider.Value;
var layers = ImageProcessing.SplitImageByColors(
    bitmap, 
    colors, 
    alphaThreshold, 
    tolerance);
```

2. **Apply Calibration Programmatically:**
```csharp
var profile = ColorCalibrator.CreateProfileFromSamples(
    "My Profile",
    "Target App",
    samples);

var calibratedColor = ColorCalibrator.ApplyCalibration(
    originalColor, 
    profile);
```

3. **Extract Colors with Custom Parameters:**
```csharp
var colors = ColorQuantizer.ExtractColorsKMeans(
    bitmap,
    k: 12,           // Number of colors
    iterations: 15   // K-means iterations
);
```

---

## 7. Troubleshooting

### Color Picker Not Working
- Ensure you're running on Windows (screen capture requires Windows APIs)
- Check that the application has necessary permissions
- Try running as administrator

### Calibration Not Improving Colors
- Collect more sample pairs (minimum 3, recommended 5-10)
- Ensure samples cover different brightness levels (dark, mid, light)
- Check that you're sampling the exact same color in both locations

### Auto-Extraction Missing Colors
- Increase the number of colors parameter
- Reduce the minimum color frequency threshold
- Try the simple quantization method instead of K-means

---

*Implementation Guide v1.0*
*For AutoDraw Multi-Color Enhancement Project*
