# Images - Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

- [ ] Every release generates checksum-pinned WinGet and Scoop manifests through `New-PackageManifests.ps1` and passes the package-manifest hash gate. Account-based submission is optional distribution follow-up, not a version gate.

- [ ] **V130-02** P2 — Bring `ObjectCli`/`OrientationCli` to full V120 CLI parity.
  Why: These two of six ML CLIs are still single-image with no Ctrl+C cancellation, no multi-path batch, and no distinct model-load exit code — even though `DetectMany`/`SuggestMany` already support batches; an incomplete rollout of the shipped pattern.
  Evidence: `ObjectCli.cs:21` / `OrientationCli.cs:19` (`args.Length != 2`); `SceneCli`/`AestheticCli`/`SafetyCli` show the target multi-path + `CliCancellation.OnCtrlC()` + exit-3 pattern; `ModelLoadFailed` exists only on Aesthetic/Scene/Safety status enums.
  Touches: `Services/ObjectCli.cs`, `Services/OrientationCli.cs`, `Services/ObjectDetectionService.cs`, `Services/OrientationSuggestionService.cs`, `Services/FaceDetectionService.cs` (add `ModelLoadFailed` status), plus `*Cli` tests.
  Acceptance: `--object-detect`/`--orientation-suggest` accept multiple paths, honor Ctrl+C (exit 130), and return exit 3 when an imported model can't load vs 2 (missing) vs 1 (image failure); tests assert all three codes for each.
  Complexity: M

- [ ] **V130-04** P3 — Bound `FaceClusterService.Cluster`'s O(n²) all-pairs loop and sort key.
  Why: The all-pairs cosine loop runs over every accepted face across the whole folder batch (images capped at 100, faces-per-image uncapped), so a crowd-photo folder is quadratic — the worst case the V120 face-NMS cap guarded one layer down but did not reach; the `Array.IndexOf` sort key adds O(n²) keying.
  Evidence: `FaceClusterService.cs:33-44` (all-pairs loop), `:49` (`Array.IndexOf(accepted, item.Face)` inside `OrderBy`).
  Touches: `Services/FaceClusterService.cs`.
  Acceptance: total faces considered are capped to a documented bound (with a logged notice when exceeded) and the sort key is O(1) via a precomputed index map; existing cluster results unchanged below the cap; a stress test with many faces stays within a bounded time budget.
  Complexity: S

- [ ] **V130-03** P3 — Persist and surface the semantic-search candidate ceiling.
  Why: `DefaultMaxSearchCandidates` shipped as a bare static that nothing sets and the GUI never passes through, so every in-app search uses the hardcoded 50k and the "Configurable at startup" comment is false; completes the unmet V120-05 "settings surface".
  Evidence: `SemanticSearchService.cs:63` (static, read only at `:224`), `SemanticSearchWindow.xaml.cs:290` (`Search(...)` without `maxCandidates`); no `SettingsService` key.
  Touches: `Services/SettingsService.cs` (+ schema key), `ViewModels/SettingsViewModel.cs`, `SettingsWindow.xaml`, `SemanticSearchWindow.xaml.cs` (pass the setting), `Services/SemanticSearchService.cs` (assign the static from settings at startup; fix comment).
  Acceptance: a persisted, user-editable ceiling drives every in-app semantic search; default preserves current behavior; a settings round-trip test asserts persistence and that the value reaches `Search`.
  Complexity: M

- [ ] **V130-05** P3 — Add service-level tests for the new ML batch surface.
  Why: No test asserts any `*Many` overload reuses one `InferenceSession`, and `ObjectDetection`/`Orientation` `*Many` have no empty-list/model-missing/cancellation coverage; the shipped batch logic is only exercised by CLI-injected fakes.
  Evidence: audit found no session-count spy and no `DetectMany`/`SuggestMany` service tests; `OnnxRuntimeService.CreateSession` is a static seam.
  Touches: `tests/Images.Tests/` (Object/Orientation batch tests; introduce an injectable session-factory seam on `OnnxRuntimeService` to assert single-session reuse across a batch).
  Acceptance: tests assert `DetectMany`/`SuggestMany`/`AnalyzeMany` open exactly one session per model per batch, return per-path failures when the model is missing, return `[]` for empty input, and throw on a pre-cancelled token.
  Complexity: M

