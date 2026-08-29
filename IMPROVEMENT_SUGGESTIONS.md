# AutoDraw Improvement Suggestions

## Overview
This document provides detailed improvement suggestions for the AutoDraw application, focusing on:
1. **Multi-color Auto-Drawing**: Enhanced support for drawing images with multiple colors automatically
2. **Color Picker Calibration**: A system to calibrate and pick colors from any external program

---

## 1. Multi-Color Auto-Drawing Enhancements

### Current State
The application currently has a "Layers" tab that allows users to:
- Enter hex colors manually (comma/space-separated)
- Generate layers by splitting an image based on target colors
- Each layer represents one color from the input list
- Uses closest color matching algorithm (Euclidean distance in RGB space)

**Key Files:**
- `MainWindow.axaml.cs` - Lines 809-880: `GenerateLayersButton_OnClick`
- `ImageProcessing.cs` - Lines 453-527: `SplitImageByColors`
- `Draw.cs` - Contains drawing logic for single-color paths

### Suggested Improvements

#### 1.1 Automatic Color Extraction
**Problem:** Users must manually identify and enter hex colors.

**Solution:** Add automatic color palette extraction from the imported image.

**Implementation:**
```csharp
// New method in ImageProcessing.cs
public static List<SKColor> ExtractDominantColors(SKBitmap bitmap, int maxColors = 8)
{
    // Use quantization algorithm (e.g., Median Cut or K-Means)
    // Return top N dominant colors
}
```

**UI Addition:**
- Add "Auto-Extract Colors" button next to the HexColorsInput textbox
- Add slider for number of colors to extract (2-16)
- Display extracted colors as swatches before generating layers

#### 1.2 Color Tolerance/Threshold Setting
**Problem:** The current color matching uses exact closest match without tolerance control.

**Solution:** Add adjustable color tolerance threshold.

**Implementation:**
```csharp
// Modify SplitImageByColors signature
public static unsafe List<ColorLayerResult> SplitImageByColors(
    SKBitmap sourceBitmap, 
    List<SKColor> targetColors, 
    byte alphaThreshold,
    double colorTolerance = 50.0) // New parameter
```

**UI Addition:**
- Add slider "Color Tolerance" (0-100) in Layers tab
- Tooltip explaining: "Higher values group similar colors together"

#### 1.3 Layer Drawing Order Optimization
**Problem:** No control over which color layers are drawn first.

**Solution:** Allow users to reorder layers and choose drawing strategy.

**Implementation:**
- Add up/down arrows in layer list for reordering
- Add dropdown for drawing order strategies:
  - "Largest to Smallest" (default - current behavior)
  - "Smallest to Smallest" (for detail-first approach)
  - "Lightest to Darkest" (for painting-style)
  - "Darkest to Lightest"
  - "Custom" (user-defined order)

**UI Addition:**
```xml
<!-- In MainWindow.axaml, add to layer item template -->
<StackPanel Grid.Column=\"7\" Orientation=\"Vertical\" Spacing=\"2\">
    <Button Name=\"MoveLayerUp\" Content=\"↑\" Padding=\"2\"/>
    <Button Name=\"MoveLayerDown\" Content=\"↓\" Padding=\"2\"/>
</StackPanel>
```

#### 1.4 Inter-Layer Actions
**Problem:** No way to perform actions between color layers (e.g., change tool, press key).

**Solution:** Allow custom action sequences between layers.

**Implementation:**
```csharp
// Add to LayerDisp class
public List<InputAction> PreDrawActions { get; set; } = new();
public List<InputAction> PostDrawActions { get; set; } = new();
```

**UI Addition:**
- Double-click on layer opens "Layer Actions" dialog
- Add "Add Action Before" and "Add Action After" buttons
- Support for: key presses, delays, mouse clicks, tool switches

#### 1.5 Batch Layer Processing
**Problem:** All layers draw immediately without pause options.

**Solution:** Add configurable delays and pause points between layers.

**Implementation:**
```csharp
// In Settings.axaml.cs or Config
public static int InterLayerDelay = 500; // milliseconds
public static bool PauseBetweenLayers = false;
```

**UI Addition:**
- "Inter-Layer Delay" slider (0-5000ms)
- Checkbox "Pause between layers (require manual continue)"
- Progress indicator showing "Layer 3 of 8"

#### 1.6 Color Blending/Dithering Support
**Problem:** Hard color transitions may not look natural.

**Solution:** Optional dithering pattern for smoother gradients.

**Implementation:**
```csharp
// New method in ImageProcessing.cs
public static SKBitmap ApplyDithering(
    SKBitmap source, 
    List<SKColor> palette, 
    string ditherType = "FloydSteinberg")
```

