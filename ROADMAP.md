# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## Research-Driven Additions

### P0 - Restore Build And Release Confidence

- [ ] P0 — Restore a valid ONNX runtime package path
  Why: Current main cannot restore because `Microsoft.ML.OnnxRuntime.DirectML` is pinned to an unavailable `1.26.0` package.
  Evidence: `src/Images/Images.csproj`; local `dotnet list Images.sln package --outdated --include-transitive` restore failure; NuGet DirectML package page; ONNX Runtime 1.25/1.26 release notes.
  Touches: `src/Images/Images.csproj`, `src/Images/Services/ClipEmbeddingProvider.cs`, `src/Images/Services/BackgroundRemovalService.cs`, `src/Images/Services/LaMaInpaintService.cs`, `src/Images/Services/SuperResolutionService.cs`, `docs/inpaint-runtime-decision.md`.
  Acceptance: `dotnet restore Images.sln`, `dotnet build Images.sln`, `dotnet test Images.sln`, `dotnet list Images.sln package --vulnerable --include-transitive`, and `dotnet list Images.sln package --outdated --include-transitive` all complete without package-resolution failure.
  Complexity: M

- [ ] P0 — Add package-resolution validation to release readiness
  Why: Dependency and roadmap docs claimed a runtime upgrade that NuGet cannot resolve, so release readiness needs a mechanical package-availability gate.
  Evidence: `scripts/Test-ReleaseReadiness.ps1`; `.github/workflows/ci.yml`; stale `RESEARCH.md`; restore failure from `Microsoft.ML.OnnxRuntime.DirectML 1.26.0`.
  Touches: `scripts/Test-ReleaseReadiness.ps1`, `.github/workflows/ci.yml`, `README.md`, `CHANGELOG.md`.
  Acceptance: Release-readiness checks fail with a clear message when any `PackageReference` version is unavailable; README/.NET/package badges and changelog dependency claims are verified after package restore succeeds.
  Complexity: S

### P1 - Trust, Runtime, And Core Workflow Coverage

- [ ] P1 — Centralize AI runtime provider selection and status reporting
  Why: Local AI is a trust feature, but provider selection is scattered and the UI/services cannot currently report the actual CPU/GPU/NPU provider path reliably.
  Evidence: `ClipEmbeddingProvider`, `BackgroundRemovalService`, `LaMaInpaintService`, `SuperResolutionService`; Windows ML execution-provider docs; ONNX Runtime DirectML docs.
  Touches: `src/Images/Services/*Embedding*`, `src/Images/Services/*Inpaint*`, `src/Images/Services/*SuperResolution*`, `src/Images/Services/CodecCapabilityService.cs`, `src/Images/Services/ModelManagerService.cs`, related tests.
  Acceptance: One runtime service owns provider selection, fallback, and display labels; feature surfaces report Windows ML, DirectML, CPU, or unavailable state truthfully; local AI failures degrade without breaking image viewing.
  Complexity: M

- [ ] P1 — Add WPF smoke automation for primary viewer flows
  Why: Service tests cannot catch broken shell bindings, focus loss, keyboard navigation regressions, theme clipping, or settings/export dialog failures.
  Evidence: `src/Images/MainWindow.xaml`, `src/Images/ViewModels/MainViewModel.cs`, `.github/workflows/ci.yml`, Czkawka preview-loading feedback issues, repeated repo emphasis on browser/device-level QA for UI work.
  Touches: `tests/Images.Tests`, `.github/workflows/ci.yml`, test fixtures under existing test project structure.
  Acceptance: A Windows CI/local smoke run launches the app, opens a fixture image, navigates next/previous, opens settings, opens export preview, exercises rename validation, and exits cleanly with screenshots or logs on failure.
  Complexity: M

- [ ] P1 — Harden listen-mode local control and network log retention
  Why: Loopback listen mode is useful for local tools, but unauthenticated local TCP control and unbounded persisted network logs weaken the app's privacy/trust story.
  Evidence: `src/Images/Services/ListenService.cs`, `src/Images/Services/NetworkEgressService.cs`, `README.md`; Microsoft named-pipe security docs; Chrome Local Network Access guidance.
  Touches: listen mode startup, privacy/settings UI, About network activity panel, network-log storage, tests.
  Acceptance: Listen mode requires either a per-session token or a per-user named-pipe/ACL boundary; input line length and connection rate are bounded; inbound local-control events are labeled separately from outbound egress; clearing network history can delete the persisted JSONL file; persisted logs rotate or retain only the newest bounded entries.
  Complexity: M

