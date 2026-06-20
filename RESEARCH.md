# Research — Images

## Executive Summary

Images is a Windows-only WPF/.NET 10 image viewer at v0.2.11, with ~21.6k lines of source, 629 tests, 105 service classes, and a feature set that already rivals or exceeds ImageGlass, nomacs, and most OSS competitors in breadth: OCR, semantic search, catalog, batch processing, archive/comic reader, culling labels, export preview, macro actions, non-destructive edit history, duplicate cleanup, file health scanning, and privacy-first network transparency. No other desktop image viewer ships the combination of local OCR + semantic search + rebuildable catalog + inline rename.

The project's strongest current shape is as a trustworthy local power-user viewer with the Windows 7 Photo Viewer aesthetic in Catppuccin Mocha. The v1.0 milestone is blocked only on code signing (D-05) and WinGet publication (D-02) — both credential-gated.

Top opportunities in priority order:
1. Pin CI runner before `windows-latest` migration breaks builds (June 2026)
2. Parallelize batch processing for throughput parity with IrfanView/ACDSee
3. Close test coverage gaps across ~30 untested services
4. Add embedded-JPEG-first RAW preview for instant browsing of large RAW folders
5. Extract dominant color palettes for color-based gallery filtering (Eagle-style)
6. Add drag-divider comparison split to the export preview (Squoosh enhancement)
7. Build edge-hover contextual panels for zero-chrome fullscreen (FastStone pattern)
8. Clean up stale documentation references

## Product Map

- **Core workflows**: Open/navigate image folders and archives; inline rename with debounced auto-save; review-label culling (1-5 star, pick/reject); inspect metadata/OCR/codecs/network activity; apply non-destructive edits (levels, curves, HSL, dodge/burn, red-eye, clone/heal, resize, perspective); batch convert/export with preview; import, dedupe, catalog, and semantic-search local collections.
- **User personas**: Windows power user replacing Photos/IrfanView/FastStone; privacy-sensitive organizer who needs offline-only workflows; creator who needs repeatable batch/export pipelines; archive/comic reader; local-AI early adopter who wants explicit model provenance.
- **Platforms and distribution**: Windows 10/11 desktop, WPF on `net10.0-windows10.0.22621.0`, GitHub release ZIP and Inno installer (self-contained), generated WinGet/Scoop manifests, CycloneDX SBOM, GitHub artifact attestations. Store/signing tracks blocked in `Roadmap_Blocked.md`.
- **Key integrations**: WIC/Magick.NET 14.14.0/SharpCompress 0.49.1/Ghostscript/jpegtran decode and writeback; SQLite settings/catalog/semantic indexes; XMP sidecars for portable review metadata; Windows.Media.Ocr; ONNX Runtime 1.24.4 DirectML with CLIP ViT-B/32 semantic search seam; GitHub Releases update check (opt-in only).

## Competitive Landscape

### ImageGlass (d2phap/ImageGlass) — 13.3k stars
v10 is a full **Avalonia UI rewrite** (cross-platform). The biggest strategic threat long-term. Strong format breadth via Magick.NET, massive install base. No OCR, no semantic search, no catalog, no batch pipeline, no archive reading. Learn from: plugin/extension ecosystem and 90+ format coverage. Avoid: unsigned binaries and ambiguous paid-tier semantics.

### NeeView (neelabo/NeeView) — same WPF/.NET 10 stack
The closest structural competitor. v46 alpha on .NET 10. Best-in-class book/comic reading UX: dual-page mode, recursive archive browsing, playlist navigation, gesture/touch support. Microsoft Store presence. No OCR, no search, no catalog, no editing. Learn from: book UX depth and touch gestures. Avoid: Japanese-centric development patterns.

### FastStone Image Viewer — commercial freeware
Edge-hover fullscreen panels (move mouse to screen edges to reveal EXIF/thumbnails/tools) with zero-chrome viewing. Batch processing handles resize/rotate/crop/DPI/watermark/border in one pass. 4-up synchronized comparison. Learn from: edge-hover UX and batch processor breadth. Avoid: closed-source limitations on community contribution.

