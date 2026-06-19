# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## Research-Driven Additions

### P1 - Trust, Runtime, And Core Workflow Coverage

- [ ] P1 — Build an operation-chain batch workflow
  Why: Competitors expose repeatable batch conversion/editing flows, and Images already has the individual operations but not a composed, previewable pipeline.
  Evidence: `src/Images/Services/BatchProcessorService.cs`; existing crop/rotate/resize/metadata/export services; FastStone batch conversion; PicView batch resize/compression; Czkawka dry-run cleanup patterns.
  Touches: `src/Images/Services/BatchProcessorService.cs`, `src/Images/ViewModels/MainViewModel.cs`, batch/export dialogs, undo/history services, tests.
  Acceptance: Users can order two or more operations, preview affected files and output names, run a dry count before writes, cancel in progress, and receive success/error totals without partial silent failure.
  Complexity: L

### P2 - Premium Workflow Polish

- [ ] P2 — Add synchronized zoom/pan and difference inspection to export preview
  Why: The export workbench already shows original and encoded panes, but premium lossy-export review needs linked inspection before blocked SSIMULACRA2/Butteraugli metrics.
  Evidence: `src/Images/ExportPreviewWindow.xaml`, `src/Images/Services/ExportPreviewService.cs`; FastStone conversion preview; Squoosh interaction pattern; blocked metric-library items in `Roadmap_Blocked.md`.
  Touches: export preview XAML/viewmodels, `src/Images/Services/ExportPreviewService.cs`, encoder option controls, tests.
  Acceptance: Export preview links zoom/pan between original and encoded panes, adds a toggleable difference or blink inspection mode, preserves current output dimensions/size/format warnings, and keeps clear apply/cancel actions.
  Complexity: M

- [ ] P2 — Centralize command registry and shortcut rebinding
  Why: The app already has many keyboard-first workflows and a `hotkeys` table, but command labels and gestures are duplicated across XAML, the command palette, README, and settings summary.
  Evidence: `src/Images/MainWindow.xaml`, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/ViewModels/SettingsViewModel.cs`, `src/Images/Services/SettingsService.cs`; WPF commanding docs; PicView/ImageGlass shortcut customization patterns.
  Touches: command registry, key binding setup, command palette entries, settings hotkey UI, README/help text, tests.
  Acceptance: One command registry drives menu labels, shortcuts, command-palette rows, help text, and README/exported shortcut data; users can rebind supported shortcuts with conflict detection and reset-to-default.
  Complexity: L

- [ ] P2 — Add large-image and long-task visual stability checks
  Why: Premium viewers must remain visually stable while decoding large files or loading previews; competitors and community threads repeatedly value small, fast, predictable viewers.
  Evidence: WPF imaging performance docs; community complaints about slow default viewers; Czkawka large-preview feedback issues; existing cache/decode services.
  Touches: image decode/cache services, loading overlays, progress/status surfaces, WPF smoke tests, large fixture generation.
  Acceptance: Large image open/export/search tasks show bounded progress or skeleton states, never leave stale preview content after cancellation, and pass automated smoke checks for no UI freeze over a representative fixture set.
  Complexity: M

