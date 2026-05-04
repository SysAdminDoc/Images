# OCR Feature Implementation Progress

## Status: Foundation Complete, UI Integration Remaining

**Date:** 2026-05-04  
**Target:** Images v0.2.0

---

## ✅ Completed Steps

### 1. Project Configuration
**File:** `src/Images/Images.csproj`

- ✅ Updated TFM from `net9.0-windows` to `net9.0-windows10.0.19041.0` (enables WinRT APIs)
- ✅ Added `Microsoft.Windows.SDK.Contracts` v10.0.22621.38 NuGet package
- **Result:** Project can now reference Windows.Media.Ocr namespace

### 2. OCR Service Layer
**File:** `src/Images/Services/OcrService.cs` (NEW)

- ✅ Created `OcrService` class with three public methods:
  - `ExtractTextAsync(Stream)` — extracts text from image stream, returns `OcrResult?`
  - `GetAvailableLanguages()` — queries Windows OCR language packs
  - `IsAvailable()` — checks if at least one OCR language installed
- ✅ Handles pixel format conversion (OCR requires Bgra8 or Gray8)
- ✅ Caches `OcrEngine` instance for performance
- ✅ Comprehensive error handling with logging
- ✅ Uses `InMemoryRandomAccessStream` for WinRT interop

---

## 🚧 Remaining Implementation Steps

### 3. ViewModel Integration
**File:** `src/Images/ViewModels/MainViewModel.cs`

**Add at line 30 (with other services):**
```csharp
private readonly OcrService _ocr = new();
```

**Add at line 96 (with other commands):**
```csharp
ExtractTextCommand = new RelayCommand(async () => await ExtractTextAsync(), () => HasImage);
```

**Add at line 890 (with other ICommand properties):**
```csharp
public ICommand ExtractTextCommand { get; }
```

**Add new properties (after line 27):**
```csharp
private bool _isOcrMode;
public bool IsOcrMode
{
    get => _isOcrMode;
    set
    {
        if (Set(ref _isOcrMode, value))
        {
            OnPropertyChanged(nameof(OcrModeTooltip));
            if (!value)
            {
                OcrOverlayLines = null;
            }
        }
    }
}

public string OcrModeTooltip => IsOcrMode ? "Hide text (T)" : "Extract text (T)";

private ObservableCollection<OcrTextLine>? _ocrOverlayLines;
public ObservableCollection<OcrTextLine>? OcrOverlayLines
{
    get => _ocrOverlayLines;
    set => Set(ref _ocrOverlayLines, value);
}
```

**Add helper class (end of file, before closing namespace):**
```csharp
/// <summary>
/// Represents one line of OCR text with bounding box for overlay rendering.
/// </summary>
public class OcrTextLine
{
    public string Text { get; set; } = string.Empty;
    public Windows.Foundation.Rect BoundingBox { get; set; }
    public bool IsSelected { get; set; }
}
```

**Add method (near other commands around line 1300):**
```csharp
private async Task ExtractTextAsync()
{
    if (CurrentPath == null || !HasImage)
    {
        Toast("No image loaded");
        return;
    }

    // Toggle off if already in OCR mode
    if (IsOcrMode)
    {
        IsOcrMode = false;
        return;
    }

    Toast("Extracting text...");

    try
    {
        using var fileStream = File.OpenRead(CurrentPath);
        var result = await _ocr.ExtractTextAsync(fileStream);

        if (result == null)
        {
            Toast("OCR unavailable — no language packs installed");
            IsOcrMode = false;
            return;
        }

        if (result.Lines.Count == 0)
        {
            Toast("No text found");
            IsOcrMode = false;
            return;
        }

        // Convert OcrResult to overlay-renderable lines
        var lines = new ObservableCollection<OcrTextLine>();
        foreach (var line in result.Lines)
        {
            lines.Add(new OcrTextLine
            {
                Text = line.Text,
                BoundingBox = new Windows.Foundation.Rect(
                    line.Words[0].BoundingRect.X,
                    line.Words[0].BoundingRect.Y,
                    line.Words[^1].BoundingRect.Right - line.Words[0].BoundingRect.X,
                    line.Words[0].BoundingRect.Height
                )
            });
        }

        OcrOverlayLines = lines;
        IsOcrMode = true;
        Toast($"{lines.Count} text region{(lines.Count == 1 ? "" : "s")} found");
    }
    catch (Exception ex)
    {
        Log.Error($"OCR extraction failed: {ex.Message}");
        Toast("Text extraction failed");
        IsOcrMode = false;
    }
}
```

---

