# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when unblocked **and** the P1 items in the audit backlog are drained.

## Research-Driven Additions (2026-07-12)

Externally-grounded, net-new items from a competitor/format/security research pass. None duplicate the AUD deep-audit backlog above or the blocked items in `Roadmap_Blocked.md`. Each was verified against current source before landing here (Compare mode, trackpad manipulation/inertia, histogram, nearest-neighbor scaling, and lossless JPEG transforms already exist and are intentionally omitted).

### P2 — trust / correctness / quiet-premium comfort

- [ ] P2 RD-02 — Display color management: honor embedded ICC + transform to the active monitor profile
  Why: `ImageLoader` does no `ColorContext`/ICC read or display-profile transform, so embedded profiles are ignored and wide-gamut/P3 monitors render over-saturated (WPF assumes sRGB output). This is the single most-repeated, still-unsolved complaint across nomacs (#459), PicView (#201), and ImageGlass (#1433 open bug). Distinct from the blocked OCIO/ACES pro pipeline (V80-26) — this is the pragmatic bake-into-display transform via Magick.NET's embedded lcms2.
  Evidence: https://github.com/nomacs/nomacs/issues/459 ; https://github.com/d2phap/ImageGlass/issues/1433 ; RESEARCH.md.
  Touches: `src/Images/Services/ImageLoader.cs` (or new `ColorManagementService`) — read embedded profile, resolve active monitor profile (`WcsGetDefaultColorProfile`), `TransformColorSpace(source, monitor)` baked into the display bitmap; a "managed / unmanaged" status token in the inspector; run lazily off the startup hot path to preserve cold-start speed.
  Acceptance: an image with an embedded non-sRGB profile displays visibly corrected on a wide-gamut monitor; a managed/unmanaged indicator reflects state; SDR sRGB path is unchanged; no measurable cold-start regression.
  Complexity: M

### P3 — comfort / polish

- [ ] P3 RD-06 — Hold-to-loupe magnifier (1:1 or Nx lens under the cursor)
  Why: No magnifier exists; a press-and-hold lens at 100%/Nx lets users check sharpness during a cull without changing base zoom. Requested in ImageGlass #1425; present in FastStone/NeeView.
  Evidence: https://github.com/d2phap/ImageGlass/issues/1425 ; RESEARCH.md.
  Touches: `src/Images/Controls/ZoomPanImage.cs` (render a circular/rect lens sampling source pixels at cursor on a modifier-hold), setting for magnification factor.
  Acceptance: holding the loupe gesture shows a fixed-magnification lens tracking the cursor; releasing restores; base zoom unchanged.
  Complexity: M

- [ ] P3 RD-07 — Live cursor pixel readout (x/y + RGBA/hex)
  Why: `ImageColorAnalysisService` already computes histogram/channel stats, but there is no live per-pixel readout under the cursor. Requested in PicView #151 and ImageGlass #913; a premium inspection touch that reuses existing inspector real estate.
  Evidence: https://github.com/Ruben2776/PicView/issues/151 ; https://github.com/d2phap/ImageGlass/issues/913 ; RESEARCH.md.
  Touches: `ZoomPanImage` (map cursor → source pixel), `MainViewModel`/inspector to show x/y + RGB(A) hex; throttle sampling.
  Acceptance: moving the cursor over the image shows current pixel coordinate and color (hex + RGBA); hides when off-image; no navigation jank.
  Complexity: M

- [ ] P3 RD-08 — Zoom-to-selection (rubber-band box zoom)
  Why: No drag-rectangle zoom exists; JPEGView's Ctrl+Shift+drag zoom-to-region is a power-user favorite for inspecting detail precisely. Distinct from crop mode.
  Evidence: https://github.com/sylikc/jpegview ; RESEARCH.md.
  Touches: `src/Images/Controls/ZoomPanImage.cs` (modifier-drag rubber-band → compute scale/translate to fit the box), guard against clashing with crop/pan gestures.
  Acceptance: modifier-dragging a rectangle zooms/pans so that region fills the viewport; Escape cancels the drag; does not trigger crop.
  Complexity: M

- [ ] P3 RD-09 — Auto session restore: reopen last folder + image + position on launch (opt-in)
  Why: Window geometry persists, but the app does not reopen the last-viewed image/position on relaunch (`SessionTrayService` is a manual basket, not auto-restore). Requested in JPEGView #216 and PicView #227; expected of a daily-driver viewer.
  Evidence: https://github.com/sylikc/jpegview/issues/216 ; RESEARCH.md.
  Touches: `SettingsService` (persist last path + index on close/navigation), `App.xaml.cs`/`MainViewModel` startup (reopen when no argv path and the file still exists), a Privacy/General toggle (default off to respect the quiet-open philosophy).
  Acceptance: with the setting on and no file passed on launch, Images reopens the last image in its folder at the prior position; missing file falls back to empty state; off by default.
  Complexity: S

- [ ] P3 RD-10 — End-of-list behavior option: stop-with-nudge or advance to sibling folder (vs. always wrap)
  Why: Navigation always wraps at the ends; competitors offer a choice — stop with a subtle visual nudge (JPEGView) or auto-advance into the next sibling directory (ImageGlass 9.5). Wrap-only surprises users paging a large tree.
  Evidence: https://github.com/d2phap/ImageGlass/releases (9.5 sibling-folder) ; RESEARCH.md.
  Touches: `src/Images/Services/DirectoryNavigator.cs` (end-of-list policy enum), `MainViewModel` Next/Prev, a setting; sibling-folder scan reuses existing directory enumeration + natural sort.
  Acceptance: a setting selects Wrap / Stop-with-nudge / Next-sibling-folder; each behaves as described at the first/last image; default remains Wrap.
  Complexity: S
