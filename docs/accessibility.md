# Accessibility: UIA tree reference

Images publishes a documented UIA tree so screen readers (Narrator, NVDA, JAWS) can announce image state, navigation, and editing actions.

This document describes what each screen reader surface exposes. It is a developer and accessibility-tester reference.

## Image canvas

The main image surface (`ZoomPanImage`) has a custom `ImageCanvasAutomationPeer` (`src/Images/Controls/ImageCanvasAutomationPeer.cs`).

| UIA property | Value |
|---|---|
| ControlType | `Image` |
| ClassName | `ZoomPanImage` |
| Name (loaded) | `"Image, {W} by {H} pixels"` (e.g. "Image, 2048 by 1365 pixels") |
| Name (empty) | `"Image (none loaded)"` |
| HelpText | `"Use arrow keys to navigate previous / next in folder. Mouse wheel zooms; drag pans; double-click fits. F1 shortcut help."` |
| IsContentElement | true |
| IsControlElement | true |

The Name updates dynamically when the source changes. WPF re-queries the peer on UIA events automatically.

The window title is bound to `WindowTitle` (format: `"{filename} -- Images"`), so Narrator announces the current file on window focus.

## Navigation

### Arrow overlays and position chip

| Element | AutomationProperties.Name | HelpText |
|---|---|---|
| Previous button | `"Previous image"` | `"Left arrow or Backspace"` |
| Next button | `"Next image"` | `"Right arrow or Space"` |
| Position chip | `"Folder position"` | -- |
| Page position chip | `"Page position"` | -- |
| Page scrubber slider | `"Page scrubber"` | `"Drag to jump to another page in the current document or archive."` |

The position chip and gallery workbench use `AutomationProperties.LiveSetting="Polite"` so Narrator announces changes without interrupting the current utterance.

### Filmstrip

The bottom filmstrip is a virtualizing ListBox:

- Container: `Name="Bottom folder filmstrip"`, `DirectionalNavigation=Cycle`
- Each thumbnail button: `Name="{FileName}"`, `HelpText="{PositionText}"`, `ItemStatus="{PositionText}"`

The side-panel folder preview (shown when the filmstrip is hidden) mirrors this pattern with `Name="Folder preview thumbnails"`.

The filmstrip toggle button is named `"Toggle filmstrip"`.

### Gallery

The gallery workbench is announced with `Name="Gallery workbench"`, `LiveSetting="Polite"`.

- Thumbnails ListBox: `Name="Gallery thumbnails"`, `HelpText="{GalleryStatusText}"`, `DirectionalNavigation=Contained`, `TabNavigation=Cycle`
- Each gallery item: `Name="{FileName}"`, `HelpText="{PositionText}"`, `ItemStatus="{PositionText}"`
- Sort controls group: `Name="Gallery sort controls"`
- Sort buttons: `"Sort gallery by name"`, `"Sort gallery by newest modified date"`, `"Sort gallery by type"`, `"Sort gallery by largest file size"`
- Filter buttons: `"Filter gallery to landscape images"`, `"Filter gallery to portrait images"`, `"Filter gallery to recently modified images"`, `"Filter gallery to large images"`, `"Filter gallery to exact duplicates"`
- Filter TextBox: `Name="Filter gallery"`
- Clear filter: `Name="Clear gallery filter"`
- Empty state: `Name="No gallery filter matches"`

### Archive book navigation

When viewing archives/multi-page documents:

- Container: `Name="Archive book controls"`, `LiveSetting="Polite"`
- Buttons: `"First book page"`, `"Previous book page"`, `"Next book page"`, `"Last book page"`
- Page-turn buttons on the viewport: `Name="{LeftBookPageTurnTooltip}"` / `Name="{RightBookPageTurnTooltip}"` (dynamic text)
- Toggle switches: `"Right-to-left book page turns"`, `"Clean old archive scans"`, `"Two-page archive spreads"`

## Rename

The rename section in the right panel:

| Element | AutomationProperties.Name | Notes |
|---|---|---|
| Stem TextBox | `"File name without extension"` | `AutomationId="StemEditor"`. `PreviewKeyDown` handler stops arrow propagation so caret moves within text. `SelectionChanged` raises `TextPatternOnTextSelectionChanged` so the Windows Magnifier follows the caret (A-04). |
| Extension chip/button | `Name="{ExtensionLockText}"` | Dynamic: locked vs unlocked state |
| Extension chip | `HelpText="{ExtensionLockHelpText}"` | Explains the lock/unlock behavior |
| Extension TextBox (unlocked) | `"File extension"` | Only visible when extension editing is unlocked |
| Recent renames list | `Name="Recent renames"` | `DirectionalNavigation=Cycle` (from A-03) |
| Undo rename button | `"Undo rename"` | Per-item in the recent renames list |

Rename status (Pending/Saved/Conflict/Error) is communicated visually via a colored dot; the `RenameStatusText` and `RenamePreview` text blocks next to it provide the same information as plain text for screen readers.

