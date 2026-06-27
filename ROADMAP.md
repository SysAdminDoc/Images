# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Audit-Surfaced Items

- [ ] P2 — **TileService concurrent pyramid build race**
  Why: Two threads can build the same tile pyramid simultaneously — both see no `pyramid.json`, both create tiles concurrently, potentially producing corrupt tile files from overlapping writes.
  Where: `src/Images/Services/TileService.cs` (BuildPyramid, lines 88-171)

- [ ] P2 — **ExifToolService pipe-buffer deadlock on large output**
  Why: stdout/stderr are read async but WaitForExit blocks synchronously. If ExifTool fills the OS pipe buffer (4-64KB), the process blocks on write while WaitForExit blocks on exit. Mitigated by timeout+kill but causes unnecessary 30s delays on large metadata sets.
  Where: `src/Images/Services/ExifToolService.cs` (RunProcess, lines 118-129)

- [ ] P3 — **DispatcherUnhandledException continues after fatal errors**
  Why: `args.Handled = true` prevents WPF from terminating after any unhandled exception, including state-corrupting ones. The crash dialog shows but the app continues in an undefined state.
  Where: `src/Images/App.xaml.cs` (DispatcherUnhandledException handler, line 67)

## Research-Driven Additions

### P1

- [ ] P1 — Replace silent trust-path catches with logged diagnostics
  Why: C2PA, contact sheet, ExifTool, listen-mode, and performance-report failures can currently disappear, which weakens supportability and trust.
  Evidence: `src/Images/Services/C2paToolRuntime.cs`, `src/Images/ViewModels/C2paInspectionController.cs`, `src/Images/Services/ContactSheetService.cs`, `src/Images/Services/ExifToolService.cs`, `src/Images/Services/ListenService.cs`, C2PA 2.2 specification
  Touches: `src/Images/Services/Log.cs`, `src/Images/Services/DiagnosticsStatusService.cs`, affected services/controllers, `tests/Images.Tests/`
  Acceptance: No non-cleanup `catch { }` remains in those paths; failures log warning-level context, diagnostics show degraded status, and tests assert at least one representative logged/degraded path.
  Complexity: M

- [ ] P1 — Add model-backed semantic search diagnostics and quality gates
  Why: CLIP search now has approved model imports and provider code, but provider creation/preprocessing can silently fall back to deterministic metadata embeddings.
  Evidence: `src/Images/Services/ClipEmbeddingProvider.cs`, `src/Images/Services/SemanticSearchService.cs`, `src/Images/Services/ModelManagerService.cs`, Excire Foto, Immich
  Touches: `src/Images/Services/ClipEmbeddingProvider.cs`, `src/Images/Services/SemanticSearchService.cs`, `src/Images/SemanticSearchWindow.xaml(.cs)`, `tests/Images.Tests/`
  Acceptance: Semantic search reports the exact active provider and fallback reason, logs model/preprocess failures, includes a small query-quality fixture, and keeps deterministic fallback explicit in UI.
  Complexity: M

- [ ] P1 — Automate UIA coverage for secondary windows
  Why: Accessibility docs cover many tool windows, but the smoke gate mostly verifies the main viewer and toolbar.
  Evidence: `docs/accessibility.md`, `tests/Images.Tests/WpfSmokeTests.cs`, Microsoft WPF accessibility guidance
  Touches: `tests/Images.Tests/WpfSmokeTests.cs`, `src/Images/*Window.xaml`, `docs/accessibility.md`
  Acceptance: Smoke tests open at least Settings, About, Duplicate Cleanup, Semantic Search, Model Manager, and Import Inbox windows and assert automation names, keyboard reachability, and no empty critical HelpText.
  Complexity: M

