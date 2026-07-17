# Research — Images

Date: 2026-07-16 — replaces all prior research (supersedes the 2026-07-14 pass, whose SQLite floor, `SqliteConnectionPolicy`, `SettingsTransferService`, and `Exif31MetadataReader` items have all since shipped).

## Executive Summary

Images is a Windows-only, local-first .NET 10/WPF viewer (v0.2.27, 125 services, 338 source files, zero TODO/FIXME markers) whose feature depth now exceeds every free competitor: it already ships multi-image compare with synced pan/zoom + difference toggle, a hold-button/keyboard loupe, Samsung/Google Motion Photo + Apple Live Photo playback, continuous vertical-scroll archive (webtoon) reading, perspective correction, tile-pyramid rendering, HDR-to-SDR tone mapping, color management, CLIP semantic search, OCR, C2PA inspection, non-destructive edits, background removal, super-resolution, LaMa inpainting, catalog/dedup, Picasa migration, and a Store-extension install-prompt path for HEIC/AVIF/JXL. Most "obvious" competitor gaps are therefore already closed. The remaining high-value direction is **trust/servicing hygiene and read-only standards inspection**, not another feature layer. In priority order: (1) take the July .NET/SQLite servicing bump (17 CVEs, 3 critical RCE); (2) fix the catalog shared-cache SQLite lock hazard that can silently blank the catalog; (3) add HDR gain-map inspection (ISO 21496-1 / UltraHDR / Apple / Adobe) — no Windows viewer does this today; (4) upgrade SharpCompress with a CBR/CBZ/tar regression gate; (5) close the small confirmed UX gaps (invert-colors toggle, next/prev-archive jump, hold-to-scrub fly-through); (6) drain the verified latent-bug backlog (import rollback stranding, animated-decode policy bypass, semantic-search full-scan, unbounded tile locks); (7) continue extracting the 318 KB `MainViewModel` god object.

Top opportunities: gain-map inspection, catalog SQLite-lock fix, dependency servicing, SharpCompress upgrade, semantic-search scaling, import-rollback correctness, MainViewModel decomposition, invert/archive-jump/fly-through UX.

## Product Map

- **Core workflows:** open files/folders/clipboard/archives; navigate, zoom, compare (N-pane synced + diff), loupe, present; inspect OCR/metadata/C2PA/gain-map; non-destructive edit + export; catalog, search (text + CLIP), dedup, import, recover.
- **User personas:** Windows power users replacing Photos/ImageGlass; photographers/archivists working directly in folders; comic/manga/webtoon readers; privacy-conscious users who value portable builds and visible runtime provenance.
- **Platforms & distribution:** Windows 10/11 x64; `net10.0-windows10.0.22621.0`; MIT; self-contained installer + portable ZIP. Releases are permanently unsigned by owner policy; checksum-pinned WinGet/Scoop manifests are supported, while account-based WinGet/Store submission remains external follow-up (`Roadmap_Blocked.md`) and does not gate v1.0.
- **Key integrations & data flows:** WIC-first decode with Magick.NET 14.15.0 (ImageMagick 7.1.2-27) fallback; SharpCompress 0.49.1 read-only archives; SQLite settings/catalog/semantic index; XMP sidecars; optional Ghostscript/jpegtran/ExifTool/c2patool child processes (bounded, egress-logged); Windows OCR; imported ONNX models via ONNX Runtime DirectML 1.24.4; default-off GitHub release checks.

## Competitive Landscape

