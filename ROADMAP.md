# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Audit-Surfaced Items

- [ ] P1 — **PreloadService cache poisoning on cancellation**
  Why: A faulted `Lazy<Task>` permanently poisons the cache slot. Cancelled preloads leave entries that always re-throw on subsequent access.
  Where: `src/Images/Services/PreloadService.cs` (Enqueue, TryGetInFlight)

- [ ] P1 — **ExifToolService executable path validation**
  Why: `TryNormalizeExecutable` accepts any path to an existing file. If the configured path were attacker-controlled, a malicious executable could be launched.
  Where: `src/Images/Services/ExifToolService.cs`

- [ ] P2 — **RecoveryCenterService restore path re-validation**
  Why: Deserialized paths from the JSONL log are used in `File.Move` without re-validation. A tampered log could move files to attacker-controlled locations.
  Where: `src/Images/Services/RecoveryCenterService.cs` (Restore method)

- [ ] P2 — **ListenService per-connection idle timeout**
  Why: `ReceiveTimeout` only works for synchronous reads; async `ReadAsync` can hold a connection open indefinitely with the session token.
  Where: `src/Images/Services/ListenService.cs` (HandleClient)

- [ ] P2 — **Slideshow shuffle index bounds check**
  Why: If the file list changes between reading `_nav.Files.Count` and indexing `_nav.Files[nextIndex]`, an out-of-bounds access is possible.
  Where: `src/Images/ViewModels/MainViewModel.cs` (SlideshowTimer_Tick)

- [ ] P2 — **Theme-aware caption re-application on runtime theme switch**
  Why: Window captions are applied once at SourceInitialized. Switching themes at runtime doesn't update already-open window captions.
  Where: `src/Images/Services/WindowChrome.cs`, `src/Images/Services/ThemeService.cs`

- [ ] P3 — **FolderPreviewController SemaphoreSlim not disposed**
  Why: `_thumbnailDecodeGate` SemaphoreSlim is never disposed in `Dispose()`.
  Where: `src/Images/ViewModels/FolderPreviewController.cs`

## Research-Driven Additions

- [ ] P0 — **Verify Magick.NET bundled codec versions against critical 2026 CVEs**
  Why: libheif CVE-2026-32740 (CVSS 8.8, heap-buffer-overflow via crafted HEIF/AVIF), libjxl CVE-2026-1837 (use-after-free), ImageMagick CVE-2026-25797 (RCE via PostScript injection). All fixed upstream but the bundled versions in Magick.NET 14.14.0 must be confirmed >= safe floors (libheif 1.22.2+, ImageMagick 7.1.2-15+).
  Evidence: Snyk advisory database, NVD
  Touches: `src/Images/Images.csproj` (package version), `src/Images/Services/ImageLoader.cs`, `src/Images/Services/CodecRuntime.cs`
  Acceptance: Runtime probe or NuGet inspection confirms all bundled codec versions are at or above patched releases. If not, Magick.NET upgraded to a version that bundles them.
  Complexity: S

- [ ] P0 — **Set Magick.NET ResourceLimits to prevent memory-bomb attacks**
  Why: No `ResourceLimits.Memory`, `ResourceLimits.Width`, or `ResourceLimits.Height` calls exist anywhere in the codebase. CVE-2026-25985 demonstrates a 674 GB allocation attack via crafted SVG. Any untrusted SVG/PS/XBM file opened in Images can exhaust system memory.
  Evidence: CVE-2026-25985, code grep for `ResourceLimits` returns zero hits
  Touches: `src/Images/App.xaml.cs` (startup initialization), potentially `src/Images/Services/ImageLoader.cs`
  Acceptance: `ResourceLimits.Memory`, `ResourceLimits.Width`, `ResourceLimits.Height`, and `ResourceLimits.Area` are set at app startup to safe defaults (e.g., 2 GB, 32768 px, 32768 px, 1 Gpx). Magick.NET operations on oversized input fail gracefully with a user-facing toast instead of OOM.
  Complexity: S

- [ ] P1 — **Validate SQLitePCLRaw embedded SQLite version**
  Why: CVE-2025-6965 affects SQLite before 3.50.2 with memory corruption risk. SQLitePCLRaw 3.0.3 should bundle a patched version but the embedded `e_sqlite3` native library version has not been confirmed.
  Evidence: NVD CVE-2025-6965
  Touches: `src/Images/Images.csproj` (SQLitePCLRaw version), `tests/` (add version assertion test)
  Acceptance: A test or startup diagnostic confirms the embedded SQLite version is >= 3.50.2. If not, SQLitePCLRaw upgraded.
  Complexity: S

