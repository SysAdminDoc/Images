# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)
3. XMP write-through for labels/keywords/location (below)

When the remaining item ships (plus the two blocked items), promote to `1.0.0`.

## Research-Driven Additions

### P1

- [ ] P1 — Enforce documented UIA accessibility contracts with FlaUI
  Why: `docs/accessibility.md` documents a rich UIA tree, but tests only cover basic launch/navigation and do not assert screen-reader names, help text, live regions, or secondary windows.
  Touches: `tests/Images.Tests/WpfSmokeTests.cs`; `MainWindow.xaml`; `SettingsWindow.xaml`; `AboutWindow.xaml`; secondary dialog XAML as needed.
  Acceptance: Automated UIA smoke verifies canvas name/help text, Settings/About controls, command/search surfaces, and at least one live status region; failures identify the missing AutomationProperties contract.
  Complexity: M

- [ ] P1 — Complete XMP folder import write-through for labels, keywords, and location
  Why: The sidecar parser extracts labels, flat keywords, hierarchical keywords, and location, but folder apply currently writes only ratings.
  Touches: `XmpSidecarImportService`; `ReviewLabelService`; `TagGraphService`; catalog refresh; import command UI/tests.
  Acceptance: Folder import preview reports ratings, labels, tags, location, unmatched files, and skipped fields; apply writes supported values to Images sidecars/catalog without touching originals; tests cover digiKam and Lightroom/XnView-style sidecars.
  Complexity: M

- [ ] P1 — Add local model runtime validation and explicit fallback reasons
  Why: Model manager verifies hashes, but CLIP provider creation catches all runtime/session/tokenizer/preprocessor failures and silently falls back to deterministic metadata embeddings.
  Touches: `ModelManagerService`; `ClipEmbeddingProvider`; `SemanticSearchService`; `ModelManagerWindow`; semantic-search status UI/tests.
  Acceptance: A Validate action runs deterministic text and image smoke inputs against installed model artifacts, records provider/device/output shape, and surfaces exact failure/fallback reasons in Model Manager and Semantic Search.
  Complexity: M

### P2

- [ ] P2 — Add incremental catalog and semantic rescan staging
  Why: Catalog rebuild currently recursively hashes all candidates, clears the catalog, and inserts current rows, which can be slow and brittle for large libraries.
  Touches: `CatalogService`; `CatalogQueryService`; `SemanticSearchService`; catalog schema/tests; semantic search UI status.
  Acceptance: Rescan reuses unchanged path/size/mtime/fingerprint rows, stages changes before swap, reports reused/updated/failed counts, and cancellation preserves the last good catalog/index.
  Complexity: L

- [ ] P2 — Validate generated WinGet and Scoop manifests in release workflow
  Why: The release workflow generates package-manager manifests but only uploads them; distribution trust docs still call out validation and install smoke as future follow-ups.
  Touches: release workflow; package manifest script/tests; release diagnostics logs.
  Acceptance: Release workflow validates generated WinGet manifests when tooling is available, validates the Scoop JSON schema/URL/hash fields, runs a portable install/launch smoke from the generated Scoop manifest or an equivalent local check, and uploads validation logs.
  Complexity: M

### P3

- [ ] P3 — Support multi-path launch sessions from the command line
  Why: Power users and external tools expect a viewer to open an explicit ad hoc set of images, not only infer siblings from one folder.
  Touches: app launch parsing; `DirectoryNavigator`; recent session/navigation state; smoke tests.
  Acceptance: `Images.exe a.jpg b.png c.webp` opens a session containing exactly those files in argument order, supports next/previous/Home/End, and falls back to current folder navigation for a single path.
  Complexity: M

## Best Viewer-Inspired Additions

### P1

- [ ] P1 — Add workflow modes with persistent chrome presets
  Why: Images now has more power surfaces than most lightweight viewers; qView, PicView, ImageGlass, XnView, and ACDSee show that users need fast switches between minimal viewing, culling, organizing, editing, book reading, and diagnostics without manually toggling panels every time.
  Evidence: qView minimal chrome; PicView customization; ImageGlass modern command surface; XnView/ACDSee browser-vs-viewer workflows; `MainWindow.xaml`; `CommandShortcutService`.
  Touches: `MainViewModel`; `SettingsViewModel`; `SettingsWindow.xaml`; `MainWindow.xaml`; `CommandShortcutService`; persisted settings schema/tests.
  Acceptance: Users can choose Viewer, Review, Organize, Edit, Book, and Diagnostics modes; each mode restores its sidebar/filmstrip/gallery/tool visibility, density, and preferred command surface; Peek and fullscreen remain image-first and are not overwritten by mode switching.
  Complexity: M

