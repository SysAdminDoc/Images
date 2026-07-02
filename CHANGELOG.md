# Changelog

All notable changes to **Images** are documented here.

## Unreleased

### Theme

- **Runtime theme switching now updates all windows** — Converted 488 `StaticResource` brush references to `DynamicResource` across MainWindow and 27 secondary window/overlay XAML files so theme changes via Settings take effect immediately without restarting or re-opening windows.

### Internal

- **AsyncRelayCommand replaces async void command pattern** — 25 `RelayCommand(async () => await …)` usages in MainViewModel now use `AsyncRelayCommand` which accepts `Func<Task>`, catches `OperationCanceledException` (expected during navigation), and routes other exceptions through the dispatcher crash handler instead of terminating the process.
- **Preload eviction now cancels in-flight decodes** — PreloadService uses per-entry linked `CancellationTokenSource` so evicted cache entries cancel their background decode immediately instead of running to completion.
- **Navigation history uses O(1) bounded collection** — DirectoryNavigator back/forward stacks replaced with `LinkedList<string>` so push-past-cap drops the oldest entry in O(1) instead of O(n) stack-to-array-to-stack rebuild.

### UX

- **Premium main-viewer command polish** — Viewport context menus are now scroll-bounded, sectioned, and submenu-capable; first-run and side-panel launch actions use consistent ellipsis copy, icon semantics, and left-aligned compact tool buttons.
- **Shared premium tool-window shell** — Secondary workbenches now use consistent header, sidebar, workspace, status-bar, icon-tile, and empty-state treatments so batch export, cleanup, recovery, model, semantic search, and edit tools feel like one coherent product surface.

### Fixes

- **Viewport context menu is smoke-covered** — Right-clicks from the image viewport now open the existing context menu, and the smoke gate verifies the constrained-window menu stays bounded or scrollable, exposes grouped root commands, opens the Compare submenu, and reaches `Compare with…` by keyboard.
- **Secondary window resource crashes are covered** — shared path converters are now registered in the theme dictionary and About's background-jobs card no longer references Settings-only resources, preventing startup/About XAML crashes from missing `StaticResource` keys.
- **Semantic search fallback is explicit** — CLIP provider creation/preprocessing failures now log warning context, semantic status reports the active provider plus fallback reason, the search window shows deterministic fallback copy, and fixture tests pin deterministic query ranking.
- **Trust-path diagnostics no longer disappear silently** — C2PA runtime/manifest failures, contact-sheet degraded reads, listen-mode client errors, ExifTool cleanup failures, and performance-report storage failures now log contextual diagnostics; About diagnostics now shows C2PA runtime degraded/ready status.
- **C2PA trust environment setup no longer throws** — c2patool inspection now initializes empty trust-anchor/config variables only when absent, preventing missing environment keys from turning a valid no-manifest read into an inspection error.
- **Dispatcher fatal exceptions no longer resume the WPF app** — the dispatcher crash path now logs, writes the crash record/minidump, shows the crash dialog, flushes logs, and returns unhandled so WPF terminates instead of continuing in an undefined state.
- **ExifTool process output no longer risks pipe-buffer stalls** — the process runner now waits asynchronously while stdout and stderr drain, kills timed-out process trees, and preserves any completed output for diagnostics.
- **Tile pyramid cache builds are serialized per image** — concurrent requests for the same deep-zoom pyramid now reuse a per-cache-key build lock, re-check the completed manifest inside the lock, and publish `pyramid.json` atomically after tiles are written.
- **Import inbox GPS-strip failures are no longer silent** — when a requested GPS metadata strip fails after a copy or move, the import is reported as failed and the transferred file is rolled back where possible so a GPS-bearing file is not silently accepted into the destination.
- **APNG export capability correction** — Save/export no longer advertises APNG as a writable target when the runtime cannot encode it reliably. APNG files still open and inspect as before; `.apng` export targets resolve to PNG.
- **Viewport context menu hit target restored** — the image viewport is explicitly hit-testable, so right-click opens the bounded context menu even when the pointer lands on empty viewport space around the rendered image.

- **Parallel batch filename collision** — `RunAsync` now reserves output filenames via `ConcurrentDictionary` across parallel tasks, preventing TOCTOU races where two concurrent writes could silently overwrite the same output file. Also removed a pre-loop cancellation check that could leave null entries in the task array, causing `Task.WhenAll` to throw `ArgumentException` instead of `OperationCanceledException`.
- **ListenService path canonicalization** — incoming TCP paths are now canonicalized with `Path.GetFullPath` before the existence check, preventing `..` segment traversal.
- **Catalog LoadAllAssets missing palette** — `ReadAssetRecord` now reads the `palette` column, so `CatalogRebuildResult.Assets` returns palette data after a scan.
- **External-edit watcher re-armed after navigation** — `ExternalEditReloadController.Arm` is now called in `CompleteCurrentLoad`, so FileSystemWatcher detects external edits to the current file after arrow-key navigation, not just after initial open.
- **CurrentPath set before controller refreshes** — `CurrentPath` is now assigned before metadata/color/C2PA controller refreshes fire, closing a brief window where controllers could compare against the previous path.
- **Activity panel faulted count asymmetry** — Background activity running/faulted counts now derive from the same job set displayed in the panel, preventing the panel from hiding while faulted jobs are still in the visible list.
- **Slideshow timer crash resilience** — Slideshow timer tick is now wrapped in a top-level try-catch to prevent silent slideshow stops on unexpected decode errors.
- **ListenService broken state on port bind failure** — `Start()` no longer leaves the service permanently broken when the requested port is in use; a subsequent call with a different port can succeed.
- **Thumbnail cache quota tracking** — LRU eviction no longer decrements the tracked total before confirming the file delete succeeded, preventing cache growth past the configured cap when files are locked by other processes.
- **NonDestructiveEditService temp file collision** — Atomic sidecar writes now use GUID-based temp filenames to prevent concurrent saves of the same sidecar from clobbering each other.
- **Catalog rebuild crash on inaccessible directories** — Directory enumeration during catalog rebuild now uses `IgnoreInaccessible` to skip permission-denied subdirectories instead of aborting the entire scan.
- **Duplicate cleanup IndexOutOfRange guard** — Similar-image finding summaries and sort now safely handle findings with fewer than two candidates.
- **Tile cache lock memory leak** — Build lock entries are now removed when tile pyramid caches are cleared, preventing unbounded dictionary growth in long sessions.
- **Import inbox GPS strip ordering** — Post-import edits now write sidecar tags and ratings before stripping GPS metadata, so rollback on failure preserves the original file's GPS coordinates.
- **Atomic write TOCTOU race** — Export and batch processor atomic writes now use `File.Move(overwrite: true)` instead of `File.Exists` + `File.Replace/Move`, eliminating a race where a concurrent write could cause data loss.
- **MenuItem hardcoded checked-highlight color** — Replaced a Mocha-specific `#1A89B4FA` in the global MenuItem template trigger with the theme-aware `AccentPanelBrush`.
- **DarkTheme missing theme name marker** — Added `Images.Theme.Name` resource key to DarkTheme for consistency with Latte and HighContrast dictionaries.
- **Gallery focus trap** — Gallery ListBoxItem now sets `Focusable="False"` to delegate keyboard focus to child buttons, matching the filmstrip pattern.
- **Annotation swatch accessibility** — Color palette buttons now have `AutomationProperties.Name` and `ToolTip` so screen readers can identify each color.
- **Sibling folder settings localization** — The sibling folder auto-switch toggle in Settings now uses localized resource strings instead of hardcoded English.
- **RAW preview loading state** — `IsImageLoading` is now re-set to `true` after displaying the embedded JPEG preview, so the loading indicator stays visible while the full RAW decode runs.
- **ApplyInpaint busy guard** — `ApplyInpaintCommand.CanExecute` now checks `!IsOperationBusy`, preventing double-invoke of concurrent inpaint operations.
- **Slideshow flush pending rename** — `SlideshowTimer_Tick` now calls `FlushPendingRename()` before navigating, so a rename in progress is committed rather than silently discarded.
- **CatalogService SHA-256 file sharing** — `ComputeSha256` now opens files with `FileShare.ReadWrite | FileShare.Delete`, matching the rest of the codebase and preventing failures on files being edited by another application.
- **Theme-aware window captions** — `WindowChrome.ApplyDarkCaption` now checks `ThemeService.CurrentMode` and applies Catppuccin Latte light caption colors when the light theme is active, preventing a dark title bar clashing with a light window body.
- **Controller error logging** — `ExternalEditReloadController`, `PhotoMetadataController`, and `ColorAnalysisController` now log exceptions instead of silently swallowing them in catch blocks. `ExternalEditReloadController.Arm` now catches specific exception types instead of bare `catch`.
- **ListenService token truncation** — session token is now logged as the first 8 characters only, preventing full token exposure in log files or support bundles.
- **ImageLoader QuickDimensions reliability** — `BitmapCacheOption.OnLoad` ensures all metadata is read before the stream closes, preventing silent failures on codecs that defer dimension reading.
- **Fullscreen edge-hover panel bugs** — fixed two issues: (1) hiding logic no longer depends on the other panel's state, so each panel hides independently when the mouse leaves its edge zone, (2) `HideFullscreenPanels` now explicitly collapses the right side panel, preventing a WPF binding-cleared bug where the side panel would stay visible on the second fullscreen entry.
- **Stabilized STA supersession controller tests** — `PhotoMetadataControllerTests` and `ColorAnalysisControllerTests` supersession tests no longer intermittently fail during parallel full-suite runs. Root cause was thread pool starvation; fixed by releasing the blocked reader immediately after supersession, widening `PumpUntil` deadline to 5s, and extending the dispatcher drain interval.
- **Release-readiness script repaired** — removed stale `PROJECT_CONTEXT.md` requirement that blocked valid releases; script now validates the current `ROADMAP.md`/`Roadmap_Blocked.md` policy instead.
- **Stale version references corrected** — release support policy updated from `0.1.x`/`net9.0` to `0.2.x`/`net10.0`; release checklist updated to match current roadmap hygiene rules.
- **23 hardcoded XAML hex colors replaced with semantic theme tokens** — status chips, selected-item highlights, overlay dimmers, and subtle surface backgrounds across 8 windows now consume `DynamicResource` brushes from the theme dictionaries. New tokens: `OverlayDimmerBrush`, `SubtleSurfaceBrush`. Light/dark/high-contrast themes all updated.
- **Async void crash prevention (3 handlers)** — `EditStackWindow.ExportButton_Click`, `ImportInboxWindow.ImportButton_Click`, and `ImportInboxWindow.ImportPicasaButton_Click` now catch I/O exceptions instead of crashing the app on disk-full or file-locked errors.
- **FindResource → TryFindResource (14 call sites)** — Programmatic resource lookups in `ReferenceBoardWindow`, `AboutWindow`, `CrashDialog`, and `MainWindow` now use `TryFindResource` with fallbacks, preventing crashes on missing theme resources.
- **Cross-monitor DPI correction** — `MonitorService.MoveWindowToMonitor` now queries the target monitor's DPI via `GetDpiForMonitor` instead of using the source monitor's DPI, fixing incorrect window sizing when moving between monitors with different DPI scaling.
- **CancellationTokenSource disposal leaks (4 windows)** — `DuplicateCleanupWindow`, `FileHealthScanWindow`, `SemanticSearchWindow`, and `ImportInboxWindow` now dispose the old CTS before creating a new one on re-scan.
- **FolderPreviewController semaphore disposal race** — Removed premature `SemaphoreSlim.Dispose()` call that raced with in-flight thumbnail decode tasks during shutdown, causing misleading error log entries.
- **CurrentPath data race in SaveAsCopy and PlayMotionVideo** — `CurrentPath` is now captured into a local variable before `Task.Run` lambdas in `SaveAsCopyAsync` and `PlayMotionVideoAsync`, preventing a race where the rename debounce timer could change the path between the null-check and lambda execution.
- **ListenService shutdown hang** — Replaced synchronous `Dispatcher.Invoke` with `BeginInvoke` in the TCP listen callback to prevent hangs when the dispatcher message pump has stopped.
- **Loading indicator storyboard leak** — Added `StopStoryboard` exit action to the loading ellipse pulse animation so each load cycle does not accumulate orphaned composition-thread clocks.
- **STA test stability** — Added `MainViewModelStateTests` to the serialized `WpfSmoke` collection to prevent intermittent failures from STA thread pollution.

### Features

- **Local assisted culling lane** — Review mode can score the current folder with local-only sharpness, exposure clipping, similarity, and existing XMP rating/pick/reject signals, show reasons per ranked file, and apply keep/reject labels without network access.
- **C2PA export provenance handoff** — Export preview and Save a copy now report whether Content Credentials will be preserved, written through an approved C2PA writer, or omitted; current re-encoded exports explicitly omit credentials unless a future approved writer is configured.
- **Picasa metadata migration importer** — Import Inbox can parse folder-level `.picasa.ini` files plus optional `contacts.xml`, then write XMP sidecars for star ratings, album tags, and face regions without modifying source images.
- **Multi-path launch sessions** — `Images.exe a.jpg b.png c.webp` opens an explicit ad hoc set spanning different folders. Next/previous/Home/End navigate the argument list in order; single-path launch falls back to the existing folder scan. `DirectoryNavigator.OpenExplicitList` supports the feature with 3 new regression tests.
- **Redacted support bundle export** — About window now offers a one-click "Export support bundle" that writes a ZIP containing system info, codec report, network activity summary, diagnostics status, recent logs, crash log, recovery records, redacted settings, and cache health. No image bytes or private paths are included.
- **Local data management panel** — Settings Diagnostics now shows per-store sizes (thumbnails, logs, recovery, semantic index, models, network log, settings DB) and offers individual clear actions for thumbnails, logs, recovery log, and network activity with a refresh button.
- **UIA accessibility contract assertions** — FlaUI smoke tests now verify canvas automation name/help text pattern, window title with filename, navigation button names, toolbar button names, and folder position chip per the documented UIA tree.
- **XMP write-through for color labels, keywords, and location** — `ReviewLabelService` now writes `xmp:Label` (color labels) and IPTC/Photoshop location fields to sidecars. `XmpSidecarImportService.ApplyFolder` applies ratings, color labels, keywords, and location in one pass with per-field counts. 4 new regression tests.
- **CLIP pipeline validation with explicit fallback reasons** — Model Manager exposes a Validate button that runs step-by-step checks (file availability, tokenizer load, preprocessor config, ONNX session creation, text embedding smoke) and surfaces exact failure reasons. `SemanticSearchService.ClipFallbackReason` explains why the deterministic provider was used.
- **Start surface with recent folders, books, and import inbox** — the no-image launch panel now shows recent folders (clickable, bound to MRU), recent archive books with page progress, and an Import Inbox button alongside the existing Open/Paste/Settings/Diagnostics actions.
- **Background jobs center** — `BackgroundJobsService` tracks running and recent async tasks with name, state, duration, error, and affected count. The About window surfaces a Jobs panel with session summary and per-job details.
- **Workflow modes** — Viewer, Review, Organize, Edit, Book, and Diagnostics modes switch between chrome presets (filmstrip, metadata HUD, gallery, review labels). Modes are accessible via the command palette ("Mode: Review", etc.), persisted across sessions, and respect peek/fullscreen isolation.

