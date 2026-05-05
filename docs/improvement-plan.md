# Images Improvement Plan

This document tracks the quality backlog from the May 2026 engineering review. It is a companion to `ROADMAP.md`: the roadmap describes product strategy and major feature direction; this plan tracks concrete engineering and UX quality work that should be completed incrementally.

Status values:
- `Planned`: accepted, not started.
- `In progress`: implementation has started.
- `Blocked`: needs a design decision or prerequisite.
- `Done`: completed and verified.

## Latest Completed Slice

`IP-15` is complete. CI now runs the same core verification path used locally: whitespace diff check, version-sync gate, vulnerable-package gate, Release build, tests, and CLI smoke commands. The release workflow uses the same reusable version-sync script.

## Next Focus

The next recommended slice is the first `IP-02` extraction: move clipboard import or folder-preview behavior out of `MainViewModel` behind a focused service/controller, then add tests around the extracted logic.

## Priority Backlog

| ID | Priority | Status | Area | Goal | Acceptance criteria |
| --- | --- | --- | --- | --- | --- |
| IP-01 | P0 | Planned | Test seams | Add seams around static/global services such as update checks, storage paths, thumbnail cache, shell integration, and clocks. | Unit tests can drive update-check retry policy, storage fallback, and thumbnail/cache behavior without real network or user profile state. |
| IP-02 | P0 | Planned | MainViewModel structure | Split the oversized main view model into focused controllers/services while preserving current behavior. | OCR, folder preview, clipboard import, reload, rename, metadata, and update-check logic are independently readable and have narrower dependencies. |
| IP-03 | P0 | Planned | UI state tests | Add regression coverage for risky WPF state transitions. | Tests or smoke harnesses cover reload failure, external file changes, OCR cancellation, thumbnail cancellation, rename debounce, and disabled/busy states. |
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
| IP-16 | P2 | Planned | Product differentiators | Track large future differentiators without disrupting the hardening sequence. | Local semantic search, duplicate cleanup, Squoosh-style visual comparison/export, and stronger library/metadata workflows have scoped design docs before implementation. |

## Implementation Order

1. Extract clipboard import and folder preview from `MainViewModel` as the first `IP-02` slices.
2. Add UI-state tests for the extracted controllers under `IP-03`.
3. Build diagnostics/status UX from existing system-info, codec, OCR, and storage services under `IP-06`.
4. Iterate on first-run, long-running, and empty/error states once diagnostics surfaces are stable.
5. Scope `IP-16` design docs once near-term reliability and testability slices are stable.

## Progress Log

- 2026-05-05: Created this improvement tracker and completed `IP-13` by adding shared shell and clipboard helpers used by About, crash dialog, settings, and main viewer actions.
- 2026-05-05: Completed `IP-05` by adding update-check seams and 10 non-network tests for release parsing, retry-state policy, trusted URLs, due logic, and state-file behavior.
- 2026-05-05: Completed `IP-15` by adding CI verification, a reusable version-sync script, release-workflow reuse of that script, vulnerability scanning, and CLI smoke checks.

## Verification Standard

Each completed slice should run, at minimum:
- `git diff --check`
- `dotnet build Images.sln -c Release`
- `dotnet test Images.sln -c Release --no-build`
- `Images.exe --system-info`

Additional feature-specific tests or smoke commands should be added when a slice changes codecs, export behavior, installer behavior, network behavior, or WPF interaction state.