- [ ] P1 — Restore local release verification parity after workflow removal
  Why: Hosted GitHub workflows and Dependabot were removed, but release/trust docs still promise workflow, SBOM, attestation, and vulnerability gates; local readiness only covers part of that path.
  Evidence: commit `55fabf2`, missing `.github`, `scripts/Test-ReleaseReadiness.ps1`, `docs/distribution-trust.md`, `docs/release-support-policy.md`, WinGet and Scoop manifest docs
  Touches: `scripts/Test-ReleaseReadiness.ps1`, `scripts/Test-LocalizationResources.ps1`, `scripts/Test-ReleaseDiagnostics.ps1`, `scripts/New-PackageManifests.ps1`, `docs/release-checklist.md`, `docs/distribution-trust.md`, `docs/release-support-policy.md`
  Acceptance: One local release command runs version sync, restore, build, tests, vulnerability scan, localization parity, release diagnostics, and package-manifest/checksum validation; docs no longer claim `.github` or Dependabot gates exist.
  Complexity: M

- [ ] P1 — Add viewport context-menu smoke and keyboard regression coverage
  Why: The viewer right-click menu was recently nested and made scrollable after becoming too long; without UIA coverage it can regress off-screen or lose keyboard access.
  Evidence: `src/Images/MainWindow.xaml`, `src/Images/Themes/DarkTheme.xaml`, `tests/Images.Tests/WpfSmokeTests.cs`, FlaUI
  Touches: `tests/Images.Tests/WpfSmokeTests.cs`, `src/Images/MainWindow.xaml`, `src/Images/Themes/DarkTheme.xaml`
  Acceptance: A smoke test opens the viewport context menu at a constrained window size, verifies grouped root items and at least one nested submenu, confirms the menu stays within the viewport or scrolls, and reaches commands by keyboard.
  Complexity: M

### P2

- [ ] P2 — Add local assisted culling score lane
  Why: Images has review labels and duplicate cleanup, but no first-pass "best shot" assistance for large photo sets.
  Evidence: Adobe Lightroom Classic assisted culling, Excire Foto, `src/Images/Services/DuplicateCleanupService.cs`, `src/Images/ViewModels/MainViewModel.cs`
  Touches: `src/Images/Services/`, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/MainWindow.xaml`, `tests/Images.Tests/`
  Acceptance: Culling mode can rank a folder by local-only signals such as sharpness, exposure warnings, similarity, and existing ratings; every score shows its reason and can be applied as keep/reject without network access.
  Complexity: L

- [ ] P2 — Add C2PA export provenance handoff
  Why: Images can inspect Content Credentials, but edited/exported files do not have a clear provenance-write or "not written" outcome.
  Evidence: `src/Images/ViewModels/C2paInspectionController.cs`, `src/Images/Services/C2paManifestService.cs`, `src/Images/Services/ImageExportService.cs`, C2PA 2.2 specification
  Touches: `src/Images/Services/ImageExportService.cs`, `src/Images/Services/C2paManifestService.cs`, `src/Images/ExportPreviewWindow.xaml(.cs)`, `tests/Images.Tests/`
  Acceptance: Export preview states whether C2PA will be preserved, written through an approved configured runtime, or omitted; exported test files verify the expected manifest/no-manifest outcome.
  Complexity: L

- [ ] P2 — Add Picasa metadata migration importer
  Why: The migration guide says Picasa requires dedicated `.picasa.ini` and `contacts.xml` parsing, but only standard XMP import exists today.
  Evidence: `docs/migration-guide.md`, `src/Images/Services/XmpSidecarImportService.cs`, digiKam migration work
  Touches: `src/Images/Services/PicasaImportService.cs`, `src/Images/ImportInboxWindow.xaml(.cs)`, `docs/migration-guide.md`, `tests/Images.Tests/`
  Acceptance: Fixture `.picasa.ini` and `contacts.xml` files convert star ratings, albums, face rectangles, and contact names into XMP sidecars without modifying originals.
  Complexity: M

- [ ] P2 — Add pseudo-locale and overflow layout gate
  Why: Localization infrastructure exists, but there are no locale files and no automated check that expanded strings fit premium WPF surfaces.
  Evidence: `src/Images/Localization/Strings.resx`, `scripts/Test-LocalizationResources.ps1`, `docs/accessibility.md`
  Touches: `src/Images/Localization/`, `scripts/Test-LocalizationResources.ps1`, `.github/workflows/ci.yml`, `tests/Images.Tests/`
  Acceptance: CI can generate or validate a pseudo-locale, run resource parity, and smoke key windows with expanded text without clipping critical controls.
  Complexity: M

- [ ] P2 — Promote background jobs into a primary activity surface
  Why: Long-running indexing, batch, contact-sheet, scan, and model work should be visible without opening About.
  Evidence: `src/Images/Services/BackgroundJobsService.cs`, `src/Images/Services/BackgroundTaskTracker.cs`, ACDSee activity-management patterns
  Touches: `src/Images/MainWindow.xaml`, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/Services/BackgroundJobsService.cs`, `tests/Images.Tests/`
  Acceptance: The main viewer exposes a compact activity entry with running/faulted counts, recent job details, cancellation where supported, and screen-reader status updates.
  Complexity: M

