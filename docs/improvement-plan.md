# Images Improvement Plan

This document tracks the quality backlog from the May 2026 engineering review. It is a companion to `ROADMAP.md`: the roadmap describes product strategy and major feature direction; this plan tracks concrete engineering and UX quality work that should be completed incrementally.

Status values:
- `Planned`: accepted, not started.
- `In progress`: implementation has started.
- `Blocked`: needs a design decision or prerequisite.
- `Done`: completed and verified.

## Latest Completed Slice

`IP-02D` is complete. Rename target validation now lives at the rename service boundary, so unsupported extension-unlocked renames are rejected before any file-system mutation, with service and ViewModel state coverage protecting the previous stale-path failure.

## Next Focus

The next recommended slice is `IP-02E`: continue extracting the oversized `MainViewModel`, with metadata loading or reload state as the next practical target because both still combine background work, cancellation/refresh state, and user-facing failure copy.

## Research Inputs

- `docs/research/open-source-viewer-scan-2026-05-05.md`: open-source viewer scan covering ImageGlass, nomacs, PicView, NeeView, QuickLook, Geeqie, gThumb, qView, JPEGView, Tacent View, Minimal Image Viewer, and LightningView. The scan confirms `IP-02` and `IP-03` as the safest next work, then recommends future design docs for local comparison, archive/book navigation, metadata culling, peek launch mode, viewer-side adjustments, technical image tools, cache health, and distribution trust.

## Priority Backlog

| ID | Priority | Status | Area | Goal | Acceptance criteria |
| --- | --- | --- | --- | --- | --- |
| IP-01 | P0 | Planned | Test seams | Add seams around static/global services such as update checks, storage paths, thumbnail cache, shell integration, and clocks. | Unit tests can drive update-check retry policy, storage fallback, and thumbnail/cache behavior without real network or user profile state. |
| IP-02 | P0 | In progress | MainViewModel structure | Split the oversized main view model into focused controllers/services while preserving current behavior. | OCR, folder preview, clipboard import, reload, rename, metadata, and update-check logic are independently readable and have narrower dependencies. |
| IP-03 | P0 | In progress | UI state tests | Add regression coverage for risky WPF state transitions. | Tests or smoke harnesses cover reload failure, external file changes, OCR cancellation, thumbnail cancellation, rename debounce, and disabled/busy states. |
| IP-04 | P1 | Planned | Background tasks | Improve observability and ownership for fire-and-forget work. | Thumbnail generation, metadata reads, preloading, clipboard pruning, cache eviction, and update checks have clear cancellation/ownership and structured logging. |
| IP-05 | P1 | Done | Update checks | Add focused update-check tests. | Timeout, network failure, HTTP failure, malformed release payload, newer release, current release, and trusted URL normalization are covered. |
| IP-06 | P1 | Planned | Diagnostics UX | Add a compact diagnostics/status pane. | Users can inspect OCR, Ghostscript, Magick.NET, logs, storage paths, and last update-check state from the app without a terminal. |
| IP-07 | P1 | Planned | First run | Improve first-run guidance. | New users can discover supported formats, OCR readiness, document-preview requirements, privacy defaults, and recovery links without reading docs. |
| IP-08 | P1 | Planned | Long-running state | Standardize busy/progress/cancel affordances. | OCR, metadata reads, document decode, large exports, and background update checks use consistent status text, disabled states, and cancellation where available. |
| IP-09 | P1 | Planned | Empty/error states | Extend polished failure and empty states across secondary flows. | Empty folder, unsupported clipboard data, missing recent folders, thumbnail-cache failure, and offline update checks have calm, actionable feedback. |
| IP-10 | P2 | Planned | Cache health | Expose thumbnail cache health controls. | Settings/About can show cache size and clear or rebuild the disposable thumbnail cache safely. |
| IP-11 | P2 | Planned | Stress testing | Add large-folder and volatile-folder stress coverage. | Navigation and thumbnail behavior are validated with thousands of files, deleted files, slow folders, and rapid directory changes. |
| IP-12 | P2 | Planned | Decode/export corpus | Add a small format corpus for decode/export regression tests. | Representative PNG, JPEG, WebP, TIFF, GIF/APNG, and document/vector samples protect codec upgrades and export behavior. |
| IP-13 | P1 | Done | Shell/clipboard integration | Centralize opening URLs/files/folders and copying text. | About, crash dialog, settings, and main viewer use shared helpers with consistent error behavior and safer Explorer argument handling. |
| IP-14 | P2 | Planned | Settings persistence | Strengthen settings schema and corruption tests. | Tests cover corruption quarantine, unavailable storage, defaults, migration behavior, and future timestamp handling. |
| IP-15 | P1 | Done | CI/release gates | Ensure CI exercises the real verification path. | CI runs solution build, tests, whitespace check, vulnerability gate, version sync gate, and CLI smoke commands used by local release validation. |
| IP-16 | P2 | Planned | Product differentiators | Track large future differentiators without disrupting the hardening sequence. | Local semantic search, duplicate cleanup, compare/overlay mode, archive/book navigation, peek launch mode, viewer-side adjustments, technical pixel/channel tools, and stronger library/metadata workflows have scoped design docs before implementation. |
| IP-17 | P2 | Planned | Distribution trust | Reduce Windows install trust friction once the next stable release is ready. | WinGet and Scoop publishing are scoped, checksums remain part of releases, and a code-signing decision doc covers certificate cost, SmartScreen reputation, release cadence, and fallback verification instructions. |

