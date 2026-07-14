# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, GUI/visual validation, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when those are unblocked.

## Actionable Work

- [ ] P2 — Gallery grid is not virtualized
  Why: the gallery `ListBox` uses a `WrapPanel` with `VirtualizingPanel.IsVirtualizing="False"`, so opening it on a large folder realizes a full Button+ContextMenu visual tree for every file at once and queues N thumbnail decodes, freezing the UI on folders of several hundred+ images. The filmstrip and side preview are virtualized; the gallery is the outlier. Needs a virtualizing wrap panel (WPF has no built-in one) — implementation work, not a one-line flag.
  Where: `src/Images/MainWindow.xaml` (gallery ListBox ItemsPanel, ~line 1422-1433)

- [ ] P2 — Per-navigation synchronous file I/O on the UI thread
  Why: `CompleteCurrentLoad` runs on the dispatcher and does, per next/prev: up to 2 `.xmp` sidecar `File.Exists` probes (+ synchronous `XDocument.Load`/JSON deserialize when present) via `GetEnabledDisplayEditOperations`, a `new FileInfo(path).Length`, and two `ImageLoader.QuickDimensions` file opens in `EnqueueNeighbours`. Sub-frame on local SSD, but adds tens of ms of input lag per arrow-press on UNC/Dropbox/network paths. Move the sidecar and neighbour-dimension probes off the dispatcher.
  Where: `src/Images/ViewModels/MainViewModel.cs` (CompleteCurrentLoad ~4508/4582/4617), `PreloadService.cs`, `NonDestructiveEditService.cs`

- [ ] P2 — RAW navigation runs the whole per-nav UI-thread tail twice
  Why: `LoadCurrentAsync` calls `CompleteCurrentLoad` once for the embedded RAW preview and again for the full decode, re-running the sidecar probe, folder-preview refresh, and metadata/color/C2PA reads (started then cancelled+restarted) on every RAW navigation. Generation-guarded so no wrong data, but wasted work felt when browsing CR2/NEF/ARW folders.
  Where: `src/Images/ViewModels/MainViewModel.cs` (LoadCurrentAsync ~4370-4386)

- [ ] P2 — MainWindow/AboutWindow visible copy bypasses the localization system
  Why: ~92 `MenuItem Header=`, ~36 `ToolTip=`, the empty state, rail labels, cheatsheet, and load-error button labels are hardcoded English, plus AboutWindow labels. The localization gate only flags hardcoded `AutomationProperties.Name`, so `Text=`/`Header=`/`ToolTip=` drifted through — a non-English build shows a half-translated shell and screen readers read a localized name over an English label. Extend `Test-LocalizationResources.ps1` to flag these too, then route them through resx.
  Where: `src/Images/MainWindow.xaml`, `src/Images/AboutWindow.xaml`, `scripts/Test-LocalizationResources.ps1`

- [ ] P3 — Directory sort re-stats each file O(n log n) times
  Why: for date/size sort modes, `DirectoryNavigator.CompareByMode` calls `File.GetLastWriteTimeUtc`/`GetCreationTimeUtc`/`FileInfo.Length` inside the comparator, so each file is stat'd ~log n times instead of once. A 5000-file folder sorted by Size issues ~60k stats; on a network share the sort can stall for seconds. Pre-stat once into tuples, then sort.
  Where: `src/Images/Services/DirectoryNavigator.cs` (CompareByMode ~474-525)

- [ ] P3 — Raw exception text still injected into ~25 secondary toasts
  Why: many `MainViewModel` catch blocks build toasts as `"<action> failed: " + ex.Message`, surfacing decoder/HRESULT jargon. The main decode-error card was cleaned up; the secondary toasts (rotation, wallpaper, export, strip-metadata, etc.) should use `FirstLine(ex.Message)` or a calm localized message consistently.
  Where: `src/Images/ViewModels/MainViewModel.cs` (various catch blocks)

- [ ] P3 — Incremental rescan re-hashes files whose sidecar check transiently fails
  Why: `CatalogService.IsUnchanged` treats any exception (e.g. a sidecar momentarily locked by cloud-sync/AV) as "changed", forcing a full SHA-256 re-hash of that file on every rescan under contention. Safe (never misses a real change) but defeats the incremental optimization. Consider distinguishing transient IO errors from genuine change signals.
  Where: `src/Images/Services/CatalogService.cs` (IsUnchanged / ReadSidecarFileSummary)

The remaining research item is decision-gated (Ghostscript-source AGPL mechanism) and lives in `Roadmap_Blocked.md`. Refill via a research pass (`RESEARCH.md`) when ready.