## Toolbar and side-panel actions

Key toolbar buttons expose `AutomationProperties.Name`:

- `"Open image"` (HelpText: "Choose an image file from disk. Shortcut: Ctrl+O.")
- `"Paste from clipboard"` (HelpText: clipboard paste behavior)
- `"Open settings"`, `"Open diagnostics"`, `"Previous image"`, `"Next image"`, `"Toggle filmstrip"`

Side-panel tool sections are grouped:

| Group Name | Buttons |
|---|---|
| `"Organization controls"` | "Open tag relationships", "Open import inbox" |
| `"Automation controls"` | "Open macro actions", "Open batch processor", "Open model manager", "Open semantic search" |
| `"Cleanup controls"` | "Open duplicate cleanup", "Open file health scan", "Open recovery center" |
| `"Compare controls"` | "Compare with next image", "Compare with chosen image", "Toggle compare layout", "Swap compare A and B", "Exit compare mode" |
| `"Pinned overlay controls"` | "Toggle pinned overlay mode", opacity slider, "Toggle click-through overlay", "Exit pinned overlay mode" |

## Editing overlays

Each editing overlay on the canvas has an `AutomationProperties.Name`:

- `"OCR text overlay"` -- individual OCR regions expose `Name="{Text}"` and `HelpText` describing copy behavior
- `"Pixel selection overlay"` -- with "Copy selected pixels", "Clear selected pixels", "Cancel pixel selection mode"
- `"Crop selection overlay"` -- aspect presets ("Free crop aspect", "Square crop aspect", etc.), "Apply crop to file", "Cancel crop"
- `"Local exposure brush overlay"` -- "Use dodge brush", "Use burn brush", radius/strength sliders
- `"Red-eye correction overlay"` -- radius/strength/threshold sliders, apply/clear/cancel
- `"Retouch brush overlay"` -- "Use clone stamp", "Use healing brush", radius/strength sliders
- Metadata HUD: `Name="Metadata HUD"`, `LiveSetting="Polite"`

The animation frame workbench (`Name="Animation frame workbench"`) exposes: "Play or pause animation", frame scrubber, first/prev/next/last frame buttons, playback speed slider, frame timeline, per-frame `Name="{FrameText}"`, "Copy selected animation frame", "Export selected animation frame".

The face-region review workbench exposes named controls for current-image and bounded-folder analysis, the cluster-grouped detected-region list, person-name editing, accept/reject decisions, and the reviewed XMP merge gate. Decision state is conveyed in text as well as color, the selected region has a thicker overlay, and status changes use a polite live region.

## Dialogs

All secondary windows use `AutomationProperties.Name` on interactive controls:

**Effects window:** Preset buttons ("Apply crisp preset", "Apply clean preset", "Apply focus preset"), named sliders ("Sharpen amount", "Noise reduction amount", "Vignette amount"), preview image, Reset/Close/Apply buttons.

**Adjustments window:** Named sliders for black point, white point, gamma, curve, hue, saturation, lightness. Preview image, Reset/Close/Apply buttons.

**Annotations window:** Text input, stroke width slider, font size slider, preview image.

**About window:** Diagnostics section with "Copy system info", "Copy codec report", "Open logs", "Open data folder", "Open thumbnail cache", "Clear thumbnails". Codec capability matrix, update check controls, GitHub/crash-log links.

**Settings window:** Each toggle is named (e.g. "Remember window position", "Filmstrip", "Metadata HUD", "Confirm recycle bin", "Reduce motion", "High contrast", "Archive RTL", "Update check"). OCR language list, "Open app data", "Open logs" buttons.

**Batch processor:** Source list, preset selector, dimension limits, preview items list, run log.

**Duplicate cleanup:** Folder lists with add/remove/clear, similarity threshold, findings list with compare/mark/quarantine/recycle actions.

**Confirm dialog:** "Don't ask again" checkbox, Cancel and confirm buttons.

**Crash dialog:** "Copy crash details", "Open crash log folder", "Open GitHub issue", "Close crash dialog".

**Export preview:** Preset selector, extension, warnings list.

**Edit stack:** Reload, create virtual copy, export. Per-edit-copy entries with reveal/copy-summary. Toggle and operation list.

Automated smoke coverage (`SecondaryWindowXamlTests`) opens Settings, About/Diagnostics, Duplicate Cleanup, Semantic Search, Model Manager, and Import Inbox with app theme resources loaded. It verifies titles, required automation names, named focusable controls, and whitespace-only HelpText regressions when `RUN_SMOKE_TESTS=1`.

### Pseudo-locale overflow gate

`scripts/Test-LocalizationResources.ps1 -GeneratePseudoLocale` creates `src/Images/Localization/Strings.qps-ploc.resx` from the base resources. The default localization gate now validates base/locale parity, generated pseudo-locale placeholder preservation, and expanded text markers before release readiness can pass.