- **Quick keyword sets** — `KeywordSetService` persists named keyword presets to `keyword-sets.json`, supports add/remove/rename/apply to image sidecars via `TagGraphService`, and exports/imports definitions as portable JSON. 7 regression tests.
- **Performance budget CLI** — `Images.exe --perf-report` measures process-to-CLI time, directory scan, thumbnail cache health, settings DB access, and memory working set against configurable thresholds with pass/warn status.
- **Incremental catalog rescan** — `CatalogService.Rebuild` now skips unchanged files (same size + modified time), removes stale paths, and reports reused/updated/removed counts. Repeat scans avoid re-hashing ~90% of files for large libraries. 2 regression tests.
- **Cross-folder session tray** — `SessionTrayService` manages an ordered, deduplicated list of image paths from any folder. Add, remove, reorder, save/load plain text file lists (with comment and blank line tolerance), and filter to valid entries. 7 regression tests.
- **Saved smart collections** — `SmartCollectionService` persists named filter collections (rating, tags, format, orientation, dimensions, date, duplicate status) to `smart-collections.json`. Collections can be added, removed, renamed, reordered, and applied against catalog/gallery items. 11 regression tests.
- **Contact sheet export** — `ContactSheetService` plans grid layouts from image lists and renders PNG contact sheets with Magick.NET, thumbnail fitting, filename captions, unreadable-file fallback, and optional watermark. 6 regression tests.

### Infrastructure

- **Pseudo-locale overflow gate** - `Strings.qps-ploc.resx` can be regenerated locally, localization validation now requires and checks the pseudo-locale by default, and WPF smoke coverage opens key secondary windows under expanded strings to catch critical control clipping.
- **Magick.NET security policy gate** - Codec runtime startup now applies and reports app-level Magick.NET resource limits, Ghostscript-gated document previews, huge-dimension rendering guards, and blocked PDF/EPS/SVG/MVG/MSL/URL-style write targets; release diagnostics now fail if the policy is not enforced.

- **Local release readiness parity restored** — `scripts\Test-ReleaseReadiness.ps1` now runs version sync, restore, Release build, tests, high/critical vulnerability blocking, localization parity, release diagnostics, checksum generation, and WinGet/Scoop manifest validation from one local command; release/trust docs now describe local-only gates instead of removed hosted workflows or Dependabot.
- **Secondary tool-window UIA smoke coverage** — Smoke-gate tests now open Settings, About/Diagnostics, Duplicate Cleanup, Semantic Search, Model Manager, and Import Inbox with app theme resources, then verify titles, named controls, keyboard focusability, and critical UIA help text hygiene.
- **Edge-hover contextual panels in fullscreen** — in fullscreen mode (F11), the bottom toolbar and right side panel auto-hide for zero-chrome image viewing. Moving the mouse to the bottom or right edge of the screen reveals the respective panel; panels auto-hide after 2 seconds when the mouse leaves the edge zone.
- **Draggable comparison divider in export preview** — the side-by-side export preview now has a draggable GridSplitter between original and encoded output. Drag the divider to adjust comparison proportions; the splitter hides in difference-view mode.
- **Color palette extraction in catalog** — catalog scans now extract the dominant color palette (red/orange/yellow/green/cyan/blue/purple/pink/dark/light/gray) from each image and persist it in the catalog DB. Gallery `palette:` filter tokens query catalog-backed palette data. Schema migrated v1→v2 with `ALTER TABLE ADD COLUMN palette`.
- **Embedded-JPEG-first RAW preview** — RAW files (DNG/NEF/CR2/CR3/ARW/RAF/ORF/PEF and 25+ formats) now show the embedded EXIF thumbnail immediately while the full demosaic decode runs in the background. The async load path extracts the preview via `MagickImage.Ping` + `ExifProfile.CreateThumbnail` without reading the full RAW data. `LoadResult.IsPreview` flag distinguishes preview from full decode.
- **Parallel batch processing** — `BatchProcessorService.RunAsync` processes files concurrently with `SemaphoreSlim`-bounded parallelism (default `ProcessorCount - 1`). The batch window now reports per-file progress during runs. Throughput scales with available cores on large batches. 3 new regression tests.
- **WCAG 2.5.7/2.5.8 accessibility audit** — fixed two undersized interactive chips (channel isolation and slideshow indicator) with `MinHeight="24"`. Documented drag-operation alternatives and known limitations in `docs/accessibility.md`.
- **Test coverage for 11 previously untested services** — 89 new regression tests covering BackgroundJobsService, SupportedImageFormats, WritebackGuardService, WorkflowModeService, DirectorySortMode, PerformanceBudgetService, MetadataEditService, CatalogQueryService, AppInfo, LaunchTiming, and SupportBundleService. Total test count: 708 passed.
- **CI runner pinned to `windows-2025`** — CI, release, and security workflows now use `windows-2025` instead of `windows-latest` to avoid breakage from the June 2026 runner-image migration to Windows Server 2025 + VS 2026.
- **WinGet and Scoop manifest validation** — the release workflow now validates generated Scoop JSON (version, URL, SHA-256 fields) and WinGet YAML (PackageIdentifier, version match) before uploading, with optional `wingetcreate validate` when available.

### Infrastructure

- **WPF smoke tests split into required gate and exploratory tiers** — `LaunchAndClose` and `OpenFixtureImage` now run as a required CI gate (`SmokeGate` category); keyboard-driven interactive tests remain `continue-on-error`.
- **Migrated xUnit v2.9.3 → v3.2.2** — dropped deprecated `Xunit.SkippableFact` in favor of native `Assert.Skip()`; updated test runner to v3.1.5.
- **Deprecated/outdated package CI reporting** — CI and daily security workflows now report deprecated and outdated NuGet packages alongside existing CVE gates, uploading maintenance reports as workflow artifacts.

### Features

- **Operation-chain batch workflow** — Batch Processor now exposes an ordered copy pipeline for resize, rotate, flip, metadata stripping, rename patterns, and export settings. Preview shows output names, dimensions, size deltas, warnings, dry-run remains default, and preview/run work can be canceled.
- **Export preview linked inspection** — Export Preview now uses synchronized pan/zoom canvases for original and encoded output, plus a toggleable difference view generated at encoded dimensions for lossy/export review before saving.
- **Command registry and shortcut rebinding** — `CommandShortcutService` centralizes command IDs, default shortcuts, and user overrides in the `hotkeys` SQLite table. Settings exposes a Hotkeys section with per-command rebinding, conflict detection, and reset-to-default. The command palette, keyboard dispatch, and settings summary now all consume the same registry.
- **Image loading state indicator** — the viewport now clears stale content and shows a pulsing loading indicator while decoding large images. `IsImageLoading` is set true in `PrepareCurrentLoad` and cleared in `CompleteCurrentLoad` (including early-return paths), preventing stale previews after navigation during decode.
- **Centralized ONNX Runtime provider** — one `OnnxRuntimeService` now owns DirectML/CPU probing, SessionOptions creation, and provider label reporting for all AI services (CLIP, background removal, LaMa inpaint, super resolution). Fixes broken provider-name detection.
- **Listen-mode hardening** — per-session cryptographic token authentication, 20/s connection rate limit, 32K char line length cap, inbound/outbound event labeling in the network activity panel, `ClearAll()` to delete persisted logs, and automatic 2000-entry JSONL rotation.
- **Tile-pyramid cache management** — 1 GB cap with LRU eviction for pyramids older than 30 days, `GetHealth()` reporting, `ClearAll()` cache clearing, and automatic post-build eviction.
- **Writeback backup policy** — optional same-folder or app-local backup before destructive crop, rotation, GPS strip, and metadata strip overwrites, configurable via settings.
- **Folder back/forward navigation** — DirectoryNavigator tracks a 50-entry history stack across folder changes, with GoBack()/GoForward() and CanGoBack/CanGoForward properties.
- **XMP sidecar folder import** — command palette action scans a folder for .xmp sidecars and applies ratings to matching images. Keywords reported in toast; write requires future ExifTool integration.
- **Catppuccin Latte light theme** — full Latte palette override with runtime switching. ThemeService supports Dark, Latte, High Contrast, and Follow System modes via the `appearance.theme` setting.
- **WPF smoke test scaffold** — FlaUI-based smoke tests for launch/close, fixture image open, navigation, and Escape exit. CI step runs with `RUN_SMOKE_TESTS=1` on windows-latest.
- **Read-only catalog query boundary** — `CatalogQueryService` wraps the catalog with ListIndexedFolders, QueryByFolder, multi-term Search, and optional path redaction for automation integrations.
- **Localization CI enhancements** — Strings.cs property parity check against resx keys, XAML hard-coded string scan with warnings, and orphaned-property detection.
- **Package-resolution validation** — release readiness script now runs dotnet restore, dotnet build, and a vulnerable-package scan before existing doc/version checks.

- **Command palette (V20-29)** — `Ctrl+Shift+P` opens a VS Code-style fuzzy-search overlay listing 55 commands across 8 categories (Navigate, View, Edit, File, Tools, Review, Compare, Help). Type to filter by name, category, or shortcut; Up/Down to select; Enter or double-click to execute; Escape or click the dimmer to dismiss. No other Windows image viewer ships a command palette.
- **Color-channel isolation (V20-28)** — view individual R, G, B, or A channels as grayscale. Cycle through modes via the command palette or click the channel chip in the bottom toolbar. Mode persists across image navigation. Tile-pyramid (DZI) images skip channel filtering gracefully.
- **Multi-monitor window placement (V20-27)** — window position is now remembered per-monitor so the viewer restores to the correct display across sessions. On multi-monitor setups, the command palette shows "Send to monitor N" commands to move the window between displays. Falls back to primary-monitor clamping when a saved monitor is disconnected.
- **Viewer sort-mode switching (V20-30)** — the main viewer now persists the folder sort order across sessions. Nine sort modes (Name A-Z/Z-A, Modified newest/oldest, Created newest/oldest, Size largest/smallest, Type) are available through the command palette. The selected mode applies to all folder navigation and survives app restarts.
- **Network-listen mode (V20-31)** — `Images.exe --listen <port>` opens the viewer in TCP listen mode on loopback (127.0.0.1). External tools send UTF-8 file paths to open/refresh images live. All received paths are logged in the network activity panel. A green status chip in the toolbar shows the active port.
- **Slideshow (V30-33)** — auto-advances through folder images with configurable 1-60 second interval (default 5s), loop, shuffle, and pause. Start/stop from the command palette; Escape stops; hover or click the green status chip to pause/resume. Manual navigation resets the timer without stopping playback.

### Security

- **Archive reader dependency** — upgraded SharpCompress from 0.47.4 to 0.48.1, clearing the GHSA-6c8g-7p36-r338 / CVE-2026-44788 NuGet vulnerability gate. Images still uses SharpCompress only for read-only archive page streams and does not call the affected `WriteToDirectory()` extraction API.
- **Magick.NET 14.13.0 → 14.14.0** — resolves 12 upstream advisories (2 high, 10 moderate severity) including GHSA-36wm-hprc-mcf5 and GHSA-7gg8-qqx7-92g5. Zero vulnerable NuGet packages remain.
- **Transitive dependency audit** — CI security and release workflows now log the full NuGet transitive dependency tree as an uploadable artifact alongside the vulnerable-package gate, with documented S-09 native decoder floor requirements for future bundled runtimes.
- **ONNX Runtime DirectML 1.24.4** — pinned to 1.24.4 (the latest available release); the previously claimed 1.26.0 version does not exist on NuGet.
- **.NET 10 LTS migration** — moved from .NET 9 STS (EOL November 2026) to .NET 10 LTS (supported through November 2028). All Microsoft.* NuGet packages updated to the 10.0.x track. CI, security, and release workflows updated.
- **Content-based format validation** — files are now probed by magic bytes on open, not just by extension. When the detected content format doesn't match the file extension, an informational toast alerts the user. The same signature detection logic is shared with the file health scanner, eliminating duplicated code.
- **Granular EXIF metadata removal** — the viewport context menu now offers a "Strip metadata" submenu with category choices: device info (make, model, serial numbers), timestamps, software and comments, or all metadata at once. Each category previews what will be removed and writes atomically. The existing GPS-only strip remains as a separate quick action. All strip operations are available through the command palette.
- **Archive password prompt** — password-protected ZIP/CBZ, RAR/CBR, and 7z/CB7 archives now prompt for a password instead of failing with a generic error. The password is cached for the current archive session and cleared when navigating to a non-archive file. SharpCompress handles decryption transparently.
- **Motion Photo / Live Photo detection** — JPEG and HEIC files containing embedded MP4 video segments (Samsung Motion Photos, Google Pixel) are detected by scanning for ftyp boxes near the end of the file. When detected, a context menu and command palette action lets users extract the embedded video to a separate file. Apple Live Photos (.mov companion files alongside JPEGs) are also detected and can be opened directly.
- **Batch metadata strip action** — the macro/batch processor now supports a `strip-metadata` action with a `categories` parameter accepting `gps`, `device`, `timestamps`, `software`, or `all` (comma-separated). Dry-run mode previews how many tags would be removed per file.

### Dependencies

- Microsoft.Data.Sqlite 9.0.0 → 10.0.9
- Microsoft.Extensions.Logging 9.0.0 → 10.0.9
- Microsoft.ML.OnnxRuntime.DirectML pinned at 1.24.4 (latest available)
- Serilog 4.2.0 → 4.3.1
- Serilog.Extensions.Logging 9.0.0 → 10.0.0
- Serilog.Sinks.File 6.0.0 → 7.0.0
- SharpCompress 0.48.1 → 0.49.1
- SQLitePCLRaw.bundle_e_sqlite3 pinned at 3.0.3 (clears GHSA-2m69-gcr7-jv3q High)
- coverlet.collector 6.0.2 → 10.0.1
- Microsoft.NET.Test.Sdk 17.12.0 → 18.6.0
- xunit 2.9.2 → 2.9.3
- Target framework net9.0 → net10.0