- [ ] P1 — Add a Light Table / Select Set comparison queue
  Why: The current compare mode handles a pair, but top culling tools use temporary sets so users can compare 3-8 near-duplicates in context before rating, rejecting, or exporting.
  Evidence: digiKam Light Table; Capture One Select Set/culling flow; nomacs synchronized viewers; existing `Compare mode`, `Gallery workbench`, and duplicate-cleanup compare handoff.
  Touches: compare-mode state in `MainViewModel`; `DuplicateCleanupWindow`; gallery/filmstrip selection handoff; review-label commands; compare UI tests.
  Acceptance: Selected images can be sent to a Light Table queue from gallery, filmstrip, duplicate cleanup, or current folder; the queue supports 2-up and 4-up layouts, linked pan/zoom/rotate, primary-image selection, and per-image rating/pick/reject without requiring a catalog rebuild.
  Complexity: L

- [ ] P1 — Surface a background jobs and task-history center
  Why: Catalog indexing, semantic indexing, thumbnailing, C2PA inspection, update checks, support bundles, batch work, and long previews already run asynchronously, but premium tools make background work observable and recoverable instead of leaving status scattered.
  Evidence: Mylio dynamic indexing UX; Hydrus large-library job visibility; ImageGlass stability/release focus; existing `BackgroundTaskTracker`, `DiagnosticsStatusService`, and long-running service calls.
  Touches: `BackgroundTaskTracker`; `DiagnosticsStatusService`; `AboutWindow`; `SettingsWindow.xaml`; batch/catalog/semantic/import callers; tests.
  Acceptance: A Jobs surface lists running and recent tasks with name, state, duration, last error, and affected count where available; cancellable tasks expose Cancel; completed/faulted tasks are retained for the session and included in support bundles without file contents.
  Complexity: M

- [ ] P1 — Add color-management truth mode and HDR/wide-gamut guardrails
  Why: Images advertises broad HDR/EXR/JXL/AVIF support and reports ICC data, but the current WPF display path explicitly does not soft-proof or apply managed display transforms; accurate color is a trust boundary for a premium viewer.
  Evidence: ImageGlass HDR/JPEG XL color issues; FastStone ICC-processing performance focus; `ImageColorAnalysisService`; `ExportCapabilityWarningService`; `docs/research-advanced-features.md`.
  Touches: `ImageColorAnalysisService`; `ImageLoader`; `ZoomPanImage` display pipeline; `ExportPreviewService`; `ExportCapabilityWarningService`; color-profile fixture tests.
  Acceptance: Profiled, unprofiled, wide-gamut, EXR/HDR, AVIF, and JXL fixtures surface explicit display-truth status; supported SDR ICC cases can preview through a managed sRGB display transform or clearly explain why not; HDR/native wide-gamut files are never implied accurate until a future renderer supports it.
  Complexity: L

### P2

- [ ] P2 — Add a cross-folder session tray with portable file lists
  Why: IrfanView file lists, FastStone input lists, and XnView batch/browser workflows let users curate arbitrary sets across folders before viewing, renaming, comparing, or exporting; Images still mainly navigates from one folder root.
  Evidence: IrfanView slideshow/batch TXT file lists; FastStone batch input list; XnView batch convert/rename workflows; existing `DirectoryNavigator` folder-root model and P3 multi-path launch item.
  Touches: new session-list service; `DirectoryNavigator` or navigation adapter; gallery/filmstrip handoff; batch/export/compare source selection; settings/recent sessions tests.
  Acceptance: Users can add current, selected, or dropped files to a session tray, reorder/remove entries, save/load a plain text file list, open missing-file-tolerant sessions, and send the tray to compare, batch, export, or slideshow-style navigation without changing source folders.
  Complexity: M

