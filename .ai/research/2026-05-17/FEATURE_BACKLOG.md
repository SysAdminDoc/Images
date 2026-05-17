# Feature Backlog

Date: 2026-05-17

This is the raw harvested backlog before final prioritization. Final tiering is in `PRIORITIZATION_MATRIX.md` and root `ROADMAP.md`.

## Documentation, Memory, And Planning

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Keep `PROJECT_CONTEXT.md` as the durable current-state summary. | User request, `AGENTS.md`, stale `ROADMAP.md` current-state line. | High |
| Add roadmap status audit step to release checklist. | `ROADMAP.md`, `CHANGELOG.md`, `docs/improvement-plan.md`. | High |
| Repair future-dated `CHANGELOG.md` entries after tag/date verification. | `CHANGELOG.md` entries for `v0.1.8` and `v0.1.9`. | High |
| Keep historical v6 roadmap as source archive rather than deleting it. | Existing `ROADMAP.md` has a large source appendix and feature harvest. | High |

## Security, Dependencies, And Runtime Provenance

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Upgrade SharpCompress 0.47.4 to 0.48.1. | GHSA-6c8g-7p36-r338, local vulnerable package scan. | Done in this run |
| Add runtime provenance dashboard for Magick.NET, Ghostscript, SharpCompress, jpegtran, OCR, and future models. | `README.md` codec report, `docs/integration-policy.md`, ImageGlass format docs. | High |
| Gate optional sidecars with source URL, license, SHA-256, version, app-local/system path, and smoke result. | `docs/codec-bundling.md`, `docs/integration-policy.md`, Ghostscript release docs. | High |
| Monitor Ghostscript CVEs and update bundle/source archive on each release. | Ghostscript release/CVE pages. | High |
| Defer .NET 10 migration until WPF/runtime/release scripts pass full validation. | .NET support policy, local .NET 9 target. | Medium |
| Consider minor Serilog and test package updates. | `dotnet list package --outdated`, Serilog releases/NuGet. | Medium |
| Add automated stale-bundled-runtime advisory check. | Ghostscript CVE history, Magick.NET NuGet vulnerability history. | Medium |

## Settings, Accessibility, And UX Trust

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Finish Settings IA: General, Appearance, Advanced, Hotkeys, Accessibility, Diagnostics. | `ROADMAP.md`, shipped feature breadth in `README.md`. | High |
| Add high-contrast and reduced-motion modes. | Accessibility gap in prior roadmap and GUI polish requirements. | High |
| Add keyboard shortcut editor/export/import. | `README.md` shortcuts, growing feature count. | High |
| Add destructive-action recovery center for quarantine, recent moves, crop/writeback rollback, and sidecar operations. | Duplicate cleanup, file health, crop overwrite, copy/move actions. | High |
| Add per-feature first-run/onboarding copy only where workflows are risky. | Current privacy/trust first-run pattern in README/docs. | Medium |
| Add operation history and background job queue diagnostics. | `BackgroundTaskTracker`, Immich job patterns. | Medium |

## Viewer, Compare, And Culling Workflows

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Build compare/overlay mode with linked pan, zoom, rotate, opacity, and A/B swap. | nomacs sync/overlay, `docs/design-product-differentiators.md`. | High - shipped 2026-05-17 under V7-10 for current+next, chosen-file, and duplicate-pair entry points. |
| Integrate compare mode with duplicate cleanup pairs and gallery selections. | Local duplicate cleanup shipped; nomacs/FastStone references. | High - duplicate-cleanup pair handoff shipped 2026-05-17; gallery-selection handoff remains optional future expansion. |
| Add culling mode: pick/reject, rating, color labels, sidecar writes, undo. | XnView/ACDSee/digiKam DAM patterns, current tag/rating sidecar flow. | High - V7-13 shipped star rating plus pick/reject labels, sidecar writes, side-panel controls, keyboard flow, and undo; color labels remain future expansion. |
| Add folder review session state: reviewed, kept, rejected, exported. | Duplicate cleanup/file health/import inbox workflows. | Medium |
| Add multi-window sync later only after in-window compare is stable. | nomacs local/LAN sync. | Low |

