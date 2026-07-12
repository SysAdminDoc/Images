# Research - Images
Date: 2026-07-12 - replaces all prior research.

## Executive Summary
Verified: Images is a Windows-only, local-first WPF/.NET 10 image viewer/workbench (v0.2.25) with broad local codec support (WIC-first, Magick.NET fallback), archive/book viewing, inline rename, metadata/provenance inspection, SQLite settings/catalog, optional local ONNX models, recovery tooling, and offline-first privacy defaults. Its strongest shape is a trustworthy Windows power viewer, and the current direction is deliberate debloat (quiet premium main surface, advanced tools discoverable but off it) plus data-safety hardening. The prior research pass (2026-07-09) and the 70-item AUD deep-audit backlog already cover the internal defect surface; this pass adds a competitively-grounded feature/format/security layer. The highest-value net-new work, in order: (1) upgrade Magick.NET 14.14.0→14.15.0 for the libheif/ImageMagick 2026 heap-overflow CVEs on the exact fallback decode path used for untrusted HEIF/AVIF; (2) real display color management (honor embedded ICC, transform to the active monitor profile) — the single most-repeated, still-unsolved complaint across nomacs/PicView/ImageGlass; (3) verify/preserve metadata on Save-a-copy re-encode (WPF `BitmapEncoder` silently drops EXIF/ICC); (4) quiet-premium viewer comfort wins that competitors ship and Images lacks — checkerboard transparency backdrop, zoom-lock across navigation, hold-to-loupe magnifier, live cursor pixel readout, zoom-to-selection; (5) auto session restore and end-of-list behavior options.

## Product Map
- Core workflows: open files/folders/sessions/archives/books; view/navigate; inline rename; inspect metadata/provenance; compare (side-by-side + opacity overlay + difference — already implemented); export/batch; local catalog search; recover destructive actions.
- User personas: Windows power users replacing Photos/ImageGlass/FastStone; photographers/archivists in local folders; technical users who value portable artifacts, checksums, runtime provenance, and visible network behavior.
- Platforms and distribution: Windows 10/11 x64 WPF targeting `net10.0-windows10.0.22621.0`; MIT; Inno installer + portable ZIP; local release scripts (SBOM/provenance, WinGet/Scoop manifests); GitHub Releases primary channel.
- Key integrations and data flows: WIC first, Magick.NET fallback (JXL/AVIF/HEIC/PSD/RAW/etc.); SharpCompress read-only archives; SQLite settings/catalog/cache; XMP sidecars; optional Ghostscript/jpegtran/ExifTool/c2patool/OCR/ONNX-DirectML local paths; opt-in GitHub release checks logged by `NetworkEgressService`.