### Changed

- **Network activity trust polish** - persisted network activity now restores the newest retained entries first, skips malformed JSONL rows without blocking startup, and the About dialog's Clear log action deletes the local persisted history instead of only clearing the current view.
- **Listen-mode operator polish** - the toolbar chip now refreshes its label/tooltip when listen mode starts, the tooltip/toast/CLI help/README explain the required session-token first line, and the listen-mode path gate has focused tests for local-only file acceptance.
- **XMP import truthfulness** - folder import now matches both `photo.jpg.xmp` and `photo.xmp` sidecars to images, applies only ratings it can actually write, and reports applied, no-rating, unmatched, and failed sidecars separately.
- **Tile-cache cleanup hardening** - failed tile-pyramid builds now remove partial cache directories, and reused/built pyramids refresh their cache timestamp so eviction better reflects recently opened large images.
- **Theme semantic brush hardening** - Latte now overrides the semantic surface/chrome brushes used by shared controls, high-contrast mode now suppresses Latte overlays when active, and selected/accent/status overlays across the viewer resolve through theme tokens instead of dark-only hex values.

- **Settings and collection chrome polish** — shared WPF `DataGrid` and `ListBoxItem` styles now provide intentional row rhythm, hover, selected, focused, and disabled states instead of default Windows chrome. The Hotkeys settings editor now shows quieter shortcut summary copy, inline Default/Custom badges, clearer shortcut edit help text, and a localized live loading indicator in the viewer.
- **Premium desktop polish pass** — refreshed the shared WPF chrome with tighter 8 px radii, calmer elevation, solid accessible focus rings, consistent ComboBox/Tab styles, refined button states, and a tabbed Settings dialog that groups General, Appearance, Accessibility, Advanced, Text extraction, and Diagnostics into a cleaner resizable surface. The main viewer now uses a more forgiving startup size/work-area clamp and a viewport measure guard so large images do not push tool chrome off-screen.
- **Complete jpegtran sidecar staging** — libjpeg-turbo release staging now copies and verifies the required adjacent `jpeg62.dll` alongside `jpegtran.exe`, and the runtime resolver marks incomplete app-local jpegtran bundles unavailable before launching them. This prevents the Windows "jpeg62.dll was not found" loader dialog during diagnostics, startup probing, and release smoke tests.
- **High-contrast runtime theme** — the accessibility high-contrast preference now installs a `SystemColors`-backed theme dictionary immediately, Windows high-contrast mode is honored automatically at startup, and Windows preference/color changes refresh the active dictionary at runtime.
- **Localization foundation** — all user-visible strings across the entire app are now routed through `Strings.resx` resources: MainViewModel (300+ toast/status/error strings), Settings, About, Effects, Adjustments, Model Manager, Duplicate Cleanup, Recovery Center, Batch Processor, Export Preview, File Health Scan, Semantic Search, Tag Graph, Reference Board, Import Inbox, Macro Actions, and all overlays/dialogs. A runtime locale switcher in Settings persists the language preference and applies it on restart.
- **Network egress transparency (P-03)** — every outbound HTTP call now records URL, purpose, bytes, and duration through `NetworkEgressService`. The About window shows a scrollable "Network activity" panel with per-entry cards, copy-to-clipboard, and clear actions. History persists across sessions in a local JSONL file. No competitor ships this.
- **Decode pipeline observability (O-03)** — a custom `Images-Decode` EventSource exposes `images-decoded`, `decode-duration-ms`, `wic-decodes`, `magick-fallback-decodes`, `thumbnail-writes`, and `decode-failures` counters plus `DecodeStarted`/`DecodeCompleted`/`DecodeFailed` events for live `dotnet-counters` monitoring. Recipe in `docs/perf.md`.
- **Store codec extension detection (V20-18)** — when HEIC, AVIF, or JXL decode fails because the Windows Store extension is missing, the load-error panel now names the required extension and shows a one-click "Get" button that opens the Microsoft Store deep-link.
- **Scoop extras manifest (D-03)** — `packaging/scoop/images.json` provides a ready-to-submit Scoop manifest with `checkver` and `autoupdate` sections pointing at GitHub Releases.
- **Migration guide** — `docs/migration-guide.md` documents actionable import steps for digiKam, XnView MP, Apple Photos (via osxphotos), Lightroom Classic, and Picasa users.
- **Metadata capture dates** — EXIF `OffsetTimeOriginal` is now covered by regression tests and rendered with an explicit signed UTC offset when present, while offset-free EXIF dates continue to display without inventing one.
- **WinGet release publisher** — published GitHub releases now have a dedicated WinGet workflow wired to `vedantmgoyal9/winget-releaser@v2`, matching the setup installer asset and cleanly skipping until the required classic `WINGET_TOKEN` secret and `winget-pkgs` fork are configured.
- **Settings information architecture** — Settings now has first-class General, Appearance, Accessibility, Advanced, Hotkeys, Diagnostics, Text extraction, and Privacy sections. New persisted controls cover window-placement restore, reduced viewer motion, high-contrast preference, and archive-book defaults; reduced motion now disables the main viewer's edge-arrow fade animation.
- **Runtime dependency provenance dashboard** — About, `--system-info`, and `--codec-report` now share structured NuGet/runtime/model rows that show source URLs, version, path, SHA-256 where available, advisory status, and setup/release action copy for Magick.NET, SharpCompress, Ghostscript, jpegtran, Windows OCR, and future model runtimes.
- **Local model/runtime manager** — the app now has a Model manager surface and service for approved local model definitions, app-local grouped storage, manual import/delete/reveal actions, pinned SHA-256 validation for OpenCV/Carve LaMa and Qdrant CLIP ViT-B/32 model/tokenizer/preprocessor candidates, Windows ML versus ONNX Runtime DirectML readiness copy, and diagnostics provenance rows without automatic model downloads.
- **Approved jpegtran release staging** — release packaging now stages libjpeg-turbo 3.1.4.1 `jpegtran.exe` from the reviewed `vc-x64` artifact, verifies the installer and extracted executable SHA-256 values, carries the libjpeg-turbo license/readme files, and keeps the executable ignored by git while including it in build/publish output.
- **Release diagnostics smoke** — the release workflow now runs portable and installed `--system-info` / `--codec-report` smoke checks, validates Ghostscript, OCR, jpegtran, and dependency-provenance rows, and uploads the diagnostics logs as workflow artifacts.
- **Compare and overlay review mode** — the viewer can compare the current image with the next folder item, a chosen local file, or the selected duplicate-cleanup pair. Compare mode supports linked pan/zoom/rotate/flip in 2-up and opacity-overlay layouts, A/B swap, keyboard opacity controls, side-panel controls, and Escape exit behavior.
- **Export preview workbench** — `Ctrl+Alt+W` opens an A/B export workbench that encodes the displayed image in memory for JPEG, PNG, WebP, AVIF, and JXL presets, shows original versus encoded preview, estimated output size and byte delta, resize-aware save output, and metadata/transparency/lossy-format warnings before writing a copy.
- **Export capability warnings** — export preview, batch preview, and batch dry-run now share target-format warnings for alpha flattening, animation frame loss, page/layer flattening, EXIF/IPTC/XMP metadata loss, ICC profile risk, and lossy quality settings.
- **Color profile and histogram awareness** — the side panel now reports embedded ICC/profile status, decoded color space, luma and RGB channel stats, shadow/midtone/highlight histogram percentages, alpha transparency stats, and safe unmanaged-color warnings without transforming pixels.
- **Destructive-action recovery center** — the viewer now records move, file-health rename, duplicate/file-health quarantine, crop/rotation/GPS writeback, and Recycle Bin actions in an app-local recovery ledger. The Recovery Center can reveal related paths, restore moves/renames/quarantines with collision-safe targets and sidecar recovery, and explains retention plus non-restorable writeback/Recycling recovery rules.
- **Read-only metadata delete sharing** — photo metadata and color-analysis reads now open Magick.NET streams with delete-sharing so background panels do not briefly block move/delete workflows while they inspect the current file.
- **Catalog schema v1** — the app now has a rebuildable local `catalog.db` foundation that indexes source path, SHA-256 fingerprint, dimensions, file dates, size, codec/format metadata, XMP sidecar path/modified time, rating, tags, and scan timestamps without treating SQLite as the authoritative source of user metadata.
- **Catalog migration guardrails** — catalog schema startup now rejects newer databases instead of downgrading them, runs forward-only SQLite migration hops with integrity checks and WAL checkpointing, creates `catalog.db.bak.v<old>-<new>` backups for existing caches, restores the backup if a hop fails, and verifies a schema canary before the cache is used.
- **Semantic search foundation** — the app now has a local semantic search service and window that explicitly index selected folders, store derived vectors in app-local `semantic-index.db`, search with a deterministic offline metadata embedding provider, filter by folder, open/reveal results, cancel indexing, and delete local search data. Approved ONNX CLIP/SigLIP inference remains gated behind future runtime validation.
- **Culling review mode** — `L` toggles review mode for folder culling. While active, `1`-`5` rate, `0` clears the rating, `P` marks pick, `R` marks reject, and `U` restores the previous review sidecar state. The side panel exposes the same controls and writes local XMP sidecars without requiring a catalog.
- **Automatic freehand crop mode** — normal flat-raster image loads now enter free-aspect crop mode immediately, so dragging on the open image starts a crop without pressing `C`; `C` still toggles crop mode when pan-only canvas control is needed.
- **Crop apply affordances** — Enter now applies an active crop selection from the preview key path, and the crop rectangle shows an on-canvas Apply button anchored to its lower-right edge.
- **Immediate edit preview** — applied crop operations now refresh the displayed image immediately, and the main viewer renders enabled edit-stack operations when loading the current image.
- **Crop file overwrite** — applying a crop now writes the cropped pixels back to the source file, clears baked edit-stack operations, resets stale preload data, and notifies the Windows shell so Explorer thumbnails refresh.
- **Crop format gate** — crop mode now starts only for flat raster image files such as JPEG, PNG, WebP, TIFF, GIF, BMP, HEIC/AVIF/JXL, and similar bitmap formats; PSD/PSB, vector/document previews, archives, and RAW files remain view-only for crop.
- **Pixel selection tool** — `S` now toggles a rectangular on-canvas pixel selection mode with copy/clear controls on the selection edge, side-panel controls, and `Ctrl+C` extraction of the selected pixels to the clipboard.
- **jpegtran runtime provenance** — the app now resolves an optional app-local libjpeg-turbo `jpegtran.exe` sidecar or explicit `IMAGES_JPEGTRAN_EXE` override, then surfaces its path, version, and SHA-256 in About, `--system-info`, and `--codec-report` diagnostics.
- **Lossless JPEG crop writeback** — when an approved local `jpegtran.exe` runtime is available, a single exact MCU-aligned JPEG crop now shells out through a test seam, validates the output, replaces the source atomically with rollback data, and avoids copying stale embedded thumbnails. Missing runtimes, unaligned crops, oriented JPEGs, and multi-operation edit stacks continue through the existing raster overwrite path.
- **Lossless JPEG trim confirmation** — interactive JPEG crop and rotation overwrite now warns before any MCU edge trim, lets the user choose trimmed lossless `jpegtran` writeback or exact raster re-encode, and keeps unattended export/writeback paths exact by default.
- **Inpaint runtime decision** — content-aware repair is now scoped as a future opt-in local LaMa ONNX workflow, using Windows ML first and ONNX Runtime DirectML as fallback, with no bundled model or automatic download in the current viewer/editor.
- **Sharpen, noise reduction, and vignette effects** — `Ctrl+Alt+F` now opens a modeless effects workbench with live Magick.NET previews, Crisp/Clean/Focus presets, Enter-to-apply behavior, XMP edit-stack storage, and Save-a-copy rendering.
- **Rotation writeback** — an explicit Apply rotation to file command now bakes the current right-angle view rotation into flat raster sources, clears baked edit-stack operations, refreshes the displayed image and shell thumbnail, and uses `jpegtran -rotate` for exact aligned JPEGs when the optional runtime is present.
- **Annotations and redaction** — a modeless annotations workbench now supports arrows, boxes, circles, text labels, numbered callouts, freehand pen strokes, blur redaction, and pixelate redaction as non-destructive edit-stack operations rendered on Save a copy.
- **Perspective correction** — `Ctrl+Alt+P` opens a modeless four-corner perspective workbench with draggable handles, keystone nudges, XMP edit-stack storage, and Magick.NET perspective rendering on Save a copy.
- **WinGet and Scoop package manifests** — the release workflow now generates ready-to-submit WinGet multi-file manifests (`SysAdminDoc.Images`) and a Scoop portable manifest (`images.json`) from release checksums, uploads them as workflow artifacts, and prints submission steps. Signing status is documented in `docs/distribution-trust.md`.
- **Deep-zoom tile engine** — huge images (>256 MB or >50 megapixels) now load through a DZI-style WebP tile pyramid under `%LOCALAPPDATA%\Images\tiles\`. The viewer binds the pyramid into the WPF canvas, chooses tile levels from the current zoom, renders only visible cached tiles while panning, supports paged raster frame keys, and disables bitmap-only edit/copy/compare workflows while the display is tile-backed.
- **Background removal** — when an approved segmentation ONNX model (BiRefNet, U-2-Net, or similar) is imported through the Model Manager, background removal runs locally through ONNX Runtime DirectML, produces a transparency mask or a foreground-only RGBA result, and resizes the output back to the original dimensions. Mask-only mode lets users inspect or edit the segmentation before applying.
- **Super-resolution** — when an approved upscaling ONNX model (Real-ESRGAN or similar) is imported through the Model Manager, super-resolution runs locally through ONNX Runtime DirectML. Dynamic-input models can upscale the full image or tile large inputs with overlap; fixed-input models resize through the model dimensions and scale back. The default 4x scale factor is configurable.
- **LaMa ONNX content-aware repair** — when an approved LaMa ONNX model is imported through the Model Manager, a new AI repair mode lets users paint circular mask regions over image areas to fill. The LaMa inference pipeline runs locally through ONNX Runtime DirectML with GPU acceleration and CPU fallback, composites the repaired output back to the original resolution, and writes the result. The Carve LaMa FP32 (512x512 fixed input) and OpenCV LaMa 2025-Jan candidates are both supported.
- **CLIP semantic search MVP** — when approved Qdrant CLIP ViT-B/32 ONNX models are imported through the Model Manager, semantic search automatically upgrades from the deterministic metadata provider to a 512-dimensional CLIP embedding provider backed by ONNX Runtime DirectML. Image and text queries run through local BPE tokenization and CLIP preprocessing with no network calls. The search window, index rebuild, folder management, and cosine similarity ranking all work with the new provider without changes. ONNX Runtime DirectML version and assembly path now appear in About, `--system-info`, and `--codec-report` diagnostics.
- **C2PA content provenance inspection** — when an optional `c2patool` runtime is available (via app-local `Codecs\C2paTool`, system PATH, or `IMAGES_C2PATOOL_EXE`), the side panel shows a read-only Content Credentials section for JPEG, PNG, WebP, AVIF, HEIC, TIFF, and other supported formats. The panel displays the trust badge, claim generator, signature date, assertions, and ingredient provenance from C2PA manifests. Images explicitly communicates that content credentials show provenance (who created or edited a file), not authenticity (whether the content is truthful). Runtime status and SHA-256 appear in About, `--system-info`, and `--codec-report` diagnostics.
- **Auto Enhance** — `Ctrl+Alt+E` now adds a one-click balanced enhancement edit with automatic gamma, white balance, contrast curve, and mild sharpening during Save a copy/export rendering.
- **Copy/Move to folder** — the viewport context menu now copies or moves the current file to a chosen folder, preserves matching XMP sidecars, resolves destination name collisions safely, remembers recent transfer destinations, and refreshes the shell after moves/copies.
- **Wallpaper layout modes** — Set as desktop wallpaper now offers Fill, Fit, Span, and Tile modes and writes the matching Windows wallpaper style before applying the stable app-data wallpaper copy.
- **Send/print/copy actions** — the viewport context menu now supports no-dialog printing to the default printer, local `.eml` email drafts with the current file attached, Copy image, and Copy image and path clipboard payloads.
- **ExifTool safe invocation wrapper** — future metadata-write workflows now have a process-boundary helper that runs ExifTool without shell invocation, sends arguments and target paths through a UTF-8 `-@` argfile, rejects line-break and shell-metacharacter path channels, and cleans temporary argfiles after execution.
- **XMP sidecar import service (M-03, M-04)** — a new `XmpSidecarImportService` reads standard `.xmp` sidecar files and extracts `xmp:Rating` (1-5 or -1 reject), `xmp:Label` (color labels), `dc:subject` (flat keywords), `lr:hierarchicalSubject` (Lightroom/XnView pipe-separated paths), `digiKam:TagsList` (digiKam slash-separated paths), and IPTC/Photoshop location fields. Covers both digiKam "Write metadata to files" and XnView MP "Export to XMP" workflows without reading either app's native database.

### Accessibility

- **A-05 Published UIA tree documentation** — `docs/accessibility.md` documents the full UI Automation tree structure (image canvas, navigation, rename, rating/review, toolbars, editing overlays, dialogs) so screen reader users and accessibility testers know exactly what Narrator, NVDA, and JAWS will announce.
- **A-04 Magnifier caret tracking** — the rename stem TextBox now explicitly raises `TextPatternOnTextSelectionChanged` on every caret move so the Windows Magnifier follows the edit point when "Follow the text insertion point" is active. `AutomationId="StemEditor"` added for reliable UIA element identification.
- **A-06 Screen reader manual test matrix** — `docs/narrator-test-matrix.md` documents a pre-release test script for Narrator, NVDA, and JAWS covering 10 core scenarios (image load, navigation, rename, rating, pick/reject, delete confirmation, gallery, settings, about, toasts) and 5 supplementary checks (filmstrip, OCR overlay, cheatsheet, crop, compare) with expected announcements and per-reader result columns.

### Testing

- **Catalog v1 schema snapshot fixture** — `tests/Images.Tests/Fixtures/catalog.v1.db` is a checked-in SQLite v1 catalog with 3 seeded assets, 5 tags, 1 root, and a schema canary. `CatalogSchemaSnapshotTests` (43 tests) verify the fixture's table/column/index shape, seed data integrity, and that `CatalogService` opens the snapshot via forward migration without data loss. Every future schema bump adds a `catalog.vN.db` and must roll all prior snapshots forward in CI.

## v0.2.11 — 2026-05-05

Self-contained document-preview runtime release.

### Changed

- **Bundled Ghostscript runtime** — portable and installer artifacts now include app-local Ghostscript 10.07.0 so PDF, EPS, PS, and AI previews work on clean machines without requiring users to find and install Ghostscript separately.
- **Ghostscript license/provenance notes** — release documentation now identifies the bundled AGPL Ghostscript runtime, the installed license path, the official Artifex source package, and the SHA-256 values used to verify the runtime installer (`8af854e2d62f9a3a674331321b347118a83928a3726631e458194121cf3bbeec`), bundled `gsdll64.dll` (`1dce67538777ab2f312890f9a2f0ffcff6a4c58ef1149dc6a44f8bd97b31030d`), and source archive (`ddace4e1721f967a55039baff564840225e0baa1d4f5432247ca1ccd1473b7c1`).

## v0.2.10 — 2026-05-05

Performance, reliability, workflow, and product-polish release.

### Changed

- **Improvement tracking** — added a repo-local improvement plan that tracks the 15 engineering, UX, reliability, and CI follow-up items from the May 2026 quality review.
- **CI verification** — added a pull-request/push CI workflow for whitespace diff checks, version metadata sync, vulnerable-package scanning, Release build, tests, and CLI smoke commands.
- **Release metadata gate** — moved version-sync validation into a reusable PowerShell script shared by CI and the release workflow.
- **Shell and clipboard integration** — About, crash recovery, settings, and viewer actions now use shared helpers for opening files/folders/URLs and copying text, reducing duplicated process and clipboard handling.
- **Update-check testability** — update checks now have seams for HTTP, clock, retry-state recording, and state-file behavior, plus non-network tests for release parsing and transient failure policy.
- **Folder preview and sorting** — folder-preview thumbnail orchestration now lives in a focused controller, and the viewer exposes app-owned folder sort modes for natural name, reverse name, modified date, created date, size, and extension grouping.
- **Premium interaction polish** — folder-sort menus now show the active sort with checked menu states, settings changes provide tone-aware saved feedback, and OCR overlay styling now uses shared theme tokens.
- **Recycle Bin confirmation preference** — the delete confirmation now offers a "don't ask again" checkbox, and Settings can restore confirmation before future Recycle Bin deletes.
- **Clipboard import testability** — paste-from-clipboard handling now uses a focused import service with deterministic seams for file lists, image data, storage, naming, and clipboard-temp pruning.
- **Viewer state coverage** — `MainViewModel` now supports isolated settings, clipboard, delete, confirmation, and navigator dependencies for regression tests covering folder-preview sort state, filmstrip persistence, paste opening, and Recycle Bin confirmation preferences.
- **Recycle Bin delete extraction** — Recycle Bin confirmation, "don't ask again" persistence, missing-file handling, and send-failure reporting now live in a focused delete service with direct regression coverage.
- **Rename safety** — extension-unlocked renames now reject unsupported target extensions before touching disk, keeping the viewer and folder navigator in sync.
- **Metadata loading extraction** — photo metadata HUD loading now lives in a focused controller with cancellation, stale-result, timeout, and dispatcher-state coverage.
- **External-edit reload extraction** — external file-watch debounce and reload feedback now live in a focused controller with coverage for coalescing, canceled reloads, failed reload feedback, and watcher creation failures.
- **OCR workflow extraction** — OCR busy/overlay state, cancellation, stale-result guards, local extraction feedback, and overlay-line conversion now live in a focused controller with direct async coverage.
- **Update-check UI extraction** — latest-release state, manual/background update feedback, command invalidation, and release-page opening now live in a focused controller with direct coverage.
- **Folder-preview cancellation coverage** — thumbnail loading now has a deterministic test seam with regression coverage for clear and superseded-refresh cancellation paths.
- **ViewModel relay coverage** — `MainViewModel` now has an internal controller-injection seam with regression coverage for metadata, OCR, and update-check relay properties.
- **UI state hardening** — refresh remains available when the current file was removed externally, and regression tests now cover rename debounce, stale-folder recovery, and command enablement states.
- **Diagnostics status pane** — About now summarizes OCR, Ghostscript, Magick.NET, logs, storage, and update-check state in a compact status section with regression-tested status composition.
- **Diagnostics actions** — the About diagnostics section now lets users copy system info, copy the codec report, open logs, and open the app data folder directly from the status pane.
- **First-run guidance** — the empty viewer state now explains local privacy defaults, broad format support, OCR readiness, document-preview requirements, and Settings/Diagnostics recovery links before a file is opened.
- **Operation status feedback** — manual reload, Save a copy, and GPS metadata stripping now share a visible busy status, with mutating image commands disabled until the operation completes.
- **Update-check transparency** — manual and background update checks now expose busy status through the main UI, suppress duplicate manual checks, and show when GitHub Releases is being contacted.
- **Decode/navigation feedback** — file-open dialog decodes and multi-page page turns now use the shared operation-status surface before slower document or page loads begin.
- **Load/navigation responsiveness** — Open Image and next/previous navigation now decode cache misses off the UI thread, await any in-flight preload instead of duplicating work, and update same-folder preview selection in place instead of rebuilding thousands of thumbnail items on every arrow press.
- **Secondary empty/error states** — unsupported clipboard data, empty recent folders, and stale recent-folder paths now show persistent, actionable side-panel feedback instead of relying only on transient toasts.
- **Secondary recovery feedback** — thumbnail-generation failures and offline update checks now retain actionable status, including failed thumbnail placeholders and no-upload reassurance for network failures.
- **Background task ownership** — thumbnail generation, metadata reads, preload decodes, clipboard-temp pruning, and thumbnail-cache eviction now run through a shared tracker with diagnostics-visible running/completed/failed/canceled counts.
- **Update/cache observability** — manual and background update checks are now included in tracked background work, and diagnostics now shows thumbnail-cache size, file count, temp-file count, cap, and last eviction sweep.
- **Storage and cache test seams** — app storage roots, default settings construction, and default thumbnail-cache construction now have deterministic seams and tests for fallback/unavailable-storage behavior.
- **Thumbnail cache controls** — About diagnostics now lets users open the thumbnail cache folder or clear disposable cached thumbnails with confirmation, progress feedback, and automatic diagnostics refresh.
- **Settings persistence hardening** — settings corruption recovery now uses collision-resistant quarantine names, with tests for corruption reset, schema migration, unavailable storage, and primitive setting defaults.
- **Large-folder stress coverage** — navigation and folder-preview tests now cover thousands of files, volatile folder changes, enumeration failure recovery, and bounded thumbnail requests.
- **Generated codec corpus** — decode/export regression tests now generate PNG, JPEG, WebP, TIFF, GIF/APNG, and SVG samples at runtime, avoiding binary fixtures while protecting codec upgrades.
- **Product differentiator scopes** — added a design scope for local semantic search, duplicate cleanup, compare/overlay, archive/book navigation, peek launch mode, viewer-side adjustments, technical pixel tools, and metadata/culling workflows.
- **Distribution trust plan** — scoped WinGet and Scoop publishing, checksum continuity, code-signing options, SmartScreen expectations, and user verification copy for the next stable release.
- **Integration policy** — documented the no-code-copied optional-runtime gate for licenses, redistribution rights, CVE tracking, binary provenance, process isolation, network behavior, and release impact.
- **Archive/book foundation** — ZIP and CBZ files now open as read-only archive books using built-in .NET ZIP support, with natural page ordering, page-count controls, a page scrubber, unsafe-entry filtering, and recursive-archive guardrails.
- **Archive read position and history** — archive books now remember the last viewed page locally, continue there on reopen, and surface a side-panel book history with page progress.
- **Archive cover handling** — archive books now promote explicit cover/front/folder image entries before natural page order and report cover provenance in decoder details.
- **Archive reader controls** — active archive books now get a side-panel book-controls card, narrow edge page-turn click zones, and reader-mode arrow/Home/End key routing for page turns.
- **Archive runtime review** — documented the RAR/7z dependency policy and approved SharpCompress 0.47.4 as the managed MIT reader for read-only RAR/CBR and 7z/CB7 archive books.
- **RAR/7z archive expansion** — RAR/CBR and 7z/CB7 books now open through SharpCompress with the same unsafe-entry filtering, nested-archive skipping, document-entry skipping, per-entry byte cap, corrupt-archive recovery copy, diagnostics provenance, and generated 7z regression coverage as the ZIP/CBZ foundation.
- **Manga page turns** — archive books now have a persisted right-to-left page-turn mode that swaps physical edge zones and Left/Right Arrow routing without changing semantic next/previous controls.
- **Clean scan preview** — archive books now offer a persisted, preview-only high-contrast grayscale filter for yellowed or low-contrast scanned pages without modifying source archives.
- **Two-page archive spreads** — archive books now have a persisted spread mode that keeps explicit covers single, pairs natural pages, respects right-to-left composition, and advances by spread.
- **Gallery workbench** — the viewer now has a keyboard-first `G` gallery overlay for the current folder with multi-column thumbnails, quick filtering, sort shortcuts, per-thumbnail context actions, current-item selection, and Enter-to-open behavior.
- **Asset smart filters** — Gallery filtering now supports current-folder smart tokens for format, folder, sidecar rating/tag data, palette, orientation, dimensions, date, and exact duplicate status, with quick chips for common filters and a clear-filter affordance.
- **Tag relationships** — `Ctrl+Shift+T` now opens a private local tag graph for namespaces, aliases/siblings, parent-tag expansion, and current-image XMP sidecar import/export.
- **Import inbox** — `Ctrl+Shift+I` now opens a local staging inbox for new files, destination duplicate checks, tag/rating sidecars, GPS stripping on imported JPEG/TIFF copies, Recycle Bin cleanup, and copy/move import.
- **Macro actions** — `Ctrl+Shift+M` now opens an inspectable JSON action runner with dry-run support, load/save JSON, and first actions for GPS stripping, export/convert/resize copies, and rename patterns.
- **Batch processor** — `Ctrl+Shift+B` now opens a preset-based batch export surface with previewed output paths/dimensions, dry-run default, load/save preset JSON, and overwrite-safe resize/convert copies.
- **Non-destructive edit stack** — `Ctrl+Shift+E` now opens edit history with XMP-backed JSON operations, virtual copies, enable/disable controls, apply-on-export Save-a-copy support, and export provenance sidecars.
- **Non-destructive crop mode** — `C` now enables an on-canvas crop selection; dragging records pixel-accurate crop bounds, Enter or Apply adds a crop operation to the XMP edit stack, and Save a copy applies it without modifying the source image.
- **Crop composition controls** — crop mode now supports free, square, 3:2, 4:3, 16:9, and custom aspect ratios plus rule-of-thirds guides while dragging.
- **Lossless JPEG transform policy** — scoped the `jpegtran.exe` runtime gate and added tested MCU-alignment planning so future crop/rotate writeback can warn before any lossless trim.
- **Resize dialog** — `Ctrl+Alt+R` opens a non-destructive resize dialog with percent, pixel, long-edge, and short-edge modes, aspect lock, Lanczos-3/Mitchell/Bicubic filters, and live output-dimension preview.
- **Adjustment workbench** — `Ctrl+Alt+A` opens a modeless non-destructive levels, curve, and HSL workbench with live preview, reset, Enter-to-apply behavior, XMP edit-stack persistence, and Save-a-copy rendering.
- **Local exposure brush** — `Ctrl+Alt+D` toggles a no-modal dodge/burn brush with soft falloff, radius/strength/tone controls, drag-to-paint strokes, Enter-to-apply behavior, XMP edit-stack persistence, and Save-a-copy rendering.
- **Red-eye correction** — `Ctrl+Alt+Y` toggles a no-modal red-eye tool with on-canvas pupil marks, soft correction overlays, radius/strength/red-threshold controls, XMP edit-stack persistence, and Save-a-copy rendering.
- **Clone/heal retouch** — `Ctrl+Alt+H` toggles a no-modal clone stamp and healing brush with Alt-click source picking, soft source-to-target stroke overlays, radius/strength controls, XMP edit-stack persistence, and Save-a-copy rendering.
- **Reference board mode** — `Ctrl+B` now opens a non-modal local reference board seeded from the current image, with supported-file drag/drop, draggable image cards, editable notes, draggable/resizable group frames, always-on-top pinning, zoom/reset controls, clear confirmation, and PNG export bounded to visible board content.
- **Inspector tools** — the side panel now includes a pixel Inspector with live coordinates, HEX/RGB/HSV/alpha readouts, copy buttons, Shift-drag pixel measurements, and nearest-neighbor preview scaling for pixel art; reference-board image cards support Ctrl-hover sampling and Ctrl-click sample copy.
- **Animation frame workbench** — animated GIF/APNG/WebP playback now has a side-panel frame timeline, scrubber, play/pause, first/previous/next/last frame stepping, playback-speed control, current-frame copy, PNG export, and drag-out frame files.
- **Pinned overlay mode** — the current image can now be pinned above other windows with side-panel opacity controls, a visible overlay status banner, context-menu exit actions, and guarded click-through mode that only enables when the `Ctrl+Alt+O` global exit hotkey is available.
- **Duplicate cleanup center** — `Ctrl+Shift+D` now opens a local cleanup surface with exact SHA-256 duplicate groups, perceptual similarity matching, a threshold slider, reference-folder keep preference, side-by-side review, session-level false-positive dismissal, and non-destructive quarantine/Recycling actions for extra candidates.
- **File health scan** — `Ctrl+Shift+H` now opens a local scan surface for bad extensions, broken supported images, zero-byte files, and suspicious temp/partial artifacts, with preview, conflict-safe extension rename, reviewed dismissal, and app-local quarantine.
- **Peek mode hardening** — `--peek` startup now records local timing milestones and first-image timing, with parser regression tests and shell-helper documentation for chromeless preview integrations.
- **OCR workflow polish** — text extraction now has a persistent in-view busy/active status, a cancel-aware toolbar state, OCR readiness in Settings/About, and OCR language-pack status in diagnostics.
- **Open-source viewer research** — added a May 2026 research scan of ImageGlass, nomacs, PicView, NeeView, QuickLook, Geeqie, gThumb, qView, JPEGView, Tacent View, Minimal Image Viewer, and LightningView, then folded the findings into the improvement plan.
- **Trust copy** — README destructive-action wording now reflects the Recycle Bin confirmation flow.

## v0.2.9 — 2026-05-04

OCR overlay usability hotfix.

### Fixed

- **OCR overlay placement** — OCR regions now place their Canvas item containers at the recognized image coordinates instead of setting Canvas offsets inside the data template, fixing boxes that were stacked along the top edge.
- **Selectable OCR text** — OCR regions now render read-only selectable text boxes so recognized text can be highlighted and copied manually with Ctrl+C or the context menu.
- **OCR line bounds** — line overlays now use the union of all recognized word boxes instead of only the first and last word, improving placement on uneven lines.

## v0.2.8 — 2026-05-04

Installer hotfix for OCR readiness.

### Fixed

- **Installer OCR provisioning** — the Inno installer now runs an elevated Windows optional-capability provisioning step for the current UI language OCR pack plus `en-US` fallback, logging details to `Images-OCR-capability.log` in the install folder.
- **Upgrade cleanup** — the installer now removes existing Images installs before copying the new build, cleans stale per-user registry shadows that could point file opens at `%LOCALAPPDATA%`, and carries existing file-association registration forward.

## v0.2.7 — 2026-05-04

Hotfix for OCR extraction reliability and diagnostics.

### Fixed

- **OCR stream lifetime** — image streams copied for Windows.Media.Ocr now keep the WinRT write adapter alive until decoding completes, fixing `ObjectDisposedException` failures when pressing the OCR button.
- **OCR failure messaging** — true OCR extraction failures now surface as extraction failures instead of being collapsed into the misleading "no language packs installed" path.

## v0.2.6 — 2026-05-04

Sixth hardening pass for release integrity and packaging gates.

### Fixed

- **Release version integrity** — the release workflow now rejects malformed version inputs and refuses to publish unless `Images.csproj`, `app.manifest`, installer defaults, and README release commands are all synced to the dispatched version.
- **Solution build consistency** — release restore/build steps now target `Images.sln` explicitly so the artifact gate exercises the same project graph as local validation.

## v0.2.5 — 2026-05-04

Fifth hardening pass for OCR overlay coordinate correctness.

### Fixed

- **OCR overlay alignment** — OCR text boxes now share the viewer's image-pixel-to-viewport transform, keeping regions aligned across fit letterboxing, zoom, pan, rotation, and flip states instead of drawing raw pixels on the viewport.
- **Transform regression coverage** — added focused matrix tests for fit-centering, zoom/pan, rotation, and flip coordinate mapping.

## v0.2.4 — 2026-05-04

Fourth hardening pass for local state files, diagnostics exports, clipboard paste storage, and single-file runtime metadata.

### Fixed

- **Update-check state safety** — `update-check.json` now rejects oversized local state, logs read/write failures, writes through a temp file, and ignores future timestamps instead of suppressing checks indefinitely.
- **Diagnostics export safety** — About-window system-info exports now use a per-file GUID in the app diagnostics folder instead of a collision-prone timestamp in the shared temp root.
- **Clipboard paste hygiene** — pasted bitmap files now use collision-resistant names, `CreateNew` writes, and background pruning for old or excessive clipboard images.
- **Single-file metadata** — app version diagnostics now fall back to the process path when assembly location is empty in bundled deployments.

## v0.2.3 — 2026-05-04

Third hardening pass for runtime utilities, recent-folder persistence, workflow gates, wallpaper safety, and diagnostics wording.

### Fixed

- **Recent folders** — MRU entries are now normalized to full paths and only persisted when the folder still exists, avoiding duplicate relative/canonical entries.
- **Wallpaper safety** — Set-as-wallpaper now copies through a temporary file and atomically swaps the stable app-data wallpaper slot.
- **Workflow coverage** — release and security vulnerability gates now scan the whole solution, including test-project dependencies, instead of only the app project.
- **Diagnostics polish** — codec capability wording no longer claims vector editing is unavailable forever, and XCF fallback guidance correctly refers to GIMP.
- **Settings cleanup** — removed the unused telemetry settings key so the local settings surface matches the documented no-telemetry product behavior.

## v0.2.2 — 2026-05-04

Second production-hardening pass for thumbnail cancellation, export writes, metadata status, crash logs, and accessibility.

### Fixed

- **Thumbnail responsiveness** — folder-preview thumbnail generation now accepts cancellation tokens so rapid navigation stops updating superseded preview state earlier.
- **Preload cleanup** — preload cancellation sources rotate under a lock and dispose after in-flight tasks have had time to observe cancellation, reducing rare reset/dispose races.
- **Export safety** — save-a-copy paths are normalized and written through same-folder temp files before atomic replace/move so partial exports do not clobber an existing destination.
- **Crash diagnostics** — crash log appends now use `FileShare.ReadWrite`, allowing About-window diagnostics or external editors to read logs while a crash record is being written.
- **Metadata UX** — metadata reads time out with a visible status instead of leaving the HUD in a perpetual loading state.
- **OCR accessibility** — OCR regions now expose screen-reader names/help text and support keyboard copy with Enter/Space.
- **Settings clarity** — update-check copy now consistently says automatic checks are off by default.

## v0.2.1 — 2026-05-04

Production hardening release for OCR, file operations, release packaging, and privacy defaults.

### Fixed

- **OCR stability** — removed the leaked reflection-based 60 FPS overlay timer, made click-to-copy selection feedback observable, ignored empty OCR word lines, and cancel stale OCR runs when the overlay is hidden or the image changes.
- **Rename safety** — invalid filenames now surface a clear toast instead of silently no-oping, and rename/undo paths retry deterministic conflict targets when another process creates a competing filename mid-operation.
- **Folder navigation resilience** — directory refresh now clamps stale indexes after external file changes, and renamed paths are normalized and validated before the navigator follows them.
- **Metadata edit safety** — GPS-stripping writes use a short GUID sibling temp file to avoid long-path temp-name failures and keep cleanup reliable.
- **Decode guards** — Magick.NET bitmap conversion now rejects oversized dimensions before stride/pixel-buffer allocation.
- **Metadata sanitation** — embedded string metadata drops control characters and GPS display rejects malformed coordinates outside valid latitude/longitude ranges.
- **Update-check safety** — release JSON downloads are bounded to 64 KB before deserialization.
- **Settings reliability** — SQLite settings open with a busy timeout and WAL mode to reduce multi-process lock failures.

### Trust and release

- **Network quiet by default** — automatic update checks now default off; users can enable startup checks in Settings, and manual checks still work from About.
- **Release workflow hardening** — optional Ghostscript bundles require a matching SHA-256, the workflow avoids ExecutionPolicy Bypass, PDBs are stripped from portable packages, and release checksums are uploaded.
- **Version sync** — manifest, installer defaults, README badge, and assembly metadata now agree on v0.2.1.

## v0.2.0 — 2026-05-04

Text extraction (OCR) using Windows.Media.Ocr API. Local processing, privacy-first.

### Features

- **Text extraction (E key)** — press `E`, click the Extract Text toolbar button, or right-click and choose "Extract text" to overlay semi-transparent blue bounding boxes on detected text regions. Windows.Media.Ocr API provides local, offline text recognition through installed Windows OCR language capabilities. Overlay toggles on/off with the same `E` key. Toast notifications confirm extraction status (number of regions found, no text found, OCR unavailable).
- **Phase 1 implementation** — uses native Windows.Media.Ocr for feature parity with Windows Photos. No additional dependencies or deployment bloat. Accuracy: ~85-90% on clean printed documents, ~75-80% on complex layouts. Speed: ~1 second per image on CPU-only processing. Phase 2 (v0.3.0+) will add optional PaddleOCRSharp "Advanced Mode" with ~92-95% accuracy and GPU acceleration for power users.

### Technical

- **WinRT interop** — updated project to `net9.0-windows10.0.22621.0` TFM for WinRT API access. Added `Services/OcrService.cs` with `ExtractTextAsync(Stream)`, `GetAvailableLanguages()`, and `IsAvailable()` methods. Handles pixel format conversion (Bgra8/Gray8 requirement), caches `OcrEngine` instance for performance.
- **UI overlay** — new `Controls/OcrOverlay.xaml` Canvas-based control with click-to-copy functionality. Semi-transparent Catppuccin Blue (#89B4FA) at 30% opacity, 1px border. Integrated into MainWindow Viewport Grid with visibility binding to `IsOcrMode` property.
- **ViewModel integration** — `MainViewModel` now includes `IsOcrMode`, `OcrModeTooltip`, `OcrOverlayLines` properties, `ExtractTextCommand`, and `OcrTextLine` helper class. `ExtractTextAsync()` method orchestrates OCR workflow with comprehensive error handling and user feedback.

### Known Limitations (Phase 1)

- **Fixed overlay coordinates** — overlay boxes don't sync with viewport zoom/pan transform. Acceptable for v0.2.0 MVP (most users view at fit-to-window). Phase 2 will bind overlay transform to `ZoomPanImage` state.
- **Single-region copy only** — no multi-select, Ctrl+click, or Select All. Click one box → copies one line. Phase 2 will add multi-region selection and "Copy all text" button.
- **No in-app language picker** — users must install language packs via Windows Settings. Phase 2 will add Settings window OCR section with language enumeration and direct link to Windows language settings.

## v0.1.9 — 2026-05-04

Settings window, GPS-location strip, and automatic external-edit reload. Three ROADMAP items closed.

### Features

- **Settings window (Ctrl+,)** — dedicated Settings window (Item 2) with Viewer and Privacy sections. Viewer: filmstrip-visible-at-startup and metadata-HUD-visible-at-startup toggles. Privacy: update-check opt-in/out. Accessible via `Ctrl+,`, the gear icon in the right-panel header, and "Settings…" in the context menu. Settings apply immediately (no OK/Apply step) and are persisted to `settings.db`. After the window closes, the main viewer reflects any changes to filmstrip and HUD state without requiring a restart.
- **Strip GPS location (P-01)** — "Strip GPS location" toolbar button and context-menu item removes all GPS EXIF values from the current file using Magick.NET and writes the result atomically (temp-file swap — crash-safe). Reports the number of GPS fields removed via toast. Returns "No GPS data found" when the file is clean. Reloads the image and metadata HUD after stripping so the overlay updates in place.
- **Auto-reload on external edit (Item 61)** — when an image is opened, a `FileSystemWatcher` monitors it for `LastWrite` / `Size` changes. Rapid writes are coalesced via an 800 ms debounce timer so incremental saves from Photoshop / Paint.NET / etc. produce a single reload. Toast: "Reloaded — file changed externally". Degrades silently on network drives or locked volumes. Preload cache is cleared before reload so stale decoded frames are not reused.

## v0.1.8 — 2026-04-25

UI surface release. Promotes the foundation work from v0.1.7 into user-visible features: clipboard paste, open-with-default-app, richer decode error messages, and the recent-folders side panel. Eight ROADMAP items closed or advanced.

### Features

- **Clipboard paste (Ctrl+V)** — `Paste from clipboard` context-menu item and `Ctrl+V` shortcut. Accepts a clipboard file-drop list (file copied in Explorer — opens the first supported image directly) or raw pixel data (screenshot, web image) saved to `%LOCALAPPDATA%\Images\clipboard\clipboard-<ts>.png` and loaded immediately. Toast confirms the paste. Ctrl+V added to the keyboard cheatsheet.
- **Open with default app** — `Open with default app` context-menu item opens the current image in whatever app Windows has registered as the default for that file type (`UseShellExecute = true`). Errors surface as a toast. Gated on `HasImage`.
- **Richer decode error messages (item 86 enhancement)** — `SetLoadError` now detects `FileNotFoundException` (file not found title + navigate-away hint), `UnauthorizedAccessException` (access-denied title + check-permissions hint), and `OutOfMemoryException` (image-too-large title + free-memory hint) before falling back to the generic path. New `SupportedImageFormats.SuggestionForDecodeFailure(ext)` returns format-specific hints for supported-but-failing types: HEIC/AVIF (Microsoft Store codec), JXL (Windows 11 24H2+), camera RAW (DNG Converter), PSD/PSB (32-bit export workaround), TIFF (re-save as standard), SVG/SVGZ (browser preview), XCF (flatten + export from GIMP), EXR (convert with ImageMagick), and PDF/PS/EPS/AI (Ghostscript). Generic fallback unchanged when no hint applies.

### Trust

- **Item 34 — Vulnerable-package CI gate**. New [`Security` workflow](.github/workflows/security.yml) runs `dotnet list package --vulnerable --include-transitive` on every push/PR that touches dependencies, daily on a 09:00 UTC cron, and on demand. Fails the job if any package in the resolved graph (direct or transitive) carries a known CVE. Same scan is wired into [`Release` workflow](.github/workflows/release.yml) as a pre-publish gate so a vulnerable release literally cannot be uploaded.
- **Item 33 first slice — Diagnostics export from About**. Two new ChromeButtons in About: **Save system info** writes the same content as `Images.exe --system-info` to `%TEMP%\images-system-info-<timestamp>.txt` and reveals it in Explorer (UTF-8 BOM so Notepad opens it cleanly). **Open data folder** opens `%LOCALAPPDATA%\Images\` so users can reach Logs, `crash.log`, `settings.db`, `update-check.json`, and `thumbs/` in one click. Replaces "open a terminal and pipe stdout to a file" as the bug-report attachment workflow.
- **Item 90 — Trust docs**. Three new policies in `docs/`:
  - [`release-support-policy.md`](docs/release-support-policy.md) — what gets servicing, how long; servicing surface (NuGet + native runtimes + .NET); breaking-change policy (forward-only hop-by-hop migrations; caches always disposable); reporting and distribution channels.
  - [`codec-support-policy.md`](docs/codec-support-policy.md) — bundled-vs-optional tiers; what ships in the bundle; opt-in Ghostscript discovery contract; the five-point checklist that gates any new optional decoder (license / CVE / cadence / provenance / process isolation); decoder-removal policy.
  - [`privacy-policy.md`](docs/privacy-policy.md) — every network call (one — update check), how to turn it off, every file persisted to disk, what does **not** happen (no telemetry, cloud sync, OCR, face/object detection, ad SDKs, file-path egress), and a four-step verification recipe (toggle off + log inspect + `--system-info` + `grep HttpClient`).

### Features (cont.)

- **V20-37 `--system-info` / `--codec-report` / `--version` / `--help` CLI** — new `Services/CliReport.cs` resolves a single-token CLI flag in `App.OnStartup` BEFORE the codec runtime is configured and BEFORE any window is shown, then exits with a normal process exit code. Output is sent to the parent terminal via `AttachConsole(ATTACH_PARENT_PROCESS)` so `Images.exe --system-info` actually prints into the launching shell instead of vanishing into a detached console. `--system-info` reports application version + binary path, .NET runtime + OS + process arch + 64-bit flag + processor count + working set, decoder runtime (Magick.NET version + assembly path; Ghostscript availability/source/version/DLL path/SHA-256), open + export extension counts, and every writable storage path Images uses (app data root, Logs, thumbs, wallpaper, crash log). `--codec-report` prints the per-format capability matrix and the full extension catalog. The CLI surface and the About dialog read from the same `CodecCapabilityService.BuildProvenance()` call so they cannot disagree about what's loaded.
- **X-02 capability matrix in About** — About dialog now surfaces a per-format-family matrix (Common images, Design and production, Portable and scientific, Vector previews, Document previews, Camera RAW) with open/export counts, ternary animation/multi-page/metadata flags, the active runtime label, and a notes line describing concrete limitations (PSD layer flatten, RAW read-only, document DPI, etc.). Replaces the single "Codecs" line with an auditable surface so "broad codec support" is verifiable instead of asserted.
- **Item 86 unsupported-format guidance** — `SupportedImageFormats.SuggestionForUnsupported(ext)` keys human-readable hints off file extension. Toasts on dropped/opened video, audio, archive/comic, document, presentation, spreadsheet, native design-suite, and HEIC/PDF cases now point at the right tool ("Open video files in VLC or mpv", "Archive mode is not built yet — extract first or wait for the next milestone", etc.). The decode-error card surfaces the same suggestion as `LoadErrorHelpText` when a recognized but failing extension lands in the load path.
- **V20-21 first slice — bottom folder filmstrip** — the cached folder preview rail now lives in the bottom viewer chrome, can be toggled with the toolbar or `T`, persists the preference in settings, and falls back to the side panel when hidden so thumbnail jumping remains available.
- **V20-21 follow-up — centered active thumbnail** — the current thumbnail now auto-centers in the bottom filmstrip or side-panel fallback after folder refreshes, navigation, and filmstrip toggles. Thumbnail buttons also expose their position to assistive tech and use the shared accent state for keyboard focus.
- **V20-21 follow-up — thumbnail actions** — right-clicking a thumbnail in the bottom filmstrip or side-panel fallback now offers Open, Reveal in Explorer, and Copy path without changing the current image unless Open is chosen.
- **V20-21 complete — virtualized full-folder rail** — the filmstrip and side fallback now enumerate the full current folder through recycling WPF virtualization instead of the previous nine-item window. Thumbnails still decode lazily for visible and near-current items so large folders remain responsive.
- **V20-22 first slice — photo metadata summary** — new `Services/ImageMetadataService.cs` reads EXIF via Magick.NET `Ping()` on a background task and fills the side-panel Details area with captured date (via `MetadataDate` / `DateTimeOffset`), camera, lens, shutter/aperture/ISO, focal length, and GPS coordinates when present. Empty and loading states are explicit, the read is local-only, and GPS remains text-only so no map/network egress is introduced.
- **V20-22 follow-up — viewport metadata HUD** — press `I`, use the viewport context menu, or click the toolbar info button to toggle a persisted floating EXIF HUD. It reuses the same local metadata rows as the side panel, carries loading/empty states, and can be dismissed in place without changing the current image.

### Trust + provenance

- **Runtime provenance card in About** — new "Runtime provenance" section lists app directory, process architecture, Magick.NET version + on-disk assembly path, Ghostscript availability, source label (bundled / `IMAGES_GHOSTSCRIPT_DIR` / installed), version (when `gswin*c.exe` is present), absolute DLL path, and SHA-256 of the loaded `gsdll64.dll` / `gsdll32.dll`. The hash gives release maintainers a one-shot integrity check against the redistributable approved at release time. Same data is mirrored in the `--system-info` CLI output and the **Copy codec report** clipboard payload.
- **`CodecRuntime` provenance helpers** — `GetMagickAssemblyVersion()` and `GetMagickAssemblyPath()` read `AssemblyInformationalVersionAttribute` so the shown Magick.NET version always tracks the actually-loaded NuGet package, not a hardcoded "14.13" string. `GetGhostscriptDllPath()` + `GetGhostscriptDllSha256()` resolve the loaded `gsdll*` and stream-hash it for the provenance surface.
- **`docs/codec-bundling.md` provenance section** — documents the three surfaces (About card, clipboard report, CLI) and the SHA-256 drift check that must pass before a release ships.

- **V20-32 `--peek <path>` CLI mode** — chromeless, topmost, maximized overlay for PowerToys-Peek-style invocation. Side panel + bottom toolbar hidden; image fills the whole window. Escape closes. Lets Images drop into any external workflow that expects a single-image preview tool (File Explorer add-ons, terminal previewers, editor integrations). Path resolved through the same canonicalizer the regular open path uses so device-namespace shapes are rejected before downstream consumption. Two-token contract enforced exactly (`args.Length == 2`) — trailing junk falls through to regular argv handling.
- **V20-15-Loop animation loop-count badge** — the existing animated-image chip now surfaces `AnimationSequence.LoopCount`. Reads `{N} frames · loops` for the GIF-spec infinite case (`LoopCount <= 0`) and `{N} frames · plays Mx` for finite loops. `IsAnimated` tightened to require `Frames.Count >= 2` so the chip can never disagree with `ZoomPanImage.OnAnimationChanged`'s gate.
- **RecentUI — recent-folders menu in side panel** — V20-02 SQLite recent-folders MRU (data layer shipped v0.1.7) is now a clickable list between Recent renames and Details. Each entry is a folder-icon + basename card with full-path tooltip + `AutomationProperties.Name` for screen readers. Click loads the first supported image in that folder via `DirectoryNavigator.SupportedExtensions`. Empty / unreachable folders surface a toast; never crashes. Whole section hides on a fresh-install empty MRU.

## v0.1.7 — 2026-04-24

Factory iter-3 foundations release. Lays the persistence + preload + thumbnail-cache + UIA-peer quartet that multiple v0.2.0 items were blocked on. Seven ROADMAP items closed. All foundational — no user-visible UI surfaces change yet (those ship in v0.1.8+), but every open-file feels quicker after the first arrow-press thanks to preload, window geometry survives restarts, and the update check now has a proper opt-out toggle.

### Foundations

- **V20-02 SQLite settings service** — new `Services/SettingsService.cs` on `Microsoft.Data.Sqlite` 9.0.0 at `%LOCALAPPDATA%\Images\settings.db`. Schema v1 seeds three tables (`settings` key/value, `recent_folders` MRU, `hotkeys` action/key/mods). Hop-only migrations via `PRAGMA user_version`. Corruption recovery quarantines `settings.db` → `settings.db.corrupt-<ts>` and starts fresh — per SCH-01 the cache is disposable, never authoritative. Strongly-typed `Keys` class so call-sites get compile-time checking. `ILogger<T>` routes errors through the Serilog rolling file.
- **Window-state persistence** — `MainWindow` saves `Left/Top/Width/Height/Maximized` on `Closing`, restores on construction. Restore clamps to current `SystemParameters.WorkArea` so a window from a now-disconnected second monitor doesn't vanish offscreen. Maximized state persists but the saved geometry is always the `RestoreBounds` — unmaximize lands where you'd expect.
- **Recent-folders MRU** — `SettingsService.TouchRecentFolder` runs on every `OpenFile`; one-statement INSERT-OR-REPLACE-then-DELETE keeps the list at 10 entries. Filters out folders that no longer exist on disk when queried. The UI surface (Recent menu in the side panel) lands v0.1.8.
- **Update-check opt-out** — `UpdateCheckService.OptedIn` backed by `Keys.UpdateCheckEnabled` (default on). New "Automatically check for updates" checkbox in the About dialog. `IsDueForBackgroundCheck` short-circuits on false — zero network egress when disabled, cleanly fulfilling the charter's "zero telemetry" line for users who want it.

### Performance

- **V20-03 preload N±1 ring** — new `Services/PreloadService.cs` decodes next + previous image on a background `Task` as soon as the current one lands. Bounded at 3 slots (N-1, N, N+1) with LRU eviction. Cancellation-friendly — nav to a different image cancels the outstanding decodes. Files over 40 megapixels skip preload (memory pressure guard — a 100 MP panorama × 3 slots would burn gigabytes of managed heap to speculatively decode images the user may never look at). `MainViewModel.LoadCurrent` now prefers a cache hit, falls through to direct load on miss; `EnqueueNeighbours` runs after every load with wrap-around matching the nav semantics.
- **V20-04 thumbnail cache disk layer** — new `Services/ThumbnailCache.cs` at `%LOCALAPPDATA%\Images\thumbs\<2-char>\<sha1>.webp`. Key = `SHA1(path.lower() + mtime_ticks + size_bytes)` so path rename / file edit / file resize all invalidate the cached thumb naturally. Git-like 2-char partition directory avoids directory explosion on large libraries. Magick.NET resize to 256-px longest edge, WebP quality 80, EXIF stripped, 512 MB disk cap with LRU eviction. No UI consumer this iter — V20-21 filmstrip will be the first; disk layer ships now so that code lands without re-architecting the cache shape.

### Accessibility

- **A-01 `ImageCanvasAutomationPeer`** — new `Controls/ImageCanvasAutomationPeer.cs` subclasses `FrameworkElementAutomationPeer`. Reports `AutomationControlType.Image`, `GetName` = "Image, W by H pixels" from the live source, `GetHelpText` = arrow/wheel/drag/double-click semantics so Narrator/NVDA/JAWS announce on focus. `ZoomPanImage.OnCreateAutomationPeer` returns it. No OSS Windows image viewer publishes this UIA tree — positioning win against ImageGlass / nomacs / qView / JPEGView.

### Research artifacts

- `docs/research/iter-3-state-of-repo.md` — Phase 0 recon, scale-gate, iter-2 delta consumed.
- `docs/research/iter-3-scored.md` — condensed Phase 2+3+5 (same-session delta; only 10 new items warranted; all NOW-tier; 7-check self-audit with explicit mitigations for SQLite CVE scan + window-clamp + preload memory guard + thumb hash collision + UIA peer fallback via `AutomationProperties.Name`).

### Deps

- Added: Microsoft.Data.Sqlite 9.0.0.

## v0.1.6 — 2026-04-24

Factory iter-2 polish + observability release. Eight tasks closed — promotes the ad-hoc text crash log into structured Serilog + minidump + user-actionable crash dialog, ships Print + Save-as-copy + four zoom modes, adds a read-only GitHub-Releases update check (the first network egress — documented + throttled + opt-out), and lays the `MetadataDate` scaffold for v0.2.x metadata display.

### File ops

- **Print current image (V15-10)** — new `Services/PrintService.Print` wraps `PrintDialog` on a single `FixedDocument` page. 0.5in margins, fit-to-page with a never-upscale-past-1:1 ceiling. Ctrl+P + context-menu entry + toolbar-menu integration. Prints the undecorated decoded first-frame; rotation + flip aren't baked in (same convention as Windows Photos).
- **Save-as-copy (E6)** — Ctrl+Shift+S + menu. `SaveFileDialog` with format filter; picks a `BitmapEncoder` per extension (`JpegBitmapEncoder` @ quality 92 / `PngBitmapEncoder` / `BmpBitmapEncoder` / `TiffBitmapEncoder` / `GifBitmapEncoder` / PNG default). Writes the first frame; file becomes the selected navigation entry.

### Viewer

- **Four zoom modes (V20-20 partial)** — new `ZoomPanImage.ZoomMode` enum exposes `Fit` / `OneToOne` / `FitWidth` / `FitHeight` / `Fill`. `SetZoomMode` computes against the current source pixel size + viewport, reuses the baseline `Stretch.Uniform` as the 1.0x reference. Ctrl+F cycles with toast readout of the active mode. Auto + Lock-to-% deferred to V20-02 so the choice can persist across sessions.

### Observability

- **Structured logging (V02-06 / O-01)** — new `Services/Log.cs` bridges Serilog 4.2 into `Microsoft.Extensions.Logging` 9.0 so call-sites take an abstract `ILogger<T>`; rolling file at `%LOCALAPPDATA%\Images\Logs\images-yyyyMMdd.log`, 14-day retention, ISO-ish timestamp with offset. `App.xaml.cs` logs version + runtime + OS on startup, and every fatal-exception handler now emits both a structured log entry AND the plain-text `CrashLog.Append` record — forensic surface + user-actionable surface share the same event.
- **Minidump + crash dialog (V02-07 / O-04)** — new `CrashLog.TryWriteMiniDump` P/Invokes `dbghelp.dll!MiniDumpWriteDump` with `DataSegs | UnloadedModules | ThreadInfo` flags; dumps land at `%LOCALAPPDATA%\Images\Logs\crash-<yyyyMMdd-HHmmss>.dmp`. New `CrashDialog.xaml` replaces the raw `MessageBox.Show` on `DispatcherUnhandledException` — Copy details (to clipboard) / Open log folder / Open GitHub issue (with the details pre-filled in the URL, truncated at 5500 chars to respect GitHub's issue-new endpoint cap) / Close. AppDomain + TaskScheduler handlers also write dumps on termination paths.

### Distribution

- **Update check (P-04)** — new `Services/UpdateCheckService` does a read-only GET against `https://api.github.com/repos/SysAdminDoc/Images/releases/latest` with a 24-h throttle for the silent startup check; manual "Check for updates" button in the About dialog bypasses the throttle. Every call logged with URL + byte count + duration (beachhead for P-03 network-egress log panel). Last-checked timestamp persisted to `%LOCALAPPDATA%\Images\update-check.json`. On finding a newer tag, toast + stored latest-tag + URL so the UI can surface the "get the update" CTA.