- [ ] P1 — Wire XMP migration import into a real guided flow
  Why: The migration guide says XMP sidecars are picked up automatically, but `XmpSidecarImportService` is currently only referenced by tests/docs and not by a production migration workflow.
  Evidence: `docs/migration-guide.md`, `src/Images/Services/XmpSidecarImportService.cs`, `tests/Images.Tests/XmpSidecarImportServiceTests.cs`, `src/Images/TagGraphWindow.xaml.cs`, osxphotos export guidance.
  Touches: migration/import UI, `XmpSidecarImportService`, catalog/gallery/tag services, side-panel metadata display, tests.
  Acceptance: A user can scan a folder for XMP sidecars, preview imported ratings/labels/keywords/location fields, apply them to Images' sidecar/catalog surfaces without touching native databases, and get per-file success/error counts.
  Complexity: M

- [ ] P1 — Add explicit destructive-writeback confirmation and backup policy
  Why: Crop and rotation writebacks overwrite originals, while Recovery Center cannot restore those writes automatically; the app's own design principle says originals should be protected by default.
  Evidence: `README.md` destructive crop copy, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/Services/ImageExportService.cs`, `src/Images/Services/RecoveryCenterService.cs`, `docs/design-product-differentiators.md`.
  Touches: crop/rotation/GPS/metadata writeback flows, confirmation dialogs, settings, Recovery Center copy, tests.
  Acceptance: First destructive overwrite shows the exact source path and operation impact, offers Save a copy when practical, explains Recovery Center limits before the write, and optionally creates a same-folder or app-local backup according to a persisted setting.
  Complexity: M

- [ ] P1 — Refresh dependency floors after restore is green
  Why: Security and reliability package checks are currently blocked by the invalid ONNX package, and minor drift remains hidden until restore works.
  Evidence: `src/Images/Images.csproj`; `tests/Images.Tests/Images.Tests.csproj`; NuGet pages for `Microsoft.Data.Sqlite`, `Serilog.Sinks.File`, ONNX Runtime, Magick.NET, and test SDK packages.
  Touches: `src/Images/Images.csproj`, `tests/Images.Tests/Images.Tests.csproj`, `Directory.Build.props`, `CHANGELOG.md`.
  Acceptance: Outdated/vulnerable package reports are reviewed; upgrades are applied or explicitly documented with a repo-specific reason; tests and release-readiness checks pass.
  Complexity: S

- [ ] P1 — Build an operation-chain batch workflow
  Why: Competitors expose repeatable batch conversion/editing flows, and Images already has the individual operations but not a composed, previewable pipeline.
  Evidence: `src/Images/Services/BatchProcessorService.cs`; existing crop/rotate/resize/metadata/export services; FastStone batch conversion; PicView batch resize/compression; Czkawka dry-run cleanup patterns.
  Touches: `src/Images/Services/BatchProcessorService.cs`, `src/Images/ViewModels/MainViewModel.cs`, batch/export dialogs, undo/history services, tests.
  Acceptance: Users can order two or more operations, preview affected files and output names, run a dry count before writes, cancel in progress, and receive success/error totals without partial silent failure.
  Complexity: L

- [ ] P1 — Add tile-pyramid cache quota, health, and clear controls
  Why: Deep-zoom viewing can create large tile pyramids under app data, but `TileService` lacks the cap, diagnostics, and clear behavior already implemented for thumbnails.
  Evidence: `src/Images/Services/TileService.cs`, `src/Images/Services/ThumbnailCache.cs`, `src/Images/Services/DiagnosticsStatusService.cs`.
  Touches: `TileService`, diagnostics/settings storage surfaces, cache cleanup commands, large-image tests.
  Acceptance: Tile cache has a bounded size/age policy, LRU or stale-source eviction, health reporting, and a clear action; diagnostics show tile-cache size and failures alongside thumbnail cache health.
  Complexity: M

### P2 - Premium Workflow Polish

- [ ] P2 — Add synchronized zoom/pan and difference inspection to export preview
  Why: The export workbench already shows original and encoded panes, but premium lossy-export review needs linked inspection before blocked SSIMULACRA2/Butteraugli metrics.
  Evidence: `src/Images/ExportPreviewWindow.xaml`, `src/Images/Services/ExportPreviewService.cs`; FastStone conversion preview; Squoosh interaction pattern; blocked metric-library items in `Roadmap_Blocked.md`.
  Touches: export preview XAML/viewmodels, `src/Images/Services/ExportPreviewService.cs`, encoder option controls, tests.
  Acceptance: Export preview links zoom/pan between original and encoded panes, adds a toggleable difference or blink inspection mode, preserves current output dimensions/size/format warnings, and keeps clear apply/cancel actions.
  Complexity: M

- [ ] P2 — Add Catppuccin Latte and system theme follow mode
  Why: The app has a strong dark visual language, but premium desktop apps need accessible light/system options for glare, projector, office, and high-contrast-adjacent use.
  Evidence: `src/Images/Services/ThemeService.cs`, `src/Images/Themes/DarkTheme.xaml`, `src/Images/Themes/HighContrastTheme.xaml`, WPF .NET 10 Fluent/high-contrast improvements.
  Touches: theme resource dictionaries, settings UI, `ThemeService`, visual smoke snapshots.
  Acceptance: Users can select Dark, Latte Light, High Contrast, or Follow System; all primary controls meet contrast and focus-visibility requirements in each mode.
  Complexity: M

- [ ] P2 — Add localization completeness and RTL smoke checks
  Why: The app has a real localization seam, but premium i18n quality requires missing-key prevention and visual confidence before adding more locales.
  Evidence: `src/Images/Localization/Strings.resx`, `src/Images/Localization/LocExtension.cs`, `src/Images/ViewModels/SettingsViewModel.cs`, `docs/gap-research-report-1.md`.
  Touches: localization resources, settings language controls, main/settings/export smoke tests, CI validation scripts.
  Acceptance: CI fails on missing resource keys or obvious hard-coded UI strings; pseudo-locale and RTL smoke checks cover the main viewer, settings, export preview, and archive page-turn controls.
  Complexity: M

- [ ] P2 — Centralize command registry and shortcut rebinding
  Why: The app already has many keyboard-first workflows and a `hotkeys` table, but command labels and gestures are duplicated across XAML, the command palette, README, and settings summary.
  Evidence: `src/Images/MainWindow.xaml`, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/ViewModels/SettingsViewModel.cs`, `src/Images/Services/SettingsService.cs`; WPF commanding docs; PicView/ImageGlass shortcut customization patterns.
  Touches: command registry, key binding setup, command palette entries, settings hotkey UI, README/help text, tests.
  Acceptance: One command registry drives menu labels, shortcuts, command-palette rows, help text, and README/exported shortcut data; users can rebind supported shortcuts with conflict detection and reset-to-default.
  Complexity: L

