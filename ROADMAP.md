# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, GUI/visual validation, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when those are unblocked.

## Actionable Work

- [ ] P2 — Gallery grid is not virtualized
  Why: the gallery `ListBox` uses a `WrapPanel` with `VirtualizingPanel.IsVirtualizing="False"`, so opening it on a large folder realizes a full Button+ContextMenu visual tree for every file at once and queues N thumbnail decodes, freezing the UI on folders of several hundred+ images. The filmstrip and side preview are virtualized; the gallery is the outlier. Needs a virtualizing wrap panel (WPF has no built-in one) — implementation work, not a one-line flag.
  Where: `src/Images/MainWindow.xaml` (gallery ListBox ItemsPanel, ~line 1422-1433)

- [ ] P2 — Per-navigation synchronous file I/O on the UI thread
  Why: `CompleteCurrentLoad` runs on the dispatcher and does, per next/prev: up to 2 `.xmp` sidecar `File.Exists` probes (+ synchronous `XDocument.Load`/JSON deserialize when present) via `GetEnabledDisplayEditOperations`, a `new FileInfo(path).Length`, and two `ImageLoader.QuickDimensions` file opens in `EnqueueNeighbours`. Sub-frame on local SSD, but adds tens of ms of input lag per arrow-press on UNC/Dropbox/network paths. Move the sidecar and neighbour-dimension probes off the dispatcher.
  Where: `src/Images/ViewModels/MainViewModel.cs` (CompleteCurrentLoad ~4508/4582/4617), `PreloadService.cs`, `NonDestructiveEditService.cs`

- [ ] P2 — RAW navigation runs the whole per-nav UI-thread tail twice
  Why: `LoadCurrentAsync` calls `CompleteCurrentLoad` once for the embedded RAW preview and again for the full decode, re-running the sidecar probe, folder-preview refresh, and metadata/color/C2PA reads (started then cancelled+restarted) on every RAW navigation. Generation-guarded so no wrong data, but wasted work felt when browsing CR2/NEF/ARW folders.
  Where: `src/Images/ViewModels/MainViewModel.cs` (LoadCurrentAsync ~4370-4386)

- [ ] P2 — MainWindow/AboutWindow visible copy bypasses the localization system
  Why: ~92 `MenuItem Header=`, ~36 `ToolTip=`, the empty state, rail labels, cheatsheet, and load-error button labels are hardcoded English, plus AboutWindow labels. The localization gate only flags hardcoded `AutomationProperties.Name`, so `Text=`/`Header=`/`ToolTip=` drifted through — a non-English build shows a half-translated shell and screen readers read a localized name over an English label. Extend `Test-LocalizationResources.ps1` to flag these too, then route them through resx.
  Where: `src/Images/MainWindow.xaml`, `src/Images/AboutWindow.xaml`, `scripts/Test-LocalizationResources.ps1`

- [ ] P3 — Raw exception text still injected into ~25 secondary toasts
  Why: many `MainViewModel` catch blocks build toasts as `"<action> failed: " + ex.Message`, surfacing decoder/HRESULT jargon. The main decode-error card was cleaned up; the secondary toasts (rotation, wallpaper, export, strip-metadata, etc.) should use `FirstLine(ex.Message)` or a calm localized message consistently.
  Where: `src/Images/ViewModels/MainViewModel.cs` (various catch blocks)

- [ ] P3 — Incremental rescan re-hashes files whose sidecar check transiently fails
  Why: `CatalogService.IsUnchanged` treats any exception (e.g. a sidecar momentarily locked by cloud-sync/AV) as "changed", forcing a full SHA-256 re-hash of that file on every rescan under contention. Safe (never misses a real change) but defeats the incremental optimization. Consider distinguishing transient IO errors from genuine change signals.
  Where: `src/Images/Services/CatalogService.cs` (IsUnchanged / ReadSidecarFileSummary)

The remaining research item is decision-gated (Ghostscript-source AGPL mechanism) and lives in `Roadmap_Blocked.md`. Refill via a research pass (`RESEARCH.md`) when ready.