### 4. Keyboard Shortcut Handling
**File:** `src/Images/MainWindow.xaml.cs`

**Add to `Window_KeyDown` method (find existing Key.R, Key.I handlers and add after):**
```csharp
case Key.T when !isTextBoxActive:
    ViewModel.ExtractTextCommand.Execute(null);
    e.Handled = true;
    break;
```

---

### 5. Toolbar Button
**File:** `src/Images/MainWindow.xaml`

**Add after line 1037 (after ToggleMetadataHudCommand button):**
```xml
<Button Command="{Binding ExtractTextCommand}"
        Content="&#xF03B;"
        ToolTip="{Binding OcrModeTooltip}"
        Margin="4,0,0,0"
        Visibility="{Binding HasImage, Converter={StaticResource BoolToVis}}"
        AutomationProperties.Name="Extract text">
    <Button.Style>
        <Style TargetType="Button" BasedOn="{StaticResource ToolbarButton}">
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsOcrMode}" Value="True">
                    <Setter Property="Background" Value="#1F89B4FA" />
                    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}" />
                    <Setter Property="Foreground" Value="{StaticResource AccentBrush}" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

**Icon glyph:** `&#xF03B;` is Segoe MDL2 "TextDocument" (looks like a document with lines)  
**Alternative glyphs:** `&#xE8F2;` (TestBeaker), `&#xF146;` (Document Search), `&#xE943;` (TextBox)

---

### 6. OCR Overlay Control
**File:** `src/Images/Controls/OcrOverlay.xaml` (NEW)

```xml
<UserControl x:Class="Images.Controls.OcrOverlay"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Images.ViewModels"
             IsHitTestVisible="True"
             Background="Transparent">
    <ItemsControl ItemsSource="{Binding RelativeSource={RelativeSource AncestorType=Window}, Path=DataContext.OcrOverlayLines}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas />
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate DataType="{x:Type vm:OcrTextLine}">
                <Border Background="#4D89B4FA"
                        BorderBrush="#89B4FA"
                        BorderThickness="1"
                        CornerRadius="2"
                        Canvas.Left="{Binding BoundingBox.X}"
                        Canvas.Top="{Binding BoundingBox.Y}"
                        Width="{Binding BoundingBox.Width}"
                        Height="{Binding BoundingBox.Height}"
                        Cursor="Hand"
                        ToolTip="{Binding Text}"
                        MouseDown="TextRegion_MouseDown">
                    <Border.Style>
                        <Style TargetType="Border">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsSelected}" Value="True">
                                    <Setter Property="Background" Value="#9989B4FA" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </Border.Style>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</UserControl>
```

**File:** `src/Images/Controls/OcrOverlay.xaml.cs` (NEW)

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Images.ViewModels;

namespace Images.Controls;

public partial class OcrOverlay : UserControl
{
    public OcrOverlay()
    {
        InitializeComponent();
    }

    private void TextRegion_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not OcrTextLine line) return;

        // Copy text to clipboard
        try
        {
            Clipboard.SetText(line.Text);
            
            // Show visual feedback (could expand to show floating "Copy" button)
            line.IsSelected = true;
            
            // Toast notification via ViewModel
            if (Window.GetWindow(this)?.DataContext is MainViewModel vm)
            {
                vm.Toast("Text copied to clipboard");
            }
        }
        catch
        {
            // Silent fail
        }
    }
}
```

---

### 7. Integrate Overlay into MainWindow
**File:** `src/Images/MainWindow.xaml`

**Add namespace declaration (line 5, with other xmlns):**
```xml
xmlns:ctl="clr-namespace:Images.Controls"
```

**Add overlay layer inside Viewport Grid (find line 42 `<Grid x:Name="Viewport"`, add before closing `</Grid>`):**
```xml
<!-- OCR text overlay — semi-transparent boxes over detected text -->
<ctl:OcrOverlay Visibility="{Binding IsOcrMode, Converter={StaticResource BoolToVis}}"
                Panel.ZIndex="100" />
```

---

### 8. Context Menu Integration (Optional)
**File:** `src/Images/MainWindow.xaml`

**Add menu item after "Strip GPS location" (around line 140):**
```xml
<MenuItem Header="Extract text" Command="{Binding ExtractTextCommand}" InputGestureText="T">
    <MenuItem.Icon>
        <TextBlock Style="{StaticResource MenuItemIcon}" Text="&#xF03B;" />
    </MenuItem.Icon>
