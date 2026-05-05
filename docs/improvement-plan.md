# Images Improvement Plan

This document tracks the quality backlog from the May 2026 engineering review. It is a companion to `ROADMAP.md`: the roadmap describes product strategy and major feature direction; this plan tracks concrete engineering and UX quality work that should be completed incrementally.

Status values:
- `Planned`: accepted, not started.
- `In progress`: implementation has started.
- `Blocked`: needs a design decision or prerequisite.
- `Done`: completed and verified.

## Latest Completed Slice

`IP-17A` is complete, closing `IP-17`. `docs/distribution-trust.md` now scopes WinGet and Scoop publishing, preserves checksum continuity, and records the code-signing decision path for post-release trust work.

## Next Focus

The improvement-plan backlog is complete. Continue with the source-derived workstream tracker or the main `ROADMAP.md` feature backlog.

## Research Inputs

- `docs/research/open-source-viewer-scan-2026-05-05.md`: open-source viewer scan covering ImageGlass, nomacs, PicView, NeeView, QuickLook, Geeqie, gThumb, qView, JPEGView, Tacent View, Minimal Image Viewer, and LightningView. The scan confirms `IP-02` and `IP-03` as the safest next work, then recommends future design docs for local comparison, archive/book navigation, metadata culling, peek launch mode, viewer-side adjustments, technical image tools, cache health, and distribution trust.

## Priority Backlog

| ID | Priority | Status | Area | Goal | Acceptance criteria |
| --- | --- | --- | --- | --- | --- |
| IP-01 | P0 | Done | Test seams | Add seams around static/global services such as update checks, storage paths, thumbnail cache, shell integration, and clocks. | Unit tests can drive update-check retry policy, storage fallback, and thumbnail/cache behavior without real network or user profile state. |
| IP-02 | P0 | Done | MainViewModel structure | Split the oversized main view model into focused controllers/services while preserving current behavior. | OCR, folder preview, clipboard import, reload, rename, metadata, and update-check logic are independently readable and have narrower dependencies. |
| IP-03 | P0 | Done | UI state tests | Add regression coverage for risky WPF state transitions. | Tests or smoke harnesses cover reload failure, external file changes, OCR cancellation, thumbnail cancellation, rename debounce, and disabled/busy states. |
| IP-04 | P1 | Done | Background tasks | Improve observability and ownership for fire-and-forget work. | Thumbnail generation, metadata reads, preloading, clipboard pruning, cache eviction, and update checks have clear cancellation/ownership and structured logging. |
| IP-05 | P1 | Done | Update checks | Add focused update-check tests. | Timeout, network failure, HTTP failure, malformed release payload, newer release, current release, and trusted URL normalization are covered. |
| IP-06 | P1 | Done | Diagnostics UX | Add a compact diagnostics/status pane. | Users can inspect OCR, Ghostscript, Magick.NET, logs, storage paths, and last update-check state from the app without a terminal. |
| IP-07 | P1 | Done | First run | Improve first-run guidance. | New users can discover supported formats, OCR readiness, document-preview requirements, privacy defaults, and recovery links without reading docs. |
| IP-08 | P1 | Done | Long-running state | Standardize busy/progress/cancel affordances. | OCR, metadata reads, document decode, large exports, and background update checks use consistent status text, disabled states, and cancellation where available. |
| IP-09 | P1 | Done | Empty/error states | Extend polished failure and empty states across secondary flows. | Empty folder, unsupported clipboard data, missing recent folders, thumbnail-cache failure, and offline update checks have calm, actionable feedback. |
| IP-10 | P2 | Done | Cache health | Expose thumbnail cache health controls. | Settings/About can show cache size and clear or rebuild the disposable thumbnail cache safely. |
| IP-11 | P2 | Done | Stress testing | Add large-folder and volatile-folder stress coverage. | Navigation and thumbnail behavior are validated with thousands of files, deleted files, slow folders, and rapid directory changes. |
| IP-12 | P2 | Done | Decode/export corpus | Add a small format corpus for decode/export regression tests. | Representative PNG, JPEG, WebP, TIFF, GIF/APNG, and document/vector samples protect codec upgrades and export behavior. |
| IP-13 | P1 | Done | Shell/clipboard integration | Centralize opening URLs/files/folders and copying text. | About, crash dialog, settings, and main viewer use shared helpers with consistent error behavior and safer Explorer argument handling. |
| IP-14 | P2 | Done | Settings persistence | Strengthen settings schema and corruption tests. | Tests cover corruption quarantine, unavailable storage, defaults, migration behavior, and future timestamp handling. |
| IP-15 | P1 | Done | CI/release gates | Ensure CI exercises the real verification path. | CI runs solution build, tests, whitespace check, vulnerability gate, version sync gate, and CLI smoke commands used by local release validation. |
| IP-16 | P2 | Done | Product differentiators | Track large future differentiators without disrupting the hardening sequence. | Local semantic search, duplicate cleanup, compare/overlay mode, archive/book navigation, peek launch mode, viewer-side adjustments, technical pixel/channel tools, and stronger library/metadata workflows have scoped design docs before implementation. |
| IP-17 | P2 | Done | Distribution trust | Reduce Windows install trust friction once the next stable release is ready. | WinGet and Scoop publishing are scoped, checksums remain part of releases, and a code-signing decision doc covers certificate cost, SmartScreen reputation, release cadence, and fallback verification instructions. |