## Research-Driven Additions

Unblocked, net-new items only. HDR display (V100-06), full color management (V100-05), GPS map overlay (V20-23), and the Explorer thumbnail handler (V70-04) remain correctly parked in `Roadmap_Blocked.md` and are not duplicated here.

### P1

- [ ] P1 — Restrict ImageMagick to a read-coder allowlist and lock down delegates
  Why: `MagickSecurityPolicy` enforces resource limits and a write-format blocklist but sets no read-side coder policy, so a crafted exotic-format file (MNG/TIM/SF3/MSL/Log-colorspace) still reaches the native decoder — the 2025-2026 ImageMagick heap-overflow CVE class the app is exposed to when opening internet-downloaded images.
  Evidence: `src/Images/Services/MagickSecurityPolicy.cs` (no `ConfigurationFiles.Policy`/coder allowlist); imagemagick.org/script/security-policy.php; GHSA-hm4x-r5hc-794f (CVE-2025-53014), CVE-2025-55004.
  Touches: `src/Images/Services/MagickSecurityPolicy.cs`, `tests/Images.Tests/MagickSecurityPolicyTests.cs`
  Acceptance: at init the app injects a deny-all-then-permit policy allowing only the coders the viewer decodes (JPEG/PNG/GIF/WEBP/BMP/TIFF/HEIC/AVIF + the app's declared set) with delegate/`@`-path indirection set to `rights="none"`; opening an out-of-allowlist exotic format is refused cleanly (no native decode); a test asserts a blocked coder is rejected and an allowed one still decodes.
  Complexity: M

### P2

- [ ] P2 — Tonemap HDR/EXR/Radiance/16-bit content at decode instead of hard-clipping to sRGB
  Why: the decode path folds everything to sRGB and quantizes to 8-bit, so HDR-class images render as blown/hard-clipped SDR; a tonemap operator before quantization makes every HDR/EXR/RAW image look correct on any display, with no renderer rewrite (distinct from the SkiaSharp-blocked true-HDR epic).
  Evidence: `src/Images/Services/ImageLoader.cs` (`TransformColorSpace(SRGB,SRGB)` → `WriteableBitmap` Bgra32, no tonemap); MS Advanced-Color doc (Reinhard reference); ImageGlass 10 / HDRImageViewer.
  Touches: `src/Images/Services/ImageLoader.cs`, new `ToneMapService`, `tests/Images.Tests/`
  Acceptance: HDR-class inputs (EXR/HDR/16-bit/AVIF-HDR) route through a tonemap operator (Reinhard default; optional Hable/ACES) before 8-bit conversion; an EXR with values >1.0 no longer hard-clips (verified by a unit test on the float buffer); the decoder status string notes when tonemapping was applied.
  Complexity: M

- [ ] P2 — Legacy-mode monitor-ICC display output for wide-gamut accuracy
  Why: the app converts only to sRGB, so on wide-gamut (P3/AdobeRGB) monitors colors over-saturate; when Windows Advanced Color is OFF (the common case) the correct destination is the monitor's own ICC profile, achievable in the current WPF pipeline ahead of the SkiaSharp-blocked full color-management epic (V100-05).
  Evidence: `src/Images/Services/ImageLoader.cs`; HDR/color research (GetICMProfile/WcsGetDefaultColorProfile, legacy-mode path); nomacs #394, FastStone monitor-profile support.
  Touches: `src/Images/Services/ImageLoader.cs` (or a new `DisplayColorService`), settings for opt-in
  Acceptance: when Advanced Color is off and a non-sRGB monitor profile is present, decode transforms embedded → monitor profile (not sRGB); a P3-gamut test image no longer over-saturates on a wide-gamut display; falls back to sRGB when no profile/Advanced-Color-on; behavior is opt-in and reflected in diagnostics.
  Complexity: L

- [ ] P2 — Focus-peaking and highlight/shadow-clipping overlays for RAW/photo culling
  Why: the app decodes RAW and has curves/levels but no fast-culling overlays; peaking (in-focus edges) and clipping (blown highlights / crushed shadows) let a photographer triage a shoot in-viewer — a table-stakes RAW-workflow feature Images lacks.
  Evidence: FastRawViewer focus-peaking/overlay docs; no `FocusPeak`/clipping overlay in `src/Images/Services`.
  Touches: new overlay service + `ZoomPanImage`/overlay stack in `MainWindow.xaml(.cs)`, a toggle command in `CommandShortcutService`
  Acceptance: a toggle overlays edge-peaking (high-pass/Sobel on the decoded buffer) and a second toggle marks clipped highlights/shadows above/below thresholds; both are command-palette + cheatsheet discoverable, respect reduced-motion, and cost nothing when off.
  Complexity: M

- [ ] P2 — Detect HDR displays and surface an honest tonemapped-to-SDR status
  Why: on an HDR monitor the app silently tonemaps to SDR with no signal; detecting the display (IDXGIOutput6) lets the UI say "HDR display detected — content shown tonemapped to SDR", setting honest expectations and laying groundwork for a future HDR path.
  Evidence: MS `IDXGIOutput6::GetDesc1` / DXGI_OUTPUT_DESC1 docs (desktop-usable HDR detection); no HDR/AdvancedColor references in `src/Images`.
  Touches: new `DisplayCapabilityService` (P/Invoke DXGI), status/diagnostics surface
  Acceptance: the app reports per-monitor HDR capability (color space, max luminance) in diagnostics and shows a status chip/badge when an HDR-class image is displayed on an HDR monitor; no-op on SDR displays.
  Complexity: S

### P3

- [ ] P3 — Bump SharpCompress to 0.50.0 for CRC verification and truncated-stream tolerance
  Why: 0.50.0 turns on CRC verification (catches truncated/corrupt comic archives the streaming reader would otherwise mis-render) and adds `tolerateTruncatedStream` (render partially-downloaded `.cb*` files); the app never calls `WriteToDirectory`, so the unpatched zip-slip CVE-2026-44788 does not apply.
  Evidence: SharpCompress 0.50.0 release notes; `src/Images/Images.csproj` pins 0.49.1; `ArchiveBookService` streams entries.
  Touches: `src/Images/Images.csproj`, `src/Images/Services/ArchiveBookService.cs`
  Acceptance: dependency at 0.50.0, tests green; a truncated CBZ renders available pages instead of erroring; a corrupt entry is reported rather than silently mis-rendered.
  Complexity: S

- [ ] P3 — Assert native decoder/SQLite versions in diagnostics
  Why: security posture depends on the native ImageMagick (need ≥ 7.1.2-2 for the 2025 overflow CVEs) and SQLite (need ≥ 3.50.2 for CVE-2025-6965) versions bundled inside the managed packages, which aren't visible today; surfacing and asserting them makes drift detectable.
  Evidence: CVE-2025-57803 (ImageMagick), CVE-2025-6965 (SQLite); no `MagickNET.Version`/`sqlite_version()` surface in `src/Images`.
  Touches: `src/Images/Services/DiagnosticsStatusService.cs`, About/diagnostics panel
  Acceptance: diagnostics show `MagickNET.Version`, the native ImageMagick version, and `SELECT sqlite_version()`; a startup log warns if either is below the known-good floor.
  Complexity: S

- [ ] P3 — Continuous vertical-scroll (webtoon) reading mode for archives
  Why: the comic reader supports RTL and two-page spreads but only paged navigation; a continuous vertical-scroll mode is the standard webtoon/long-strip reading experience that BandiView/Honeyview ship and Images' comic audience expects.
  Evidence: BandiView webtoon/vertical-flow; `src/Images/Services/ArchiveBookService.cs` has no continuous-scroll mode.
  Touches: `ArchiveBookService`, the book viewer surface in `MainWindow.xaml(.cs)`, a reading-mode toggle
  Acceptance: a reading-mode toggle stacks archive pages in a single scrollable strip with lazy decode/recycling; the last read position persists like the existing per-book progress; paged and spread modes remain available.
  Complexity: M
