# Research - Images
Date: 2026-07-12 (pass 3) - replaces all prior research.

## Executive Summary
Verified: Images is a mature, Windows-only, local-first WPF/.NET 10 image viewer/workbench (v0.2.25, ~1007 tests). The competitor/format/CVE/dependency landscape was exhausted in this day's passes 1-2 and the high-value feature items were shipped (color management, loupe, live pixel readout, zoom-lock, transparency checkerboard, zoom-to-selection, session restore, stop-at-ends, metadata-preserving Save-a-copy, Magick.NET 14.15). This pass audits the ~10 feature commits shipped today (`0684ff6..7224130`), which are new and independently un-reviewed — that un-audited code is the only genuinely-unmined surface left. The audit found concrete correctness, reliability, security-adjacent, and accessibility defects in the new code (grounded in file:line, several re-verified against source). Highest-value direction: harden and finish the features just shipped before adding more. Top opportunities in priority order: (1) fix the loupe/zoom-to-selection mouse-capture and gesture-conflict bugs; (2) validate the session-restore path (bypasses the argv device-path guard); (3) stop the loupe and HUD pixel-readout from sampling the 1×1 tile placeholder on gigapixel images; (4) make the `ColorManagedDisplay` toggle honor already-preloaded neighbors; (5) give the loupe and zoom-to-selection command-palette/keyboard/cheatsheet discoverability to match the app's otherwise a11y-complete surface.

## Product Map
- Core workflows: open files/folders/sessions/archives/books; navigate (wrap / stop-at-ends / sibling-folder); rename; compare; inspect metadata/provenance + live pixel readout + loupe; export/Save-a-copy; recover destructive actions.
- User personas: Windows power users replacing Photos/ImageGlass/FastStone; photographers/archivists in local folders; technical users valuing portable artifacts, checksums, provenance, and visible network behavior.
- Platforms and distribution: Windows 10/11 x64 WPF, `net10.0-windows10.0.22621.0`, MIT; Inno installer + portable ZIP; scripted local release gates; GitHub Releases; framework-dependent.
- Key integrations and data flows: WIC first, Magick.NET fallback (opt-in embedded-ICC→sRGB); SharpCompress read-only archives; SQLite settings/catalog/cache; XMP sidecars; optional Ghostscript/jpegtran/ExifTool/c2patool/OCR/ONNX-DirectML; opt-in GitHub release checks logged by `NetworkEgressService`.

## Competitive Landscape
Unchanged from passes 1-2 (same day); the OSS/commercial field was fully mined and the feature gaps are either shipped today or parked in `Roadmap_Blocked.md`. The only competitor-relevant note for this pass: ImageGlass, PicView, and JPEGView all surface their new gestures through visible hints/shortcut lists — Images' newly-shipped loupe (middle-button) and zoom-to-selection (Ctrl+Shift+drag) currently have neither, a regression against Images' own established discoverability bar (command palette, `?` cheatsheet, per-command rebinding). Learn: every new gesture must join the palette + cheatsheet. Avoid (unchanged): WebView2 dependency, cross-platform rewrite, cloud/multi-user.

