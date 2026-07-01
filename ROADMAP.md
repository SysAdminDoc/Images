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
