# DPI audit — v0.1.6 snapshot

Audit produced by Factory iter-2 Phase 3 NEXT-12 (`docs/research/iter-2-scored.md`). Goal: document the state of hard-coded pixel literals in the XAML so the next UI-scale bug can be diagnosed in under five minutes instead of an archaeology dig.

## Headline

**Images is DPI-aware by design.** The `app.manifest` declares `permonitorv2` (`dpiAwareness` = `permonitorv2`, `dpiAware` = `true/pm`). WPF then scales every "WPF unit" (nominally 1/96 inch at 100% DPI) against the current monitor's scale factor automatically. Hard-coded values like `Width="360"` and `Padding="20,12"` are DPI-safe — they're device-*independent* units, not raw pixels.

## What was counted

| File | `Width` / `Height` / `Margin` / `Padding` literal-number attributes |
|---|---|
| `src/Images/MainWindow.xaml` | 74 |
| `src/Images/AboutWindow.xaml` | 17 |
| `src/Images/CrashDialog.xaml` | 15 |
| `src/Images/Themes/DarkTheme.xaml` | 4 |

Total: **110** literal-number size attributes across 4 XAML files.

## What's actually fragile

None of them. A "Width=40" attribute is 40 DIU which WPF multiplies by the active monitor's DPI factor. On a 125% display that's rendered at 50 physical pixels; on 150% it's 60 px. This is the correct WPF idiom.

Places that would be fragile if they existed (none do):
- `RenderTransform` with a literal `TranslateTransform(100, 100)` that isn't wrapped in a value-converter (safe because WPF treats the 100 as DIU not pixels).
- P/Invoke calls that pass pixel values to `SetWindowPos` or `MoveWindow` without multiplying by `Dpi.PixelsPerDip` (we don't make those calls).
- `Canvas.SetLeft` / `SetTop` with literal pixel math (in `PrintService.cs` we compute `(pageWidth - renderedW) / 2` where both terms come from `PrintDialog.PrintableAreaWidth`, which is already in DIU. Safe.)
- Raw `HwndSource.SizeToContent` interactions with Win32 windows measured in physical pixels (not used).

## Recommendation for v0.2.x
When the SkiaSharp canvas migration (V20-01) lands, an audit pass on the new draw-surface geometry is worth it — SkiaSharp's `SKCanvas` takes raw pixels, so every call-site needs to multiply by `VisualTreeHelper.GetDpi(this).PixelsPerDip` to get the right on-screen size. Track this as a V20-01 sub-task.

## Conclusion
**No changes required for v0.1.6.** The XAML is DIU-clean; the manifest is per-monitor-v2; the screenshot-capture guide (`screenshots.md`) already accounts for 125% DPI. Future fragility risk lives in code-behind that bypasses the WPF layout system — we have no such code today.
