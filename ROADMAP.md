# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Deferred audit findings (2026-07-03)

Surfaced during the v0.2.16 deep audit but not fixed in that pass.

- [ ] P3 — SuperResolution/BackgroundRemoval are dead code with a latent tile-compositing bug
  Why: Neither service has callers or an approved model group, so `IsAvailable()` is always false. When wired up, `SuperResolutionService` composites tiles at the caller's assumed scale factor rather than the model's actual output ratio.
  Where: `src/Images/Services/SuperResolutionService.cs`, `BackgroundRemovalService.cs`
- [ ] P3 — Listen-mode `--perf-report` mode and ExifTool write path lack live callers
  Why: `ExifToolService.Run` has no production caller (scaffolding); its UTF-8 safety is only exercised by tests. Track for when a writer is wired up.
  Where: `src/Images/Services/ExifToolService.cs`
- [ ] P3 — TileService background eviction can theoretically delete an in-use pyramid
  Why: `EvictIfOverCap` can remove a >1 GB single pyramid mid-display; bounded in practice by the 32768-px Magick dimension caps but not guarded explicitly.
  Where: `src/Images/Services/TileService.cs`
- [ ] P3 — Test isolation: global BackgroundTaskTracker counters flake under parallel execution
  Why: `UpdateCheckControllerTests.CheckAsync_WhenManualCheckRuns_RecordsTrackedUpdateTask` asserts `before.Started + 1 == after.Started` on the process-wide `BackgroundTaskTracker.Snapshot`; a concurrent preload/thumbnail task in another collection perturbs the counter, so it fails intermittently in full-suite runs (passes in isolation). Same class of flake seen once in `SemanticSearchServiceTests.Search_AppliesFolderFilter`. Fix by snapshotting per-name counters or serializing these tests into a dedicated collection.
  Where: `tests/Images.Tests/UpdateCheckControllerTests.cs`, `src/Images/Services/BackgroundTaskTracker.cs`

## Research-Driven Additions