## Security, Privacy, and Reliability
- Verified (source): session restore reopens `Keys.LastImagePath` with only `!IsNullOrWhiteSpace(last) && File.Exists(last)` then `window.OpenPath(last)` — it bypasses `TryResolveArgPath`, which the argv path uses to reject `\\?\`/`\\.\` device-namespace shapes and canonicalize. A persisted path (settings DB is user-writable) reaches `OpenPath` unfiltered, and `File.Exists` on a device path can block. `src/Images/App.xaml.cs:167-180`.
- Verified (source): the loupe never captures the mouse and does not guard an in-progress pan. `StartLoupe` (`src/Images/Controls/ZoomPanImage.cs`) sets `_loupeActive=true` without `CaptureMouse()`; `OnMove` short-circuits into `UpdateLoupe` while a left-drag pan is active (`_dragStart != null`), freezing the pan and leaving a stale `_dragStart` so the next left-up jumps the image. Releasing the middle button outside the control leaves the loupe stuck (no capture → no `OnAnyButtonUp`).
- Verified (source): tile-backed (gigapixel) loads set `LoadResult.Image = TilePlaceholder` (a 1×1 BitmapSource, `src/Images/Services/ImageLoader.cs:445`). The loupe guard (`_image.Source is not ImageSource`) and the HUD readout guard (`CurrentImage is BitmapSource`) both pass for the placeholder, so the loupe renders a solid 1×1-sampled block and the live pixel readout shows the placeholder's single value for every hover over a huge image. `ZoomPanImage.StartLoupe`/`UpdateLoupe`; `src/Images/MainWindow.xaml.cs:1210-1214`.
- Verified (source): `ImageLoader.ColorManagedDisplay` is a process-global mutable static, set on the UI thread (`SettingsViewModel`) and read on background decode threads (`Task.Run`/preload) with no memory barrier; preloaded neighbors are cached by path with no knowledge of the flag value used, so toggling color management does not re-color already-preloaded images and a decode in flight can observe a stale value. `src/Images/Services/ImageLoader.cs:67`.
- Verified (source): `Canvas_MouseMove` calls `TrySampleInspectorPixel` twice for the same position/event when both the metadata HUD and Inspector mode are on (once for the HUD readout, once for the inspector), unthrottled — a `CopyPixels` per WM_MOUSEMOVE, doubled. `src/Images/MainWindow.xaml.cs:1210-1219`.
- Verified (source): `DirectoryNavigator.LastMoveStoppedAtEnd` is reset only in `MoveNext`/`MovePrevious`, not in `MoveFirst`/`MoveLast`/`MoveToIndex`/`Open`/`Rescan`, so a stale "stopped at end" flag persists after a jump. Low impact (current sole consumer reads it immediately after a failed move) but latent. `src/Images/Services/DirectoryNavigator.cs`.
- Refuted (do not re-investigate): the claim that `TransformToSrgbIfProfiled`'s `TransformColorSpace(ColorProfiles.SRGB, ColorProfiles.SRGB)` is a no-op is false — the two-arg overload's first argument is the assumed source used only when no profile is embedded; with an embedded profile it converts embedded→target(sRGB). Proven by the passing test `ImageLoaderTests.TransformToSrgbIfProfiled_WideGamutProfile_ConvertsToSrgb`. Narrow real caveat: it returns `true` for CMYK/gray profiles without confirming a meaningful RGB result.

## Architecture Assessment
- Color-management completeness gap: memory-mapped (>256 MB) and tile-backed decodes silently bypass `TransformToSrgbIfProfiled` (documented in the comment at `ImageLoader.cs:62-67`), so the exact wide-gamut originals (large RAW/PSD) the feature targets render uncorrected with no status signal. Either apply the transform on the Magick MMF/tile source or surface "color management unavailable for this image" in the decoder/status string.
- Save-a-copy fidelity: `SaveCopyWithC2paHandoff` reloads via `new MagickImage(sourcePath)` (single frame). Copying an animated GIF / multi-page TIFF flattens to frame 0 with no warning; a JPEG target of a PNG source strips alpha — inherent to a user-chosen format change, but the animated-flatten deserves a warning or `MagickImageCollection`. `src/Images/Services/ImageExportService.cs:128-202`.
- Discoverability/a11y as an architectural invariant: the app has a command palette + `?` cheatsheet + per-command rebinding + custom UIA peers, but the loupe and zoom-to-selection were added as raw mouse gestures with no palette entry, no `Strings.resx` hint, no cheatsheet line, and no settings (`LoupeFactor` is an unexposed DP). New interactions should route through the existing command/cheatsheet infrastructure by construction.
- Test gaps: the new gesture/geometry code (loupe viewbox on tile-backed, zoom-select capture lifecycle, checker alignment under rotation) has control-level math tests but no coverage for the tile-placeholder and capture-conflict paths found here; each fix below should land a focused regression.

## Rejected Ideas
- Color-transform "no-op" fix (auditor #5): refuted by a passing test (see above); source = code-auditor subagent, dropped.
- Preserve 16-bit depth on the color-managed path (auditor #6): the display path produces a WPF `WriteableBitmap` Bgra32 (8-bit) either way, so the depth "loss" is moot for on-screen rendering; source = code-auditor subagent, rejected as no user-visible effect.
- Transparency-checkerboard rotation mis-size (auditor #8): the checker rect lives inside `_visual` and shares the same flip/rotate/scale transform as the image, so they rotate together; could not confirm a real misalignment without a wide-gamut/rotated live capture; needs-live-validation only, not a firm item.
- Re-anchor zoom-lock to preserve the same screen region (auditor #9): the current center-on-new-image behavior matches the shipped acceptance ("re-anchors pan to center"); changing it is a spec change, not a bug fix.
- Broad competitor/format re-survey: completed same day in passes 1-2; re-mining returns no new signal.

## Sources
Code findings (primary evidence, this pass):
- src/Images/App.xaml.cs:167-180
- src/Images/Controls/ZoomPanImage.cs (StartLoupe/UpdateLoupe/OnMove/OnDown/OnUp/UpdateTransparencyGrid)
- src/Images/Services/ImageLoader.cs:62-103,445,666-701
- src/Images/Services/ImageExportService.cs:128-202
- src/Images/Services/DirectoryNavigator.cs (MoveNext/MovePrevious/LastMoveStoppedAtEnd)
- src/Images/MainWindow.xaml.cs:1210-1219
- tests/Images.Tests/ImageLoaderTests.cs (TransformToSrgbIfProfiled_WideGamutProfile_ConvertsToSrgb)

External (grounding for specific claims):
- https://github.com/dlemstra/magick.net/blob/main/docs/ConvertImage.md
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.capturemouse
- https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file
- https://github.com/d2phap/ImageGlass/issues/1425
- https://github.com/sylikc/jpegview

## Open Questions
- Loupe on tile-backed (gigapixel) images: disable it entirely, or sample from the highest-resolution loaded tile? The latter is more capable but needs a tile-lookup path in `ZoomPanImage` that does not exist yet — decides the complexity of the fix.
- Should color management be applied to the memory-mapped/tile Magick paths (heavier, touches the large-image pipeline) or only signaled as unavailable? Decides whether the completeness fix is S or L.