`SecondaryWindowsPseudoLocaleDoesNotClipCriticalText` sets `Strings.Culture` to `qps-ploc`, opens the secondary windows above, and checks critical named controls for no-wrap text that measures wider or taller than its rendered bounds. Run it with `RUN_SMOKE_TESTS=1` when validating WPF layout changes.

## Keyboard focus

All interactive controls use a shared `FocusVisual` style (dashed ring, ~7:1 contrast ratio on the Catppuccin base -- WCAG AA pass). Applied to `ChromeButton`, `ToolbarButton`, `NavArrowButton`, and standard controls.

The rename TextBox intercepts arrow keys via `PreviewKeyDown` so left/right move the caret instead of navigating images. `Escape` in the rename field reverts; `Enter` commits.

`DirectionalNavigation=Cycle` is set on Recent renames, filmstrip, gallery thumbnails, and folder preview lists.

Window-level `Escape` dismisses toasts, overlays, or closes peek-mode windows.

## WCAG 2.5.7 / 2.5.8 audit

Audited 2026-06-19 against WCAG 2.2 success criteria 2.5.7 (Dragging Movements) and 2.5.8 (Target Size Minimum, 24x24 DIU).

### 2.5.8 Target Size Minimum (24x24 DIU)

| Element | Size | Status |
|---|---|---|
| `ToolbarButton` (style) | 40 x 34 | Pass |
| `ChromeButton` (style) | MinHeight 34 | Pass |
| `NavArrowButton` (style) | 58 x 58 | Pass |
| Page nav buttons (page scrubber area) | 30 x 26 | Pass |
| Folder sort button | Padding 9,4 + MinHeight 26 | Pass |
| Crop overlay Apply button | MinWidth 78, MinHeight 32 | Pass |
| Selection overlay Copy / Clear buttons | MinWidth 72, MinHeight 32 | Pass |
| Channel isolation chip | Padding 8,2 + MinHeight 24 | Pass (fixed) |
| Slideshow indicator chip | Padding 8,2 + MinHeight 24 | Pass (fixed) |
| OCR text regions | Sized by OCR bounding box (dynamic) | N/A (content-driven) |

**Fixes applied:** Added `MinHeight="24"` to the channel isolation chip and slideshow indicator chip in `MainWindow.xaml`. Both are interactive Border elements (MouseLeftButtonUp/MouseLeftButtonDown handlers) that previously had only `Padding="8,2"`, which at FontSize 10.5 rendered below the 24 DIU minimum.

### 2.5.7 Dragging Movements

| Drag operation | Non-drag alternative | Status |
|---|---|---|
| Image pan (ZoomPanImage drag) | Shift+Wheel (horizontal), +/- zoom, 0/1 fit/1:1, Ctrl+F zoom cycle, double-click fit/1:1 | Partial -- no vertical-pan keyboard alternative |
| Crop rectangle (CropOverlay drag) | Enter applies crop; no keyboard positioning/resizing | Fail -- drag-only positioning |
| Pixel selection (SelectionOverlay drag) | Ctrl+C copies; Escape cancels; no keyboard positioning | Fail -- drag-only positioning |
| Local exposure brush (drag painting) | Enter applies; Escape cancels; radius/strength via sliders | Fail -- painting is drag-only |
| Retouch brush (drag painting) | Enter applies; Escape cancels; radius/strength via sliders | Fail -- painting is drag-only |
| Red-eye correction (click to place) | Enter applies; Escape cancels; radius/strength/threshold via sliders | Fail -- placement is click-only |
| Page scrubber slider | Arrow keys (native WPF Slider), PageUp/PageDown, direct page buttons | Pass |
| Filmstrip scroll | Mouse wheel, keyboard arrow navigation in ListBox | Pass |

**Known drag-only limitations (not addressed in this audit):**
- Crop/selection rectangle positioning requires mouse drag. A future enhancement could add arrow-key nudging when the crop/selection overlay is active.
- Brush-based editing (local exposure, retouch, red-eye) is inherently spatial and mouse/pen-driven. Keyboard painting is not practical for these tools.
- Vertical image panning has no keyboard-only path. Shift+Wheel covers horizontal pan; vertical pan currently requires mouse drag or zoom-to-fit shortcuts.

## Known limitations

- **No live-region on rename status**: The rename status dot color changes (Pending/Saved/Conflict/Error) are not raised as UIA `LiveRegionChanged` events. Screen reader users must Tab to the status area to hear the current rename state.
- **Pixel inspector values**: Color values under the cursor (HEX/RGB/HSV) update on mouse move but do not raise live-region events; they must be explicitly focused.
- **No NVDA/JAWS test matrix**: Automated screen reader testing is not yet part of the release checklist (roadmap item A-06). Testing has been done manually with Windows Narrator.
- **Gallery item count**: The gallery status text is exposed via `HelpText` on the ListBox but does not auto-announce item count changes.
- **Filmstrip thumbnail labels**: Filmstrip items expose the filename but not the image dimensions or file size.