### i18n scaffolding

- **`MetadataDate` value type (NEXT-11 / I-04 precursor)** — new `Services/MetadataDate.cs` wraps `DateTimeOffset?` with an explicit `HasOffset` flag (mirrors EXIF 3.0 convention where `DateTimeOriginal` is local-no-TZ and `OffsetTimeOriginal` carries the offset). Parses EXIF strings + formats per `CultureInfo.CurrentCulture`. Beachhead so v0.2.x metadata overlay never bakes `DateTime` into a signature that'd need a compat break.

### Docs

- **DPI audit (NEXT-12)** — new `docs/dpi-audit.md` documents that 110 literal-size attributes across 4 XAML files are all DIU (device-independent units), not raw pixels. `permonitorv2` in app.manifest + WPF layout system means all are DPI-safe. Future fragility risk lives in code-behind that bypasses WPF layout (we have none today).

### Research artifacts

- `docs/research/iter-2-state-of-repo.md` / `iter-2-sources.md` (+7 delta entries) / `iter-2-harvest.md` (12 delta items) / `iter-2-scored.md` (6 NOW + 2 NEXT) / `iter-2-audit.md` (7-check self-audit with two explicit mitigations — Serilog dep scan after this lands, update-check egress transparency via logged URL/bytes/duration).

