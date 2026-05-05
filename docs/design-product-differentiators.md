# Product Differentiator Design Scopes

Status: `IP-16A` design scope  
Date: 2026-05-05  
Scope: future-facing product differentiation only. This document does not approve new runtime dependencies or implementation work by itself.

## Product Guardrails

Images should stay a fast, local-first Windows viewer before it becomes a library manager or editor. Future differentiators must preserve these constraints:

- No silent network access. Any feature that can contact a service must be opt-in, visible, and reflected in diagnostics.
- Originals are protected by default. Destructive or metadata-writing actions require explicit confirmation, undo, or a sidecar-first model.
- The first-screen viewer remains image-first and low chrome. Advanced panels should appear only when relevant or when the user asks for them.
- Long-running work must have ownership, cancellation where practical, and visible progress through the existing operation/background-task surfaces.
- Tests should land with the first implementation slice, not after the UI is complete.
- New dependencies need a license, size, security, and packaging review before they enter the app.

## Shared Foundations

Several differentiators need the same infrastructure. Implementing these once will keep later features coherent.

| Foundation | Purpose | Current fit |
| --- | --- | --- |
| Asset identity | Stable ID for an image across rename/move where possible. | Start with normalized path + size + modified time; add content hash only when indexing/culling requires it. |
| Sidecar/catalog boundary | Decide which data is app-local, sidecar-backed, or embedded in the original. | App-local SQLite for disposable/search state; sidecar for portable user intent; no original writes by default. |
| Command registry | Central names, shortcuts, tooltips, enabled states. | Needed before searchable shortcuts and complex compare/culling commands. |
| Job queue | Own indexing, duplicate scan, thumbnail work, and model inference. | Extend `BackgroundTaskTracker`; add durable progress only when jobs outlive the current window. |
| Selection model | Multi-select, primary item, comparison pair, keep/reject sets. | Add before duplicate cleanup, culling, and batch metadata actions. |
| Reversible action log | Undo moves, rejects, sidecar writes, and compare selections. | Use existing rename undo patterns as the small-scope reference. |

## 1. Local Semantic Search

### User Promise

Search a local folder or library with natural language such as "sunset over water", "receipt", or "dog on couch" without uploading images.

### MVP Scope

- Index a user-selected folder on demand, not automatically at startup.
- Run image embeddings locally using an approved ONNX model after dependency review.
- Store index records in app-local SQLite with path, file fingerprint, embedding version, model ID, and indexed timestamp.
- Provide a search panel with text query, top results, score, folder filter, and "show in folder" action.
- Make indexing progress visible and cancelable.
- Add diagnostics showing model path, index size, last index run, and whether any network access is enabled. Default: none.

### Architecture Sketch

- `SemanticIndexService`: schedules scan/index work and persists metadata.
- `EmbeddingProvider`: interface for image/text embeddings; first implementation can be an in-process ONNX provider.
- `SimilaritySearch`: start with exact cosine search for small libraries; evaluate sqlite-vec only after model and data shape are stable.
- `SearchResultsViewModel`: UI state only; no model inference directly from the view.

### Non-Goals

- No cloud search.
- No face/person recognition in the first semantic-search slice.
- No background indexing of the whole user profile.
- No automatic model download unless a visible opt-in download flow exists.

### Test Gates

- Index skips unsupported files and missing paths.
- Reindex updates stale records after file changes.
- Search returns stable ordering for deterministic fake embeddings.
- Cancellation leaves the previous index usable.
- Diagnostics report local/offline model state accurately.

### Risks And Decisions

- Model size can bloat releases. Prefer external app-data model storage with checksum validation.
- Vector persistence needs a deliberate dependency decision. Do not add a local server process for MVP.
- Embeddings are derived user data. Treat the index as local private data and expose clear delete controls.

## 2. Duplicate And Near-Duplicate Cleanup

### User Promise

Find exact duplicates and visually similar files, compare them quickly, keep the best copy, and move rejected copies safely.

### MVP Scope

- Exact duplicate pass using file size + strong content hash.
- Near-duplicate pass using perceptual hash after dependency review or an internal implementation spike.
- Results grouped into duplicate sets with a recommended keep candidate.
- Actions: keep selected, move rejects to Recycle Bin, move rejects to folder, reveal, open compare.
- Always show why a recommendation was made: larger dimensions, newer modified time, preferred folder, or exact hash match.
- No automatic deletion.

### Architecture Sketch

- `DuplicateScanService`: enumerates supported files and computes fingerprints.
- `DuplicateSet`: immutable group with candidates, confidence, and explanation.
- `DuplicateCleanupController`: owns selection and action enablement.
- Reuse Recycle Bin delete service for destructive flows.

### Non-Goals

- No cross-drive background scan without explicit folder selection.
- No permanent deletion in MVP.
- No face-aware or semantic duplicate logic until semantic search and metadata scopes are stable.

### Test Gates

- Exact duplicate grouping is deterministic.
- Near-duplicate threshold boundaries are covered with fake hashes.
- Missing files are removed from a group without crashing the flow.
- Recycle Bin opt-out preference does not bypass duplicate-cleanup confirmation copy.
- Move/delete failures produce actionable per-file results.