**UI Addition:**
- Dropdown "Dithering": None, Floyd-Steinberg, Ordered, Random
- Only available when "Advanced Mode" is enabled

---

## 2. Color Picker Calibration System

### Current State
The application has no built-in color picking or calibration capability. Users must:
1. Manually find hex colors in other programs
2. Type them into the HexColorsInput textbox
3. Hope the colors match what the target application expects

### Proposed Color Calibration System

#### 2.1 Screen Color Picker
**Feature:** Pick colors directly from anywhere on screen.

**Implementation:**
```csharp
// New class: ColorPicker.cs
public class ScreenColorPicker
{
    public static SKColor PickColor(int x, int y)
    {
        // Capture screen pixel at coordinates
        // Return SKColor
    }
    
    public static async Task<SKColor> InteractivePickAsync()
    {
        // Show magnifier overlay
        // Return color on click
    }
}
```

**Required NuGet Packages:**
```xml
<PackageReference Include="System.Drawing.Common" Version="8.0.0" />
```

**UI Addition:**
- Add "🎨 Pick Color" button next to HexColorsInput
- Click button → cursor changes to eyedropper
- Click anywhere on screen → color added to input box
- Support multi-pick mode (pick multiple colors in sequence)

#### 2.2 Color Calibration Wizard
**Feature:** Calibrate colors to match how the target application displays them.

**Why Needed:** Different applications may:
- Use different color spaces (sRGB, Adobe RGB, etc.)
- Apply gamma correction differently
- Have color profiles that shift values
- Compress colors in unexpected ways

**Implementation:**
```csharp
// New class: ColorCalibrator.cs
public class ColorCalibrationProfile
{
    public string ProfileName { get; set; }
    public string TargetApplication { get; set; }
    public Dictionary<SKColor, SKColor> ColorMapping { get; set; }
    public float GammaCorrection { get; set; } = 1.0f;
    public float SaturationShift { get; set; } = 0.0f;
    public float BrightnessShift { get; set; } = 0.0f;
}

public class ColorCalibrator
{
    public static ColorCalibrationProfile CreateCalibrationProfile(
        string appName,
        List<(SKColor expected, SKColor actual)> samplePairs)
    {
        // Calculate transformation matrix
        // Return profile
    }
    
    public static SKColor ApplyCalibration(
        SKColor originalColor, 
        ColorCalibrationProfile profile)
    {
        // Apply calibrated transformation
        // Return adjusted color
    }
}
```

**UI Addition - Calibration Wizard Dialog:**
```
Step 1: Select Target Application
  [Dropdown of saved profiles] [New Profile]

Step 2: Sample Collection
  "Click 'Pick Expected' then click the color in your reference image"
  "Click 'Pick Actual' then click the same color in the target application"
  
  Expected Color: [■ #FF5500] [Pick]
  Actual Color:   [■ #FF4A00] [Pick]
  
  [+ Add Sample] (collect 3-5 samples minimum)
  
Step 3: Review & Save
  Preview: Original → Calibrated
  [Save Profile] [Cancel]
```

#### 2.3 Application-Specific Profiles
**Feature:** Save and load color profiles for different applications.

**Implementation:**
```csharp
// Store in user config directory
// Format: JSON
{
  "profiles": [
    {
      "name": "Gartic Phone",
      "targetApp": "Chrome-GarticPhone",
      "gammaCorrection": 1.05,
      "colorSamples": [...]
    },
    {
      "name": "Sketchful.io",
      "targetApp": "Chrome-Sketchful",
      "gammaCorrection": 0.98,
      "colorSamples": [...]
    }
  ]
}
```

**UI Addition:**
- New "Profiles" tab in Settings
- Dropdown to select active profile
- "Test Calibration" button to preview adjustments

#### 2.4 Live Color Preview
**Feature:** Show how colors will appear after calibration before drawing.

**Implementation:**
```csharp
// In MainWindow.axaml.cs
private void UpdateColorPreview()
{
    // Show side-by-side comparison
    // Left: Original colors
    // Right: Calibrated colors
    // Highlight differences
}
```

**UI Addition:**
- Split preview panel in Layers tab
- Toggle switch "Show Calibrated Preview"
- Delta-E difference indicator for each color

#### 2.5 Hotkey-Activated Color Picker
**Feature:** Global hotkey to pick colors while in another application.

**Implementation:**
```csharp
// Using SharpHook (already in project)
private static KeyCode _colorPickHotkey = KeyCode.F9;

private async void OnGlobalKeyDown(object sender, KeyboardHookEventArgs e)
{
    if (e.Data.Keyboard.KeyCode == _colorPickHotkey && 
        ModifierKeys.Control == (e.Data.Keyboard.Modifiers & ModifierKeys.Control))
    {
        await PickColorAtCursorAsync();
    }
}
```