### XnView MP — commercial freeware (personal use)
SQLite-backed catalog with ratings, labels, hierarchical category tree (not just flat tags), drag-drop batch assignment. Metadata editor is the deepest in the OSS/freeware space. Smart folders auto-populate by criteria. Learn from: category tree sidebar and metadata depth. The power-user reference implementation for organizing.

### IrfanView — commercial freeware
Added explicit multithreaded batch conversion in v4.75 (May 2025). Plugin ecosystem, regex/counter/EXIF-field batch rename patterns. Learn from: multithreaded batch (single-threaded is a dealbreaker for power users). Avoid: the plugin-dependency spaghetti.

### Adobe Lightroom Classic — $10/mo subscription
AI-assisted culling (Subject Focus + Eye Sharpness scoring, 0-100 sliders) is the highest-value AI feature in the 2025-2026 cycle. Reduces multi-hour culling to ~20 minutes. AI denoise runs as a non-blocking background queue. Learn from: tunable strictness culling UX and background queue pattern. Reference for when V60-01 inference runtime ships.

### Eagle — $30 perpetual
Auto-extracts color palettes from every imported asset. Color swatch click → find visually-similar assets. Stackable filters: color + tag + rating + dimensions + annotation. Learn from: perceptual color search (no OSS viewer offers this). Avoid: cloud-sync defaults.

### Squoosh / Squoosh CLI — Google, free
Draggable split-view showing original vs. compressed with real-time file-size delta. Gold standard for compression-quality comparison UX. Learn from: the draggable divider interaction (Images already has linked pan/zoom + difference toggle, but lacks the divider). Avoid: WASM-only limitation.

## Security, Privacy, and Reliability

- **Verified (fixed)**: `scripts/Test-ReleaseReadiness.ps1` no longer references `PROJECT_CONTEXT.md`. The stale gate that blocked valid releases was removed in commit `a1c9bb9`.
- **Verified (fixed)**: Stale `0.1.x`/`net9.0` references in `docs/release-support-policy.md` were updated per CHANGELOG fixes.
- **Verified (fixed)**: WPF smoke tests are now split into `SmokeGate` (required CI gate, line 97-100 of ci.yml) and exploratory (`continue-on-error: true`). The prior research concern is resolved.
- **Verified (fixed)**: 23 hardcoded XAML hex colors were replaced with semantic theme tokens (commit `6970d5a`). Only 2 near-transparent hit-test backgrounds remain (`#01000000` in MainWindow.xaml), which are standard WPF practice.
- **Verified (fixed)**: CLIP pipeline now has explicit validation with step-by-step failure reasons via `ClipFallbackReason` and Model Manager Validate button.
- **Verified (fixed)**: Local data management panel with per-store sizes and clear actions shipped in Settings.
- **Verified (active)**: SharpCompress CVE-2026-44788 (zip-slip in `WriteToDirectory`, CVSS 5.9). Images uses SharpCompress for read-only archive page streams and does **not** call `WriteToDirectory`, so the attack vector is not reachable. SharpCompress 0.49.1 is pinned. No action needed beyond monitoring.
- **Verified (active)**: `docs/archive-runtime-review.md` line 15 still says `net9.0` — a stale reference from the original SharpCompress evaluation. Low severity but should be corrected.
- **Risk**: GitHub Actions `windows-latest` is migrating to Windows Server 2025 + VS 2026 (June 8-15, 2026). CI should pin to `windows-2025` or `windows-2025-vs2026` to prevent surprise breakage.
- **Risk**: Ghostscript 10.08.0 released 2026-06-11 with security hardening (`.tempfile` operator removal, temp directory restrictions). Bundled 10.07.0 is now two versions behind. This is already tracked in `Roadmap_Blocked.md` and cannot be unblocked without downloading the binary.
- **Observation**: Batch processing in `BatchProcessorService.cs` is fully sequential — no `Parallel`, `SemaphoreSlim`, or `Task.WhenAll` anywhere in the services directory. This is a measurable throughput gap vs competitors.

