# Open Source Image Viewer Research Scan — 2026-05-05

Purpose: identify concrete improvements for Images by reviewing active or instructive open-source image viewers and adjacent photo browsers. This scan complements `docs/research-viewers-editors.md` and focuses on implementation-ready product ideas rather than broad market strategy.

## Sources Reviewed

| Project | Source | Relevant pattern |
| --- | --- | --- |
| ImageGlass | <https://imageglass.org/docs/features>, <https://imageglass.org/news/imageglass-roadmap-update-2026-98>, <https://imageglass.org/news/imageglass-10-beta-1-is-here-99> | Broad format support, Explorer sort-order sync, color picker, channel isolation, theme/icon/language packs, v10 rewrite transition, signing friction. |
| nomacs | <https://github.com/nomacs/nomacs>, <https://nomacs.org/blog/synchronization/> | Multi-instance sync, opacity-overlay comparison, LAN image send, plugin-backed paint/composite features. |
| PicView | <https://picview.org/download/> | Archive navigation, searchable shortcuts, RAW/EXIF improvements, hoverbar polish, print-to-PDF, cross-platform Avalonia lessons. |
| NeeView | <https://neelabo.github.io/NeeView/en-us/userguide.html> | Treat folders and archives as books, page list/history panels, dockable/floating panels, deep settings. |
| QuickLook | <https://quicklookapp.vercel.app/> | Explorer Spacebar preview, plugin ecosystem, keyboard-first zero-friction workflow. |
| Geeqie | <https://www.geeqie.org/help/Features.html>, <https://github.com/BestImageViewer/geeqie> | XMP keywords/search, collections, grouped RAW+JPEG+XMP entities, one-click copy/move with undo. |
| gThumb | <https://help.gnome.org/users/gthumb/stable/gthumb-introduction.html.en> | Catalog organization, comments, camera import, slideshow, print, desktop background workflows. |
| qView | <https://interversehq.com/qview/> | Minimal no-clutter viewer model with strong preferences and native-feeling behavior. |
| JPEGView fork | <https://github.com/sylikc/jpegview> | Minimal GUI with on-the-fly processing, portable settings, package-manager distribution. |
| Tacent View | <https://bluescan.github.io/tacentview/>, <https://github.com/bluescan/tacentview> | Texture/HDR tools, metadata-sort slideshow, command-line batch parity, fast thumbnail view, per-image undo/redo. |
| Minimal Image Viewer | <https://github.com/deminimis/minimalimageviewer> | Ultra-small WIC/Direct2D viewer, area OCR, OSD metadata, non-destructive crop/rotate/filters, portable INI settings. |
| LightningView | <https://lightningview.app/> | Lightweight RAW/FITS culling, inertia scrolling, large-image performance focus. |

## High-Value Patterns To Adopt

### 1. Comparison Mode

nomacs has the strongest open-source comparison pattern: synchronized panning/zooming across windows, an opacity overlay mode, synchronized next/previous navigation, and optional LAN sync. Images should not copy LAN sync first because it conflicts with the product’s network-transparency posture, but local comparison is highly aligned.

Recommended Images work:
- Add a local 2-up compare mode.
- Add linked pan/zoom and linked next/previous navigation.
- Add an opacity overlay mode for before/after or near-duplicate comparison.
- Keep all comparison local-only and log no network activity.

Tracker fit: `IP-16` product differentiators, with a near-term `IP-03` UI-state test target.

### 2. Archive And Book Navigation

NeeView treats folders and archive files as the same conceptual unit: a book containing pages. PicView and Tacent View also validate archive navigation and folder-to-folder continuity. Images already has folder navigation and multi-page document navigation, so archive support would be a natural extension rather than a new product mode.

Recommended Images work:
- Treat `.zip`, `.cbz`, `.7z`, and `.rar/.cbr` as navigable books if dependencies are approved.
- Add page list/history semantics for archive/document navigation.
- Preserve current image-first UI by hiding book controls until the file is an archive or multi-page source.
- Keep extraction streaming/temp-safe; do not silently unpack whole archives into user folders.

Tracker fit: new `IP-16` design doc before implementation; may become a future P1 feature after reliability work.

### 3. Explorer And Sorting Fidelity

ImageGlass explicitly supports File Explorer sort order, including saved searches, while NeeView and Geeqie expose strong folder/history models. Images currently natural-sorts the folder. That is predictable, but it can be wrong when the user opened from a folder sorted by date, rating, type, or search results.

Recommended Images work:
- Add folder sort modes: name, modified date, created date, size, extension, EXIF date, and manual current natural sort.
- Add a visible current-sort chip and menu in the folder preview/filmstrip.
- Investigate Explorer sort-order sync only after simpler app-owned sort modes ship.
- Preserve natural sort as the default to avoid surprising existing users.

Tracker fit: `IP-02` folder-preview extraction, then `IP-03` state tests.

### 4. Metadata Workflows Beyond Read-Only Details

Geeqie supports XMP keyword assignment and search. gThumb adds comments/catalogs/import. Tacent can sort slideshows by EXIF metadata fields. Images already reads EXIF and strips GPS, but it does not yet use metadata to organize, sort, or cull.