- [ ] P2 — Produce a local release SBOM and provenance bundle
  Why: Distribution docs promise CycloneDX SBOMs and build/artifact provenance from hosted workflows, but current releases are local-build-only.
  Evidence: `docs/distribution-trust.md`, `docs/codec-bundling.md`, `scripts/Test-ReleaseDiagnostics.ps1`, CycloneDX ML-BOM, C2PA provenance expectations
  Touches: `scripts/Test-ReleaseReadiness.ps1`, `scripts/Test-ReleaseDiagnostics.ps1`, `scripts/New-PackageManifests.ps1`, `src/Images/Services/CodecCapabilityService.cs`, `docs/distribution-trust.md`
  Acceptance: Local release output includes SHA-256 checksums, a CycloneDX SBOM covering NuGet dependencies plus staged native runtimes/model definitions, and a provenance summary that matches `--system-info`/`--codec-report` diagnostics.
  Complexity: M

### P3

- [ ] P3 — Resolve the Windows ML versus ONNX Runtime backend mismatch
  Why: Model manager copy promises "Windows ML first", but runtime code only probes ONNX Runtime DirectML/CPU.
  Evidence: `src/Images/Services/ModelManagerService.cs`, `src/Images/Services/OnnxRuntimeService.cs`, `src/Images/Images.csproj`, Microsoft Windows ML documentation
  Touches: `src/Images/Services/OnnxRuntimeService.cs`, `src/Images/Services/ModelManagerService.cs`, `src/Images/Images.csproj`, `docs/integration-policy.md`
  Acceptance: A code-backed decision either implements a Windows ML probe behind the existing service seam or updates product/runtime copy to ONNX-first; diagnostics and tests agree with the chosen backend contract.
  Complexity: M

- [ ] P3 — Build an HDR and color-management decision fixture set
  Why: Competitors are moving toward HDR/color-managed preview, while Images currently reports color profile risk more than it transforms display output.
  Evidence: `src/Images/Services/ImageColorAnalysisService.cs`, `src/Images/Services/ImageLoader.cs`, ImageGlass 10 roadmap, HN image-viewer color-management discussion
  Touches: `tests/Images.Tests/Fixtures/`, `src/Images/Services/ImageLoader.cs`, `src/Images/ViewModels/ColorAnalysisController.cs`, `docs/codec-support-policy.md`
  Acceptance: A fixture-backed test/diagnostic corpus covers ICC, wide-gamut, HDR-like, AVIF/JXL, and TIFF samples; the output documents whether WPF/Magick transforms are sufficient or a renderer/runtime decision must move to `Roadmap_Blocked.md`.
  Complexity: M

- [ ] P3 — Scout Lightroom collection migration from exported metadata
  Why: The migration guide marks Lightroom catalog import as planned, but direct `.lrcat` parsing risks brittle schema coupling.
  Evidence: `docs/migration-guide.md`, `src/Images/Services/XmpSidecarImportService.cs`, Lightroom Classic metadata export documentation
  Touches: `src/Images/Services/XmpSidecarImportService.cs`, `docs/migration-guide.md`, `tests/Images.Tests/`
  Acceptance: A small exported Lightroom fixture proves whether ratings, keywords, hierarchical subjects, labels, and collection membership can be imported from XMP or sidecar-adjacent data without reading private catalog tables.
  Complexity: S
