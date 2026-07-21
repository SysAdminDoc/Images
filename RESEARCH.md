# Research — Images

Date: 2026-07-20 — replaces all prior research (supersedes the 2026-07-16 pass, whose catalog private-cache fix, archive-gate dispose, invert-colors quick toggle, SharpCompress 0.50.0 bump, .NET/SQLite 10.0.10 servicing, Magick.NET 14.15.0, and the local-ML CLI wave have all since shipped — v0.2.27 → v0.2.30).

## Executive Summary

Images is a Windows-only, local-first .NET 10/WPF viewer (v0.2.30, 155 services, ~1,231 first-party sources, 1,156 test methods, zero TODO/FIXME markers) whose feature depth exceeds every free competitor. Since the last pass it shipped a large, high-quality **local-ML wave** — offline ONNX CLIs for face detect/cluster (YuNet+SFace), object tagging (YOLOX-S), scene (Places365), aesthetic (NIMA), orientation (ConvNeXtV2), safety (Marqo) — plus catalog near-duplicate stacks, trip/event grouping, watched roots, and tag category sets. The two prior known hazards (catalog shared-cache lock, undisposed archive gate) are **both fixed** (`CatalogService.cs:108-112` now `Private`+WAL; `MainViewModel.cs:8280` disposes the gate), and the invert-colors accessibility toggle prior research requested is **already live** (`MainViewModel.cs:3175`).

The codebase is now correctness-solid; the residual work is **perf/UX refinement of the new ML surface, an owner-policy hygiene fix, and one genuinely strategic scope decision**. In priority order: (1) delete the GitHub Actions smoke workflow that violates owner "build/test locally" policy; (2) reuse one ONNX `InferenceSession` across the single-shot face/object/orientation batch paths (currently N sessions per run); (3) thread `CancellationToken` through the uncancellable ML batch APIs; (4) add a SharpCompress-0.50.0 archive-detection regression gate; (5) cap the semantic full-scan cosine query; (6) close two test gaps (pHash, Exif 3.1). The one big open question is **inline video/MP4 playback** — the single most-validated community gap and the headline feature of every rising competitor (Lap, LowKey, LightningView) — which is XL and directly tensions the "image viewer" identity; documented below as decision-gated, not scheduled.

Top opportunities: GitHub-Actions policy fix, ML session reuse, ML batch cancellation, SharpCompress regression gate, semantic-scan ceiling, pHash/Exif31 tests. Strategic (decision-gated): inline video, gain-map display.

## Product Map

- **Core workflows:** open files/folders/clipboard/archives; navigate, zoom, compare (N-pane synced + diff), loupe, present; inspect OCR/metadata/C2PA/gain-map; non-destructive edit + export; catalog + search (text + CLIP), dedup/near-dup stacks, trip/event grouping, import, recover; offline ML review CLIs (face/object/scene/aesthetic/orientation/safety).
- **User personas:** Windows power users replacing Photos/ImageGlass; photographers/archivists working directly in folders; comic/manga/webtoon readers; privacy-conscious users who value portable builds and visible runtime provenance.
- **Platforms & distribution:** Windows 10/11 x64; `net10.0-windows10.0.22621.0`; MIT; self-contained installer + portable ZIP; permanently unsigned by owner policy. Checksum-pinned WinGet/Scoop manifests supported; account-based submission is external follow-up, not a v1.0 gate.
- **Key integrations & data flows:** WIC-first decode with Magick.NET 14.15.0 (ImageMagick 7.1.2-27, libjxl 0.12.0) fallback; SharpCompress 0.50.0 read-only archives; SQLite (`Microsoft.Data.Sqlite` 10.0.10) settings/catalog/semantic index (all `Private` cache + WAL); XMP sidecars; **Windows ML** (`Microsoft.Windows.AI.MachineLearning` 2.1.74, system-managed ORT, DirectML EP appended at runtime) for ONNX; SkiaSharp 4.150.1 software presenter; optional Ghostscript/jpegtran/ExifTool/c2patool child processes; Windows OCR; default-off GitHub release checks.

## Competitive Landscape

