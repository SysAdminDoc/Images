# Research — Images

## Executive Summary
Verified: Images is a local-first Windows WPF/.NET 10 image viewer and organizer with unusually broad format, metadata, edit, recovery, diagnostics, package, accessibility, and local-model foundations. Its strongest shape is not "minimal viewer" parity anymore; it is a privacy-preserving power viewer for Windows users who need fast browsing plus trustworthy local workflows. Highest-value direction: finish the remaining reliability/trust defects, make local AI/search genuinely diagnosable, and deepen migration/provenance/accessibility without turning the app into a server product. Top opportunities: fix the current audit items in `ROADMAP.md`; replace silent trust-path catches with logged diagnostics; harden model-backed semantic search and runtime reporting; add local assisted culling; support C2PA export/provenance handoff; add Picasa/Lightroom migration paths; promote long-running work into a visible activity surface; add secondary-window UIA and pseudo-locale gates; keep release trust work blocked until signing/package credentials exist.

## Product Map
- Core workflows: open/navigate local files and archive books; inspect metadata/color/C2PA/runtime provenance; rename/cull/tag/rate/catalog/search; export, batch process, edit non-destructively, recover destructive actions.
- User personas: Windows power users replacing Photos/ImageGlass/FastStone; photographers and archivists managing mixed local folders; developers/IT users who value portable builds, diagnostics, checksums, and network-quiet defaults.
- Platforms and distribution: Windows x64 desktop, `net10.0-windows10.0.22621.0`, WPF, GitHub Release ZIP/Inno installer, planned WinGet/Scoop publication pending first submissions and credentials.
- Key integrations and data flows: WIC first with Magick.NET fallback; SharpCompress archive reads without `WriteToDirectory`; SQLite catalog/cache/search databases; optional Ghostscript/jpegtran/c2patool; optional WinRT OCR; optional ONNX Runtime DirectML/CPU local models; opt-in GitHub update checks logged by `NetworkEgressService`.

## Competitive Landscape
- ImageGlass: strong modern Windows viewer polish, broad codec positioning, motion photo/HDR/plugin SDK direction. Learn from its visible runtime capability surface and extension ambition; avoid adding a plugin SDK before Images has stable extension/security boundaries.
- FastStone and XnView MP: mature viewer-manager utilities with batch conversion, compare, metadata, and dense keyboard workflows. Learn from their table-stakes batch and migration affordances; avoid their dated, crowded information architecture.
- digiKam: strongest OSS DAM signal for metadata, face recognition, duplicate detection, and migration depth. Learn from separate databases for catalog/thumbnails/recognition/similarity and serious migration tooling; avoid GPL code reuse and full DAM/server scope creep.
- Lightroom Classic and Excire Foto: commercial benchmark for assisted culling, AI search, duplicate cleanup, and confidence-building triage UX. Learn from local ranking/explanation of best shots; avoid cloud coupling, subscription assumptions, and opaque automatic decisions.
- Immich and PhotoPrism: adjacent self-hosted photo libraries with strong background indexing, AI search, maps, and multi-device import. Learn from durable indexing/job observability; avoid multi-user server architecture because Images is a single-user desktop viewer.
- qView, qimgv, NeeView: minimal/fast viewer and archive-reading references. Learn from launch speed, keyboard-first browsing, and touch/archive ergonomics; avoid under-instrumented failure states.
- Mylio Photos: useful local-first privacy and multi-device organization benchmark. Learn from trust copy and offline-first positioning; avoid sync/account features that contradict Images' no-surprise-network posture.

## Security, Privacy, and Reliability
- Verified: `ROADMAP.md` already identifies four unresolved correctness risks: GPS strip success reporting in `src/Images/Services/ImportInboxService.cs`, concurrent tile pyramid builds in `src/Images/Services/TileService.cs`, ExifTool pipe-buffer delay/deadlock risk in `src/Images/Services/ExifToolService.cs`, and fatal WPF exception continuation in `src/Images/App.xaml.cs`.
- Verified: non-cleanup silent catches remain in trust/diagnostic paths: `src/Images/Services/C2paToolRuntime.cs`, `src/Images/ViewModels/C2paInspectionController.cs`, `src/Images/Services/ContactSheetService.cs`, `src/Images/Services/ExifToolService.cs`, `src/Images/Services/ListenService.cs`, and `src/Images/Services/PerformanceBudgetService.cs`. These should log warnings and surface degraded status instead of disappearing.
- Verified: dependency posture is strong: CI/release workflows run vulnerability checks, Magick.NET is at 14.14.0, SharpCompress is at 0.49.1, and `docs/archive-runtime-review.md` confirms Images avoids SharpCompress `WriteToDirectory()`.
- Verified: privacy posture is a product advantage: optional update checks are off by default, network activity is visible in About, local model imports require hash verification, and semantic search uses app-local `semantic-index.db`.
- Likely: C2PA inspection without export/provenance writeback will feel incomplete as Content Credentials adoption rises; export flows should either write/verify credentials when configured or state clearly that no credential was written.