- **QuickView 5 / qView / JarkViewer:** win on raw first-paint speed — GPU (Direct2D) multithreaded JXL/AVIF decode, ARM64 native, virtualized 10k+ galleries. Learn: decode/first-paint speed is now the reviewer-decisive axis; a rich-but-slow WPF path loses "which is fastest" threads. Avoid: chasing every exotic format at the cost of cold-open latency.
- **FastStone / nomacs / Butterfly Viewer:** the compare workspace (synced zoom, overlay-opacity, slider/flicker). Images already matches this — the remaining delta is overlay-opacity/slider/flicker *modes* on top of the existing diff toggle. Avoid: nomacs's dense multi-instance organizer complexity.
- **BandiView / OpenComic:** webtoon continuous reading + archive-to-archive navigation. Images has continuous archive reading; it still lacks jump-to-next/previous-archive. Learn the reader-flow polish; avoid paid-gate/advertising UX.
- **ImageGlass v9.x:** Explorer sort-order parity, Invert-Colors quick toggle, Live-Photo playback, no-runtime self-contained build. Learn the small quick toggles; avoid the v8→v9 settings break (Images already has versioned settings transfer).
- **XnView MP:** hierarchical XMP keyword tree + Lightroom/Bridge round-trip and star/color-label DAM. Images intentionally removed the review/rating DAM lane; it retains `TagGraphService` for hierarchical tags. Do not reintroduce a rating workflow (see Rejected).
- **PicView / IrfanView:** searchable shortcut list, batch convert, TWAIN acquire, panorama. Images covers batch/convert; TWAIN and panorama are niche and out of the viewer-first scope.
- **FlyPhotos:** hold-arrow "fly-through" rapid folder scrubbing — repeatedly praised in community threads as the antidote to per-image reload stutter. Images has preload but no explicit hold-to-scrub mode.

## Security, Privacy, and Reliability

- **[Verified] July 2026 servicing gap.** `src/Images/Images.csproj` pins `Microsoft.Data.Sqlite` 10.0.9; 10.0.10 (2026-07-14) ships with the .NET 10.0.10 wave that fixes 17 CVEs (3 critical RCE across EncryptedXml/SslStream/HTTP-2). Client-side exposure is limited but the app does HTTPS update checks — take the servicing bump and pin the SDK/runtime to 10.0.10.
- **[Verified] Catalog SQLite lock hazard (data-visibility).** `CatalogService.Open` uses `SqliteCacheMode.Shared` (`CatalogService.cs:89`) while `SemanticSearchService.Open` uses `Private` (`SemanticSearchService.cs:102`). Under shared-cache + WAL, a background `Rebuild` write transaction (held across many `UpsertAsset` calls, `CatalogService.cs:155-171`) can raise table-level `SQLITE_LOCKED` on a concurrent UI read; the `busy_timeout` handler covers `SQLITE_BUSY` only, and `GetByPath`/`GetAllAssets` swallow `SqliteException` → the user silently sees an empty catalog. Fix: use `Private` cache for the catalog too.
- **[Verified] Import rollback can strand a moved original.** `ImportInboxService.RollBackFailedTransfer` (`ImportInboxService.cs:325-330`) only restores a moved original `if (!File.Exists(sourcePath))`; if the source slot is re-occupied, the file is left only at the destination while the UI reports failure — perceived data loss. Fix: restore to a unique sibling and surface the recovered path.
- **[Verified] Animated-decode bypasses the native policy seam.** `ImageLoader.TryLoadAnimated` (`ImageLoader.cs:689`) constructs `new MagickImageCollection(bytes)` directly rather than through `MagickSafeReader`/`CodecRuntime.Configure()`. Safe today only because `ImageLoader.Load` pre-runs `TileService.Preflight`; a future caller reaching this first would decode untrusted bytes before the coder allowlist/resource limits install. Fix: call `CodecRuntime.Configure()` at entry or add `MagickSafeReader.ReadCollection`.
- **[Verified] ImageMagick CVE posture is current.** Magick.NET 14.15.0 bundles ImageMagick 7.1.2-27, which is past the fix lines for the 2025-2026 decoder overflows (XBM CVE-2026-23876, Sun CVE-2026-25897, MAT CVE-2026-48994, MIFF CVE-2026-46521). `MagickSecurityPolicy`/`CodecRuntime.Configure` already install a coder allowlist + resource limits. No action beyond staying current.
- **[Verified] SharpCompress 0.49.1 is one minor behind.** 0.50.0 (2026-07-13) reduces LZMA/RAR decode allocation (direct CBR/CBZ benefit) and fixes Zip64 non-seekable streaming + entry-metadata corruption, but breaks Tar auto-decompress and the Detection API — not a blind bump.
- **[Low] Listen-mode token compare is non-constant-time** (`ListenService.cs:141`, `string.Equals`), and the concurrent-connection cap can be transiently exceeded because `_activeConnections` is incremented inside the handler (`ListenService.cs:89-111`). Both are heavily mitigated by loopback-only bind, `ExclusiveAddressUse`, rate limiting, and a 10 s pre-auth window.
- **Recovery/rollback:** existing corrupt-DB quarantine, atomic sidecar/temp-swap writes, quarantine-over-delete, and bounded child-process runners are well built and verified — keep them as the fallback contract.

