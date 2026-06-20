# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Research-Driven Additions

### P0

- [ ] P0 — **Pin CI runner for windows-latest migration**
  Why: GitHub Actions `windows-latest` is migrating to Windows Server 2025 + VS 2026 (June 8-15, 2026). Unpinned CI will break without warning.
  Evidence: https://github.com/actions/runner-images/issues/14017 — migration rolled out June 2026
  Touches: `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `.github/workflows/security.yml`
  Acceptance: All workflow `runs-on` values use `windows-2025` or an explicit label instead of `windows-latest`
  Complexity: S

### P1

- [ ] P1 — **Parallel batch processing**
  Why: `BatchProcessorService` processes files sequentially. IrfanView 4.75 and ACDSee 2026 run multithreaded batch — single-threaded is a dealbreaker for power users processing hundreds of files.
  Evidence: IrfanView v4.75 changelog (May 2025); ACDSee Activity Manager; no `Parallel`/`SemaphoreSlim`/`Task.WhenAll` in any of 105 service files
  Touches: `src/Images/Services/BatchProcessorService.cs`, `src/Images/BatchProcessorWindow.xaml.cs`
  Acceptance: Batch runs use bounded concurrency (configurable, default `ProcessorCount - 1`), progress updates per-file, cancellation still works, and throughput improves measurably on 100+ file batches
  Complexity: M

- [ ] P1 — **Test coverage for untested core services**
  Why: ~30 of ~105 services have no dedicated test file. Key gaps: `ImageLoader`, `OcrService`, `PreloadService`, `CrashLog`, `ImageMetadataService`, `PrintService`, `CodecRuntime`, `OnnxRuntimeService`, `BackgroundJobsService`, `SupportBundleService`, `ClipEmbeddingProvider`, `PerformanceBudgetService`.
  Evidence: `comm -23` between `tests/Images.Tests/` and `src/Images/Services/` file lists
  Touches: `tests/Images.Tests/` — new test files per service
  Acceptance: At least 15 of the untested core services gain focused regression tests (constructor, happy path, error path)
  Complexity: L

- [ ] P1 — **Embedded-JPEG-first RAW preview**
  Why: RAW files (DNG/NEF/CR2/CR3/ARW/RAF/ORF/PEF) contain an embedded JPEG preview. Showing it first while decoding the full RAW in the background is the single most praised performance trick in viewer reviews (Photo Mechanic, FastStone, IrfanView).
  Evidence: Photo Mechanic's "perceived instant" RAW browsing; FastStone viewer reviews
  Touches: `src/Images/Services/ImageLoader.cs`, `src/Images/Services/PreloadService.cs`
  Acceptance: RAW files display the embedded JPEG preview within 100ms, then swap to the full-resolution decode when ready. A subtle indicator shows when full resolution is loaded.
  Complexity: M

- [ ] P1 — **Color palette extraction for gallery filtering**
  Why: Eagle auto-extracts dominant color palettes from every image and enables color-swatch-click search. No OSS viewer offers perceptual color search. Images already has `AssetSmartFilterService` with palette filter tokens.
  Evidence: https://en.eagle.cool/article/505-search-by-color — Eagle's top-cited differentiator
  Touches: `src/Images/Services/ImageColorAnalysisService.cs` (extend), `src/Images/Services/AssetSmartFilterService.cs`, `src/Images/Services/CatalogService.cs` (store palette)
  Acceptance: Gallery filter tokens support `palette:red`, `palette:blue`, etc. based on extracted dominant colors. Palette computed during catalog scan, persisted in catalog DB.
  Complexity: M

### P2

- [ ] P2 — **Drag-divider comparison split in export preview**
  Why: Squoosh's draggable split-view divider is the gold standard for compression-quality comparison. Images has linked pan/zoom and a difference toggle but lacks the tactile divider interaction.
  Evidence: Squoosh UX; community comparison reviews citing the divider as essential
  Touches: `src/Images/ExportPreviewWindow.xaml`, `src/Images/ExportPreviewWindow.xaml.cs`
  Acceptance: A draggable vertical divider splits the export preview between original and encoded output. Divider position persists during zoom/pan. Existing linked-view and difference toggle remain available.
  Complexity: S

- [ ] P2 — **WCAG 2.5.7/2.5.8 audit pass**
  Why: WCAG 2.2 (ISO/IEC 40500:2025) requires drag alternatives for every drag operation (2.5.7) and 24x24 px minimum target sizes (2.5.8). Crop, pan, brush overlays, and some toolbar buttons may not meet these requirements.
  Evidence: WCAG 2.2 spec; most image viewers fail on drag alternatives and minimum target sizes
  Touches: `src/Images/Controls/CropOverlay.xaml`, `src/Images/Controls/LocalExposureBrushOverlay.xaml`, `src/Images/Controls/RetouchBrushOverlay.xaml`, `src/Images/Controls/ZoomPanImage.cs`, toolbar/button XAML
  Acceptance: Every drag operation has a keyboard/input-field alternative. All interactive targets are at least 24x24 DIU. Audit results documented in `docs/accessibility.md`.
  Complexity: M

- [ ] P2 — **Edge-hover contextual panels in fullscreen**
  Why: FastStone's zero-chrome fullscreen with mouse-edge EXIF/thumbnail/tool reveals is the most praised single UX pattern in viewer reviews. Images has fullscreen and peek mode but no edge-hover panels.
  Evidence: FastStone Image Viewer feature descriptions; power-user review threads
  Touches: `src/Images/MainWindow.xaml`, `src/Images/MainWindow.xaml.cs`, `src/Images/ViewModels/MainViewModel.cs`
  Acceptance: In fullscreen mode, moving the mouse to screen edges reveals contextual panels (top: toolbar, bottom: filmstrip/thumbnails, right: metadata/EXIF). Panels auto-hide after mouse leaves.
  Complexity: M

- [ ] P2 — **Stale documentation reference cleanup**
  Why: `docs/archive-runtime-review.md` line 15 still references `net9.0`. Historical research docs under `docs/research/iter-1-*` and `docs/research/iter-2-*` reference `net9.0-windows` and `0.1.x`. These are historical artifacts but create confusion for new contributors.
  Evidence: `grep -r "net9.0" docs/` confirms 3 hits in archival docs
  Touches: `docs/archive-runtime-review.md`
  Acceptance: All references to `net9.0` in active policy documents (not historical research artifacts) are updated to `net10.0`.
  Complexity: S