**UI Addition:**
- Settings → Hotkeys → "Color Picker"
- Default: Ctrl+F9
- Option: "Copy to clipboard automatically"

---

## 3. Additional Quality-of-Life Improvements

### 3.1 Color Palette Management
- **Saved Palettes**: Save favorite color combinations
- **Import Palette**: Support .aco (Adobe Color), .gpl (GIMP), .pal formats
- **Export Palette**: Share palettes with community
- **Palette Library**: Pre-built palettes (web safe, material design, etc.)

### 3.2 Smart Color Grouping
- Automatically group similar colors within tolerance
- Option to merge layers post-generation
- "Simplify Colors" feature to reduce color count

### 3.3 Visual Feedback During Drawing
- Highlight current layer being drawn
- Show progress per layer
- Estimated time remaining per layer
- Option to skip problematic layers

### 3.4 Undo/Redo for Layer Operations
- Track layer modifications
- Allow reverting accidental deletions
- History panel for layer operations

---

## 4. Technical Implementation Priority

### Phase 1 (High Priority - Quick Wins)
1. ✅ Screen color picker with eyedropper
2. ✅ Adjustable color tolerance slider
3. ✅ Layer reordering (up/down buttons)
4. ✅ Inter-layer delay setting

### Phase 2 (Medium Priority - Core Features)
5. ✅ Automatic color extraction from image
6. ✅ Color calibration wizard
7. ✅ Application-specific profiles
8. ✅ Layer action sequences (pre/post draw)

### Phase 3 (Lower Priority - Advanced)
9. ⏸️ Dithering algorithms
10. ⏸️ Palette import/export
11. ⏸️ Smart color grouping
12. ⏸️ Live calibrated preview

---

## 5. Code Structure Recommendations

### New Files to Create
```
/AutoDraw/
├── Color/
│   ├── ColorPicker.cs          # Screen color picking
│   ├── ColorCalibrator.cs      # Calibration logic
│   ├── ColorPalette.cs         # Palette management
│   └── ColorQuantizer.cs       # Color extraction
├── Calibration/
│   ├── CalibrationProfile.cs   # Profile data model
│   ├── CalibrationWizard.axaml # Wizard UI
│   └── CalibrationWizard.axaml.cs
└── Controls/
    ├── ColorSwatch.axaml       # Reusable color display
    ├── EyedropperButton.axaml  # Color picker button
    └── LayerItem.axaml         # Enhanced layer template
```

### Modified Files
- `ImageProcessing.cs` - Add color extraction, tolerance, calibration
- `MainWindow.axaml.cs` - Integrate new features
- `MainWindow.axaml` - Add UI controls
- `Settings.axaml.cs` - Add calibration settings
- `Draw.cs` - Support inter-layer actions

---

## 6. User Workflow Example

### Before (Current)
1. Open image in AutoDraw
2. Open image in Photoshop/color picker
3. Manually note hex codes
4. Type hex codes into AutoDraw
5. Generate layers
6. Draw (all colors, no customization)

### After (With Improvements)
1. Open image in AutoDraw
2. Click "Auto-Extract Colors" → 8 colors detected
3. Adjust tolerance to merge similar shades → 6 colors
4. Click "Pick Color" → sample from reference game screenshot
5. Run Calibration Wizard → create "Gartic Phone" profile
6. Reorder layers: background first, details last
7. Add "Press B" action before brush layers
8. Set 500ms delay between layers
9. Draw with calibrated colors, automatic tool switching

---

## 7. Testing Recommendations

### Unit Tests
- Color distance calculations
- Calibration matrix application
- Layer ordering algorithms
- Tolerance threshold behavior

### Integration Tests
- End-to-end layer generation
- Color picker accuracy across DPI settings
- Profile save/load functionality
- Multi-monitor color picking

### User Testing Scenarios
- Novice user: Auto-extract + default settings
- Power user: Manual calibration + custom actions
- Accessibility: High contrast mode compatibility

---

## Conclusion

These improvements would transform AutoDraw from a single-color tracing tool into a comprehensive multi-color drawing automation system. The color calibration feature alone would solve a major pain point for users drawing in applications with non-standard color rendering.

**Estimated Development Effort:**
- Phase 1: 2-3 weeks
- Phase 2: 4-6 weeks
- Phase 3: 3-4 weeks

**Impact:**
- ⭐⭐⭐⭐⭐ Usability improvement
- ⭐⭐⭐⭐ Competitive advantage
- ⭐⭐⭐⭐⭐ User satisfaction

---

*Document created for AutoDraw enhancement planning*
*Version: 1.0*
*Date: 2024*