## Architecture Assessment

- **`MainViewModel` god object.** `ViewModels/MainViewModel.cs` is ~8,300 lines / 318 KB owning 20+ services, 4 `DispatcherTimer`s, slideshow, rename editor, archive reader, gallery smart-filter indexing, motion-photo, export, and metadata. The team already extracts `*Controller` helpers (FolderPreview, PhotoMetadata, ColorAnalysis, C2paInspection, Ocr, ExternalEditReload, UpdateCheck) — continue that pattern for slideshow, archive-reader, and rename-editor behind the existing `_uiDispatcher`/`() => _isDisposed` convention. Also dispose `_continuousArchiveDecodeGate` (`MainViewModel.cs:59`), the one semaphore missed by an otherwise thorough `Dispose`.
- **Semantic search does not scale.** `SemanticSearchService.Search` (`SemanticSearchService.cs:194-235`) selects every row for the active model (no SQL `LIMIT`), deserializes each 512-dim blob, and dot-products in managed code on the calling thread on every query. Cache normalized vectors in memory keyed by index generation, or cap the candidate set / add an ANN index.
- **`TileService.BuildLocks` leaks.** `TileService.cs:58,147` adds one lock per distinct huge-image path for the process lifetime and never removes it. Remove in the build `finally` or use an expiring map.
- **Gain-map inspection is greenfield.** No gain-map/UltraHDR code exists (`grep` clean). ISO 21496-1:2025 gain maps are now written by Adobe (LrC v17), Apple (iOS 18/Sequoia), and Google (Android 15 UltraHDR). Windows WIC silently ignores them. A read-only inspector (base + gain-map grayscale layer + min/max content boost + flavor detection) is fully doable via Magick.NET/manual JUMBF/MPF parsing and would beat every mainstream Windows viewer. HDR *display* of gain maps needs a D3D11/D2D scRGB-float swapchain (WPF's SDR pipeline can't composite it) — treat display as a later, blocked renderer decision; ship inspection first.
- **JXL provenance.** `Exif31MetadataReader` already handles UTF-8 tags correctly; the remaining read-only add is detecting JXL lossless-JPEG *transcode* (recompression) vs native codestream vs ISOBMFF container — a headline JXL trait no viewer surfaces.
- **DirectML is in maintenance mode** (not deprecated). ONNX Runtime DirectML 1.24.4 is current and supported; the Windows ML migration remains a correctly-blocked renderer/runtime decision, not a current-sprint item.
- **Category audit:** security/servicing and reliability covered by the SQLite/import/animated-decode fixes; performance by semantic-search + tile-lock work; accessibility by the invert-colors toggle (also a WCAG-adjacent aid); UX by archive-jump and fly-through; docs by undersold-feature README sync. The Skia renderer and Windows ML front-end are now actionable keystones in `ROADMAP.md`; i18n accounts, Store/WinGet submission, plugin isolation, face clustering, and lab/VFX format packs remain external or predecessor-gated. Signing is retired, not deferred.

## Rejected Ideas

