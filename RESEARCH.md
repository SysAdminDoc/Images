# Research — Images

## Executive Summary
Verified: Images is a Windows-only, local-first WPF/.NET 10 image viewer and organizer with broad codec coverage, archive viewing, metadata/C2PA inspection, catalog/search, recovery, diagnostics, optional local models, and privacy-preserving defaults. Its strongest current shape is a trustworthy power viewer for local Windows collections, not a cloud library or generic minimal viewer. Highest-value direction: close reliability/trust defects, restore release verification parity after hosted workflows were removed, protect the recently simplified viewport context menu with regression coverage, then extend AI/provenance/migration only where the local-first model stays explicit. Top opportunities, in order: fix the four audit risks already in `ROADMAP.md`; restore a single local release-readiness gate; add viewport context-menu smoke coverage; replace silent trust-path catches with diagnostics; make semantic search/provider fallback visible and testable; generate a local SBOM/provenance bundle; promote background work into a primary activity surface; add C2PA export handoff and migration importers after trust basics are stable.

## Product Map
- Core workflows: open and navigate local images, folders, and archive books; inspect metadata, color risk, codec provenance, C2PA, and runtime status; rename, cull, rate, tag, catalog, and search; export, batch process, compare, edit non-destructively, and recover destructive actions.
- User personas: Windows power users replacing Photos/ImageGlass/FastStone; photographers and archivists managing mixed local folders; technical users who value portable builds, diagnostics, checksums, and no-surprise network behavior.
- Platforms and distribution: Verified Windows x64 desktop, WPF, `net10.0-windows10.0.22621.0`, MIT license, Inno/portable packaging scripts, WinGet/Scoop manifest generation. Verified `.github` is absent after commit `55fabf2`, so current verification/distribution must be local unless hosted automation is intentionally restored later.
- Key integrations and data flows: WIC first with Magick.NET fallback and startup `ResourceLimits`; SharpCompress archive reads without extraction; SQLite catalog/cache/search databases; optional Ghostscript/jpegtran/c2patool runtimes; optional WinRT OCR; optional ONNX Runtime DirectML/CPU local models; opt-in GitHub release checks logged by `NetworkEgressService`.

## Competitive Landscape
- ImageGlass, PicView, qView, NeeView: modern/minimal Windows and cross-platform viewer references with strong keyboard, archive, codec, and context-menu expectations. Learn from their fast surfaces and restrained command grouping; avoid adding a plugin SDK before Images has stable extension/security boundaries.
- FastStone and XnView MP: mature viewer-manager utilities with table-stakes batch conversion, compare, metadata, and dense keyboard workflows. Learn from their breadth and migration affordances; avoid their dated, crowded information architecture.
- digiKam: strongest OSS DAM reference for metadata, face recognition, duplicate detection, import/export, and migration depth. Learn from durable databases and serious migration tooling; avoid GPL code reuse and full DAM/server scope creep.
- Lightroom Classic and Excire Foto: commercial benchmark for assisted culling, AI search, duplicate cleanup, XMP handoff, and confidence-building triage UX. Learn from local ranking/explanation of best shots; avoid cloud coupling, subscription assumptions, and opaque automatic decisions.
- Immich and PhotoPrism: adjacent self-hosted libraries with strong background indexing, AI search, maps, and multi-device import. Learn from job observability and rebuildable indexes; avoid multi-user server architecture because Images is a single-user desktop app.
- Mylio, Eagle, and PowerToys Peek: useful adjacent references for local-first trust copy, asset-library polish, and zero-friction preview workflows. Learn from confidence-building status and low-friction entry points; avoid sync/account or file-manager shell scope that contradicts Images' focused viewer model.

## Security, Privacy, and Reliability
- Verified: `ROADMAP.md` already tracks unresolved correctness risks in `src/Images/Services/ImportInboxService.cs`, `src/Images/Services/TileService.cs`, `src/Images/Services/ExifToolService.cs`, and `src/Images/App.xaml.cs`; these remain the highest root-cause reliability work.
- Verified: non-cleanup silent catches remain in trust/diagnostic paths: `src/Images/Services/C2paToolRuntime.cs`, `src/Images/ViewModels/C2paInspectionController.cs`, `src/Images/Services/ContactSheetService.cs`, `src/Images/Services/ExifToolService.cs`, `src/Images/Services/ListenService.cs`, and `src/Images/Services/PerformanceBudgetService.cs`. These should log warning-level context and surface degraded status.
- Verified: dependency hardening improved: Magick.NET is current at 14.14.0, SharpCompress is current at 0.49.1, `src/Images/App.xaml.cs` sets Magick.NET resource ceilings, and `docs/archive-runtime-review.md` records the non-extracting SharpCompress pattern. Verified risk: docs still claim Dependabot/security workflow coverage even though `.github` is absent.
- Verified: release-trust docs are stale after workflow removal. `docs/distribution-trust.md`, `docs/release-checklist.md`, `docs/release-support-policy.md`, `docs/codec-bundling.md`, and `docs/integration-policy.md` still reference GitHub Actions, Dependabot, workflow artifacts, SBOMs, or attestations. `scripts/Test-ReleaseReadiness.ps1` is a useful local gate, but it does not yet run the full build/test/localization/diagnostics/package-manifest/SBOM path.
- Verified: privacy posture is a product advantage: update checks are opt-in, network activity is visible in About, model imports require explicit local files and SHA-256 verification, and semantic search uses app-local storage. Preserve this by rejecting automatic model downloads and cloud analysis defaults.
- Likely: C2PA inspection without export/provenance writeback will feel incomplete as Content Credentials adoption grows; export flows should either preserve/write credentials when configured or explicitly state when no credential was written.