## Architecture Assessment
- Verified: `src/Images/ViewModels/MainViewModel.cs` is still the primary coordination hotspot; avoid broad refactors, but route new feature work through existing service/window seams rather than adding more viewer orchestration.
- Verified: `BackgroundTaskTracker` and `BackgroundJobsService` exist, but job visibility is mostly in About; long-running indexing, batch, contact sheet, scan, and model work need a primary-window activity surface.
- Verified: semantic search has a real `ClipEmbeddingProvider`, approved model registry, tokenizer/preprocessor loaders, and deterministic fallback. Missing quality gates are diagnostic: `TryCreate` and image preprocessing can fail silently, and search quality/regression fixtures are not yet productized.
- Verified: `ModelManagerService` says "Windows ML first", but `Images.csproj` references ONNX Runtime DirectML and `OnnxRuntimeService` only probes DirectML/CPU. This mismatch needs a backend decision spike before expanding local AI features.
- Verified: accessibility documentation is broad, but `tests/Images.Tests/WpfSmokeTests.cs` mainly gates the main viewer. Secondary windows documented in `docs/accessibility.md` need automated UIA snapshots for names/help text/keyboard reachability.
- Verified: localization infrastructure exists (`Strings.resx`, `Strings.cs`, `scripts/Test-LocalizationResources.ps1`), but there are no locale or pseudo-locale resource files. Overflow and hard-coded-string gates should be expanded before translating.
- Verified: release trust is well scoped in `docs/distribution-trust.md`; package-manager installation smoke tests are worthwhile only after accepted WinGet/Scoop manifests exist.

## Rejected Ideas
- Cross-platform or mobile rewrite: sourced from qView/Immich/Mylio comparisons, rejected because the project is explicitly Windows/WPF and release engineering is Windows-specific.
- Multi-user server, web gallery, or cloud sync: sourced from Immich/PhotoPrism/Mylio, rejected because it would invert the single-user local-first desktop model and introduce account/network trust work.
- Automatic model downloads: sourced from Excire/Immich/AI DAM comparisons, rejected because Images' model manager intentionally requires explicit local import and SHA-256 verification.
- Public plugin SDK now: sourced from ImageGlass roadmap, rejected for this cycle because extension loading creates security, versioning, signing, and support-surface obligations before internal seams are stable enough.
- Direct Apple Photos SQLite import: sourced from `docs/migration-guide.md` and osxphotos ecosystem research, rejected because Apple Photos schema changes across macOS versions; exporting XMP through osxphotos is the maintainable path.
- Full renderer rewrite for HDR/wide-gamut as immediate work: sourced from ImageGlass HDR direction and Images color warnings, rejected for now because unresolved renderer/runtime decisions belong in `Roadmap_Blocked.md`; a fixture-backed decision is still useful.
- Chocolatey-first distribution: sourced from Windows package research, rejected because current docs correctly prioritize GitHub Releases, WinGet, and Scoop.

## Sources
OSS and adjacent projects:
- https://github.com/d2phap/ImageGlass
- https://imageglass.org/news/imageglass-roadmap-update-2026-98
- https://github.com/nomacs/nomacs
- https://github.com/neelabo/NeeView
- https://github.com/jurplel/qView
- https://github.com/easymodo/qimgv
- https://www.digikam.org/news/2026-06-07-9.1.0_release_announcement/
- https://github.com/immich-app/immich
- https://www.photoprism.app/
- https://github.com/awesome-selfhosted/awesome-selfhosted#photo-and-video-galleries

Commercial and community:
- https://dl.acdsystems.com/media-releases/20250827_ACDSee_Photo_Studio_2026_Press_Release.pdf
- https://helpx.adobe.com/lightroom-classic/help/assisted-culling.html
- https://helpx.adobe.com/lightroom-classic/help/create-xmp-acr-files.html
- https://www.xnview.com/en/xnviewmp/
- https://www.faststone.org/FSViewerDetail.htm
- https://excire.com/en/excire-foto/
- https://mylio.com/
- https://news.ycombinator.com/item?id=43164794
- https://news.ycombinator.com/item?id=34716924

Standards, platform, and distribution:
- https://c2pa.org/specifications/specifications/2.2/index.html
- https://cyclonedx.org/capabilities/mlbom/
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/basic-accessibility-information
- https://learn.microsoft.com/en-us/windows/ai/windows-ml/
- https://learn.microsoft.com/en-us/windows/package-manager/package/manifest
- https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests

Dependencies and advisories:
- https://github.com/dlemstra/Magick.NET/releases/tag/14.14.0
- https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML/1.24.4
- https://github.com/advisories/GHSA-6c8g-7p36-r338
- https://sqlite.org/changes.html

## Open Questions
- Needs live validation: what publisher identity and signing channel will be available for release artifacts?
- Needs live validation: which Windows ML package/API path should be accepted if Microsoft guidance supersedes ONNX Runtime DirectML for desktop local AI?
- Needs product decision: should C2PA export write unsigned/action manifests by default, require user-provided signing material, or remain inspect-only?
