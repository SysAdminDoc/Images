# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, GUI/visual validation, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when those are unblocked.

## Research-Driven Additions

Net-new, evidence-grounded, code-ready items from the 2026-07-12 (pass 2) research. The feature backlog is otherwise drained; larger bets remain in `Roadmap_Blocked.md`.

### P2 — reliability / distribution

- [ ] P2 — Serialize timing-sensitive tests to stop parallel-load flakes
  Why: `CodecRuntimeTests.RunVersionProbe_DrainsStderrWhileWaiting` and `UpdateCheckServiceTests.CheckAsync_WhenContentLengthIsUnknown_RecordsActualBytesRead` intermittently fail only under full-suite CPU saturation (process-spawn stdout/stderr drain timing and HTTP stream byte-count timing); they pass isolated. A green build can flip red on unrelated PRs.
  Evidence: first-hand test runs this session (2 failures on a full run, 0 on a clean re-run / isolated); xUnit docs (config-xunit-runner-json, running-tests-in-parallel); `tests/Images.Tests/CodecRuntimeTests.cs:44`, `tests/Images.Tests/UpdateCheckServiceTests.cs:49`.
  Touches: new `[CollectionDefinition("Timing-Sensitive", DisableParallelization = true)]` + `[Collection("Timing-Sensitive")]` on the process-spawn/stream-timing classes (CodecRuntimeTests, UpdateCheckServiceTests, and any other child-process/stream-timing class); optionally add `tests/Images.Tests/xunit.runner.json` (`parallelAlgorithm: "conservative"`, a `maxParallelThreads` cap) with `CopyToOutputDirectory=PreserveNewest` in the csproj.
  Acceptance: the full `dotnet test Images.sln -c Release` suite passes green across 10 consecutive runs with no intermittent timing failures.
  Complexity: S

- [ ] P2 — Cut the v0.2.26 release from the accumulated Unreleased CHANGELOG
  Why: ~29 lines of shipped user-facing features (opt-in color management, loupe, live pixel readout, zoom-lock, transparency checkerboard, zoom-to-selection, session restore, stop-at-ends, metadata-preserving Save-a-copy, Magick.NET 14.15 security bump) sit unreleased since v0.2.25 with no version cut. The unsigned ZIP/installer path is not gated on signing.
  Evidence: `CHANGELOG.md` `## Unreleased` section; `scripts/Test-ReleaseReadiness.ps1` / local release scripts; git log since `45494cb` (release Images 0.2.25).
  Touches: version strings (`src/Images/Images.csproj`, `app.manifest`, installer defaults, README badge), `CHANGELOG.md` (promote Unreleased → `v0.2.26 - <date>`), run release-readiness gates (version sync, tests, vulnerable/deprecated scan, localization parity, provenance docs, package-manifest hashes, WinGet/Scoop manifest validation), tag + GitHub Release with the unsigned artifacts.
  Acceptance: all version strings match 0.2.26, release-readiness script passes, a GitHub Release v0.2.26 exists with the portable ZIP + installer attached and downloadable.
  Complexity: M

### P3 — dependency currency

- [ ] P3 — Bump Serilog 4.3.1 → 4.4.0
  Why: One routine minor behind (4.4.0 released 2026-07-10); keeps the logging dependency current. No security advisory — low priority, but a clean quick win alongside the release cut.
  Evidence: https://www.nuget.org/packages/Serilog/ ; `src/Images/Images.csproj` pins `Serilog` 4.3.1.
  Touches: `src/Images/Images.csproj` (Serilog PackageReference); verify build + `dotnet list --deprecated/--vulnerable` clean; Serilog.Extensions.Logging/Sinks.File stay on their current pins unless a resolution conflict appears.
  Acceptance: Serilog resolves to 4.4.0, solution builds, full suite green, vulnerable/deprecated scans clean.
  Complexity: S

### P2 — hardening of today's shipped features (2026-07-12 pass 3 code audit)