## Export, Batch, And Conversion

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Add Squoosh/FastStone-style visual diff export workbench with quality/size preview. | Squoosh, FastStone Save As comparison. | High - shipped 2026-05-17 under V7-11 with original/encoded preview, size delta, resize-aware save, warnings, and batch dry-run estimates. |
| Add batch preset provenance and dry-run result export. | Current batch processor, macro actions. | Medium |
| Add target-format capability warnings before conversion. | ImageGlass and nomacs format matrices, local codec report. | High |
| Add visual regression corpus for export quality and metadata preservation. | Current generated fixtures, codec-aware export memory. | Medium |

## Catalog, Metadata, And Search

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Design catalog schema v1 with source path, content hash, sidecar state, tags, rating, dimensions, dates, and codec metadata. | digiKam database docs, local SQLite state. | High - shipped 2026-05-17 under V7-12 as rebuildable `catalog.db` schema and `CatalogService`. |
| Keep source files authoritative; catalog can be rebuilt. | Eagle lock-in concerns, local-first philosophy. | High |
| Add incremental indexer with cancel/rebuild/delete controls. | Immich/PhotoPrism indexing patterns. | High |
| Add advanced metadata search before embedding search. | Immich/PhotoPrism/digiKam filters, current gallery smart filters. | High |
| Add sidecar conflict detection and repair UI. | Current XMP edit stack/tag relationships. | Medium |

## Models, Datasets, And AI-Assisted Local Tools

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Build a local model manager before semantic search/inpaint/upscale/background removal. | `docs/inpaint-runtime-decision.md`, Windows ML docs, Immich model settings. | High |
| Add OpenCLIP/SigLIP embedding provider behind model manager. | OpenCLIP, SigLIP, Immich search docs. | Medium |
| Use sqlite-vec only after catalog and embedding shapes stabilize. | `docs/design-product-differentiators.md`, sqlite-vec source. | Medium |
| Add LaMa ONNX content-aware repair only after model foundation. | `docs/inpaint-runtime-decision.md`, LaMa sources. | Medium |
| Add background removal with BiRefNet/U2-Net/rembg-style models after model foundation. | BiRefNet, U2-Net, rembg. | Medium |
| Add local super-resolution with OpenModelDB/Real-ESRGAN model provenance. | OpenModelDB, Real-ESRGAN, Upscayl. | Medium |
| Defer face recognition until catalog, consent, delete controls, and derived-data policy are robust. | Immich/PhotoPrism/digiKam face pipeline docs. | Later |

## Codec, Color, And Large Images

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Add ICC/profile awareness, display transform status, and profile mismatch warnings. | OpenColorIO/OpenImageIO references, user expectations around color-managed viewers. | High |
| Add histogram/channel panels with color counter and pixel stats. | FastStone histogram, local pixel inspector. | Medium |
| Build deep-zoom/tile architecture for huge images. | OpenSeadragon, OpenSlide, libvips. | Later |
| Add scientific/whole-slide format review only if a target workflow emerges. | Bio-Formats, napari, QuPath. | Later |
| Add C2PA/content provenance inspection after metadata/catalog foundations. | C2PA spec. | Later |

## Distribution And Release

| Idea | Source evidence | Fit |
| --- | --- | --- |
| Add package-manager publishing plan for WinGet and Scoop. | Existing release docs and prior roadmap. | Medium |
| Add code-signing decision and cost/risk note. | Distribution trust gap, SmartScreen expectations. | Medium |
| Add install/portable post-install smoke script. | Release workflow and Ghostscript/OCR/runtime risks. | High |
| Add update check release-channel controls. | Current opt-in update check. | Low |

## Rejected Or Deferred

| Idea | Reason |
| --- | --- |
| Cloud sync or hosted account system. | Violates local-first/no-subscription positioning. |
| Automatic model downloads without user action. | Violates current model/runtime policy. |
| Full video player scope. | Dilutes image workflow priorities. |
| Full Lightroom-style RAW development. | Too large and outside current viewer/editor scope. |
| Bundling unreviewed native sidecars. | Conflicts with integration policy and release trust model. |