### Risks And Decisions

- Perceptual hash false positives can destroy trust. Default to review, never auto-clean.
- Large scans need cancellation and progress; scans should not block normal viewing.

## 3. Compare And Overlay Mode

### User Promise

Compare two images locally with linked pan/zoom, side-by-side view, and opacity overlay for before/after inspection or near-duplicate review.

### MVP Scope

- Enter compare mode from current image plus next image, selected duplicate pair, or explicit "Compare with..." file.
- Show 2-up synchronized viewers with linked zoom, pan, rotate, and next/previous pair navigation.
- Add overlay mode with opacity slider and swap A/B.
- Keep compare mode local-only and independent of network sync features seen in other viewers.
- Persist no compare session state unless the user pins a pair in a future collection workflow.

### Architecture Sketch

- `CompareSession`: paths, decoded surfaces, active mode, linked transform state.
- `CompareViewModel`: command state and status text.
- Reuse `ImageLoader` and future decode cancellation hooks; do not duplicate codec code.
- Introduce transform synchronization tests before building complex UI chrome.

### Non-Goals

- No LAN sync or remote comparison.
- No annotation or merge tooling in the first slice.
- No automatic duplicate choice without the duplicate-cleanup workflow.

### Test Gates

- Linked pan/zoom applies symmetrical transforms.
- Rotation/flip state stays aligned between images.
- Missing secondary image returns to single-view mode with status.
- Overlay opacity clamps and preserves active image dimensions.
- Keyboard escape exits compare mode predictably.

### Risks And Decisions

- WPF layout can become janky with two large decoded surfaces. Start with bounded decode reuse and add performance evidence before polishing animation.

## 4. Archive And Book Navigation

### User Promise

Open image archives and multi-page sources as books, with page navigation that feels like folder navigation.

### MVP Scope

- Support `.zip` and `.cbz` first if dependency review approves the archive reader.
- Treat archives as read-only virtual folders with page count, page list, current page, and next/previous navigation.
- Extract individual entries to app temp/cache only as needed; do not unpack whole archives into user folders.
- Hide archive/book controls for normal single-image folders.
- Keep unsupported archive formats with clear copy: "archive mode is not installed yet" or "extract this archive first".

### Architecture Sketch

- `ImageSourceProvider`: abstraction over folder, archive, and document page sources.
- `ArchiveBookProvider`: enumerates supported entries, normalizes order, streams one entry at a time.
- `PageSequence` can remain the UI concept for multi-page labels; extend it only if archive page metadata requires more.
- Disposable temp files should live under app storage with bounded cleanup and diagnostics.

### Non-Goals

- No write-back into archives.
- No DRM/comic metadata parsing in MVP.
- No RAR/7z until license and native dependency costs are approved.

### Test Gates

- Archive enumeration ignores unsafe paths and unsupported entries.
- Natural ordering inside an archive is deterministic.
- Rapid page navigation cancels superseded extraction/decode.
- Temp cleanup removes extracted entries without touching source archives.
- Corrupt archives surface actionable error copy.

### Risks And Decisions

- Archive libraries have a history of zip-slip and decompression-bomb risks. Security review is mandatory before implementation.

## 5. Keyboard-First Peek Mode

### User Promise

Launch Images as a lightweight preview surface from Explorer, close it with Escape, and leave no surprise settings or network changes behind.

### MVP Scope

- Support existing/future `--peek` launch mode with minimal chrome, single-image focus, and close-on-Escape.
- Do not write window placement, recent folders, or viewer preferences unless the user opens the full app or changes a setting.
- Record startup timing: process start, app initialized, first file requested, first image displayed.
- Document shell-helper integration separately before changing file associations.

### Architecture Sketch

- `LaunchMode`: normal, peek.
- `LaunchOptions`: parsed once in app startup and passed into main view model/window.
- `PeekModeController`: chrome visibility, close behavior, persistence suppression.
- Startup timing can be logged through existing Serilog infrastructure and surfaced in diagnostics.

### Non-Goals

- No global keyboard hook in MVP.
- No automatic Explorer integration without a separate installer/security decision.
- No hidden resident background process.

### Test Gates

- `--peek` suppresses persistence writes.
- Escape closes peek but only dismisses overlays in normal mode.
- Startup timing logs are emitted for successful and failed opens.
- Invalid path in peek mode shows a concise failure and exits cleanly when appropriate.

### Risks And Decisions

- Shell integration can create trust friction. Keep the first implementation CLI-driven and documented.

## 6. Viewer-Side Adjustments

### User Promise

Temporarily adjust how an image looks for inspection or export without modifying the original.

### MVP Scope

- Add preview-only adjustments: exposure, contrast, saturation, grayscale, and sharpen.
- Show an active-adjustments status chip with reset and save-copy actions.
- Add `Save a copy with adjustments`; do not overwrite the current file.
- Persist no per-image adjustment state until sidecar/catalog decisions are complete.
- Use current `ImageExportService` path for output, with operation-status feedback.

### Architecture Sketch