## Source-Derived Workstream Tracker

| ID | Priority | Status | Workstream | Source signal | Planned sequence |
| --- | --- | --- | --- | --- | --- |
| RS-01 | P2 | Planned | Local comparison mode | nomacs synchronized views and opacity overlay | Design doc, UI-state tests, local 2-up compare, linked pan/zoom, linked next/previous, overlay opacity. |
| RS-02 | P2 | Planned | Archive/book navigation | NeeView book model, PicView archive navigation, Tacent folder continuity | Dependency review, streaming/temp-safe archive reader, page list/history, archive-only controls, smoke corpus. |
| RS-03 | P0 | In progress | Folder sorting and Explorer fidelity | ImageGlass Explorer sort sync, NeeView/Geeqie folder models | App-owned sort modes first, visible sort control, sort-state tests, later Explorer saved-search/sort investigation. |
| RS-04 | P2 | Planned | Metadata culling workflow | Geeqie XMP keywords/search, gThumb catalogs/comments, Tacent metadata sort | Sidecar decision, rating/reject model, folder filters, keep/reject/move actions, undo and no-original-write defaults. |
| RS-05 | P1 | In progress | Keyboard-first peek mode | QuickLook Spacebar preview, qView/JPEGView minimal chrome | Existing `--peek` foundation, startup timing logs, close-on-Esc smoke test, shell-helper documentation. |
| RS-06 | P2 | Planned | Technical pixel tools | ImageGlass color/channel tools, Tacent alpha/HDR tools | Color picker, alpha checkerboard/background toggle, RGB/alpha channel toggles, HDR/EXR exposure preview. |
| RS-07 | P2 | Planned | Non-destructive viewer adjustments | JPEGView and Minimal Image Viewer processing controls | Preview-only adjustment state, reset affordance, save-copy-with-adjustments path, active-adjustment status. |
| RS-08 | P1 | Planned | Large-folder and cache confidence | Tacent thumbnail speed, LightningView large-image focus | Cache size/clear UI, first-thumbnail timings, large/volatile folder tests, cache-unavailable fallback. |
| RS-09 | P1 | Planned | Settings, shortcuts, themes, localization | ImageGlass language/theme packs, PicView searchable shortcuts, NeeView deep settings | Searchable shortcuts, central command labels/tooltips, light/system/high-contrast themes, localization plan. |
| RS-10 | P2 | Planned | Distribution trust | ImageGlass signing friction, JPEGView WinGet/Scoop distribution | WinGet/Scoop manifests, release checksum continuity, code-signing decision doc, user verification copy. |

## Implementation Order

1. Add UI-state tests for the extracted folder-preview and clipboard-import controllers under `IP-03`.
2. Continue `IP-02` with the next narrow extraction after those state contracts are stable.
3. Build diagnostics/status UX from existing system-info, codec, OCR, and storage services under `IP-06`.
4. Iterate on first-run, long-running, and empty/error states once diagnostics surfaces are stable.
5. Scope `IP-16` design docs once near-term reliability and testability slices are stable.
6. Scope `IP-17` after the next stable release artifact path is verified.

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

## Verification Standard

Each completed slice should run, at minimum:
- `git diff --check`
- `dotnet build Images.sln -c Release`
- `dotnet test Images.sln -c Release --no-build`
- `Images.exe --system-info`

Additional feature-specific tests or smoke commands should be added when a slice changes codecs, export behavior, installer behavior, network behavior, or WPF interaction state.
