# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, GUI/visual validation, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when those are unblocked.

## Research-Driven Additions

Net-new, evidence-grounded, code-ready items from the 2026-07-12 (pass 2) research. The feature backlog is otherwise drained; larger bets remain in `Roadmap_Blocked.md`.

### P2 — reliability / distribution

- [ ] P2 — Cut the v0.2.26 release from the accumulated Unreleased CHANGELOG
  Why: ~29 lines of shipped user-facing features (opt-in color management, loupe, live pixel readout, zoom-lock, transparency checkerboard, zoom-to-selection, session restore, stop-at-ends, metadata-preserving Save-a-copy, Magick.NET 14.15 security bump) sit unreleased since v0.2.25 with no version cut. The unsigned ZIP/installer path is not gated on signing.
  Evidence: `CHANGELOG.md` `## Unreleased` section; `scripts/Test-ReleaseReadiness.ps1` / local release scripts; git log since `45494cb` (release Images 0.2.25).
  Touches: version strings (`src/Images/Images.csproj`, `app.manifest`, installer defaults, README badge), `CHANGELOG.md` (promote Unreleased → `v0.2.26 - <date>`), run release-readiness gates (version sync, tests, vulnerable/deprecated scan, localization parity, provenance docs, package-manifest hashes, WinGet/Scoop manifest validation), tag + GitHub Release with the unsigned artifacts.
  Acceptance: all version strings match 0.2.26, release-readiness script passes, a GitHub Release v0.2.26 exists with the portable ZIP + installer attached and downloadable.
  Complexity: M

### P3 — dependency currency


### P2 — hardening of today's shipped features (2026-07-12 pass 3 code audit)

### P3 — new-feature polish (2026-07-12 pass 3 code audit)

- [ ] P3 — Keyboard-invocable loupe + adjustable magnification
  Why: The loupe and zoom-to-selection are now documented in the `?` cheatsheet, but remain pointer-only. A keyboard user cannot trigger a cursor-relative loupe, and `LoupeFactor` is an unexposed DP fixed at 2x. Needs a design decision on how a keyboard user positions/triggers a magnifier (e.g. a toggle mode centered on the viewport or last cursor) before implementation.
  Evidence: `src/Images/Controls/ZoomPanImage.cs` (`LoupeFactor`, middle-button-only loupe); ImageGlass #1425; RESEARCH.md.
  Touches: `CommandShortcutService.cs` + `MainViewModel` palette (a "Toggle loupe" command that follows the caret/viewport center), a Settings entry for `LoupeFactor`, localized strings.
  Acceptance: the loupe can be toggled and positioned without a mouse and its magnification is adjustable in Settings; localization parity passes.
  Complexity: M

- [ ] P3 — Warn (or preserve frames) when Save-a-copy flattens an animated/multi-page source
  Why: `SaveCopyWithC2paHandoff` reloads via `new MagickImage(sourcePath)` (single frame), so copying an animated GIF or multi-page TIFF silently produces a frame-0 flatten.
  Evidence: `src/Images/Services/ImageExportService.cs:128-202`; RESEARCH.md.
  Touches: `ImageExportService`/Save-a-copy path (detect multi-frame sources and either preserve via `MagickImageCollection` or surface a "saved first frame only" notice).
  Acceptance: copying an animated GIF either preserves its frames or reports that only the first frame was saved; a test covers the multi-frame source.
  Complexity: M