- [ ] P2 — Validate the session-restore path before opening
  Why: Session restore reopens `Keys.LastImagePath` with only `File.Exists(last)` then `OpenPath(last)`, bypassing the `TryResolveArgPath` device-namespace (`\\?\`/`\\.\`) rejection + canonicalization the argv path uses; the settings DB is user-writable, and `File.Exists` on a device path can block.
  Evidence: `src/Images/App.xaml.cs:167-180`; the argv guard at `TryResolveArgPath`; RESEARCH.md Security section.
  Touches: `src/Images/App.xaml.cs` (route `last` through `TryResolveArgPath(last, out var safe)` and open only `safe`); regression test for a device-path/nonexistent persisted value.
  Acceptance: a persisted `LastImagePath` containing a device/UNC/reparse or nonexistent shape is rejected (falls back to empty state), and only canonicalized real files are reopened.
  Complexity: S

- [ ] P2 — Fix loupe/zoom-to-selection mouse-capture and gesture conflicts
  Why: `StartLoupe` sets `_loupeActive` without `CaptureMouse()`, so releasing the middle button off-control leaves the loupe stuck; middle-down during an active left-drag pan (`_dragStart != null`) freezes the pan and strands `_dragStart`, so the next left-up jumps the image; the zoom-select `LostMouseCapture`→`CancelZoomSelection`→`ReleaseMouseCapture` path re-enters once.
  Evidence: `src/Images/Controls/ZoomPanImage.cs` (`StartLoupe`, `StopLoupe`, `OnMove`, `OnAnyButtonDown`, `CancelZoomSelection`, `LostMouseCapture`); RESEARCH.md.
  Touches: `ZoomPanImage.cs` — capture on `StartLoupe`/release on `StopLoupe`; ignore middle-down while `_dragStart`/`_zoomSelectStart` is active; make loupe and zoom-select mutually exclusive; avoid `ReleaseMouseCapture` re-entry from the `LostMouseCapture` handler.
  Acceptance: loupe hides reliably on middle-up anywhere; a pan interrupted by middle-down resumes without jumping; no captured-mouse leak after either gesture; a control test covers the pan-then-loupe sequence.
  Complexity: M

- [ ] P2 — Loupe and live pixel readout must not sample the 1×1 tile placeholder on gigapixel images
  Why: Tile-backed loads set `LoadResult.Image = TilePlaceholder` (1×1 BitmapSource); the loupe guard (`_image.Source is not ImageSource`) and HUD guard (`CurrentImage is BitmapSource`) both pass, so the loupe shows a solid block and the readout shows the placeholder's single pixel for every hover over a huge image.
  Evidence: `src/Images/Services/ImageLoader.cs:445`; `ZoomPanImage.StartLoupe`/`UpdateLoupe`; `src/Images/MainWindow.xaml.cs:1210-1214`; RESEARCH.md.
  Touches: `ZoomPanImage` (require `_image.Source is BitmapSource { PixelWidth: > 1 }` or `TilePyramid is null` before showing the loupe); `MainWindow.xaml.cs` (blank the HUD readout when tile-backed); tests.
  Acceptance: with a tile-backed image loaded, the loupe does not appear (or samples real tiles) and the HUD pixel readout is blank rather than showing 1×1 placeholder values.
  Complexity: M

- [ ] P2 — Make `ImageLoader.ColorManagedDisplay` toggle-safe and preload-aware
  Why: The flag is a process-global mutable static read on background decode threads with no memory barrier, and preloaded neighbors are cached by path with no record of the flag value used — so toggling color management does not re-color already-preloaded images and an in-flight decode can read a stale value.
  Evidence: `src/Images/Services/ImageLoader.cs:67`; `src/Images/ViewModels/SettingsViewModel.cs` (setter); `src/Images/ViewModels/MainViewModel.cs` preload cache; RESEARCH.md.
  Touches: mark the flag `volatile` at minimum; better, thread the bool through `Load(...)` and include it in the preload cache key (or invalidate/clear the preload cache when the setting flips); a test asserting a toggle re-decodes/re-colors.
  Acceptance: toggling color management updates the current and preloaded neighbors on next navigation without stale-mode results; no torn read under concurrent load.
  Complexity: M

### P3 — new-feature polish (2026-07-12 pass 3 code audit)

- [ ] P3 — Give the loupe and zoom-to-selection discoverability and keyboard access
  Why: Both were added as raw mouse gestures (middle-button hold; Ctrl+Shift+drag) with no command-palette entry, no `Strings.resx` hint, no `?` cheatsheet line, and no settings (`LoupeFactor` is an unexposed DP) — a regression against the app's established palette + cheatsheet + rebinding discoverability bar.
  Evidence: no matches for loupe/zoom-select in `CommandShortcutService.cs`/palette/`Strings.resx`/cheatsheet; `ZoomPanImage.cs`; ImageGlass #1425 (surfaces the magnifier); RESEARCH.md.
  Touches: `CommandShortcutService.cs` + `MainViewModel` palette (add "Toggle loupe" / "Zoom to selection" commands + localized strings), the `?` cheatsheet content, and a Settings entry for `LoupeFactor`; keyboard-invocable loupe following the last cursor.
  Acceptance: both features appear in the command palette and the `?` cheatsheet, are invocable without a mouse, and `LoupeFactor` is adjustable in Settings; localization parity gate passes with the new strings.
  Complexity: M

- [ ] P3 — Sample the hover pixel once per mouse-move and throttle it
  Why: `Canvas_MouseMove` calls `TrySampleInspectorPixel` twice for the same position when the HUD and Inspector are both on (once for the HUD readout, once for the inspector), unthrottled — a `CopyPixels` per WM_MOUSEMOVE, doubled.
  Evidence: `src/Images/MainWindow.xaml.cs:1210-1219`; RESEARCH.md.
  Touches: `MainWindow.xaml.cs` (sample once, reuse the result for HUD + inspector; add a lightweight per-frame/dispatcher throttle).
  Acceptance: at most one pixel sample per mouse-move event feeds both the HUD and inspector; no measurable UI-thread cost increase on a fast drag over a large image.
  Complexity: S

- [ ] P3 — Reset `LastMoveStoppedAtEnd` on all navigation entry points
  Why: The flag is cleared only in `MoveNext`/`MovePrevious`, so a stale "stopped at end" value persists after `MoveFirst`/`MoveLast`/`MoveToIndex`/`Open`/`Rescan` (latent now, but the flag is public and read by the nudge path).
  Evidence: `src/Images/Services/DirectoryNavigator.cs` (`LastMoveStoppedAtEnd`); RESEARCH.md.
  Touches: `DirectoryNavigator.cs` (reset the flag in `MoveFirst`, `MoveLast`, `MoveToIndex`, `Open`, `Rescan`); a navigator test asserting the flag is false after a jump.
  Acceptance: `LastMoveStoppedAtEnd` is false after any first/last/jump/rescan; the end-of-folder nudge only fires on an actual stopped prev/next.
  Complexity: S

- [ ] P3 — Signal when color management is unavailable for an image
  Why: Memory-mapped (>256 MB) and tile-backed decodes silently bypass `TransformToSrgbIfProfiled`, so the large wide-gamut RAW/PSD originals the feature exists for render uncorrected with color management "on" and no indication.
  Evidence: `src/Images/Services/ImageLoader.cs:62-103,435-451`; RESEARCH.md Architecture section.
  Touches: `ImageLoader` decoder/status string (report "color management unavailable for this image" on the MMF/tile paths when the flag is on); optionally apply the transform on those Magick paths (larger).
  Acceptance: with color management on, loading a >256 MB or tile-backed image shows an explicit unavailable/uncorrected indication rather than silently rendering uncorrected.
  Complexity: S

- [ ] P3 — Warn (or preserve frames) when Save-a-copy flattens an animated/multi-page source
  Why: `SaveCopyWithC2paHandoff` reloads via `new MagickImage(sourcePath)` (single frame), so copying an animated GIF or multi-page TIFF silently produces a frame-0 flatten.
  Evidence: `src/Images/Services/ImageExportService.cs:128-202`; RESEARCH.md.
  Touches: `ImageExportService`/Save-a-copy path (detect multi-frame sources and either preserve via `MagickImageCollection` or surface a "saved first frame only" notice).
  Acceptance: copying an animated GIF either preserves its frames or reports that only the first frame was saved; a test covers the multi-frame source.
  Complexity: M