Recommended Images work:
- Add metadata sort/filter to the folder preview after folder-preview extraction.
- Add star/rating and reject/keep flags stored in XMP sidecars or app-local sidecars.
- Add a culling panel: keep, reject, move/copy to target folder, undo.
- Keep sidecar-backed edits explicit and reversible; avoid writing metadata into originals by default.

Tracker fit: `IP-16` library/metadata workflow, with early service seams under `IP-01`.

### 5. Keyboard-First Zero-Friction Mode

QuickLook’s core value is instant Spacebar preview from Explorer. qView and JPEGView keep chrome minimal and keyboard-driven. Images already has strong keyboard navigation but opens as a full app. A fast preview-oriented launch mode would make it feel less heavyweight.

Recommended Images work:
- Add a `--preview` or `--peek` launch mode with minimal chrome, close-on-Esc, no settings write unless explicitly changed.
- Consider a shell helper or documented PowerToys/Explorer integration after the mode exists.
- Add startup timing logs: process start to first image displayed.
- Keep full viewer as the default for normal file association.

Tracker fit: `IP-04` observability plus `IP-16` product differentiator.

### 6. Designer And Technical Image Tools

ImageGlass has color picker and color-channel viewing. Tacent View adds alpha/checkerboard behavior, exact pixel color copy, HDR/EXR exposure controls, mipmaps/cubemaps, texture formats, and CLI parity for batch operations. Images supports broad formats but does not yet expose enough per-pixel or channel tooling.

Recommended Images work:
- Add color picker: RGB, HEX, HSL, image pixel coordinate, copy value.
- Add alpha checkerboard and background toggle for transparent images.
- Add individual channel view toggles for RGB and alpha.
- Add HDR/EXR exposure preview controls without modifying the source.
- Defer mipmaps/cubemaps unless the app explicitly targets game/texture workflows.

Tracker fit: `IP-16`; color picker and alpha background are likely small P1/P2 UI additions.

### 7. Non-Destructive Viewer-Side Adjustments

JPEGView and Minimal Image Viewer show that a viewer can provide useful non-destructive adjustments without becoming a full editor. Tacent View goes deeper with undo/redo and batch conversion, but Images should keep the source-of-truth model simple.

Recommended Images work:
- Add temporary view adjustments: exposure, contrast, saturation, grayscale, sharpen.
- Make the default behavior preview-only.
- Provide `Save a copy with adjustments`, not overwrite-original, as the first shipping path.
- Add a visible reset control and clear status when adjustments are active.

Tracker fit: `IP-08` long-running/state consistency and `IP-16`.

### 8. Thumbnail Cache And Large Folder Behavior

Tacent View emphasizes fast thumbnail generation for thousands of photos. LightningView emphasizes large-image and lower-end hardware performance. Images has a thumbnail cache and preload ring but needs explicit stress coverage and user-visible cache controls.

Recommended Images work:
- Add folder stress tests with thousands of files and delete/rename churn.
- Add cache size reporting and clear-cache action.
- Add telemetry-free structured timings for thumbnail decode queue latency and first-thumbnail time.
- Add a fallback state for unavailable cache storage.

Tracker fit: `IP-10` cache health and `IP-11` stress testing.

### 9. Settings, Themes, Shortcuts, And Localization

ImageGlass supports layout/theme/icon/language packs and configurable keyboard/mouse actions. PicView has searchable shortcuts. NeeView has deep dockable panels and settings. Images has a small settings surface today.

Recommended Images work:
- Add searchable keyboard-shortcuts settings before full rebinding.
- Add light/system/high-contrast themes before custom theme packs.
- Add command labels/tooltips from a central command registry.
- Delay full plugin/theme-pack architecture until the command/settings model is stable.

Tracker fit: `IP-07` first-run/settings polish and `IP-03` command state tests.

### 10. Distribution Trust

ImageGlass and JPEGView both show the recurring OSS Windows problem: unsigned binaries trigger trust friction. JPEGView also distributes through WinGet, PortableApps, and Scoop. Images already has CI/release gates and checksums, but still needs distribution trust work.

Recommended Images work:
- Add WinGet package submission after the next stable release.
- Add Scoop manifest.
- Continue publishing checksums.
- Create a code-signing decision doc: certificate cost, user trust impact, SmartScreen reputation, release cadence, and fallback verification instructions.

Tracker fit: `IP-15` follow-up or new release/distribution row if this becomes a priority.

## Ranked Additions To The Improvement Plan

1. `IP-02` first: extract folder-preview and clipboard-import logic so new sort/archive/preview features have a clean home.
2. `IP-03`: add state tests for navigation, sort changes, archive-page transitions, and compare mode before adding heavy UI.
3. `IP-10` and `IP-11`: expose cache health and stress-test large folders because this is already core to the app.
4. `IP-16`: split into design docs for compare mode, archive/book mode, metadata/culling workflow, and preview/peek launch mode.
5. Add a future `IP-17` if distribution becomes near-term: WinGet/Scoop/code-signing trust plan.

## Near-Term Implementation Recommendation

Do not jump straight to semantic search or batch editing. The best next implementation remains the first `IP-02` extraction:

- Extract folder-preview behavior from `MainViewModel`.
- Add explicit sort-mode primitives while extracting.
- Add tests for current natural sort and future sort modes.
- Then use the extracted surface to add metadata sort, cache health, and later archive/book navigation.

This sequence turns the open-source viewer research into infrastructure the app can safely build on.