## Architecture Assessment

- **MainViewModel.cs** at 7,249 lines with 159 public commands remains the largest file. 7 controllers have been factored out (OCR, C2PA, color analysis, update check, folder preview, photo metadata, external edit reload) per the completed IP-02 improvement plan. Further decomposition is optional — the current structure is functional.
- **Test coverage**: ~30 of ~105 services lack dedicated test files. Key untested services: `ImageLoader`, `OcrService`, `PreloadService`, `CrashLog`, `ImageMetadataService`, `PrintService`, `CodecRuntime`, `OnnxRuntimeService`, `BackgroundJobsService`, `SupportBundleService`, `ClipEmbeddingProvider`, `PerformanceBudgetService`. The 629 existing tests cover the most critical services well.
- **Localization**: Only English locale exists (`Strings.resx`). Localization infrastructure is in place (CI parity checks, `LocExtension`, `Strings.cs` typed accessors) but no satellite `.resx` files. Crowdin setup is blocked on external account (I-02 in `Roadmap_Blocked.md`).
- **Batch processing**: `BatchProcessorService.cs` (637 lines) processes files sequentially. No concurrency primitives exist in any of the 105 service files. Adding `SemaphoreSlim`-bounded parallel processing would be a targeted change.
- **CatalogService.cs** (1,011 lines): Incremental rescan was added (commit `e9297b8`), resolving the prior research concern about full-rebuild-only behavior. Reused/updated/removed counts are now tracked.
- **Theme system**: Three complete themes (Catppuccin Mocha dark, Catppuccin Latte light, SystemColors high-contrast) with Follow System mode via `ThemeService`. DynamicResource throughout. Semantic tokens `OverlayDimmerBrush` and `SubtleSurfaceBrush` were added. No theme-token drift detected.
- **ETW observability**: `ImageEventSource` provides decode pipeline counters via `dotnet-counters` and structured events via `dotnet-trace`. Well-designed for performance regression tracking.

## Rejected Ideas

- **Cross-platform (Avalonia/MAUI)**: Rejected. ImageGlass v10 is betting on Avalonia; NeeView stays WPF. Images' WPF investment is deep (13.9k lines of XAML, custom UIA peers, Win32 P/Invoke for dark caption/wallpaper/minidump). Migration would be a rewrite, not a port. WPF on .NET 10 is actively maintained with new features. Source: ImageGlass v10 beta, NeeView v46 alpha.
- **Cloud sync, accounts, multi-user**: Rejected per project philosophy and privacy policy. Source: Mylio/Immich/PhotoPrism.
- **Full Photoshop-class editor**: Rejected. PhotoDemon proves OSS can ship 200+ filters but at the cost of being an editor, not a viewer. Images should stay viewer-first. Source: PhotoDemon.
- **AI denoise/masking/upscaling as immediate work**: Rejected until V60-01 inference runtime ships (blocked in `Roadmap_Blocked.md`). Commercial tools (Lightroom, DxO DeepPRIME) prove the value but the ONNX Runtime integration must come first. Source: Lightroom AI denoise, Capture One Enhanced Denoise.
- **Video playback**: Rejected per codec-support-policy.md tier table. Not in scope. Source: IrfanView plugin.
- **Tabbed MDI windows**: Rejected. ACDSee's persistent tabs are high-retention but would require significant window management infrastructure. The existing `SessionTrayService` (cross-folder file list) serves the same multi-folder use case without UI complexity.
- **TWAIN scanner integration**: Already tracked as V30-24 in `Roadmap_Blocked.md` with MSIX compatibility concerns.

## Sources

