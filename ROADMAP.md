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

Continues the `V###-##` scheme (V110 is the next free hundred-block above V100). Evidence lives in `RESEARCH.md` (2026-07-16). Most 2024-2026 competitor "gaps" were verified as already shipped (compare, loupe, motion/live photo, continuous webtoon archive reading, perspective correction, PDF export, codec-install prompts, UTF-8 Exif) and are intentionally absent below.

#### P1 — Data safety, trust, and high-value inspection

#### P2 — Scale, decomposition, and confirmed UX gaps

- [ ] P2 — Render the extracted gain map as a grayscale overlay (V110-16)
  Why: `GainMapInspectionService` (V110-05) now detects gain maps and reports their metadata, but the extracted gain-map image is not yet shown. A grayscale-layer viewer would let users see the HDR boost map itself, completing the inspection story.
  Evidence: `src/Images/Services/GainMapInspectionService.cs` (detection + MPF/aux presence already landed); RESEARCH.md Architecture §
  Touches: `GainMapInspectionService.cs` (add MPF/aux image extraction to a `BitmapSource`), a viewer overlay/command, `CommandShortcutService.cs`
  Acceptance: for an Ultra HDR/Apple/ISO gain-map file, a command renders the decoded gain map as a grayscale layer over the base image; files without a gain map keep the command disabled. Needs a foreground GUI verification pass.
  Complexity: M

- [ ] P2 — Continue extracting the `MainViewModel` god object (V110-09)
  Why: ~8,300 lines / 318 KB owning 20+ services and 4 timers is a change-magnet where any edit risks unrelated regressions (memory notes confirm parallel-agent work on this tree); the team's existing `*Controller` extraction pattern is the proven path.
  Evidence: RESEARCH.md Architecture §; `ViewModels/MainViewModel.cs`
  Touches: `src/Images/ViewModels/MainViewModel.cs`, new `Slideshow*/ArchiveReader*/RenameEditor*Controller.cs`
  Acceptance: slideshow, archive-reader, and rename-editor state/commands move into dedicated controllers behind the existing `_uiDispatcher`/`() => _isDisposed` convention; behavior and tests unchanged; `MainViewModel` shrinks measurably.
  Complexity: L

- [ ] P2 — Jump to next/previous archive without leaving the reader (V110-12)
  Why: readers browsing a folder of CBZ/CBR want to roll into the next archive at the last page (and previous at the first) without returning to the folder; PicView ships this and continuous archive reading already exists here.
  Evidence: RESEARCH.md Competitive §; https://github.com/Ruben2776/PicView/releases ; `src/Images/Services/ArchiveBookService.cs`, `src/Images/Services/DirectoryNavigator.cs`
  Touches: `src/Images/Services/ArchiveBookService.cs`, `src/Images/ViewModels/MainViewModel.cs`, `DirectoryNavigator.cs`
  Acceptance: at the last page of an archive, next advances into the first page of the next archive in natural folder order (and symmetrically for previous); wraps or stops per existing navigation semantics; non-archive navigation unchanged.
  Complexity: M

- [ ] P2 — Hold-to-scrub rapid folder fly-through (V110-13)
  Why: holding an arrow to fly through a large folder with instant decode is repeatedly praised (FlyPhotos) and directly answers the "per-image reload stutter" complaint; Images has preload but no explicit accelerated scrub mode.
  Evidence: RESEARCH.md Competitive §; https://www.makeuseof.com/free-windows-photos-app-replacement/
  Touches: `src/Images/MainWindow.xaml.cs`, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/Services/PreloadService.cs`
  Acceptance: holding Next/Prev accelerates navigation using thumbnail/preloaded frames without full-decode stutter and settles on a full decode when the key is released; single presses behave as before.
  Complexity: M

#### P3 — Hardening and hygiene

