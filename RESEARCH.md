# Research — Images

## Executive Summary

Images is a Windows-only WPF/.NET 10 image viewer at v0.2.11, with ~62k lines of production code (48k C# + 14k XAML), 617 tests, 105 service classes, and a feature set that exceeds ImageGlass, nomacs, and most OSS competitors in breadth: OCR, semantic search, catalog, parallel batch processing, archive/comic reader, culling labels, export preview with draggable comparison divider, macro actions, non-destructive edit history, duplicate cleanup, file health scanning, edge-hover fullscreen panels, and privacy-first network transparency. No other desktop image viewer ships the combination of local OCR + semantic search + rebuildable catalog + inline rename + C2PA credential inspection.

The project's strongest current shape is as a trustworthy local power-user viewer with the Windows 7 Photo Viewer aesthetic in Catppuccin Mocha. The v1.0 milestone is blocked only on code signing (D-05) and WinGet publication (D-02) — both credential-gated.

Top opportunities in priority order:
1. Verify Magick.NET bundled codec versions against critical 2026 CVEs (libheif, libjxl, ImageMagick)
2. Set Magick.NET ResourceLimits to prevent memory-bomb attacks via crafted SVG/PS input
3. Close remaining 21 untested service test gaps (ImageLoader, PreloadService, OcrService highest priority)
4. Add touch/gesture input for pan/zoom/navigate (NeeView differentiator, tablet users)
5. Benchmark and optimize cold-start performance (IrfanView/FastStone sub-second bar)
6. Add motion photo embedded video playback (ImageGlass 10 now ships this)
7. Complete WCAG 2.5.7 drag alternatives audit for crop/pan/brush tools
8. Validate SQLitePCLRaw embedded SQLite version against CVE-2025-6965

## Product Map

- **Core workflows**: Open/navigate image folders and archives; inline rename with debounced auto-save; review-label culling (1-5 star, pick/reject); inspect metadata/OCR/codecs/network activity; apply non-destructive edits (levels, curves, HSL, dodge/burn, red-eye, clone/heal, resize, perspective); parallel batch convert/export with preview; import, dedupe, catalog, and semantic-search local collections.
- **User personas**: Windows power user replacing Photos/IrfanView/FastStone; privacy-sensitive organizer who needs offline-only workflows; creator who needs repeatable batch/export pipelines; archive/comic reader; local-AI early adopter who wants explicit model provenance.
- **Platforms and distribution**: Windows 10/11 desktop, WPF on `net10.0-windows10.0.22621.0`, GitHub release ZIP and Inno installer (self-contained), generated WinGet/Scoop manifests, CycloneDX SBOM, GitHub artifact attestations. Store/signing tracks blocked in `Roadmap_Blocked.md`.
- **Key integrations**: WIC/Magick.NET 14.14.0/SharpCompress 0.49.1/Ghostscript/jpegtran decode and writeback; SQLite settings/catalog/semantic indexes; XMP sidecars for portable review metadata; Windows.Media.Ocr; ONNX Runtime 1.24.4 DirectML with CLIP ViT-B/32 semantic search seam; GitHub Releases update check (opt-in only).

## Competitive Landscape

### ImageGlass (d2phap/ImageGlass) — 13.5k stars
v10 Beta 2 (June 2026) is a full **Avalonia UI rewrite** with cross-platform support. New: native HDR tone-mapping for AVIF/JXL/HDR/EXR, SVG/SVGZ vector rendering with animated SMIL, ImageGlass.SDK plugin system with out-of-process IPC, motion photo video playback, gallery persistent cache. The biggest strategic threat long-term. Learn from: plugin SDK architecture, HDR tone-mapping, motion photo playback. Avoid: unsigned binaries and ambiguous paid-tier semantics.

### NeeView (neelabo/NeeView) — 879 stars, newly OSS
Newly open-sourced January 2025. Same WPF/.NET 10 stack. v46 Alpha 7 (June 20, 2026). Best-in-class book/comic reading UX: dual-page mode, recursive archive browsing, touch/gesture input, Susie plugin support, playlist navigation, script customization. Microsoft Store presence. No OCR, no search, no catalog, no editing. Learn from: touch/gesture support and scripting. Avoid: Japanese-centric defaults.

### PicView (Ruben2776/PicView) — 3.3k stars
Avalonia-based, cross-platform (Windows/macOS). v4.2 (March 2026). Signed binaries, image effects window with presets, side-by-side viewing, reactive programming, multi-language. Learn from: binary signing approach (SignPath Foundation for OSS). Avoid: slow release cycle.

### FastStone Image Viewer — commercial freeware
Edge-hover fullscreen panels with zero-chrome viewing. 4-up synchronized comparison. Batch processing handles resize/rotate/crop/DPI/watermark/border in one pass. Learn from: established batch processing depth. Images now has edge-hover panels (shipped in commit `5ed2a49`).

### XnView MP — commercial freeware (personal use)
SQLite-backed catalog with hierarchical category tree, drag-drop batch assignment. Deepest metadata editor in the freeware space. Smart folders auto-populate by criteria. Learn from: category tree sidebar and metadata editor depth. Images has smart collections (commit `56c4578`) and catalog (commit `e9297b8`).

### nomacs (nomacs/nomacs) — 3k stars
Qt6 port complete and stable. v3.22.1 (April 2026). Active translation community. OpenCV-based image processing. Plugin system. Learn from: localization community management.

### JPEGView (sylikc/jpegview) — 2.9k stars
**Inactive since August 2024.** No 2025-2026 updates. Not a competitive concern.

## Security, Privacy, and Reliability

### Critical — Action Required

- **libheif CVE-2026-32740 (CVSS 8.8)**: Heap-buffer-overflow in chroma plane allocation via crafted HEIF/AVIF. Multiple related CVEs (CVE-2026-32738/32739/32741/32814/32882/41069/41071). Fixed in libheif 1.22.2+. Magick.NET 14.14.0 bundles libheif — **must verify bundled version is >= 1.22.2**. Source: Snyk advisory database.
- **libjxl CVE-2026-1837**: Use-after-free in grayscale color transformation. CVE-2025-70103 adds a heap overflow in PNM decoding. **Must verify bundled libjxl version** in Magick.NET. Source: Snyk advisory database.
- **ImageMagick CVE-2026-25797**: RCE via PostScript injection (insufficient sanitization in PS header generation). Fixed in ImageMagick 7.1.2-15. CVE-2026-46557 (stack overflow in fx, fixed in Magick.NET 14.13.1). **Verify Magick.NET 14.14.0 includes both fixes.** Source: Snyk advisory database.
- **No Magick.NET ResourceLimits configured**: `ImageLoader.cs` and all Magick.NET call sites set no memory, width, height, or area limits. CVE-2026-25985 demonstrates a 674 GB allocation attack via crafted SVG. `ResourceLimits.Memory`, `ResourceLimits.Width`, `ResourceLimits.Height` should be set at app startup. Source: CVE-2026-25985, code inspection.

### High — Validate

- **SQLitePCLRaw CVE-2025-6965**: Bundled SQLite before 3.50.2 has memory corruption risk. SQLitePCLRaw 3.0.3 **should** bundle a patched version, but the embedded `e_sqlite3` native library version must be confirmed. Source: NVD.
- **WIC CVE-2025-50165**: Uninitialized function pointer in JPEG 12/16-bit compression path. Triggered during re-encoding, not decoding. Patched in Windows builds 10.0.26100.4946+. Already tracked as S-06 in `Roadmap_Blocked.md`. Source: ESET.
- **ImageMagick CVE-2026-23876 (XBM heap overflow), CVE-2026-23952 (MSL NULL deref)**: DoS or memory corruption through crafted images. Magick.NET policy should restrict or sandbox XBM/MSL input. Source: Snyk.

### Verified — Previously Fixed

- **Verified (fixed)**: CI pinned to `windows-2025` (commit `3b04fda`). Prior risk of `windows-latest` migration breaking builds is resolved.
- **Verified (fixed)**: Batch processing now uses `SemaphoreSlim`-bounded parallelism (commit `4c929f9`). Prior concern about sequential processing is resolved.
- **Verified (fixed)**: WCAG 2.5.8 undersized interactive chips fixed (commit `e2e2bfe`). 24x24 minimum target sizes addressed.
- **Verified (fixed)**: Edge-hover fullscreen panels shipped (commit `5ed2a49`).
- **Verified (fixed)**: Draggable comparison divider added to export preview (commit `e19dcad`).
- **Verified (fixed)**: Color palette extraction added to catalog (commit `23a8ffe`).
- **Verified (fixed)**: Embedded-JPEG-first RAW preview shipped (commit `c0fd6d8`).
- **Verified (fixed)**: 89 regression tests added for 11 previously untested services (commit `145603f`).
- **Verified (active)**: SharpCompress CVE-2026-44788 (zip-slip, CVSS 5.9). Images uses read-only archive streams and does **not** call `WriteToDirectory`. Not reachable. SharpCompress 0.49.1 pinned.
- **Verified (active)**: Ghostscript 10.07.0 bundled, now two versions behind 10.08.0. Already tracked in `Roadmap_Blocked.md`. Cannot unblock without downloading the binary.

## Architecture Assessment

- **Codebase**: 48,381 lines C# + 13,897 lines XAML across 105 service files, 32 XAML files, 7 controllers. 13,905 lines of test code (617 test methods).
- **MainViewModel.cs** at 7,263 lines with ~159 public commands. 7 controllers factored out (OCR, C2PA, color analysis, update check, folder preview, photo metadata, external edit reload). Further decomposition is optional.
- **Test coverage**: 21 of 105 services lack dedicated test files: `ChannelIsolationService`, `ChannelMode`, `CliReport`, `ClipEmbeddingProvider`, `ClipboardService`, `CodecRuntime`, `CrashLog`, `ImageEventSource`, `ImageLoader` (796 lines), `ImageMetadataService`, `Log`, `MonitorService`, `OcrCapabilityService`, `OcrService`, `OnnxRuntimeService`, `PreloadService`, `PrintService`, `ShellChangeNotificationService`, `ShellIntegration`, `StoreExtensionService`, `WindowChrome`. The most critical gaps are `ImageLoader` (core decode path) and `OcrService` (user-facing feature).
- **Largest untested services by line count**: `ImageLoader` (796), `CodecRuntime` (estimated ~200), `OcrService` (estimated ~200), `ClipEmbeddingProvider` (estimated ~200).
- **Touch/gesture input**: No `ManipulationDelta`, `Stylus`, or WPF touch-input handling anywhere in the codebase. Pan/zoom is mouse-only. NeeView has touch/gesture support — this is a gap for tablet and touchscreen Windows users.
- **Magick.NET resource limits**: No `ResourceLimits` calls anywhere. SVG/PS input can trigger unbounded memory allocation.
- **MotionPhotoService**: Detects and extracts embedded MP4 byte ranges but does not play or preview video content. ImageGlass 10 now plays motion photo video inline.
- **Batch processing**: Now parallel with `SemaphoreSlim`-bounded concurrency (commit `4c929f9`, `BatchProcessorService.cs` line 193). This resolves the prior sequential-processing concern.
- **Localization**: Infrastructure complete (CI parity checks, `LocExtension`, typed accessors) but only English locale exists. Crowdin blocked on I-02.
- **CatalogService.cs** (1,090 lines): Incremental rescan operational (commit `e9297b8`). Color palette extraction added (commit `23a8ffe`, v1→v2 migration).
- **Theme system**: Three themes (Mocha dark, Latte light, SystemColors high-contrast) with Follow System mode. DynamicResource throughout. No token drift detected.

## Rejected Ideas

- **Cross-platform (Avalonia/MAUI)**: Rejected. ImageGlass v10 bets on Avalonia; NeeView stays WPF. Images has 14k lines of XAML, custom UIA peers, Win32 P/Invoke for dark caption/wallpaper/minidump. Migration is a rewrite, not a port. WPF on .NET 10 is actively maintained. Source: ImageGlass v10 beta, NeeView v46 alpha.
- **Cloud sync, accounts, multi-user**: Rejected per project philosophy and privacy policy. 78% of surveyed users refuse cloud AI features. Source: Mylio/Immich/PhotoPrism, AI Digital Space survey 2026.
- **Full Photoshop-class editor**: Rejected. Images should stay viewer-first with non-destructive edit capabilities. Source: PhotoDemon (200+ filters but became an editor, not a viewer).
- **AI denoise/masking/upscaling as immediate work**: Rejected until V60-01 inference runtime ships (blocked in `Roadmap_Blocked.md`). Source: Lightroom AI denoise, Capture One Enhanced Denoise.
- **Video playback**: Rejected per codec-support-policy.md tier table. Motion photo video preview (extracting and playing the embedded MP4 segment) is a separate, smaller scope item. Source: IrfanView plugin, ImageGlass 10 motion photo.
- **Tabbed MDI windows**: Rejected. `SessionTrayService` (cross-folder file list) serves the same multi-folder use case without UI complexity. Source: ACDSee.
- **TWAIN scanner integration**: Already tracked as V30-24 in `Roadmap_Blocked.md`. Source: IrfanView/Saraff.Twain.NET.
- **Anti-AI development stance**: Rejected. PhotoDemon explicitly adopted a "no-AI/no-LLM" policy (June 2026). Images' position is local-only AI with explicit model provenance — the right balance for privacy-conscious power users. Source: PhotoDemon repository.

## Sources

Competitors:
- https://github.com/d2phap/ImageGlass/releases
- https://github.com/neelabo/NeeView
- https://github.com/Ruben2776/PicView
- https://github.com/nomacs/nomacs
- https://github.com/tannerhelland/photodemon
- https://www.faststone.org/FSViewerDetail.htm
- https://www.irfanview.com/history_old.htm
- https://www.xnview.com/wiki/index.php?title=How_to_batch_convert_and_batch_process_with_XnView_MP
- https://en.eagle.cool/article/505-search-by-color

Commercial/AI:
- https://helpx.adobe.com/lightroom-classic/help/assisted-culling.html
- https://support.captureone.com/hc/en-us/articles/35747427882653-Capture-One-16-8-release-notes
- https://mylio.com/face-recognition/
- https://excire.com/en/best-photo-organizing-software/

Security:
- https://security.snyk.io/vuln/SNYK-DOTNET-MAGICKNETQ8ANYCPU-10905670
- https://security.snyk.io/vuln/SNYK-DOTNET-MAGICKNETQ8ANYCPU-15792499
- https://advisories.gitlab.com/nuget/sharpcompress/CVE-2026-44788/

Platform and ecosystem:
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview
- https://github.com/dlemstra/Magick.NET/releases
- https://ghostscript.readthedocs.io/en/latest/News.html
- https://spec.c2pa.org/specifications/specifications/2.4/index.html
- https://sqlite.org/releaselog/current.html

Community:
- https://www.dpreview.com/forums/threads/do-some-programs-support-heif.4770308/
- https://windowsforum.com/threads/fix-slow-photos-app-on-windows-11-tips-for-smooth-performance.344392/
- https://aidigitalspace.com/offline-ai-tools/
- https://www.microfournerds.com/blog/sick-of-paying-monthly-best-lightroom-alternatives-2025
- https://github.com/d2phap/ImageGlass/issues/794
- https://news.ycombinator.com/item?id=46794971

Adjacent:
- https://github.com/ibaaj/awesome-OpenSourcePhotography
- https://github.com/meichthys/foss_photo_libraries

## Community Signals

- **Format gaps remain the #1 complaint** about Windows image viewers. HEIC requires a paid codec extension; AVIF has rendering bugs above 4000px in many viewers; JXL support was only recently added to Windows Photos. Images already handles all of these via Magick.NET fallback — this is a strong solved differentiator worth highlighting in marketing. Source: DPReview forums, r/windows, gHacks.
- **Performance expectations are hardening**: IrfanView and FastStone are praised as "instant" openers. ImageGlass GitHub issue #794 specifically asks for faster launch times. Microsoft Photos is called "bloatware" (882 MB for a photo viewer). Users expect sub-second startup and smooth 10k+ folder scrolling. Images has `LaunchTiming` and `PerformanceBudgetService` infrastructure but no published benchmarks. Source: DonationCoder, Windows Forum, Eleven Forum.
- **Privacy-first is now mainstream**: 78% of surveyed users refuse cloud AI features; 91% would pay more for on-device processing. Microsoft Photos telemetry and Recall deepened distrust. Mylio and Excire built entire value propositions around "100% local" AI. Images' zero-network-egress default is perfectly positioned. Source: AI Digital Space survey 2026.
- **AI features wanted locally**: Natural-language photo search, face clustering, duplicate detection via visual similarity, and aesthetic scoring/culling. All cloud-skeptical. Images has the ONNX/CLIP foundation but it's blocked on V60-01. Source: Unite.AI, Reddit.
- **Emerging use case — AI art curation**: Generative AI users producing thousands of images need fast browse, aesthetic rating, and prompt-metadata search. No desktop tool does this well. Images' catalog + gallery + smart filters could serve this niche. Source: Reddit r/StableDiffusion.
- **Emerging use case — screenshot OCR search**: Power users accumulate thousands of screenshots with no organization. OCR-searchable screenshots is an unmet need. Images already has OCR + catalog — connecting them would be novel. Source: Reddit r/DataHoarder.
- **Subscription fatigue drives OSS adoption**: "Sick of paying monthly" is a literal article title about Lightroom alternatives. Free/OSS image management tools are seeing increased interest. Source: Micro Four Nerds.
- **WCAG 2.2** became ISO/IEC 40500:2025. Key criteria: 2.5.7 (drag alternatives), 2.5.8 (24x24 target sizes — now fixed), 2.4.13 (focus indicators — already strong). 2.5.7 drag alternatives still need audit for crop/pan/brush tools. Source: W3C WCAG 2.2.
- **C2PA/Content Credentials momentum**: Samsung Galaxy S25 and Google Pixel 10 sign photos by default. EU AI Act Article 50 enforcement begins August 2026. Images' read-only C2PA inspection via c2patool is correctly positioned ahead of the market. Source: C2PA spec 2.4.

## Open Questions

- Should Magick.NET ResourceLimits be configurable per-user (Settings) or hardcoded at safe defaults (e.g., 2 GB memory, 32768 px max dimension)?
- What is the actual bundled libheif/libjxl version inside Magick.NET 14.14.0? Needs a runtime probe or NuGet package inspection to confirm CVE coverage.
- Should WCAG 2.5.7 drag alternatives (crop, pan, brush) be tracked as one audit pass or per-tool?
