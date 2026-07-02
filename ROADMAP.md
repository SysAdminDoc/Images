# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Audit-Surfaced Items

- [ ] P1 — Convert StaticResource brush references to DynamicResource for runtime theme switching
  Why: MainWindow uses ~280 StaticResource brush references that do not update when the user changes themes at runtime via Settings. Theme changes only take full effect in newly-created secondary windows.
  Where: `src/Images/MainWindow.xaml` (primary), all secondary window XAML files

- [ ] P2 — Add AsyncRelayCommand to safely observe fire-and-forget async commands
  Why: 25+ RelayCommand instances use `async () => await X()` lambdas that compile to async void. Uncaught exceptions in the inner methods would crash the process instead of reporting gracefully.
  Where: `src/Images/ViewModels/MainViewModel.cs` command declarations, `src/Images/ViewModels/RelayCommand.cs`

- [ ] P2 — Cancel orphaned preload tasks on eviction
  Why: PreloadService evicts cache entries when the ring buffer is full, but in-flight decode tasks for evicted entries continue to completion, wasting CPU and memory for images that will never be displayed.
  Where: `src/Images/Services/PreloadService.cs`

- [ ] P3 — Replace Stack-based history trimming with bounded collection
  Why: DirectoryNavigator.PushBack/PushForward do O(n) stack-to-array-to-stack rebuilds on every push past the 50-item cap, creating unnecessary GC pressure during rapid navigation.
  Where: `src/Images/Services/DirectoryNavigator.cs`

- [ ] P3 — Fix GetCurrentMonitorWorkArea fallback coordinate space mismatch
  Why: The two fallback paths (hwnd == IntPtr.Zero and GetMonitorInfo failure) return SystemParameters.WorkArea in logical units, but the method's contract promises physical pixels. Callers that apply physical-to-logical conversion would double-convert. Extremely unlikely to hit in practice.
  Where: `src/Images/Services/MonitorService.cs` GetCurrentMonitorWorkArea

## Research-Driven Additions