- [ ] P2 — Improve folder and archive history navigation
  Why: Viewer/book competitors make backtracking and folder context cheap, while Images still risks forcing users to reopen or remember recent locations manually.
  Evidence: `ArchiveBookService`; README archive/book claims; NeeView history/book workflows; existing recent-folder surfaces in the UI concept and docs.
  Touches: navigation/history services, archive book viewmodel state, recent folders UI, keyboard commands, tests.
  Acceptance: Folder and archive sessions expose back/forward history, current location context, recent-book reopening, and keyboard-accessible navigation without losing the selected image.
  Complexity: M

- [ ] P2 — Add large-image and long-task visual stability checks
  Why: Premium viewers must remain visually stable while decoding large files or loading previews; competitors and community threads repeatedly value small, fast, predictable viewers.
  Evidence: WPF imaging performance docs; community complaints about slow default viewers; Czkawka large-preview feedback issues; existing cache/decode services.
  Touches: image decode/cache services, loading overlays, progress/status surfaces, WPF smoke tests, large fixture generation.
  Acceptance: Large image open/export/search tasks show bounded progress or skeleton states, never leave stale preview content after cancellation, and pass automated smoke checks for no UI freeze over a representative fixture set.
  Complexity: M

### P3 - Strategic Later Bets

- [ ] P3 — Prototype a read-only catalog query boundary before automation integrations
  Why: Automation/MCP/plugin ideas need a privacy-safe, stable, read-only catalog API before any external control surface is added.
  Evidence: Existing SQLite/catalog/search services; MCP item previously marked under consideration; ImageGlass plugin boundary; local-first privacy positioning.
  Touches: catalog/search services, CLI or internal service boundary, privacy documentation, tests.
  Acceptance: A local-only read API can list indexed folders, query metadata/search results, and redact private paths when requested; no write operations or network exposure are introduced.
  Complexity: L