- **Star-rating / color-label / review DAM lane:** rejected — repository history deliberately removed the Review/rating workflow; reintroducing it contradicts the viewer-first philosophy. Source: XnView MP, prior CHANGELOG "Review workflow removal."
- **TWAIN scanner acquire, panorama stitch, WebP2 (`.wp2`) support:** rejected — TWAIN/panorama are editor-suite scope creep; WebP2 is an abandoned Google research codebase with no shipping files, no WIC, no browser. Sources: IrfanView/Imagine; WebP2 repo status.
- **Multipage-PDF export from a selection:** rejected as low-value — single-image PDF export already exists (`ImageExportService` maps `MagickFormat.Pdf/Pdfa`) and multi-page assembly duplicates the batch/contact-sheet paths.
- **GPU/Direct2D decode rewrite as a near-term item:** rejected for this pass — real (QuickView-class speed win) but an XL renderer decision that belongs with the blocked libvips/Windows-ML runtime evaluations, not the actionable roadmap. Source: QuickView 5, `Roadmap_Blocked.md` V80-24.
- **Face detection/clustering:** deferred to Under Consideration, not roadmap — genuine (digiKam) and philosophy-compatible (local ML), but XL, needs a new model class beyond CLIP, and is not community-demanded for this tool. Source: digiKam.
- **AI image-captioning alt-text via Windows ML:** deferred — a novel accessibility angle, but requires a captioning model and the blocked Windows ML/NPU path; `ClipEmbeddingProvider.DescribeAsset` already gives a lightweight description. Source: Windows AI Foundry (Build 2025).
- **Second HDR-detection service / immediate DirectML→Windows ML migration:** rejected as duplicates — `DisplayColorService`/`HdrDisplayCapabilityProbe` already cover monitor Advanced-Color state; Windows ML is parked as a blocked runtime decision.

## Sources

### Competitors and adjacent products
- https://github.com/d2phap/ImageGlass/releases
- https://nomacs.org/
- https://github.com/Ruben2776/PicView/releases
- https://www.xnview.com/mantisbt/changelog_page.php
- https://www.irfanview.com/history_old.htm
- https://en.bandisoft.com/bandiview/help/webtoon-mode/
- https://github.com/justnullname/QuickView
- https://github.com/jark006/JarkViewer/blob/main/README_EN.md
- https://github.com/jurplel/qView
- https://github.com/sylikc/jpegview
- https://www.faststone.org/FSViewerDetail.htm
- https://github.com/olive-groves/butterfly_viewer

### Ecosystem and community signal
- https://www.makeuseof.com/free-windows-photos-app-replacement/
- https://discuss.privacyguides.net/t/open-source-image-viewer-recommendations/24694
- https://www.dpreview.com/forums/threads/image-viewer-to-compare-2-or-more-images.4791876/
- https://news.ycombinator.com/item?id=42868394
- https://news.ycombinator.com/item?id=44299970
- https://learn.microsoft.com/en-us/answers/questions/3293744/windows-10-photo-app-works-atrociously-slow

### Standards and platform
- https://www.iso.org/standard/86775.html
- https://gregbenzphotography.com/hdr-photos/iso-21496-1-gain-maps-share-hdr-photos/
- https://en.wikipedia.org/wiki/Ultra_HDR
- https://github.com/microsoft/WindowsAppSDK/issues/4968
- https://learn.microsoft.com/en-us/windows/win32/wic/heif-codec
- https://en.wikipedia.org/wiki/JPEG_XL
- https://spec.c2pa.org/specifications/specifications/2.4/index.html
- https://www.cipa.jp/e/std/std-sec.html
- https://www.w3.org/TR/wcag2ict-22/

### Dependencies and advisories
- https://github.com/dlemstra/Magick.NET/releases
- https://github.com/adamhathcock/sharpcompress/releases/tag/0.50.0
- https://www.nuget.org/packages/Microsoft.Data.Sqlite
- https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-july-2026-servicing-updates/
- https://www.sqlite.org/cves.html
- https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html
- https://github.com/microsoft/DirectML
- https://www.sentinelone.com/vulnerability-database/cve-2026-23876/

## Open Questions

None that block the actionable additions. HDR gain-map *display* (vs inspection) and downstream ML work remain predecessor-gated; WinGet/Store publication, Crowdin localization, and lab/VFX format packs remain externally gated. The renderer swap and Windows ML migration are now scheduled in `ROADMAP.md`; code signing is permanently retired by owner policy.