- [ ] **V130-06** P3 — Enable ReadyToRun for cold-start; pin the viewer GC mode.
  Why: The release publish ships JIT-only (community-reported ~2–3 s self-contained WPF cold start is largely JIT); `PublishReadyToRun` is the one unclaimed first-paint lever (WPF isn't Native-AOT-compatible). Gallery virtualization and thumbnail decode-downscaling are already implemented, so no work there.
  Evidence: `Images.csproj`/`Directory.Build.props` have no `PublishReadyToRun`/`ServerGarbageCollection`; gallery already uses `VirtualizationMode=Recycling` (`MainWindow.xaml:507-517`), `ImageLoader.cs:992-994` already sets `DecodePixelWidth/Height`.
  Touches: `src/Images/Images.csproj` (`<PublishReadyToRun>true</PublishReadyToRun>` for the win-x64 release publish; keep Workstation-concurrent GC explicit), `README.md` publish command, `docs/release-checklist.md`.
  Acceptance: the release publish produces R2R images; `dotnet publish` succeeds and the app launches; runtimeconfig confirms Workstation+concurrent GC; a documented cold-start Stopwatch (App ctor → main-window `ContentRendered`) is captured as a baseline.
  Complexity: S

- [ ] **V130-07** P3 — WPF accessibility pass: live-region announcements + explicit tab order.
  Why: Status/toast/search-count text has no UIA live-region announcement (WCAG 2.2 SC 4.1.3), and there is zero explicit `KeyboardNavigation.TabIndex` anywhere, so tab order is implicit declaration order across the 408 KB `MainWindow.xaml` and 70 KB `SettingsWindow.xaml`.
  Evidence: audit found no `TabIndex`/`KeyboardNavigation.TabIndex` in any window; WPF live regions require `AutomationProperties.LiveSetting` **plus** `peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)` after text mutation (MS Learn AutomationLiveSetting).
  Touches: toast/status/search-count controls + their code-behind (`LiveSetting` + `RaiseAutomationEvent`), `MainWindow.xaml`/`SettingsWindow.xaml` (explicit `TabIndex` on logical groups; `TabNavigation="Once"` on the thumbnail grid with arrow-key traversal).
  Acceptance: status/toast/search-count changes fire a UIA `LiveRegionChanged` event (assertable via an automation-peer test); logical tab order is set explicitly; reading-order correctness and focus-visibility verified in a GUI session.
  Complexity: M

- [ ] **V130-08** P3 — Gain-map WRITE (ISO 21496-1 / UltraHDR authoring) via a shell-out CLI.
  Why: `imazen/ultrahdr` (Apache-2.0, Windows x86_64/aarch64 CI) now reads **and writes** ISO 21496-1 gain-map metadata, giving Images a Windows-bindable path from gain-map *inspection* to *authoring* — export an UltraHDR JPEG that renders as HDR in Chrome/Photos/phones; no free Windows viewer offers this. (Gain-map *display* stays renderer-blocked.)
  Evidence: https://github.com/imazen/ultrahdr (v0.5.0, 2026-04-26), https://docs.rs/ultrahdr; existing bundled-binary provenance pattern (jpegtran/Ghostscript, `docs/integration-policy.md`).
  Touches: new shell-out tool staged like the ONNX-CLI pattern (SHA-pinned Rust-built `ultrahdr` binary + license/provenance per `docs/codec-support-policy.md`), export-workbench/`ImageExportService` handoff, `docs/integration-policy.md`.
  Acceptance: exporting an HDR/high-bit-depth source produces a valid ISO 21496-1 UltraHDR JPEG (base + gain map) that a gain-map-aware target renders as HDR; the binary is SHA-256-pinned with license/provenance; the feature no-ops with a clear message when the runtime is absent.
  Complexity: L