</MenuItem>
```

---

## 🧪 Testing Checklist

**Before marking complete:**
- [ ] Build succeeds (`dotnet build -c Release`)
- [ ] App launches without errors
- [ ] "Extract Text" button appears in toolbar when image loaded
- [ ] Button tooltip shows "Extract text (T)"
- [ ] Pressing `T` key triggers OCR extraction
- [ ] Toast shows "Extracting text..." during processing
- [ ] Toast shows "X text regions found" on success
- [ ] Semi-transparent blue overlays appear on detected text
- [ ] Clicking text region copies to clipboard
- [ ] Toast shows "Text copied to clipboard"
- [ ] Pressing `T` again hides overlays
- [ ] Button tooltip updates to "Hide text (T)" when active
- [ ] Button shows blue accent state when OCR mode active
- [ ] Test on various image types: screenshots, photos, receipts, memes
- [ ] Test with no text → toast "No text found"
- [ ] Test multilingual (French/Spanish) if language packs installed
- [ ] Verify overlay scaling when zooming image
- [ ] Verify overlay position when panning image
- [ ] No crashes when switching images while in OCR mode
- [ ] No memory leaks after repeated extract/hide cycles

---

## 📝 Documentation Updates

**After implementation complete:**

### README.md
Add new feature section after "Photo metadata at a glance":
```markdown
- **Text extraction** — Press `T` or click "Extract Text" to recognize text in the image using Windows OCR. Semi-transparent overlays highlight detected text regions. Click any region to copy its text to the clipboard. Works offline with Windows language packs (English guaranteed, additional languages via Settings → Language).
```

### CHANGELOG.md
Add new v0.2.0 entry:
```markdown
## v0.2.0 — 2026-XX-XX

OCR text extraction, feature parity with Windows Photos.

### Features

- **Text extraction (T key)** — Extract text from images using Windows OCR (local, privacy-first). Press `T` or click "Extract Text" in the toolbar. Semi-transparent blue overlays highlight detected text regions. Click any region to copy its text to the clipboard. English is supported by default; additional languages require Windows language packs (Settings → Time & language → Add language → enable "Handwriting" feature). Powered by Windows.Media.Ocr — the same API used by Windows Photos.
```

### ROADMAP.md
Mark Item XX as complete:
```markdown
- [x] **OCR text extraction** — Windows.Media.Ocr integration for local text recognition
```

### Trust docs
**File:** `docs/privacy-policy.md`
Add after "Metadata display" section:
```markdown
## OCR Text Extraction

When you use the "Extract Text" feature (T key), Images uses Windows' built-in OCR API (`Windows.Media.Ocr`) to recognize text in the currently displayed image. All OCR processing happens **locally on your device** — the image is never sent to any server or cloud service. Recognized text is displayed as overlay rectangles; clicking a region copies the text to your clipboard. No text data is saved or transmitted anywhere.

The OCR engine uses Windows language packs installed on your system (Settings → Time & language → Add language). English is included by default. Additional languages are optional and installed by you.
```

---

## 🎯 Known Limitations (Phase 1)

1. **Overlay scaling/panning:** Current implementation uses fixed Canvas coordinates. Need to bind overlay transform to `ZoomPanImage` transform for proper zoom/pan sync.
2. **Multi-word selection:** Phase 1 only copies single regions. Ctrl+click multi-select and Select All (Ctrl+A) deferred to Phase 2.
3. **Language selection UI:** No in-app language picker yet. User must install language packs via Windows Settings. Settings window integration deferred to Phase 2.
4. **Accuracy on complex layouts:** Windows.Media.Ocr struggles with tables, receipts, curved text. PaddleOCRSharp "Advanced mode" deferred to Phase 2.

---

## 🚀 Phase 2 Enhancements (Future)

**Deferred to v0.3.0+:**
- [ ] Settings window → OCR section with language dropdown
- [ ] "Install additional languages" deep-link button
- [ ] Multi-region selection (Ctrl+click, drag-select)
- [ ] "Copy all text" toolbar button (Ctrl+Shift+C)
- [ ] Export text to `.txt` file
- [ ] Advanced OCR mode with PaddleOCRSharp (higher accuracy, GPU acceleration)
- [ ] Overlay transform binding for proper zoom/pan sync
- [ ] Confidence score badges per text region
- [ ] Batch OCR (extract text from entire folder → CSV export)

---

## 📦 Estimated Deployment Impact

**Binary size:** +0 MB (Windows.Media.Ocr is already on every Windows 10/11 system)  
**Runtime deps:** None (NuGet package only adds compile-time references)  
**Compatibility:** Windows 10 build 19041+ required (same floor as current .NET 9 WPF)

---

**Next step:** Implement ViewModel changes and UI integration following the checklist above.