Competitors:
- https://github.com/d2phap/ImageGlass/releases
- https://github.com/nomacs/nomacs
- https://github.com/Ruben2776/PicView
- https://neeview.org/
- https://github.com/tannerhelland/photodemon
- https://github.com/qarmin/czkawka
- https://www.faststone.org/FSViewerDetail.htm
- https://www.irfanview.com/history_old.htm
- https://www.xnview.com/wiki/index.php?title=How_to_batch_convert_and_batch_process_with_XnView_MP
- https://en.eagle.cool/article/505-search-by-color
- https://www.acdsee.com/en/photo-studio/whats-new/

Commercial/AI:
- https://helpx.adobe.com/lightroom-classic/help/assisted-culling.html
- https://support.captureone.com/hc/en-us/articles/35747427882653-Capture-One-16-8-release-notes
- https://mylio.com/face-recognition/
- https://lifeafterphotoshop.com/dxo-photolab-9-review/

Platform and ecosystem:
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview
- https://github.com/dlemstra/Magick.NET/releases
- https://ghostscript.readthedocs.io/en/latest/News.html
- https://spec.c2pa.org/specifications/specifications/2.4/index.html
- https://github.com/actions/runner-images/issues/14017
- https://sqlite.org/releaselog/current.html
- https://advisories.gitlab.com/nuget/sharpcompress/CVE-2026-44788/

Security:
- https://security.snyk.io/vuln/SNYK-DOTNET-MAGICKNETQ8ANYCPU-10905670
- https://security.snyk.io/vuln/SNYK-DOTNET-MAGICKNETQ8ANYCPU-15792499

Adjacent:
- https://github.com/ibaaj/awesome-OpenSourcePhotography
- https://github.com/meichthys/foss_photo_libraries

## Community Signals

- **Format gaps are the #1 complaint** in Reddit/HN discussions about Windows image viewers. WPF's native `BitmapDecoder` only handles GIF/ICO/JPEG/PNG/TIFF/WMP. Users expect AVIF/HEIC/JXL/WebP out of the box. Images already solves this via Magick.NET fallback, but users need to discover it. Source: dotnet/wpf#6337, r/windows threads.
- **Performance expectations**: Benchmarks from power-user forums show 84ms to display an image and 143ms to enumerate 30,000 files as the acceptable bar. Users explicitly leave Windows Photos over slow startup and crash-on-large-images. Source: DonationCoder benchmarks.
- **Sorting fidelity** with Windows Explorer's natural sort order is a surprisingly common friction point. Images already uses `StrCmpLogicalW` via P/Invoke for natural sort — this is a solved differentiator worth highlighting.
- **WCAG 2.2** (ISO/IEC 40500:2025) adds criteria most image viewers ignore: 2.5.7 (drag alternatives for every drag operation), 2.5.8 (24x24 CSS px minimum target sizes), and 2.4.13 (visible focus indicators). Images has strong UIA peers and focus styles but should audit minimum target sizes and drag alternatives for crop/pan/brush tools.
- **C2PA/Content Credentials**: Samsung Galaxy S25 and Google Pixel 10 sign photos by default. EU AI Act Article 50 enforcement begins August 2026. Being the first desktop viewer to show verified/AI-generated/edited badges is a strong differentiator. Images already has read-only C2PA inspection — this is correctly positioned.
- **Privacy-first trend**: Users are leaving Google Photos (2026 trend per community forums). A local-only viewer that indexes, tags, and searches without any network calls has a clear market position. Images is already here.
- **Comic/manga reading**: qView's most-upvoted feature request category. Requirements overlap with Images' existing archive book mode (CBZ/CBR/CB7, right-to-left page order, two-page spreads, reading progress tracking — all shipped).

## Open Questions

- Should the batch processor use a configurable concurrency level (e.g., `Environment.ProcessorCount - 1`) or a fixed cap (e.g., 4) to avoid starving the UI thread during large batch runs?
- Is the publisher willing to pin CI to `windows-2025-vs2026` immediately, or wait for the `windows-latest` migration to complete and fix any breakage reactively?
- Should WCAG 2.5.8 minimum target size compliance be tracked as a single audit pass or as incremental checks per-window?
