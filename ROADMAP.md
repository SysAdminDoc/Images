# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Only the two blocked credential items remain. Promote to `1.0.0` when unblocked.

## Audit-Surfaced Items

## Research-Driven Additions

### P2

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
  Touches: `src/Images/Localization/`, `scripts/Test-LocalizationResources.ps1`, `tests/Images.Tests/`
  Acceptance: Local validation can generate or validate a pseudo-locale, run resource parity, and smoke key windows with expanded text without clipping critical controls.
  Complexity: M

- [ ] P2 — Promote background jobs into a primary activity surface
  Why: Long-running indexing, batch, contact-sheet, scan, and model work should be visible without opening About.
  Evidence: `src/Images/Services/BackgroundJobsService.cs`, `src/Images/Services/BackgroundTaskTracker.cs`, ACDSee activity-management patterns
  Touches: `src/Images/MainWindow.xaml`, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/Services/BackgroundJobsService.cs`, `tests/Images.Tests/`
  Acceptance: The main viewer exposes a compact activity entry with running/faulted counts, recent job details, cancellation where supported, and screen-reader status updates.
  Complexity: M

- [ ] P2 — Produce a local release SBOM and provenance bundle
  Why: Local release output now has checksums, diagnostics, and package-manifest validation, but still lacks a generated SBOM/provenance bundle.
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

### P1

- [ ] P1 - Add Magick.NET codec security-policy gate
  Why: Images sets Magick.NET resource limits, but untrusted image/document inputs also need an explicit policy audit so supported formats, delegates, and failure modes match the codec policy.
  Evidence: `src/Images/App.xaml.cs`, `src/Images/Services/ImageLoader.cs`, `docs/codec-support-policy.md`, ImageMagick security policy
  Touches: `src/Images/App.xaml.cs`, `src/Images/Services/ImageLoader.cs`, `src/Images/Services/CodecCapabilityService.cs`, `docs/codec-support-policy.md`, `tests/Images.Tests/`
  Acceptance: Startup diagnostics and tests prove the active Magick.NET limits/policy reject or safely route high-risk delegate/document paths, huge dimensions, and disallowed write targets without silently widening supported codec behavior.
  Complexity: M

### P2

- [ ] P2 - Add Explorer-parity navigation modes
  Why: Images has persisted sort modes, but Windows viewer users expect direct-open navigation to optionally follow Explorer order and to auto-switch sibling folders at folder boundaries.
  Evidence: `src/Images/Services/DirectoryNavigator.cs`, `src/Images/Services/DirectorySortMode.cs`, ImageGlass 10 Explorer sort order, ImageGlass 9.5 sibling directory auto-switch
  Touches: `src/Images/Services/DirectoryNavigator.cs`, `src/Images/Services/DirectorySortMode.cs`, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/SettingsWindow.xaml`, `tests/Images.Tests/`
  Acceptance: Settings exposes Explorer order and sibling-folder auto-switch toggles; direct-open folder navigation preserves current behavior by default, follows Explorer-compatible ordering when enabled, and moves to the next/previous sibling folder only when explicitly enabled.
  Complexity: M

- [ ] P2 - Add optional local face-region review workflow
  Why: Images has `person:` tag namespaces and planned Picasa face-region migration, but modern photo managers treat face grouping as core organization; this should land as an explicit local review lane, not an automatic write.
  Evidence: `README.md`, `docs/migration-guide.md`, PhotoPrism ONNX SCRFD face detector, digiKam face recognition, Immich facial recognition, Excire Search 2026 people culling
  Touches: `src/Images/Services/ModelManagerService.cs`, `src/Images/Services/TagGraphService.cs`, `src/Images/Services/ReviewLabelService.cs`, `src/Images/MainWindow.xaml`, `tests/Images.Tests/`
  Acceptance: With an approved local face model installed, Images can detect candidate face regions for a selected folder, group visually similar faces, require user confirmation before assigning names, and write reviewed regions/names to XMP sidecars with a delete-derived-data control.
  Complexity: L

### P3

- [ ] P3 - Build native-vector fidelity fixture set
  Why: SVG/SVGZ previews currently rely on existing decode paths, while competing viewers are adding native vector renderers; Images needs fixture evidence before changing renderer architecture.
  Evidence: `src/Images/Services/ImageLoader.cs`, `src/Images/Controls/ZoomPanImage.cs`, ImageGlass 10 native vector rendering
  Touches: `tests/Images.Tests/Fixtures/`, `tests/Images.Tests/ImageLoaderTests.cs`, `src/Images/Services/ImageLoader.cs`, `docs/codec-support-policy.md`
  Acceptance: Fixture tests cover SVG/SVGZ scaling, transparency, embedded raster images, text fallback, and animated-SMIL unsupported/degraded states; the result documents whether Magick/WPF is sufficient or a renderer decision must move to blocked work.
  Complexity: M

- [ ] P3 - Scout signed Windows preview/thumbnail handler integration
  Why: `--peek` covers external preview workflows, but Explorer Preview Pane and thumbnails require shell-extension trust, install, rollback, and signing evidence before implementation.
  Evidence: `docs/peek-mode.md`, `installer/Images.iss`, PowerToys Peek, Microsoft preview-handler guidance, ImageGlass shell thumbnail settings
  Touches: `docs/peek-mode.md`, `installer/Images.iss`, `src/Images/Services/ShellIntegration.cs`, `scripts/Test-ReleaseDiagnostics.ps1`
  Acceptance: A decision spike documents COM/MSIX/preview-handler options, signing requirements, uninstall rollback, crash isolation, and a minimal non-registered prototype or fixture; no installer registration ships until code signing is unblocked.
  Complexity: M