## Source-Derived Workstream Tracker

| ID | Priority | Status | Workstream | Source signal | Planned sequence |
| --- | --- | --- | --- | --- | --- |
| RS-01 | P2 | Planned | Local comparison mode | nomacs synchronized views and opacity overlay | Design doc, UI-state tests, local 2-up compare, linked pan/zoom, linked next/previous, overlay opacity. |
| RS-02 | P2 | In progress | Archive/book navigation | NeeView book model, PicView archive navigation, Tacent folder continuity | ZIP/CBZ read-only pages, page scrubber, archive-only controls, edge page-turn zones, reader-mode keyboard routing, remembered read position, side-panel book history, explicit cover promotion, and RAR/7z runtime review shipped; next: additional reader comfort and spread handling. |
| RS-03 | P0 | In progress | Folder sorting and Explorer fidelity | ImageGlass Explorer sort sync, NeeView/Geeqie folder models | App-owned sort modes first, visible sort control, sort-state tests, later Explorer saved-search/sort investigation. |
| RS-04 | P2 | Planned | Metadata culling workflow | Geeqie XMP keywords/search, gThumb catalogs/comments, Tacent metadata sort | Sidecar decision, rating/reject model, folder filters, keep/reject/move actions, undo and no-original-write defaults. |
| RS-05 | P1 | Done | Keyboard-first peek mode | QuickLook Spacebar preview, qView/JPEGView minimal chrome | `--peek` foundation, startup timing logs, close-on-Esc smoke script, parser regression tests, and shell-helper documentation are complete. |
| RS-06 | P2 | Planned | Technical pixel tools | ImageGlass color/channel tools, Tacent alpha/HDR tools | Color picker, alpha checkerboard/background toggle, RGB/alpha channel toggles, HDR/EXR exposure preview. |
| RS-07 | P2 | Planned | Non-destructive viewer adjustments | JPEGView and Minimal Image Viewer processing controls | Preview-only adjustment state, reset affordance, save-copy-with-adjustments path, active-adjustment status. |
| RS-08 | P1 | Planned | Large-folder and cache confidence | Tacent thumbnail speed, LightningView large-image focus | Cache size/clear UI, first-thumbnail timings, large/volatile folder tests, cache-unavailable fallback. |
| RS-09 | P1 | Planned | Settings, shortcuts, themes, localization | ImageGlass language/theme packs, PicView searchable shortcuts, NeeView deep settings | Searchable shortcuts, central command labels/tooltips, light/system/high-contrast themes, localization plan. |
| RS-10 | P2 | Planned | Distribution trust | ImageGlass signing friction, JPEGView WinGet/Scoop distribution | WinGet/Scoop manifests, release checksum continuity, code-signing decision doc, user verification copy. |

## Implementation Order

1. Iterate on long-running and empty/error states now that diagnostics and first-run guidance are stable.
2. Harden background-task ownership and observability under `IP-04`.
3. Scope `IP-16` design docs once near-term reliability and testability slices are stable.
4. Scope `IP-17` after the next stable release artifact path is verified.

## Progress Log