### Deps

- Added: Serilog 4.2.0, Serilog.Sinks.File 6.0.0, Serilog.Extensions.Logging 9.0.0, Microsoft.Extensions.Logging 9.0.0.

## v0.1.5 — 2026-04-24

Factory iter-1 polish release. Nine input + discovery affordances the charter expects but v0.1.x deliberately deferred. All additive — no decoder, persistence, or theme changes. Closes ten ROADMAP items (V15-01 through V15-09 + the context-menu absorbs V15-02's original scope plus three bonus items: Rotate 180° / Flip Horizontal / Flip Vertical / Set as wallpaper / Reload).

### Input affordances

- **Mouse XButton1 / XButton2 → previous / next** (V15-01). `MainWindow.Window_PreviewMouseDown` catches the 5-button-mouse back/forward before any element captures it. TextBox-focus short-circuit prevents hijacking an in-progress rename.
- **Right-click context menu on the viewport** (V15-02). 11 items across open / reveal / reload / rotate (CW / CCW / 180°) / flip (H / V) / set as wallpaper / delete. Attached to the viewport Grid, not descendants, so the rename TextBox keeps its own edit menu. `ViewportContextMenu` + `MenuItem` + `Separator` styles in `DarkTheme.xaml` match Mocha instead of rendering system white.
- **Set as desktop wallpaper** (V15-02 bonus). New `WallpaperService.SetFromFile` copies the current image to `%LOCALAPPDATA%\Images\wallpaper\current.<ext>` before calling `SystemParametersInfo(SPI_SETDESKWALLPAPER, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE)` — a later rename or delete of the source doesn't break the desktop.
- **Flip horizontal / vertical** (V15-08). New `FlipHorizontal` / `FlipVertical` DPs on `ZoomPanImage`; the flip `ScaleTransform` sits BEFORE rotate in the transform stack so flip H flips in image frame (user intuition) rather than post-rotation frame. Pan + zoom state preserved across flip.
- **Rotate 180°** (V15-02 bonus). `Rotate180Command` — the missing neighbor of the CW / CCW pair.
- **Shift + scroll-wheel → horizontal pan** (V15-05). `ZoomPanImage.OnWheel` branches on `ModifierKeys.Shift`; translates X by ±80 px per notch. Plain wheel still zooms, drag still pans vertical.
- **Ctrl+Shift+R reload current image** (V15-04). `ReloadCommand` re-runs the decoder on the same path — useful after external edit in Photoshop / mspaint. Rotation + flip state preserved; nav index unchanged.

### Discovery + polish

- **`?` keyboard cheatsheet overlay** (V15-03). Full-width translucent card groups Navigate / View / File shortcuts including the new XButton and Shift+wheel bindings. Any key dismisses the overlay AND swallows the key so the shortcut doesn't double-fire.
- **F11 fullscreen toggle** (V15-07). `MainWindow.ToggleFullscreen` saves `WindowState` + `WindowStyle`, flips to `None` + `Maximized`, collapses the side panel via the `IsFullscreen` VM flag bound to column-1 `Border.Visibility`. Side panel `ColumnDefinition` switched to `Width="Auto"` so the column collapses with the hidden Border. `Escape` also exits fullscreen (convention).
- **About dialog** (V15-06). New `AboutWindow.xaml` + `AboutWindow.xaml.cs` + `AppInfo` service surface version + `ProductVersion` with commit SHA + .NET runtime description + OS description + decoder list + MIT copyright. GitHub + Crash-log-folder buttons. Dark native caption via existing `WindowChrome.ApplyDarkCaption` for caption consistency with the main window. Info-icon chip (`E946`) in the side-panel header opens it.

### Observability

- **Unified crash log** (V15-09). New `CrashLog` service at `%LOCALAPPDATA%\Images\crash.log` captures all three fatal-exception paths — `AppDomain.UnhandledException`, `Application.DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException` — with version + runtime + OS + full inner-exception chain per entry. Thread-safe `Append` method is reusable for non-fatal diagnostic events too. Replaces the ad-hoc inline `AppendAllText` that used to live in `App.xaml.cs`. Dispatcher dialog now points at the log path so users can attach it when reporting. Precursor to V02-07 (minidump + "Open GitHub Issue" dialog).

### Research artifacts

- `docs/research/iter-1-state-of-repo.md` — Phase 0 recon (scale gate, phase rotation, charter).
- `docs/research/iter-1-sources.md` — Phase 1 landscape scan, 60 sources across 9 classes (OSS competitors, commercial, adjacent, awesome-lists, community signal, standards, academic, dep changelogs, security advisories).
- `docs/research/iter-1-harvest.md` — Phase 2 raw candidates (115 items across 6 buckets — delta from v0.1.3/v0.1.4 ship, competitive gap, infra concretizations, cross-cutting, net-new, research spikes). Auto-extended by the Gemini probe with per-competitor feature breakdowns.
- `docs/research/iter-1-scored.md` — Phase 3 scoring on six dimensions (Fit / Impact / Effort / Risk / Dependencies / Novelty), bucketed into Now / Next / Later / Under-Consideration / Rejected.
- `docs/research/iter-1-audit.md` — Phase 5 self-audit across 7 dimensions (source traceability, tier placement, category coverage, internal consistency, adversarial review, charter alignment, file-on-disk).

## v0.1.4 — 2026-04-24

Distribution release. The portable zip stays the primary artifact; a signed-ready Inno Setup installer ships alongside it so Images can land in Settings → Apps → Installed apps like any other Windows program, with proper uninstall semantics and optional non-destructive "Open with" registration.

### Installer

- **New**: `installer/Images.iss` — Inno Setup 6 script. Installs to `%ProgramFiles%\Images` (admin, default) or `%LOCALAPPDATA%\Programs\Images` (per-user via UAC override); `PrivilegesRequiredOverridesAllowed=dialog commandline` lets the user pick at the elevation prompt. Stable `AppId` GUID so future installers auto-upgrade rather than piling up side-by-side.
- **Prerequisite check** — `InitializeSetup` probes `{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\9.*` (per-machine) + `{localappdata}\Microsoft\dotnet\shared\Microsoft.WindowsDesktop.App\9.*` (per-user) and refuses to proceed without the .NET 9 Desktop Runtime, offering to open the Microsoft download page.
- **Non-destructive file associations** — the "Add to Open with" optional task registers a `Images.File` ProgID + `Applications\Images.exe` entry + `OpenWithProgids` values for 16 extensions (jpg, jpeg, jfif, png, gif, webp, heic, heif, avif, jxl, tif, tiff, bmp, ico, psd). Also writes `Software\RegisteredApplications` + `Software\Images\Capabilities\FileAssociations` so Images surfaces in Settings → Default Apps. Never overrides the user's current default for any extension — that stays their choice. `uninsdeletevalue` cleans each added value without touching siblings on uninstall.
- **Artifacts**: `Images-v0.1.4-setup-win-x64.exe` (LZMA2/ultra64; ~11 MB on v0.1.3 dry run). Start Menu shortcut always; Desktop shortcut optional (unchecked by default); post-install "Launch Images" checkbox.
- **Verified**: local compile against v0.1.3 succeeds in ~5 s, produces a runnable installer that decompresses into a working viewer. No compile warnings.

### Release workflow

- `.github/workflows/release.yml` now builds both artifacts and uploads them to the same GitHub Release. Inno Setup 6 is pre-installed on `windows-latest`; a `choco install innosetup -y` fallback step kicks in if the runner image ever stops bundling it. `iscc /DMyAppVersion=...` passes the release version through to the script.

### Docs

- README install section split into **Installer** and **Portable** paths, with clear guidance on what each gives you and a snippet for building the installer locally.

## v0.1.3 — 2026-04-24

Format-coverage + pixel-hygiene release. Animated GIFs actually animate, toolbar / nav-arrow icons render on enterprise Win11 images that previously showed tofu boxes, and files above 256 MB decode through a memory-mapped view instead of a managed byte[].

### Viewer

- **Animated GIF playback** — `ImageLoader.Load` now probes `.gif` / `.webp` / `.apng` / `.png` via `MagickImageCollection` before falling through to the single-frame WIC path. When a file decodes as a multi-frame sequence, `collection.Coalesce()` resolves each frame's disposal method to a full-canvas BGRA `WriteableBitmap`, and the full list is returned on a new `AnimationSequence` record (frames + per-frame delays + loop count). Fixes [V20-15]. Single-frame GIFs still fast-path through WIC — the animated decoder only pays its cost when there are ≥ 2 frames.
- **Frame-delay clamp** — 0- and sub-20-ms GIF frame delays are promoted to 100 ms the way every shipping browser does, so hostile / malformed GIFs can't pin a CPU core.
- **Loop-count honored** — `AnimationSequence.LoopCount` follows the GIF convention (0 = infinite, any other value = exact iteration count) and feeds `ObjectAnimationUsingKeyFrames.RepeatBehavior` directly, so bounded-loop GIFs stop on the right frame instead of cycling forever.
- **Animated chip** — a compact green `N frames` pill lights up in the bottom toolbar's file-info row whenever `MainViewModel.IsAnimated` is true. Reads at a glance without competing with the primary metadata.
- **V20-06 memory-mapped I/O** — files ≥ 256 MB skip the byte[] round-trip in `ImageLoader.Load` and decode directly from a `MemoryMappedFile` view (`MemoryMappedFileAccess.Read`, `FileShare.ReadWrite | Delete` to preserve the existing rename/delete story). Both the WIC primary and the Magick.NET fallback now take their own `CreateViewStream` per attempt, so a 500 MB RAW or multi-GB PSD no longer lands on the LOH — the OS pages the mapping in on demand. `DecoderUsed` reports `"WIC (memory-mapped)"` / `"Magick.NET (memory-mapped)"` so you can see which path was used.

### UI fix

- **Toolbar + nav-arrow glyphs now render everywhere** — `Themes/DarkTheme.xaml` promotes a shared `IconFontFamily` resource (`"Segoe Fluent Icons, Segoe MDL2 Assets, Symbol"`) and every icon `FontFamily` setter in `DarkTheme.xaml` + `MainWindow.xaml` (10 call sites) resolves through it. On Win11 IoT Enterprise LTSC and a handful of corporate WinPE-derived images, WPF's MDL2-only lookup landed on a text fallback and rendered every icon button as an empty white tofu rectangle; declaring Fluent Icons first + MDL2 second + `Symbol` as a last-ditch fallback collapses all three worlds without touching the glyph codepoints. Same fix applied to the six MDL2 glyph `TextBlock`s (error icon, drop-accept icon, gesture-hint icon, toast icon, extension-lock padlock + unlock pencil).

### Roadmap

- `[x]` **V20-06** — Memory-mapped I/O for files > 256 MB (avoids blowing the managed heap on 500 MP RAW).
- `[x]` **V20-15** — Animated GIF / APNG / animated AVIF playback. Transport controls (play/pause/frame-step/speed) deferred; core playback is live.

## v0.1.2 — 2026-04-24

Security + accessibility + CI hardening plus a three-wave premium-polish pass that elevates the product from functional to intentional.

### UI / UX — premium polish pass (wave 3)

- **Smooth rotate** — `ZoomPanImage.RotationProperty` now animates the `RotateTransform` via an eased (`CubicEase EaseInOut`) `DoubleAnimation` instead of snapping the angle. Duration scales with angular delta (180 ms base + up to 162 ms for a 180-degree flip) so a single rotate-left still feels quick while a 270-degree round trip stays controlled.
- **Extension chip state** — locked vs unlocked now reads at a glance. Unlocked: button border + fill inherit the `YellowBrush` / `WarningPanelBrush` pair used by the warning panel below, so the two surfaces read as a coordinated state. Glyph swaps padlock (`&#xE72E;`) → pencil-edit (`&#xE70F;`) tinted yellow.
- **Window title** — `MainWindow.Title` binds to `MainViewModel.WindowTitle`, which exposes `"{filename} — Images"` when a file is open and falls back to bare `"Images"` otherwise. Standard Windows convention; makes the taskbar label + Alt-Tab card useful.

### UI / UX — premium polish pass (wave 2)

- **Windows 11 dark caption** — new `Services/WindowChrome.cs` calls `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE, 1)` in `Window.SourceInitialized` so the native title bar stops clashing with the Mocha interior. Best-effort P/Invoke — pre-20H1 no-ops cleanly with no visual regression. `DWMWA_SYSTEMBACKDROP_TYPE` (Mica, Win11 22H2+) is documented as a future hook; wiring it wants an alpha-aware window background, deferred to a later pass.
- **Status-dot success pulse** — when `RenameStatus` transitions to `Saved`, the rename-status dot scales 1.0 → 1.5 → 1.0 via a `DataTrigger.EnterActions` storyboard walking the `(UIElement.RenderTransform).(ScaleTransform.ScaleX)` property path. Commit feedback now feels confirmed, not silent.
- **First-run gesture-hint pill** — `MainViewModel` exposes a one-shot `ShowGestureHint` + `_hintTimer` (2.4 s). First image to land in the viewport surfaces an `FloatingPill` reading "Scroll to zoom · drag to pan · double-click to fit", fades in on 280 ms `Motion.Slow`, auto-dismisses. Never shown again in the session.
- **Recent-rename hover affordance** — new `InteractiveCard` style (based on `Card`) animates `BorderBrush.Color` toward `BlueColor` on `IsMouseOver`, with matching exit transition on leave. Recent-rename entries now read as tappable rows rather than static blocks.
- **Decoder badge** — "Decoder" detail row switches from raw text to a compact `Badge` style (Surface0 fill, Hairline border, 4-px radius). WIC / Magick.NET / Unavailable now reads as a label, not a value.
- **Toolbar top highlight** — 1-px inner highlight (`#12CDD6F4` ≈ 7% white-on-Mantle) sits just above the toolbar divider line for a gently lit upper edge — the layered-chrome cue you see in polished macOS / Win11 apps.

### UI / UX — premium polish pass

- **Design token system** — `Themes/DarkTheme.xaml` now exposes explicit radius (`Sm` 6 · `Md` 10 · `Lg` 14 · `Xl` 18), elevation (`Low` / `Medium` / `High` / `Focus` as `DropShadowEffect` resources), and motion tokens (`Motion.Fast` 120 ms · `Motion.Base` 180 ms · `Motion.Slow` 280 ms + shared `CubicEase` easing). Styles now compose from tokens instead of ad-hoc per-element values.
- **Reusable surface styles** — `Card`, `ElevatedCard`, `FloatingPill`, `Toast`, `Divider` styles retire the copy-pasted Border-with-radius blocks scattered through `MainWindow.xaml`. Empty state, decode-error state, drop-confidence panel, rename status card, and recent-rename entries now all inherit a single visual language.
- **Typography scale** — new `Text.Display` / `Text.Title` / `Text.Subtitle` / `Text.Body` / `Text.Caption` / `Text.Hint` styles built on Windows 11's `Segoe UI Variable` (graceful fallback to `Segoe UI` on Win10). `SectionLabel` switches to OpenType small-caps (`Typography.Capitals="AllSmallCaps"`) for a refined tracked look without per-letter hackery.
- **Motion** — `ChromeButton` and `PrimaryButton` hover now cross-fades background color via a 120 ms eased `ColorAnimation` instead of a binary setter flip. `NavArrowButton` hover adds a 1.06× scale cue + border-tint transition + elevation shadow. Toast fades in via Opacity animation from its Style trigger. Nav-arrow viewport fade now uses `CubicEase EaseOut` instead of linear.
- **Floating chrome elevation** — position chip, toast, nav arrows, and the empty / error / drop-overlay cards gain layered `DropShadowEffect` so they read as lifted above the viewport instead of floating flat on the near-black background.
- **Hairline unification** — `HairlineBrush` tuned to `#4045475A` (lower opacity); all 1-px dividers now inherit the new `Divider` style so separators no longer compete with content. Toolbar top border switches from `Surface0Brush` to `HairlineBrush`.
- **Toolbar polish** — outer padding tightens rhythm (`14,9` → `20,12`), button cluster gaps go `6` → `4` px (denser), divider bar gets more vertical breathing room. `ToolbarButton` ships a transparent resting state so icons sit on the bar rather than on a box.
- **Empty-state invitational card** — larger logo (74 → 84 px), tighter copy, new inline hint line ("Tip — arrow keys browse the folder, Enter commits a rename."). Copy rewritten for warmer, shorter cadence.
- **Decode-error semantic surface** — low-opacity `DangerPanelBrush` background replaces full red fill so the panel reads as informative not alarming; icon sizes up to 38 px.
- **Drop-overlay hierarchy** — inner card now uses `ElevatedCard` with 2-px themed border, keeping the accept / reject color signal while cleaning up the doubled-border construction that previously sat inside another border.
- **Toolbar + right panel microcopy** — warning/hint copy tightened ("Changing the extension renames the file — it won't convert the image."), rename helper collapses three sentences into "Renames save on pause. Enter commits now · Esc reverts.", empty-undo copy trims to "Your undo list will appear here."
- **Escape discipline extended** — `Window_KeyDown` Escape now also dismisses an active toast via `MainViewModel.DismissToast()` (A-03 extension).
- **Right panel spacing** — column width 340 → 360, panel padding 18 → 22, header margin 0,0,0,18 → 0,0,0,22 — tighter rhythm without feeling airy.
- **Form polish** — `TextBox` focused state switches from 2-px ring color-on-color ambiguity to crisp 2-px `AccentBrush` border + hover hint on `Surface2Brush`; selection opacity drops to 35% so highlighted text stays readable.
- **ScrollBar retemplate** — compact pill thumb on transparent track replaces the default Aero chrome.
- **Accessibility extras** — position chip gets `AutomationProperties.LiveSetting="Polite"` so folder-position changes are announced; folder label inherits `ToolTip` so ellipsized paths are fully recoverable.

### Security

- **S-02** — Argv-open hardening. `App.xaml.cs` normalizes `argv[0]` through `Path.GetFullPath` + `File.Exists` and rejects device-namespace (`\\?\`, `\\.\`) shapes outright. `MainViewModel.RevealInExplorer` switches from `UseShellExecute=true` + embedded-quote `Arguments` string to `UseShellExecute=false` + `ArgumentList.Add("/select," + Path.GetFullPath(CurrentPath))`, so filenames with commas, quotes, or trailing spaces cannot compose an injection against `CommandLineToArgvW` quoting rules.

### Accessibility

- **A-03** — Keyboard focus + Escape discipline. New shared `FocusVisual` style in `Themes/DarkTheme.xaml` (2 px inset dashed `FocusRingBrush` rect, ~7:1 contrast on the Catppuccin base — WCAG-AA pass) is wired via `FocusVisualStyle` setters on `ChromeButton` / `PrimaryButton` / `NavArrowButton` / `ToolbarButton` / the ambient `TextBox` style. The `RecentRenames` ItemsControl gains `KeyboardNavigation.DirectionalNavigation="Cycle"` + `TabNavigation="Continue"` + `AutomationProperties.Name`. `Window_KeyDown` now handles `Escape` to dismiss an active drop overlay and return focus to the shell.

### CI

- Bumped release workflow actions off the deprecated Node 20 runner: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. GitHub removes Node 20 runners on 2026-06-02 — this clears that deprecation well ahead of the deadline.
- **S-03** — `.github/dependabot.yml` added for the two ecosystems in use (`nuget`, `github-actions`). Weekly sweep on Monday, grouped by package family (`Magick.NET*`, `Microsoft.*`, `actions/*`), commit prefixes `chore(deps)` / `chore(ci)`. Security-advisory PRs bypass the 5-PR throttle per Dependabot defaults.

### Dependencies

- `Magick.NET-Q16-AnyCPU` 14.12.0 → 14.13.0 and `Magick.NET.Core` 14.12.0 → 14.13.0 (minor bump; keeps the bundled native decoder stack current for ImageGlass-advisory CVE hygiene per ROADMAP S-03). Build clean, 0 warnings.

### Branding

- Added the project logo. `src/Images/Resources/logo.png` ships as a WPF `<Resource>` for in-app use; `src/Images/Resources/icon.ico` is a 7-frame multi-resolution Windows app icon (16/24/32/48/64/128/256, Catmull-Rom downscale from a square-padded 431×431 source) wired via `<ApplicationIcon>` in `Images.csproj` — the built `Images.exe` now shows the logo in Explorer, the taskbar, and Alt-Tab. `icon.svg` is a PNG-embedded SVG wrapper for web/README contexts.
- Added the project banner at `assets/banner.png` and embedded it at the top of the README.

### CI

- Bumped release workflow actions off the deprecated Node 20 runner: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. GitHub removes Node 20 runners on 2026-06-02 — this clears that deprecation well ahead of the deadline.

### Docs

- `ROADMAP.md` refreshed to v2 (2026-04-24). Seeded v1 covered viewer/editor/organizer/converter/AI/plugins; v2 adds nine cross-cutting tracks — security (S-01–S-10 incl. WIC CVE-2025-50165, libwebp CVE-2023-4863, SharpCompress zip-slip, ExifTool safe invocation, MSIX AppContainer, Wasmtime-hosted decoder spike), privacy (P-01–P-07 incl. strip-location, default-off telemetry, network-egress log panel, C2PA read/verify via `c2patool`), accessibility (A-01–A-06 incl. custom `ImageAutomationPeer`, high-contrast `SystemColors` theme, Magnifier-aware UIA events, published UIA tree), i18n/l10n (I-01–I-05 incl. Crowdin for OSS, RTL audit, `DateTimeOffset` switch), observability (O-01–O-05 incl. Serilog, opt-in Sentry, ETW decode counters, local minidump), testing (T-01–T-05 incl. `Images.Domain` pure lib + FlaUI smoke + golden-image diff), distribution (D-01–D-07 incl. winget via `WinGet Releaser`, Scoop `extras`, Microsoft Store MSIX, Azure Trusted Signing), catalog-schema strategy (SCH-01–SCH-05 — XMP sidecars authoritative, forward-only hop-don't-jump EF Core migrations), and migration-from-competing-tools (M-01–M-06 — Picasa `.picasa.ini`, Lightroom `.lrcat`, digiKam, XnView, Apple Photos, IrfanView). Refreshes AI track with Windows ML dual-path (saves ~150 MB installer on Win11 24H2+ by skipping private ORT), cjpegli export (F-03, ~35% smaller JPEG), LaMa generative erase (U-03), Copilot+ Restyle (U-04); drops HEVC-bundling (Nokia enforcement). Adds Appendix A with 220+ deduplicated source URLs so every item is traceable. Companion research filed under `docs/gap-research-report-1.md` + `docs/gap-research-report-2.md`.
- README architecture tree now shows `Resources/icon.ico`, `icon.svg`, `logo.png` instead of the "not yet added" placeholder.

## v0.1.1 — 2026-04-24

### Changed

- Folder watcher (`FileSystemWatcher`) now actually runs — external add/delete/rename from Explorer or another app refreshes the list without pressing F5. The position chip updates live, and if the currently displayed file vanishes, the viewer advances to the next slot the navigator lands on.
- `BoolToVis` converter is now declared in `Themes/DarkTheme.xaml` (single source of truth, available to any view) instead of being redeclared per-window.
- `ImageLoader.Load` narrows its WIC catch to decode/format exceptions — `OutOfMemoryException` and thread aborts now propagate instead of silently falling through to Magick.NET. The WIC-path `MemoryStream` is disposed deterministically.
- `DirectoryNavigator.Open` short-circuits when called with a path inside the already-watched folder — no more full rescans on repeat drops from the same directory.
- `DirectoryNavigator.Rescan` catches transient IO / ACL / disconnection errors and keeps the prior list instead of throwing to the UI thread.

### Docs

- README zoom row clarifies wheel-zoom anchors on the cursor; removed the stray `Ctrl+wheel` alias claim that the code never honored.
- README architecture tree no longer claims a shipped `Resources/icon.ico` (icon is a v0.1.2 follow-up).

## v0.1.0 — 2026-04-24

Initial release.

- WPF / .NET 9 image viewer with WIC + Magick.NET decode pipeline (~100 formats incl. BMP/JPG/PNG/GIF/TIFF/WEBP/HEIC/ICO/JXL/AVIF/PSD/TGA/RAW).
- Windows 7 Photo Viewer–inspired chrome in Catppuccin Mocha dark theme.
- Hover-reveal left/right navigation arrows; Left/Right/Home/End/Space/Backspace keyboard navigation with wrap-around.
- Natural-sort directory scan on open; auto-refresh when files are added/removed.
- Split stem/extension rename editor with 600 ms debounced auto-save, live conflict preview, commit-on-navigation.
- Recent Renames panel with one-click undo for the last 10 renames.
- Zoom (wheel / Ctrl+wheel), pan (drag), fit-to-window, 1:1, rotate, delete-to-recycle-bin.
- Command-line: `Images.exe "C:\path\to\image.jpg"` opens file and populates directory.