## Competitive Landscape
- ImageGlass 10 (imageglass.org, d2phap/ImageGlass): the Windows-viewer benchmark. Learn from Explorer-identical sort (already matched via `ExplorerLike`), per-monitor ICC intent, HDR tone-map (beta), selectable interpolation, gallery cache. Note its live open bugs are the open lanes: #1433 color-profile not respected, #496 save-loses-metadata, #1958 config resets, #254 archive browsing (top FR). Avoid its WebView2 dependency and dated-perf reputation.
- PicView (Ruben2776/PicView): fast minimal viewer. Learn zoom-lock across images (4.1), Ctrl+F in-folder search, hover/auto-hide chrome, drag-move-vs-pan disambiguation. Open FRs signal gaps: #151 pixel info, #222 animation speed, #201 color management, #257 animated AVIF.
- nomacs (nomacs/nomacs): synced-pan/zoom compare is the gold standard (Images already has compare). Its long-open, never-shipped requests are opportunity: #459 color management, #607 always-on-top frameless (16 reactions), #285 HDR/high-bit. Maintainer-starved (#987) — those lanes stay open.
- JPEGView (sylikc/jpegview): zoom-to-selection, embedded ICC honoring, borderless/always-on-top, checkerboard (#43 top-voted), remember-sort (#216). A dense reference for viewer ergonomics to selectively borrow without its dated UI.
- FastStone / Honeyview / XnView MP: edge-hover fly-out panels (FastStone's signature quiet-UI pattern; Images already collapses side panel in fullscreen), loupe magnifier, in-archive paging (Honeyview). Match durable comfort, not their dense IA.
- Windows 11 Photos (the incumbent being replaced): loudest complaints are slow cold start, background autostart, cloud nagging, and wrong colors/brightness under HDR. Images already wins on start-up/privacy; correct color is the remaining wedge.

## Security, Privacy, and Reliability
- Verified: `src/Images/Images.csproj` pins Magick.NET 14.14.0; 14.15.0 is current and bundles libheif ≥1.22.0 (fixes CVE-2026-32740, heap write on crafted HEIF/AVIF grid tiles, CVSS 8.8) plus 2026 ImageMagick heap/OOB-write CVEs (MAT/YUV/Sun/XBM/MIFF). These are on the exact untrusted-file fallback decode path. AnyCPU/64-bit already sidesteps the 32-bit-only variants. Upgrade is both feature and security.
- Verified: `src/Images/Services/ImageLoader.cs` performs no ICC `ColorContext` read or display-profile transform. Embedded profiles and wide-gamut/P3 monitors render uncorrected (over-saturated). WPF assumes sRGB output and never applies the monitor profile. Magick.NET embeds lcms2 (`TransformColorSpace`) for a pragmatic bake-into-display fix; this is distinct from the blocked OCIO/ACES pro pipeline (V80-26).
- Verified: Save-a-copy (v0.1.6 E6) re-encodes via WPF `BitmapEncoder`, which does not copy EXIF/IPTC/XMP/ICC unless explicitly threaded through — likely silent metadata loss (the ImageGlass #496 failure mode). Needs behavior verification then a preservation fix.
- Verified: `Microsoft.ML.OnnxRuntime.DirectML` is 1.24.4 (latest on NuGet); the DirectML EP NuGet is frozen in maintenance mode while core ORT moved to 1.27. No upgrade available; the strategic replacement is Windows ML (research-gated, needs Windows App SDK — see Open Questions).
- Verified: `SharpCompress` 0.49.1 is past the zip-slip CVE-2026-44788 fix (0.48.0). No action beyond never calling `WriteToDirectory` on untrusted archives.
- Note: JXL/AVIF/HEIC WIC Store extensions are Windows 11 24H2-only, and the project's `10.0.22621.0` (22H2) floor cannot install them — so Magick.NET is effectively the primary path for those formats on most machines, reinforcing the Magick.NET currency requirement above.

## Architecture Assessment
- The 70-item AUD backlog (ROADMAP.md) already captures the internal correctness/concurrency/a11y/theme defect surface; do not duplicate it. This pass adds only externally-grounded, net-new feature/security items.
- Color pipeline: introducing a display-ICC transform belongs in `ImageLoader`/a new `ColorManagementService`, applied lazily and off the startup hot path so accuracy never costs the sub-second cold-start that is Images' advantage over Photos.
- Viewer-comfort items (checkerboard, zoom-lock, loupe, pixel readout, zoom-to-selection) live in `Controls/ZoomPanImage.cs` + `MainViewModel` and fit the quiet-premium surface without adding permanent rail/inspector real estate — expose via existing command palette + a single toggle each.
- Manipulation (trackpad pinch/pan/inertia), Compare (side-by-side/overlay/difference), histogram/channel stats (`ImageColorAnalysisService`), nearest-neighbor scaling, and lossless JPEG transforms (`JpegTranTransformService`) already exist — confirmed in source; these were common competitor gaps Images has already closed.

## Rejected Ideas
- HDR (PQ/HLG) true display output: requires a Direct2D/DXGI FP16 island (WPF renders 8-bit sRGB; .NET 10 adds nothing here). XL, renderer-architecture decision — belongs with the blocked SkiaSharp/renderer track (V20-01), not active code-ready work. A tone-mapped SDR preview of HDR sources via Magick.NET is the pragmatic subset if demand appears.
- Per-channel / alpha-isolation inspection (Oculante): niche VFX feature that adds inspector complexity against the debloat direction.
- EXIF star-rating / pick-reject culling (ImageGlass #141, nomacs #138): the Review/rating workflow was deliberately removed; re-adding it contradicts the stated debloat philosophy.
- Slideshow background music / 150+ transitions (FastStone): music contradicts the quiet-viewer identity; heavy transition libraries are bloat.
- 360°/equirectangular panorama, barcode/QR detection, on-canvas measure tool: distinctive but niche; add main-surface complexity disproportionate to demand for a debloating viewer.
- Selectable Lanczos/Catmull interpolation beyond the existing nearest-vs-HighQuality toggle: WPF's `BitmapScalingMode` only exposes those two tiers; current toggle is adequate.
- Windows ML migration of the ONNX path: strategically correct (evergreen, smaller app) but research-gated — needs Windows App SDK adoption and interop design; not code-ready. Tracked as an Open Question, not a roadmap item.
- Cross-platform rewrite, cloud/multi-user, automatic model downloads, public plugin SDK now: unchanged rejections from prior research (conflict with Windows-native, local-single-user, explicit-import, unsigned-extension-risk posture).

## Sources
OSS viewers, releases, and open feature requests:
- https://github.com/d2phap/ImageGlass/releases
- https://github.com/d2phap/ImageGlass/issues/636
- https://github.com/d2phap/ImageGlass/issues/1425
- https://github.com/d2phap/ImageGlass/issues/1433
- https://github.com/d2phap/ImageGlass/issues/496
- https://github.com/d2phap/ImageGlass/issues/1958
- https://github.com/d2phap/ImageGlass/issues/254
- https://github.com/d2phap/ImageGlass/issues/141
- https://github.com/Ruben2776/PicView/releases
- https://github.com/Ruben2776/PicView/issues/151
- https://github.com/Ruben2776/PicView/issues/201
- https://github.com/Ruben2776/PicView/issues/257
- https://github.com/nomacs/nomacs/issues/459
- https://github.com/nomacs/nomacs/issues/607
- https://github.com/nomacs/nomacs/issues/285
- https://github.com/sylikc/jpegview
- https://github.com/sylikc/jpegview/issues/43
- https://github.com/sylikc/jpegview/issues/216
- https://github.com/woelper/oculante
- https://www.faststone.org/FSViewerDetail.htm

Formats, platform APIs, and dependencies:
- https://learn.microsoft.com/en-us/windows/win32/wic/heif-codec
- https://apps.microsoft.com/detail/9mzprth5c0tb
- https://learn.microsoft.com/en-us/windows/win32/direct2d/hdr-tone-map-effect
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview
- https://github.com/dlemstra/Magick.NET/releases

Security advisories:
- https://www.thehackerwire.com/libheif-heap-buffer-overflow-cve-2026-32740/
- https://github.com/advisories/GHSA-6c8g-7p36-r338
- https://www.welivesecurity.com/en/eset-research/revisiting-cve-2025-50165-critical-flaw-windows-imaging-component/
- https://dailycve.com/imagemagick-heap-buffer-over-write-cve-2026-48994-moderate-dc-jun2026-663/

Community pain points:
- https://learn.microsoft.com/en-us/answers/questions/4308808/photos-app-in-windows-11-modifies-quality-when-ope
- https://news.ycombinator.com/item?id=43164794
- https://discuss.privacyguides.net/t/open-source-image-viewer-recommendations/24694

## Open Questions
- Windows ML migration: is adopting the Windows App SDK (to replace the frozen DirectML 1.24.4 EP with the evergreen, Windows-Update-serviced Windows ML) worth the interop cost and the 24H2 gating for NPU/optimized EPs? Blocks whether the ONNX dependency stays pinned or is re-platformed.
- Save-a-copy metadata: does the current WPF `BitmapEncoder` path already thread EXIF/ICC through, or does it strip? Determines whether RD-03 is a fix or a no-op (verify before implementing).
- Display color management scope: transform to the active monitor's ICC via Magick.NET/lcms2 baked into the display bitmap, or defer entirely to the blocked OCIO/ACES track? The pragmatic bake is code-ready today and answers the loudest complaint; confirm it won't conflict with a future managed renderer.
