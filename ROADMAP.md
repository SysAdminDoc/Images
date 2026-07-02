# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Audit-Surfaced Items

- [ ] P3 — Replace hardcoded Mocha palette colors in PerspectiveCorrectionWindow canvas overlays
  Why: Handle strokes, polygon fills, and label backgrounds use hardcoded Mocha hex values that don't adapt to Latte or HighContrast themes. The label backgrounds (#11111B at ~59%) appear as dark spots in Latte mode.
  Where: `src/Images/PerspectiveCorrectionWindow.xaml.cs` lines 252-303

## Research-Driven Additions