- [ ] P1 — **Close remaining 21 untested service test gaps**
  Why: 21 of 105 services lack dedicated test files. The most critical untested services are `ImageLoader` (796 lines, core decode path), `OcrService` (user-facing OCR feature), `PreloadService` (cache correctness), `CodecRuntime` (codec detection), and `CrashLog` (crash reporting reliability). Untested services are fragile to refactoring.
  Evidence: `diff` of `src/Images/Services/*.cs` basenames vs `tests/Images.Tests/*Tests.cs` basenames
  Touches: `tests/Images.Tests/` (new test files for each untested service)
  Acceptance: Every service file in `src/Images/Services/` has a corresponding `*Tests.cs` file with at least basic construction and key-path coverage. Test count increases from 617 to ~700+.
  Complexity: L

- [ ] P1 — **Cold-start performance benchmark and optimization pass**
  Why: IrfanView and FastStone open in sub-second; ImageGlass GitHub issue #794 specifically asks for faster launch. Microsoft Photos is called "bloatware." Images has `LaunchTiming` and `PerformanceBudgetService` infrastructure but no published benchmarks or optimization targets. Users choosing between viewers compare startup speed directly.
  Evidence: ImageGlass #794, DonationCoder benchmarks, community forums
  Touches: `src/Images/Services/LaunchTiming.cs`, `src/Images/App.xaml.cs`, `src/Images/MainWindow.xaml.cs`, potentially service initialization order
  Acceptance: `--perf-report` output documents cold-start time on reference hardware. If >1 second, identify and defer non-critical initialization (catalog scan, thumbnail cache, ONNX runtime, settings migrations) to post-render.
  Complexity: M

- [ ] P1 — **Touch and gesture input for pan/zoom/navigate**
  Why: No `ManipulationDelta`, `Stylus`, or WPF touch-input handling exists anywhere in the codebase. NeeView (same WPF/.NET 10 stack) has touch/gesture support. Windows tablets and touchscreen laptops are common. Pan, zoom, and next/prev navigation should respond to pinch-to-zoom, swipe, and drag gestures.
  Evidence: NeeView feature set, code grep for `ManipulationDelta`/`Stylus`/`Touch` returns zero hits
  Touches: `src/Images/Controls/ZoomPanImage.cs` (manipulation events), `src/Images/MainWindow.xaml.cs` (swipe navigation)
  Acceptance: Pinch-to-zoom works on touchscreen. Swipe left/right navigates images. Single-finger drag pans when zoomed. Multi-touch does not conflict with mouse input.
  Complexity: M

- [ ] P2 — **Motion photo embedded video playback**
  Why: `MotionPhotoService` detects and extracts embedded MP4 byte ranges from Samsung/Google motion photos but does not play or preview the video segment. ImageGlass 10 Beta 2 now plays motion photo video inline. Users with modern phones produce motion photos by default.
  Evidence: ImageGlass 10 feature list, code inspection of `src/Images/Services/MotionPhotoService.cs`
  Touches: `src/Images/Services/MotionPhotoService.cs`, `src/Images/ViewModels/MainViewModel.cs` (playback command), `src/Images/MainWindow.xaml` (video surface)
  Acceptance: When a motion photo is displayed, a UI control allows playing the embedded MP4 segment inline. Playback uses `MediaElement` or equivalent WPF video surface. Non-motion photos are unaffected.
  Complexity: M

- [ ] P2 — **WCAG 2.5.7 drag alternatives audit for crop/pan/brush tools**
  Why: WCAG 2.2 SC 2.5.7 requires that every drag operation has a non-drag alternative. Crop selection, pan, dodge/burn brush, clone/heal brush, and red-eye correction all use drag-only interaction. WCAG 2.5.8 target sizes were fixed (commit `e2e2bfe`) but 2.5.7 remains unaudited.
  Evidence: WCAG 2.2 / ISO 40500:2025, code inspection of overlay controls
  Touches: `src/Images/Controls/CropOverlay.xaml.cs`, `src/Images/Controls/LocalExposureBrushOverlay.xaml.cs`, `src/Images/Controls/RetouchBrushOverlay.xaml.cs`, `src/Images/Controls/RedEyeCorrectionOverlay.xaml.cs`, `src/Images/Controls/ZoomPanImage.cs`
  Acceptance: Each drag-only tool has a documented non-drag alternative (arrow-key nudge for crop handles, keyboard pan for viewport, coordinate-entry fields for precise placement). Audit results documented in `docs/accessibility.md`.
  Complexity: M

- [ ] P2 — **CycloneDX SBOM spec upgrade to 1.7**
  Why: CycloneDX 1.7 (March 2026) adds ML-BOM support for ONNX model provenance and TLP distribution constraints. Images bundles ONNX Runtime and has a model manager — ML-BOM would declare approved model hashes in the SBOM alongside NuGet dependencies.
  Evidence: CycloneDX 1.7 spec release notes
  Touches: `.github/workflows/release.yml` (SBOM generation step), potentially `src/Images/Services/ModelManagerService.cs` (model hash declarations)
  Acceptance: Release workflow generates CycloneDX 1.7 SBOM. ONNX model definitions from ModelManagerService are included as ML-BOM components with pinned SHA-256 hashes.
  Complexity: S

