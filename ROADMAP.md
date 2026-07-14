# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, GUI/visual validation, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when those are unblocked.

## Actionable Work

- [ ] P3 — Incremental rescan re-hashes files whose sidecar check transiently fails
  Why: `CatalogService.IsUnchanged` treats any exception (e.g. a sidecar momentarily locked by cloud-sync/AV) as "changed", forcing a full SHA-256 re-hash of that file on every rescan under contention. Safe (never misses a real change) but defeats the incremental optimization. Consider distinguishing transient IO errors from genuine change signals.
  Where: `src/Images/Services/CatalogService.cs` (IsUnchanged / ReadSidecarFileSummary)

The remaining research item is decision-gated (Ghostscript-source AGPL mechanism) and lives in `Roadmap_Blocked.md`. Refill via a research pass (`RESEARCH.md`) when ready.