- **Lap (julyx10/lap, ~1.4k stars, v0.3.0 2026-07-17, GPL-3.0):** the closest new competitor — Tauri/Rust, SQLite, folder-first, on-device CLIP semantic search + InsightFace clustering + similar-image + smart tags, 4-pane compare, **and inline video (MP4/MOV/MKV/WebM)**. Learn: it proves the CLIP+face+dedup stack Images already has is table-stakes now, and that **video is the differentiator new entrants lead with**. Avoid: nothing to avoid — it validates the direction.
- **LowKey Media Viewer (SteveCastle/loki):** mixed images+video+audio+comics for "tens of thousands" of files, comic archives auto-extract like a directory, folder-first no-takeover. Images matches the comic/scale half; the video/audio half is the gap. Avoid: the optional server/web-UI/AI-tagging sprawl.
- **HDRImageViewer (linyusenzz, v1.0.28 2026-07-15, WinUI3+D3D11 FP16 scRGB):** the proof that ISO 21496-1 gain-maps can be **displayed** (not just inspected) on Windows via a float swapchain + reconstruction shader. Learn: this is the concrete renderer pattern for promoting Images' gain-map inspection to display. Avoid: it's decode-only and narrow — Images should keep its broader pipeline.
- **FlyPhotos (riyasy, v2.7.0):** WinUI speed-first Picasa-style viewer; touchpad/pinch gestures, Mica/Acrylic. HDR not implemented (Images is ahead). Learn: first-paint architecture and hold-to-fly scrubbing (already parked as V110-13). Avoid: its thin feature set.
- **RAWviewer (markyip, v3.0.1 2026-07-20, MIT):** RAW culling with star-rate + XMP-sidecar non-destructive dev + synced-zoom compare. Learn: nothing new — Images has compare + non-destructive edits. Avoid: the star-rating/DAM culling lane Images **deliberately removed** (see Rejected).
- **LightningView (dividebysandwich, v3.0.0 2026-07-05, GPL-2.0):** Rust, RAW+SVG+**FITS astro**+**video-with-subtitles**, deliberately no editing. Learn: FITS + subtitle handling as niche depth. Avoid: its no-edit minimalism.

## Security, Privacy, and Reliability