- [ ] P2 — Add quick keyword sets and code-replacement snippets for sidecar metadata
  Why: Photo Mechanic, ACDSee, XnView, and Eagle make repetitive caption/tag entry fast; Images already has local tag relationships and XMP sidecars, but repeated event/project tagging still requires manual entry.
  Evidence: Photo Mechanic code replacements; ACDSee Quick Keywords; XnView IPTC/XMP metadata workflows; Eagle tag groups; `TagGraphService`; `XmpSidecarImportService`.
  Touches: `TagGraphService`; `ReviewLabelService`; XMP write-through flow; `TagGraphWindow`; import inbox tag UI; settings persistence/tests.
  Acceptance: Users can define named keyword sets and text snippets, preview their sidecar changes on one or many selected images, apply them without touching embedded originals, and export/import the preset definitions as local JSON.
  Complexity: M

- [ ] P2 — Add a no-image start surface for common workflows
  Why: Feature-rich viewers lose trust when launch-without-file looks empty; the best lightweight viewers keep the first screen quiet while still making open, recent, paste, import, settings, and diagnostics obvious.
  Evidence: qView empty minimalist surface; PicView out-of-box customization; ImageGlass simple opening model; existing recent folders/books, import inbox, clipboard import, and diagnostics commands.
  Touches: `MainWindow.xaml`; `MainViewModel`; recent folder/book services; clipboard import; Settings/About command bindings; UIA smoke tests.
  Acceptance: Launching with no file shows a restrained start surface with Open file, Open folder, Recent folders, Recent books, Paste image, Import inbox, Settings, and Diagnostics actions; it has keyboard/UIA names, hides once an image loads, and does not appear in peek mode.
  Complexity: S

- [ ] P2 — Add saved smart collections for catalog and gallery filters
  Why: Eagle Smart Folders, XnView Smart Albums, Mylio Dynamic Search, and ACDSee saved searches show that users expect repeatable local collections for recurring criteria, not one-off filter strings.
  Evidence: Eagle Smart Folders; XnView Smart Albums/catalog search; Mylio Dynamic Search; ACDSee catalog/search model; existing `AssetSmartFilterService`, `CatalogQueryService`, and gallery smart filter tokens.
  Touches: `AssetSmartFilterService`; `CatalogQueryService`; `SettingsService`; gallery workbench; semantic search window; persisted schema/tests.
  Acceptance: Users can save, rename, reorder, and delete smart collections based on folder roots, rating/label/tag, format, dimensions, date, duplicate status, palette/orientation, and optional semantic text; selecting a collection replays the filter against current catalog state without copying originals.
  Complexity: M

- [ ] P2 — Add contact sheet and proof sheet export
  Why: FastStone, Photo Mechanic, IrfanView, XnView, and ACDSee all treat contact/proof sheets as a core delivery and review workflow; Images has print/export/reference-board pieces but no repeatable sheet generator.
  Evidence: FastStone contact sheet/slideshow tooling; Photo Mechanic contact/proof sheet printing with variables and watermarks; IrfanView/XnView print-layout workflows; existing `PrintService`, `ReferenceBoardLayoutService`, and export pipeline.
  Touches: `PrintService`; new contact-sheet/proof-sheet planner; `ImageExportService`; `PhotoMetadataController`; session tray/gallery selections; XAML dialog/tests.
  Acceptance: Selected images, session trays, or current-folder filters can generate PDF/PNG contact sheets and single-image proof sheets with configurable grid, margins, filename/rating/metadata captions, optional watermark text, and dry-run preview before writing.
  Complexity: M

- [ ] P2 — Add viewer performance budgets and release diagnostics
  Why: qView, FastStone, PicView, and ImageGlass compete heavily on instant open, fast folder switching, and low memory; Images has launch/decode instrumentation but no regression budget or fixture-backed performance report.
  Evidence: qView fast/minimal positioning; FastStone speed/reliability positioning; PicView fast customizable viewer positioning; existing `LaunchTiming`, `ImageEventSource`, `DirectoryNavigator`, `ThumbnailCache`, and deep-zoom pipeline.
  Touches: `LaunchTiming`; `ImageEventSource`; `DirectoryNavigator`; `ThumbnailCache`; `TileService`; release diagnostics scripts; performance fixture tests.
  Acceptance: A local diagnostics command and CI/release report measure cold start, first image decode, next/previous navigation, large-folder scan, thumbnail generation, and huge-image tile open against fixed fixtures; regressions are reported with thresholds and memory snapshots without blocking emergency security releases.
  Complexity: M
