# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Audit-Surfaced Items

## Research-Driven Additions

### P2

### P3

- [ ] P3 - Scout signed Windows preview/thumbnail handler integration
  Why: `--peek` covers external preview workflows, but Explorer Preview Pane and thumbnails require shell-extension trust, install, rollback, and signing evidence before implementation.
  Evidence: `docs/peek-mode.md`, `installer/Images.iss`, PowerToys Peek, Microsoft preview-handler guidance, ImageGlass shell thumbnail settings
  Touches: `docs/peek-mode.md`, `installer/Images.iss`, `src/Images/Services/ShellIntegration.cs`, `scripts/Test-ReleaseDiagnostics.ps1`
  Acceptance: A decision spike documents COM/MSIX/preview-handler options, signing requirements, uninstall rollback, crash isolation, and a minimal non-registered prototype or fixture; no installer registration ships until code signing is unblocked.
  Complexity: M
