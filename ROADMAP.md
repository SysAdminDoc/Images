# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Audit-Surfaced Items

- [ ] P1 — **PreloadService cache poisoning on cancellation**
  Why: A faulted `Lazy<Task>` permanently poisons the cache slot. Cancelled preloads leave entries that always re-throw on subsequent access.
  Where: `src/Images/Services/PreloadService.cs` (Enqueue, TryGetInFlight)

- [ ] P1 — **ExifToolService executable path validation**
  Why: `TryNormalizeExecutable` accepts any path to an existing file. If the configured path were attacker-controlled, a malicious executable could be launched.
  Where: `src/Images/Services/ExifToolService.cs`

- [ ] P2 — **RecoveryCenterService restore path re-validation**
  Why: Deserialized paths from the JSONL log are used in `File.Move` without re-validation. A tampered log could move files to attacker-controlled locations.
  Where: `src/Images/Services/RecoveryCenterService.cs` (Restore method)

- [ ] P2 — **ListenService per-connection idle timeout**
  Why: `ReceiveTimeout` only works for synchronous reads; async `ReadAsync` can hold a connection open indefinitely with the session token.
  Where: `src/Images/Services/ListenService.cs` (HandleClient)

- [ ] P2 — **Slideshow shuffle index bounds check**
  Why: If the file list changes between reading `_nav.Files.Count` and indexing `_nav.Files[nextIndex]`, an out-of-bounds access is possible.
  Where: `src/Images/ViewModels/MainViewModel.cs` (SlideshowTimer_Tick)

- [ ] P2 — **Theme-aware caption re-application on runtime theme switch**
  Why: Window captions are applied once at SourceInitialized. Switching themes at runtime doesn't update already-open window captions.
  Where: `src/Images/Services/WindowChrome.cs`, `src/Images/Services/ThemeService.cs`

- [ ] P3 — **FolderPreviewController SemaphoreSlim not disposed**
  Why: `_thumbnailDecodeGate` SemaphoreSlim is never disposed in `Dispose()`.
  Where: `src/Images/ViewModels/FolderPreviewController.cs`

