# Research — Images

## Executive Summary

Images is a Windows-only WPF/.NET 10 image viewer at v0.2.11, with ~62k lines of production code (48k C# + 14k XAML), 617 tests, 105 service classes, and a feature set that exceeds ImageGlass, nomacs, and most OSS competitors in breadth: OCR, semantic search, catalog, parallel batch processing, archive/comic reader, culling labels, export preview with draggable comparison divider, macro actions, non-destructive edit history, duplicate cleanup, file health scanning, edge-hover fullscreen panels, and privacy-first network transparency. No other desktop image viewer ships the combination of local OCR + semantic search + rebuildable catalog + inline rename + C2PA credential inspection.

The project's strongest current shape is as a trustworthy local power-user viewer with the Windows 7 Photo Viewer aesthetic in Catppuccin Mocha. The v1.0 milestone is blocked only on code signing (D-05) and WinGet publication (D-02) — both credential-gated.

All NuGet dependencies are at their latest versions with no outstanding CVEs. The ResourceLimits gap remains the only P0 security item.

Top opportunities in priority order:
1. Set Magick.NET ResourceLimits to prevent memory-bomb attacks (the only remaining P0 — CVE items are resolved)
2. Fix PreloadService cache poisoning on faulted Lazy (existing audit item)
3. Close remaining 21 untested service test gaps (ImageLoader, OcrService highest priority)
4. Add touch/gesture input via DirectManipulation COM interop (the correct WPF approach)
5. Benchmark and optimize cold-start performance (IrfanView/FastStone sub-second bar)
6. Detect and prefer Windows 11 24H2 native JPEG XL WIC codec when available
7. Log tile processing errors in TileService instead of swallowing silently
8. Complete WCAG 2.5.7 drag alternatives audit for crop/pan/brush tools

## Product Map

- **Core workflows**: Open/navigate image folders and archives; inline rename with debounced auto-save; review-label culling (1-5 star, pick/reject); inspect metadata/OCR/codecs/network activity; apply non-destructive edits (levels, curves, HSL, dodge/burn, red-eye, clone/heal, resize, perspective); parallel batch convert/export with preview; import, dedupe, catalog, and semantic-search local collections.
- **User personas**: Windows power user replacing Photos/IrfanView/FastStone; privacy-sensitive organizer who needs offline-only workflows; creator who needs repeatable batch/export pipelines; archive/comic reader; local-AI early adopter who wants explicit model provenance.
- **Platforms and distribution**: Windows 10/11 desktop, WPF on `net10.0-windows10.0.22621.0`, GitHub release ZIP and Inno installer (self-contained), generated WinGet/Scoop manifests, CycloneDX SBOM, GitHub artifact attestations. Store/signing tracks blocked in `Roadmap_Blocked.md`.
- **Key integrations**: WIC/Magick.NET 14.14.0/SharpCompress 0.49.1/Ghostscript/jpegtran decode and writeback; SQLite settings/catalog/semantic indexes; XMP sidecars for portable review metadata; Windows.Media.Ocr; ONNX Runtime 1.24.4 DirectML with CLIP ViT-B/32 semantic search seam; GitHub Releases update check (opt-in only).

## Competitive Landscape

### ImageGlass (d2phap/ImageGlass) — 13.5k stars
v10 is still **beta** (Beta 2, June 2026); v9.4.1.15 (Jan 2026) is the latest stable. The Avalonia rewrite adds HDR tone-mapping, SVG/SMIL animation, and the new **ImageGlass.SDK** plugin system with in-process codec plugins and out-of-process IPC tools declared via `igplugin.json`. No GA date announced. Learn from: plugin SDK architecture and HDR tone-mapping. Avoid: unsigned binaries and ambiguous paid-tier semantics. Source: imageglass.org/news roadmap-2026, beta-2 announcement.

### NeeView (neelabo/NeeView) — 879 stars, newly OSS
v45.3 stable (March 2026), v46.0-Alpha.6 in preview. Same WPF/.NET 10 stack. **Correction from prior research**: NeeView does NOT have first-class touch/gesture support — it relies on standard WPF touch-to-mouse translation, same as any WPF app. Best-in-class book/comic reading UX: dual-page mode, recursive archive browsing, playlist navigation, script customization. Microsoft Store presence. No OCR, no search, no catalog, no editing. Learn from: book UX depth and scripting. Source: NeeView GitHub releases, documentation.

### PicView (Ruben2776/PicView) — 3.3k stars
Avalonia-based, cross-platform (Windows/macOS). v4.2 (March 2026). Uses **SignPath Foundation** for free OSS code signing — the same path available to Images (D-05a in `Roadmap_Blocked.md`). Ground-up v5.0 rewrite in progress. Learn from: SignPath signing pipeline. Source: PicView GitHub, picview.org.

### FlyPhotos (riyasy/FlyPhotos) — ~454 stars, NEW
WinUI 3 / Win2D / native AOT. Lightweight Picasa successor leveraging WIC codecs for JXL, AVIF, HEIC, RAW out of the box. Worth monitoring as a fast-startup competitor — native AOT gives it near-instant launch. Not yet feature-competitive with Images. Source: GitHub riyasy/FlyPhotos.

### FastStone Image Viewer — commercial freeware
Edge-hover fullscreen panels with zero-chrome viewing. 4-up synchronized comparison. Batch processing handles resize/rotate/crop/DPI/watermark/border in one pass. Learn from: established batch processing depth. Images now has edge-hover panels (shipped in commit `5ed2a49`).

### digiKam 9.1.0 (June 2026) — OSS, Qt6
Jumped from 8.x to 9.0 with Qt6 migration. Face pipeline rewrite (8.6): KNN+SVM classifiers, YuNet/SFace, FIQA gating, 25-50% speedup. The most mature OSS face recognition in the desktop photo space. Learn from: face pipeline architecture when V60-03 ships. Source: digikam.org announcements.

### ACDSee Photo Studio Ultimate 2026 — commercial
AI Denoise, AI Hair, AI Presets combining adjustments with AI masking (Background, Portrait, Sky, Subject). JXL and AVIF support added. Multi-threaded Activity Manager for background batch ops. Learn from: AI preset concept for the export/batch pipeline. Source: acdsee.com/whats-new.

## Security, Privacy, and Reliability

### Verified — All Dependencies at Safe Versions

- **Magick.NET 14.14.0**: Bundles ImageMagick 7.1.2-25 (June 2026) with fixes for 12 security issues including CVE-2026-25797 (RCE via PostScript), CVE-2026-46557 (stack overflow in fx), CVE-2026-23952 (NULL deref in MSL). Bundles libheif 1.23.0 (above 1.22.2 floor for CVE-2026-32740), aom 3.14.1, libde265 1.1.0, openexr 3.4.12. **All previously flagged CVEs are patched at the current package version.** Source: Magick.NET 14.14.0 release notes, Snyk.
- **SQLitePCLRaw 3.0.3**: Depends on SourceGear.sqlite3 >= 3.50.4.5, meaning bundled SQLite is 3.50.4+ — well above the 3.50.2 floor for CVE-2025-6965. **No action needed.** Source: NuGet dependency chain, Broadcom advisory.
- **SharpCompress 0.49.1**: CVE-2026-44788 (zip-slip) affects versions 0.43.0-0.47.4, fixed in 0.48.0. **Current version is patched.** Source: Snyk, GitHub advisory GHSA-6c8g-7p36-r338.
- **Microsoft.ML.OnnxRuntime.DirectML 1.24.4**: At latest version. 1.24.x included fix for out-of-bounds read in ArrayFeatureExtractor. No new advisories. Source: NuGet.
- **Serilog 4.3.1, Serilog.Extensions.Logging 10.0.0, Serilog.Sinks.File 7.0.0**: All at latest, no CVEs. Source: NuGet.
- **Microsoft.Data.Sqlite 10.0.9**: At latest (released June 2026). No advisories. Source: NuGet.

**Consequence**: The two P0 "verify bundled codec versions" and P1 "validate SQLitePCLRaw" items in the existing ROADMAP.md Research-Driven Additions are already resolved. They should be removed from the roadmap.

### Remaining Security Gap

- **No Magick.NET ResourceLimits configured**: `ImageLoader.cs` and all Magick.NET call sites set no memory, width, height, or area limits. CVE-2026-25985 demonstrates a 674 GB allocation attack via crafted SVG. `ResourceLimits.Memory`, `ResourceLimits.Width`, `ResourceLimits.Height` should be set at app startup. Zero `ResourceLimits` hits in codebase. Source: CVE-2026-25985, code inspection.

### Code Quality Observations

- **TileService bare catches**: `TileService.cs` lines 314, 474, 515 have bare `catch { }` blocks that silently swallow tile processing errors. Unlike `process.Kill()` or `File.Delete()` best-effort catches (which are appropriate), tile decode errors should be logged so users can diagnose display issues with specific image formats.
- **Ghostscript 10.07.0 bundled**: Now two versions behind 10.08.0. Already tracked in `Roadmap_Blocked.md` — cannot unblock without downloading the binary.
- **WIC CVE-2025-50165**: Already tracked as S-06 in `Roadmap_Blocked.md`.

## Architecture Assessment

- **Codebase**: 48,381 lines C# + 13,897 lines XAML across 105 service files, 32 XAML files, 7 controllers. 13,905 lines of test code (617 test methods). 100 test files.
- **MainViewModel.cs** at 7,263 lines with ~159 public commands. 7 controllers factored out. Further decomposition is optional.
- **Test coverage**: 21 of 105 services lack dedicated test files. Full list: `ChannelIsolationService`, `ChannelMode`, `CliReport`, `ClipEmbeddingProvider`, `ClipboardService`, `CodecRuntime`, `CrashLog`, `ImageEventSource`, `ImageLoader` (796 lines), `ImageMetadataService`, `Log`, `MonitorService`, `OcrCapabilityService`, `OcrService`, `OnnxRuntimeService`, `PreloadService`, `PrintService`, `ShellChangeNotificationService`, `ShellIntegration`, `StoreExtensionService`, `WindowChrome`. Most critical: `ImageLoader` (core decode path, 796 lines) and `OcrService` (user-facing feature).
- **Touch/gesture input**: Zero ManipulationDelta, Stylus, or touch handling in codebase. **Correction**: NeeView also lacks first-class touch — it uses standard WPF touch-to-mouse translation. The correct WPF approach for smooth pinch-to-zoom is the Win32 `IDirectManipulationManager` COM interop (per Garuma/blog.neteril.org), not WPF's ManipulationDelta which predates precision trackpads. Source: blog.neteril.org, dev.to/garuma.
- **Magick.NET resource limits**: No `ResourceLimits`, `MagickNET.Initialize()`, or `OpenCL` calls anywhere. SVG/PS input can trigger unbounded memory allocation.
- **MotionPhotoService**: Detects and extracts embedded MP4 byte ranges but does not play video. WPF's `MediaElement` wraps Windows Media Foundation and handles MP4/H.264 if OS codecs are present. For robust playback, FFME (FFmpeg-based MediaElement replacement) is the WPF standard. Source: github.com/unosquare/ffmediaelement.
- **Batch processing**: Parallel with `SemaphoreSlim`-bounded concurrency (commit `4c929f9`). Resolved.
- **Localization**: Infrastructure complete but only English locale exists. Crowdin blocked on I-02.
- **Dispose patterns**: 11 of 105 services implement IDisposable (51 total Dispose references). The `PreloadService` has the most complex disposal (12 references) — the `Lazy<Task>` cache poisoning bug (existing audit item) is confirmed in the code at line 75.
- **Sealed classes**: 230 sealed declarations vs 199 total class declarations — good default-sealed discipline.
- **Windows JPEG XL WIC codec**: Windows 11 24H2 shipped a native JXL WIC codec extension (v1.2.36.0). Images could detect it and prefer WIC over Magick.NET for .jxl, getting native OS decode performance and automatic Explorer thumbnail support. Source: WindowsForum, gHacks.

### .NET 10 / WPF Platform Notes

- **WPF .NET 10**: BitmapMetadata bug fixes and null bitmap stream crash fix (directly relevant). Fluent theme expansion. Grid shorthand syntax. No new GPU pipeline, touch APIs, or media stack. Source: MS Learn whats-new/net100.
- **.NET 10 JIT**: ~11% throughput gain, Span operations 11x faster, delegate stack alloc 19.5ns→6.7ns, doubled inlining budget, try/finally inlining. Source: .NET Blog performance-improvements-net-10.
- **SkiaSharp 4.0**: Shipped with Skia M147 (2.5 years of upstream improvements). Uno Platform joined as co-maintainer. Relevant when V20-01 canvas swap activates. Source: .NET Blog.
- **ImageSharp 4.0.0** (May 2026): Vector512 SIMD, decode-time color conversion, expanded metadata APIs, faster JPEG hot paths. Could improve ThumbnailCache generation. Source: SixLabors announcement.

## Rejected Ideas

- **Cross-platform (Avalonia/MAUI)**: Rejected. Images has 14k lines of XAML, custom UIA peers, Win32 P/Invoke. Migration is a rewrite, not a port. WPF on .NET 10 is actively maintained. Source: ImageGlass v10 beta (Avalonia), NeeView (stays WPF).
- **Cloud sync, accounts, multi-user**: Rejected per project philosophy and privacy policy. Source: Mylio/Immich.
- **Full Photoshop-class editor**: Rejected. Viewer-first with non-destructive edit capabilities. Source: PhotoDemon.
- **AI denoise/masking/upscaling as immediate work**: Rejected until V60-01 inference runtime ships. Source: Lightroom AI denoise, ACDSee AI Denoise.
- **Video playback (full)**: Rejected per codec-support-policy.md. Motion photo MP4 preview is a separate, smaller scope item. Source: IrfanView plugin.
- **Tabbed MDI windows**: Rejected. `SessionTrayService` serves the multi-folder use case. Source: ACDSee.
- **TWAIN scanner integration**: Already tracked as V30-24 in `Roadmap_Blocked.md`. Source: IrfanView/Saraff.Twain.NET.
- **Native AOT compilation**: Rejected. FlyPhotos uses native AOT for instant startup, but WPF does not support AOT compilation. Source: FlyPhotos GitHub.
- **Anti-AI development stance**: Rejected. PhotoDemon adopted "no-AI/no-LLM" (June 2026). Images' position is local-only AI with explicit model provenance. Source: PhotoDemon repository.

## Sources

Competitors:
- https://github.com/d2phap/ImageGlass/releases
- https://imageglass.org/news/imageglass-roadmap-update-2026-98
- https://github.com/neelabo/NeeView/releases
- https://github.com/Ruben2776/PicView
- https://github.com/riyasy/FlyPhotos
- https://github.com/tannerhelland/photodemon
- https://www.faststone.org/FSViewerDetail.htm
- https://www.digikam.org/news/2026-06-07-9.1.0_release_announcement/
- https://www.acdsee.com/en/photo-studio/whats-new/

Commercial/AI:
- https://helpx.adobe.com/lightroom-classic/help/assisted-culling.html
- https://excire.com/en/lightroom-classic-ai-culling-vs-excire-search-plugin/
- https://www.photoworkout.com/best-software-organize-photos-windows/

Security:
- https://github.com/dlemstra/Magick.NET/releases/tag/14.14.0
- https://github.com/advisories/GHSA-6c8g-7p36-r338
- https://knowledge.broadcom.com/external/article/405851/sqlite-vulnerability-cve20256965.html
- https://advisories.gitlab.com/nuget/sqlitepclraw.lib.e_sqlite3/CVE-2025-6965/

Platform and ecosystem:
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/
- https://sixlabors.com/posts/announcing-imagesharp-400/
- https://devblogs.microsoft.com/dotnet/welcome-to-skia-sharp-40-preview1/
- https://dev.to/garuma/using-directmanipulation-with-wpf-247k
- https://github.com/unosquare/ffmediaelement
- https://windowsforum.com/threads/windows-11-24h2-update-embracing-jpeg-xl-for-enhanced-imaging.355915/

Community:
- https://github.com/d2phap/ImageGlass/issues/794
- https://aidigitalspace.com/offline-ai-tools/
- https://www.microfournerds.com/blog/sick-of-paying-monthly-best-lightroom-alternatives-2025
- https://news.ycombinator.com/item?id=46794971

## Open Questions

- Should Magick.NET ResourceLimits be configurable per-user (Settings) or hardcoded at safe defaults?
- Should the touch/gesture implementation use DirectManipulation COM interop (smooth, precision-trackpad-aware) or WPF ManipulationDelta (simpler, touchscreen-only)?
- Should WCAG 2.5.7 drag alternatives be tracked as one audit pass or per-tool?
