# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, GUI/visual validation, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when those are unblocked.

## Actionable Work

The remaining research item is decision-gated (Ghostscript-source AGPL mechanism) and lives in `Roadmap_Blocked.md`. Refill via a research pass (`RESEARCH.md`) when ready.

## Research-Driven Additions

Unblocked, net-new items only. HDR display (V100-06), full color management (V100-05), GPS map overlay (V20-23), and the Explorer thumbnail handler (V70-04) remain correctly parked in `Roadmap_Blocked.md` and are not duplicated here.

### 2026-07-16 research pass (V110 block)

The actionable V110 items shipped (see CHANGELOG and git history). The remaining V110 items (V110-09 MainViewModel extraction, V110-12 archive-to-archive rolling navigation, V110-13 hold-to-scrub fly-through, V110-16 gain-map grayscale overlay) require foreground GUI verification of interactive behavior and are parked in `Roadmap_Blocked.md` until a GUI-verifiable session is available.