- 2026-05-05: Created this improvement tracker and completed `IP-13` by adding shared shell and clipboard helpers used by About, crash dialog, settings, and main viewer actions.
- 2026-05-05: Completed `IP-05` by adding update-check seams and 10 non-network tests for release parsing, retry-state policy, trusted URLs, due logic, and state-file behavior.
- 2026-05-05: Completed `IP-15` by adding CI verification, a reusable version-sync script, release-workflow reuse of that script, vulnerability scanning, and CLI smoke checks.
- 2026-05-05: Added an open-source viewer research scan and converted its findings into explicit future tracks for compare mode, archive/book navigation, metadata culling, peek launch mode, technical image tools, viewer-side adjustments, cache health, shortcut/settings polish, and distribution trust.
- 2026-05-05: Completed `IP-02A` / started `RS-03` by extracting folder-preview thumbnail orchestration into `FolderPreviewController`, adding app-owned sort modes, adding a visible sort control, and covering sort/preload behavior with tests.
- 2026-05-05: Completed `IP-02B` by extracting clipboard import and clipboard-temp pruning into `ClipboardImportService`, adding deterministic file-list/image/storage/time/GUID seams, and covering clipboard import and pruning behavior with tests.
- 2026-05-05: Completed `IP-03A` by adding isolated `MainViewModel` state tests for folder-preview sort state, filmstrip persistence, paste-from-clipboard opening, and Recycle Bin confirmation preference behavior.
- 2026-05-05: Completed `IP-02C` by extracting Recycle Bin confirmation/settings/delete execution into `RecycleBinDeleteService` and covering skip-confirmation, cancel, opt-out persistence, missing-file, and send-failure outcomes with tests.
- 2026-05-05: Completed `IP-02D` by moving rename target-extension validation into `RenameService`, preventing unsupported extension-unlocked renames from moving files outside the app's navigable format set, and covering the service and ViewModel stale-path regression.
- 2026-05-05: Completed `IP-02E` by extracting photo metadata HUD loading into `PhotoMetadataController`, adding owned cancellation/status handling, and covering success, superseded-result, and timeout outcomes with dispatcher-backed tests.
- 2026-05-05: Completed `IP-02F` by extracting external-edit watcher/debounce/reload feedback into `ExternalEditReloadController` and covering coalesced reloads, failed reload notifications, disarm cancellation, and watcher creation failure.
- 2026-05-05: Completed `IP-02G` by extracting OCR busy/active overlay workflow into `OcrWorkflowController` and covering no-image, success, no-text, cancellation, and stale-result outcomes.
- 2026-05-05: Completed `IP-02H` and closed `IP-02` by extracting update-check UI state into `UpdateCheckController` and covering background skip, newer-release, current-release, error, and release-link opening outcomes.
- 2026-05-05: Completed `IP-03B` by adding a deterministic folder-preview thumbnail loader seam and regression tests for clear and superseded-refresh cancellation paths.
- 2026-05-05: Completed `IP-03C` by adding an internal `MainViewModel` controller-injection seam and tests that relay metadata, OCR, and update-check state through the view model.
- 2026-05-05: Completed `IP-03D` and closed `IP-03` by keeping folder refresh enabled after external current-file removal and covering rename debounce, stale-folder recovery, and command enablement states.
- 2026-05-05: Completed `IP-06A` by adding a compact About diagnostics status section for OCR, Ghostscript, Magick.NET, logs, storage, and update-check state with regression-tested status composition.
- 2026-05-05: Completed `IP-06B` and closed `IP-06` by adding diagnostics-local actions for copying system info, copying the codec report, opening logs, and opening the app data folder.
- 2026-05-05: Completed `IP-07A` and closed `IP-07` by turning the empty first-run card into capability-backed guidance for privacy, format support, OCR readiness, document previews, and Settings/Diagnostics recovery links.
- 2026-05-05: Completed `IP-08A` by adding shared operation-status feedback for manual reload, Save a copy, and GPS-strip work, with command disabled-state coverage for mutating image actions during active operations.
- 2026-05-05: Completed `IP-08B` by adding update-check busy/status state to `UpdateCheckController`, relaying it through the main view model, suppressing duplicate manual checks, and showing a side-panel live status while GitHub Releases is contacted.
- 2026-05-05: Completed `IP-08C` and closed `IP-08` by extending operation-status feedback to file-open dialog decodes and multi-page navigation, with regression coverage for page-turn busy state.
- 2026-05-05: Completed `IP-09A` by adding persistent secondary status feedback for unsupported clipboard data, empty recent folders, and stale recent-folder recovery, with regression coverage for all three flows.
- 2026-05-05: Completed `IP-09B` and closed `IP-09` by retaining actionable update-check failure status, marking thumbnail placeholder failures, and relaying thumbnail-generation failure counts through the main secondary status surface.
- 2026-05-05: Completed `IP-04A` by adding a shared `BackgroundTaskTracker`, routing folder thumbnails, metadata reads, preload decodes, clipboard-temp pruning, and thumbnail-cache eviction through it, and exposing session task counts in diagnostics with regression coverage.
- 2026-05-05: Completed `IP-04B` and closed `IP-04` by adding async update-check tracking plus thumbnail-cache health diagnostics and focused tests for async task tracking, update-check tracking, and cache health scans.
- 2026-05-05: Completed `IP-01A` and closed `IP-01` by adding deterministic app-storage, settings-default, and thumbnail-cache-default seams plus regression tests for fallback, unavailable storage, and unsafe relative path segments.
- 2026-05-05: Completed `IP-10A` and closed `IP-10` by adding About diagnostics actions to open and clear the disposable thumbnail cache, backed by a safe cache-clear service and regression tests.
- 2026-05-05: Completed `IP-14A` and closed `IP-14` by hardening settings corruption quarantine naming and adding coverage for corrupt DB reset, schema migration, unavailable storage, primitive defaults, and existing future timestamp behavior.
- 2026-05-05: Completed `IP-11A` and closed `IP-11` by adding large-folder navigation coverage, volatile rescan coverage, enumeration-failure recovery coverage, and bounded folder-preview thumbnail request coverage for thousands of files.
- 2026-05-05: Completed `IP-12A` and closed `IP-12` by adding generated decode/export corpus coverage for PNG, JPEG, WebP, multi-page TIFF, animated GIF, SVG vector decode, and APNG export round-trips without binary fixtures.
- 2026-05-05: Completed `IP-16A` and closed `IP-16` by adding scoped product-differentiator design guidance for semantic search, duplicate cleanup, compare/overlay mode, archive/book navigation, peek launch mode, viewer adjustments, technical pixel tools, and metadata/culling workflows.
- 2026-05-05: Completed `IP-17A` and closed `IP-17` by adding distribution-trust guidance for WinGet, Scoop, checksum continuity, signing options, SmartScreen expectations, and user verification copy.
- 2026-05-05: Completed roadmap `X-03` by adding an optional-runtime integration policy covering license, redistribution, CVE tracking, binary provenance, process isolation, network behavior, test corpus, and release gates.
- 2026-05-05: Started `RS-02` / `V20-33` by adding ZIP/CBZ read-only archive page loading through `ImageLoader`, including natural page ordering, unsafe-entry filtering, recursive-archive skipping, folder navigation inclusion, and generated regression tests.
- 2026-05-05: Continued `RS-02` / `V20-33` by adding a shared multi-page scrubber for archive books and existing multi-page documents, with view-model regression coverage for direct page jumps.
- 2026-05-05: Completed `RS-05` by adding local startup and first-image timing logs for normal/peek launches, exact `--peek` parser tests, and shell-helper documentation with a close-on-Escape manual smoke script.
- 2026-05-05: Continued `RS-02` / `V20-36` by adding local remembered read position and continue-reading feedback for ZIP/CBZ archive books, with settings-key and view-model resume coverage.
- 2026-05-05: Continued `RS-02` / `V20-33` by adding a persisted side-panel book history for ZIP/CBZ archives, including page progress, missing-file recovery feedback, and service/view-model coverage.
- 2026-05-05: Continued `RS-02` / `V20-33` by promoting explicit cover/front/folder entries ahead of natural archive page order, with decoder provenance and archive-service coverage.
- 2026-05-05: Continued `RS-02` / `V20-36` by adding archive-only side-panel reader controls for current page progress plus first/previous/next/last page actions.
- 2026-05-05: Continued `RS-02` / `V20-36` by adding archive-only edge page-turn click zones and routing arrow/Home/End keys to book pages while a ZIP/CBZ book is active.
- 2026-05-05: Continued `RS-02` / `V20-33` by documenting the RAR/7z archive-runtime decision in `docs/archive-runtime-review.md`, keeping ZIP/CBZ first-party while gating future runtimes on license, provenance, CVE, isolation, and generated-corpus checks.

## Verification Standard

Each completed slice should run, at minimum:
- `git diff --check`
- `dotnet build Images.sln -c Release`
- `dotnet test Images.sln -c Release --no-build`
- `Images.exe --system-info`

Additional feature-specific tests or smoke commands should be added when a slice changes codecs, export behavior, installer behavior, network behavior, or WPF interaction state.