## Architecture Assessment
- Verified: `src/Images/ViewModels/MainViewModel.cs` and `src/Images/MainWindow.xaml` remain coordination hotspots. New work should keep moving behavior into services/controllers and avoid increasing viewer-window orchestration.
- Verified: the viewport context menu has been nested and given scrollable styling in `src/Images/MainWindow.xaml` and `src/Images/Themes/DarkTheme.xaml`, directly addressing the long/off-screen menu problem. Verified gap: `tests/Images.Tests/WpfSmokeTests.cs` has no regression that opens the viewport menu at a constrained viewport, checks root grouping, submenu access, scrollability, or keyboard reachability.
- Verified: `BackgroundTaskTracker` and `BackgroundJobsService` exist, but job visibility is mostly secondary. Long-running indexing, batch, contact sheet, scan, and model work need a compact primary activity surface before adding more async workflows.
- Verified: semantic search has a real `ClipEmbeddingProvider`, approved model registry, tokenizer/preprocessor loaders, and deterministic fallback. Missing quality gates are diagnostic: provider creation/preprocessing failures can degrade to fallback without enough user-facing evidence.
- Verified: `ModelManagerService` copy says "Windows ML first", but `Images.csproj` references ONNX Runtime DirectML and `OnnxRuntimeService` probes DirectML/CPU. Resolve the backend contract before expanding local AI features.
- Verified: accessibility documentation is broad, but automated UIA coverage is concentrated on the main viewer. Secondary windows and the viewport context menu need automated names/help text/keyboard coverage.
- Verified: localization infrastructure exists (`Strings.resx`, `Strings.cs`, `scripts/Test-LocalizationResources.ps1`) but no pseudo-locale/expanded-text smoke gate exists. The existing roadmap item still references `.github/workflows/ci.yml`; implementation should target local scripts unless hosted CI returns.
- Verified: `src/Images/Services/CodecCapabilityService.cs` still tells users to update when ".NET 9 servicing releases" ship while the project targets .NET 10; runtime copy and release docs should be synchronized with the current target.

## Rejected Ideas
- Reintroduce GitHub Actions or Dependabot as the default plan: sourced from stale docs and prior commits, rejected because commit `55fabf2` explicitly removed workflows for local builds; roadmap should restore local parity first.
- Cross-platform or mobile rewrite: sourced from qView/Immich/Mylio comparisons, rejected because the project is intentionally Windows/WPF and release engineering is Windows-specific.
- Multi-user server, web gallery, or cloud sync: sourced from Immich/PhotoPrism/Mylio, rejected because it would invert the single-user local-first desktop model and introduce account/network trust work.
- Automatic model downloads: sourced from Excire/Immich/AI DAM comparisons, rejected because Images' model manager intentionally requires explicit local import and SHA-256 verification.
- Public plugin SDK now: sourced from ImageGlass roadmap, rejected because extension loading creates security, versioning, signing, and support obligations before internal seams are stable.
- Direct Apple Photos SQLite import: sourced from `docs/migration-guide.md` and osxphotos ecosystem research, rejected because Apple Photos schemas are private and change across macOS versions; exported XMP is the maintainable path.
- Full renderer rewrite for HDR/wide-gamut as immediate work: sourced from ImageGlass HDR direction and Images color warnings, rejected because unresolved renderer/runtime decisions belong in `Roadmap_Blocked.md`; fixture-backed decision work is enough for now.
- C2PA signing by default without user identity/signing material: sourced from C2PA/Content Credentials research, rejected because unverifiable signing UX would weaken trust; export should be explicit about preserved, unsigned, signed, or omitted provenance.

## Sources
OSS and adjacent projects:
- https://github.com/d2phap/ImageGlass/releases
- https://imageglass.org/news/imageglass-roadmap-update-2026-98
- https://github.com/Ruben2776/PicView
- https://github.com/jurplel/qView
- https://github.com/neelabo/NeeView
- https://www.digikam.org/about/features/
- https://www.xnview.com/en/xnviewmp/
- https://www.faststone.org/FSViewerDetail.htm
- https://github.com/immich-app/immich
- https://www.photoprism.app/

Commercial and adjacent products:
- https://helpx.adobe.com/lightroom-classic/help/assisted-culling.html
- https://helpx.adobe.com/lightroom-classic/help/create-xmp-acr-files.html
- https://excire.com/en/excire-foto/
- https://mylio.com/
- https://www.acdsee.com/en/products/photo-studio-ultimate/
- https://learn.microsoft.com/en-us/windows/powertoys/peek
- https://www.eagle.cool/

Standards, platform, testing, and distribution:
- https://c2pa.org/specifications/specifications/2.2/index.html
- https://cyclonedx.org/capabilities/mlbom/
- https://imagemagick.org/script/security-policy.php
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/basic-accessibility-information
- https://learn.microsoft.com/en-us/windows/ai/windows-ml/
- https://learn.microsoft.com/en-us/windows/package-manager/package/manifest
- https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests
- https://github.com/FlaUI/FlaUI

Dependencies and advisories:
- https://github.com/dlemstra/Magick.NET/releases
- https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML/1.24.4
- https://github.com/advisories/GHSA-6c8g-7p36-r338
- https://sqlite.org/changes.html

## Open Questions
- Needs live validation: what publisher identity and signing channel will be available for release artifacts?
- Needs product decision: should C2PA export write unsigned/action manifests by default, require user-provided signing material, or remain inspect-only?
- Needs implementation decision: should local AI copy stay ONNX Runtime DirectML-first, or should a real Windows ML backend be added?