- `AdjustmentState`: immutable values and active flag.
- `AdjustmentPipeline`: applies transformations to a display/export bitmap.
- `AdjustmentController`: command state, reset, save-copy orchestration.
- Future sidecar-backed edit stacks can reuse the state model but should not be part of MVP.

### Non-Goals

- No crop, brush, masks, layers, or history panel in the first slice.
- No RAW development pipeline.
- No automatic adjustment presets.

### Test Gates

- Reset returns the rendered output to the original state.
- Save-copy uses adjusted pixels and never overwrites source.
- Commands disable while export is active.
- Adjustment state clears on image change unless explicitly carried forward by a future batch workflow.

### Risks And Decisions

- WPF pixel transforms can be slow. Build the MVP with small, measured transformations and revisit Skia/Direct2D only with evidence.

## 7. Technical Pixel And Channel Tools

### User Promise

Inspect exact pixel data, transparency, channels, and HDR/technical images without leaving the viewer.

### MVP Scope

- Color picker with image coordinate, screen coordinate, RGB, HEX, HSL, alpha, and copy actions.
- Alpha checkerboard and background color toggle for transparent images.
- Channel toggles for red, green, blue, and alpha preview.
- Optional histogram panel after color picker lands.
- HDR/EXR exposure preview as a later sub-slice after test images and render behavior are understood.

### Architecture Sketch

- `PixelInspectionService`: maps viewport coordinate to source pixel coordinate and sample.
- `TransparencyPreviewState`: checkerboard/background mode.
- `ChannelPreviewState`: active channel mask and display label.
- Reuse image transform math already covered by viewport transform tests.

### Non-Goals

- No destructive channel editing.
- No mipmap/cubemap tooling in MVP.
- No color-management overhaul in the first pixel-tools slice.

### Test Gates

- Coordinate mapping remains correct across fit, zoom, pan, rotate, and flip.
- Color sampling handles transparent pixels and out-of-bounds pointer positions.
- Channel toggles do not mutate source pixels or export state.
- Keyboard focus and screen-reader names exist for picker/copy controls.

### Risks And Decisions

- Color values must be described honestly. Until color management is implemented, label sampled values as decoded/display-space values, not authoritative device-independent color.

## 8. Library And Metadata Workflows

### User Promise

Cull, rate, tag, filter, and move images locally while keeping user intent portable and reversible.

### MVP Scope

- Add app-local pick/reject and 1-5 rating first.
- Add keep/reject culling panel with move/copy target, undo, and clear per-folder review state.
- Add metadata sort/filter in the folder preview: rating, pick/reject, capture date, modified date, extension.
- Decide sidecar format before any portable writes. Preferred first sidecar: adjacent app-owned XMP or JSON sidecar; no embedded original writes by default.
- Expose "clear local library data" and "open sidecar location" where applicable.

### Architecture Sketch

- `LibraryCatalog`: app-local records keyed by asset identity.
- `AssetMetadataRecord`: rating, pick state, labels, sidecar status, timestamps.
- `CullingWorkflowController`: review queue, current decision, undo stack, move/copy execution.
- `MetadataWriteService`: future sidecar writer behind an interface and explicit confirmation.

### Non-Goals

- No Lightroom-style catalog import in MVP.
- No automatic cloud sync.
- No embedded EXIF/IPTC/XMP writes until a separate safety design is approved.
- No face/person tagging in the first library workflow.

### Test Gates

- Ratings and pick/reject state survive app restart.
- Missing/offline files stay visible as recoverable records, not silent deletes.
- Move/copy actions are atomic per file and report partial failures.
- Undo restores file location and review state where possible.
- Sidecar writes, once added, are covered for malformed paths and concurrent file changes.

### Risks And Decisions

- The app can become heavy if library mode leaks into the core viewer. Keep folder-local review as the first step and let full catalogs emerge only after users have reliable culling primitives.

## Recommended Sequencing

1. `RS-05` Peek mode: smallest product differentiator, validates launch-mode and persistence suppression seams.
2. `RS-01` Compare/overlay mode: high user value and reuses existing decode/viewport infrastructure.
3. `RS-06` Pixel tools: color picker and alpha background are compact, trust-building tools for broad-format users.
4. `RS-07` Viewer-side adjustments: pairs naturally with save-copy export and operation-status work.
5. `RS-04` Library/metadata culling: requires selection, sidecar/catalog decisions, and stronger undo semantics.
6. Duplicate cleanup: should reuse compare mode and culling actions instead of inventing its own review UI.
7. `RS-02` Archive/book navigation: useful but needs dependency/security review before code.
8. Local semantic search: strongest long-term differentiator, but model/index/dependency choices make it the heaviest first implementation.

## Open Decisions Before Implementation

- Which app-local catalog schema owns asset identity, and how much is shared with settings?
- Is the first portable sidecar XMP, JSON, or both?
- Which dependency review template should be used for ONNX models, archive libraries, and perceptual hashing?
- Should compare mode be a separate window, an in-window split view, or both over time?
- What is the maximum acceptable model/download size for local semantic search?
- Should package-manager distribution wait for code signing or ship with checksum-first guidance?