- **[Verified] Prior catalog SQLite-lock hazard is fixed.** `CatalogService.Open` now uses `SqliteCacheMode.Private` with an explicit rationale comment (`CatalogService.cs:108-112`); `SemanticSearchService.cs:124` and `SettingsService.cs:58` are Private too. Rebuild runs in a single transaction with in-loop cancellation (`CatalogService.cs:186-205`); reads degrade to empty-list with a **warning** log rather than silently (`CatalogService.cs:242-245`). No action.
- **[Verified] Dependency posture is current.** Magick.NET 14.15.0 (IM 7.1.2-27), SharpCompress 0.50.0, `Microsoft.Data.Sqlite` 10.0.10 (July-2026 servicing, 17 CVEs incl. 3 RCE), `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 / `SourceGear.sqlite3` 3.53.3. There is **no .NET 10.0.11** yet — 10.0.10 is the latest servicing wave as of 2026-07-20. The DirectML nuget is frozen at 1.24.4 (2026-03), but Images no longer depends on it: it uses **Windows ML** system-managed ORT, so it inherits evergreen EPs. No servicing action.
- **[Verified] SharpCompress 0.50.0 has two breaking changes with archive-read blast radius.** 0.50.0 (2026-07-13) changed the Detection API and stopped `TarArchive` auto-decompressing streams (only `TarReader` still does); CRC verification is now default-on. Images supports ZIP/CBZ, RAR/CBR, 7z/CB7 (no CBT/tar), so the Tar change is low-impact, but the **Detection API change** touches format sniffing in `ArchiveBookService`. No regression was observed, but there is no fixture pinning post-0.50.0 detection/CRC behavior → roadmap item V120-03.
- **[Verified/Low] ML batch APIs are uncancellable.** `AestheticScoringService.ScoreMany` (`:41`), `SceneClassificationService.ClassifyMany` (`:58`), `SafetyClassificationService.ClassifyMany` (`:44`), and `FaceCli.ExecuteCluster` (`:97`) take no `CancellationToken`; a large `--scene-classify`/`--aesthetic-score`/`--face-cluster` run cannot be interrupted. Catalog/semantic rebuilds already thread cancellation correctly — this is the one gap. Correctness-safe (batches reuse one session and dispose it), UX-only → V120-02.
- **[Verified/Low] Per-image failure fan-out is indistinguishable from systemic failure.** A model-load exception in `ScoreMany`/`ClassifyMany` returns a generic `Failed` result for *every* path (`AestheticScoringService.cs:65`, `SceneClassificationService.cs:82`, `SafetyClassificationService.cs:68`), so the CLI exit code can't tell "model broken" from "one corrupt image" → V120-04.
- **Recovery/rollback:** corrupt-DB quarantine, atomic sidecar/temp-swap writes, quarantine-over-delete, bounded egress-logged child processes, and the import-rollback correctness fix are all in place and verified. Keep as the fallback contract.

## Architecture Assessment

- **Single-shot ML paths rebuild a session per image.** `FaceDetectionService.Detect`→`RunInference` (`FaceDetectionService.cs:138`), `ObjectDetectionService` (`:119`), and `OrientationSuggestionService` (`:96`) each build a fresh `InferenceSession` per call; `FaceRecognitionService.Analyze` (`:63,:115`) then builds its own SFace session. `--face-cluster` over N images = 2N sessions. Scene/Aesthetic/Safety already cache one session across the batch (`ScoreMany`/`ClassifyMany` `using(session){foreach…}`). Give face/object/orientation the same batched overload → V120-01.
- **Semantic search is a brute-force in-RAM cosine scan** with no ceiling: `SemanticSearchService.Search` (`:216-239`) scores every cached vector, then `Take(limit)` (1–500). `EnsureVectorCache` (generation-versioned, loads vectors once per index generation) already removes the per-query DB deserialization cost, so this is acceptable at local scale but is O(N) RAM-bound per query. A configurable candidate ceiling / early-out is a dependency-free perf guard (distinct from the blocked SigLIP-2 model swap) → V120-05.
- **`FaceDetectionService.ApplyNonMaximumSuppression` is worst-case O(k²)** over `topK=5000` survivors (`FaceDetectionService.cs:165,259-263`, linear `selected.All(...)` scan in the loop). Bounded but pathological on dense grids; cap survivors before suppression → V120-08. (YOLOX NMS is class-partitioned and capped at 300 — fine.)
- **`MainViewModel` is still an 8,316-line god object** (`ViewModels/MainViewModel.cs`) owning 20+ services and 4 timers; the `*Controller` extraction pattern (FolderPreview, PhotoMetadata, ColorAnalysis, C2paInspection, Ocr, ExternalEditReload, UpdateCheck) should continue for slideshow/archive-reader/rename-editor. This is already tracked as **V110-09** and remains GUI-verification-blocked (`Roadmap_Blocked.md`) — not re-added.
- **Test gaps:** `PerceptualHashService` (the input to near-duplicate stacking — a wrong hash silently mis-stacks) and `Exif31MetadataReader` (provenance/security-relevant) have no dedicated test file; the real ONNX runtime path is only exercised by the FlaUI smoke lane (unit tests inject fake detectors) → V120-06.
- **Owner-policy violation:** `.github/workflows/wpf-background-smoke.yml` runs the smoke lane on `windows-latest` CI (`pull_request` + `push:main`), contradicting the owner "no GitHub Actions for builds/deploys/tests — build/test locally" rule. The v1.0 milestone defines the smoke lane as the **local** `scripts/Test-WpfBackgroundSmoke.ps1`, which already satisfies it → V120-07.
- **Category audit:** security/servicing = current (no action); reliability = ML cancellation + archive-detection gate; performance = ML session reuse + semantic ceiling + NMS cap; accessibility = invert-colors already shipped, AutomationProperties on 26/33 XAML files; testing = pHash/Exif31; distribution/packaging = WinGet/Store remain external-gated; docs = README oversells nothing new to sync this pass. i18n accounts (Crowdin), plugin isolation, lab/VFX packs, gain-map display, and inline video remain external- or decision-gated.

## Rejected Ideas

- **Star-rating / color-label / review-DAM culling lane** (RAWviewer, XnView MP): rejected again — repository history deliberately removed the Review/rating workflow; `TagGraphService` covers hierarchical tags without a rating lane. Source: `markyip/RAWviewer`, prior CHANGELOG "Review workflow removal."
- **Bundling a native JXL WIC codec** (`mirillis/jpegxl-wic`): rejected — decode-only, no encode/animation, no released binary, 5 commits. Images already decodes JXL via Magick.NET/libjxl 0.12.0, ahead of the OS. Source: `github.com/mirillis/jpegxl-wic`.
- **Immediate DirectML→standalone-ORT upgrade** to chase the stalled 1.24.4 nuget: rejected — Images already uses Windows ML (system-managed ORT); the frozen DirectML nuget is irrelevant. Source: `learn.microsoft.com/windows/ai/new-windows-ml/overview`.
- **FITS astro + subtitle handling** (LightningView): rejected as niche scope creep for a consumer viewer; multidimensional scientific navigation is already parked as V80-23 (blocked). Source: `dividebysandwich/LightningView`.
- **Second HDR-detection service:** rejected — `DisplayColorService`/`HdrDisplayCapabilityProbe` already cover monitor Advanced-Color state.

## Under Consideration (decision-gated — not scheduled)

- **Inline video/MP4/WebM playback.** The single most-validated community gap and the headline feature of every 2025-26 competitor that gained traction (Lap ~1.4k stars, LowKey, LightningView). It is **XL** and materially tensions the "local-first *image* viewer" identity: it pulls in a media stack (Media Foundation / FFmpeg), expands HDR/codec-licensing surface, and reshapes navigation. Recommendation: treat as a deliberate product decision, not an autonomous roadmap item. If pursued, scope a Media-Foundation-only (no FFmpeg redistribution) preview of MP4/MOV/WebM in the existing stage before committing to a full player. Sources: `github.com/julyx10/lap`, `lowkeyviewer.com`, `xda-developers.com/favorite-image-viewers-microsoft-photos-alternatives` (2025-05-14).
- **Gain-map DISPLAY (vs current inspection).** `linyusenzz/HDRImageViewer` (WinUI3+D3D11 FP16 scRGB) proves it's achievable; WPF's SDR pipeline can't composite it, so it needs the same blocked float-swapchain renderer decision as HDR gain-map display (already parked, V100-06 / V110-16 inspection shipped). Not autonomously actionable.

## Sources

### Competitors and adjacent products
- https://github.com/julyx10/lap
- https://news.ycombinator.com/item?id=47050377
- https://github.com/SteveCastle/loki
- https://lowkeyviewer.com/
- https://github.com/riyasy/FlyPhotos/releases
- https://github.com/markyip/RAWviewer
- https://github.com/dividebysandwich/LightningView
- https://github.com/woelper/oculante
- https://github.com/linyusenzz/HDRImageViewer

### Ecosystem and community signal
- https://www.xda-developers.com/favorite-image-viewers-microsoft-photos-alternatives/
- https://www.dpreview.com/forums/thread/2205681
- https://community.adobe.com/t5/bridge-discussions/bridge-super-slow-to-generate-thumbnails-and-previews/

### Standards and platform
- https://gregbenzphotography.com/hdr-photos/iso-21496-1-gain-maps-share-hdr-photos/
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview
- https://www.phoronix.com/news/JPEG-XL-Returns-Chrome-Chromium
- https://github.com/mirillis/jpegxl-wic
- https://spec.c2pa.org/specifications/specifications/2.4/index.html

### Dependencies and advisories
- https://github.com/dlemstra/Magick.NET/releases
- https://github.com/adamhathcock/sharpcompress/releases
- https://www.nuget.org/packages/SharpCompress
- https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-july-2026-servicing-updates/
- https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML
- https://www.nuget.org/packages/SkiaSharp

## Open Questions

- **Inline video playback** is a product-identity decision (Media Foundation preview vs. full player vs. stay image-only) that only the owner can make — it blocks correct prioritization of the largest opportunity.
- All other additions are code-ready and dependency-free. Gain-map display, SigLIP-2, WinGet/Store publication, and Crowdin localization remain renderer-, download-, or account-gated in `Roadmap_Blocked.md`.
