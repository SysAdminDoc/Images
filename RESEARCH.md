# Research - Images

## Executive Summary
Verified: Images is a Windows-only, local-first WPF/.NET 10 image viewer and organizer with broad codec coverage, archive/book viewing, inline rename, metadata/C2PA inspection, local catalog/search, recovery, diagnostics, optional local models, and privacy-preserving defaults. Its strongest current shape is a trustworthy Windows power viewer that can grow into a local photo workbench without becoming a cloud library. Highest-value direction: keep closing trust and release-verification gaps after the workflow removal, harden untrusted-codec boundaries, then add selective parity where competitors are visibly moving: Explorer-order fidelity, local assisted culling/people review, C2PA export handoff, and renderer evidence for HDR/vector fidelity. Top opportunities, in order: finish the existing local release-readiness/SBOM items in `ROADMAP.md`; add an ImageMagick/Magick.NET security-policy gate around the current `ResourceLimits`; protect secondary windows and context menus with UIA smoke coverage; resolve Windows ML vs DirectML copy; add Explorer parity navigation modes; add optional local face-region review only after model/runtime provenance stays explicit; add C2PA export outcomes; and build vector/HDR fixture evidence before any renderer rewrite.

## Product Map
- Core workflows: open local images, folders, explicit multi-path sessions, archives/books, documents, RAW previews, and huge images; rename while viewing; rate, tag, cull, search, compare, export, batch process, inspect metadata/provenance, and recover destructive operations.
- User personas: Windows power users replacing Photos/ImageGlass/FastStone; photographers and archivists managing local folders; technical users who want portable artifacts, runtime provenance, checksums, and no hidden network behavior.
- Platforms and distribution: Windows x64 WPF app targeting `net10.0-windows10.0.22621.0`; MIT; Inno installer plus portable ZIP; WinGet/Scoop manifest generation; `.github` is absent after commit `55fabf2`, so release verification must be local.
- Key integrations and data flows: WIC first with Magick.NET fallback; SharpCompress read-only archive pages; SQLite settings/catalog/search caches; XMP sidecars as metadata authority; optional Ghostscript, jpegtran, c2patool, OCR, ExifTool, and ONNX Runtime DirectML local model paths; opt-in GitHub release checks logged by `NetworkEgressService`.

## Competitive Landscape
- ImageGlass 10 beta: moving to a new .NET/Avalonia foundation with HDR tone mapping, native vector rendering, plugin SDK, shell thumbnail controls, motion photos, Explorer sort-order support, configurable cache budgets, and cross-platform targets. Images should learn from HDR/vector/cache/navigation evidence and intentionally avoid a public plugin SDK until extension signing, sandboxing, and support boundaries are mature.
- PicView, qView, NeeView, JPEGView, and Oculante: fast viewer references for minimal chrome, file operations, archive/book reading, trackpad/touch polish, and zero-friction preview workflows. Images already beats most on diagnostics and local-first trust; keep borrowing focused input/navigation affordances without diluting the side-panel rename identity.
- FastStone and XnView MP: mature viewer-manager tools with dense batch conversion, compare, contact sheets, metadata workflows, similar-file cleanup, and reliable Windows utility breadth. Images should match their durable batch/metadata confidence with modern WPF clarity, not copy their crowded information architecture.
- digiKam, PhotoPrism, and Immich: strongest OSS references for face/person organization, ML jobs, smart search, duplicate detection, metadata portability, and background-job visibility. Images should borrow local/rebuildable index patterns and explicit job states, while rejecting server/multi-user/sync architecture.
- Lightroom Classic, Excire Search/Foto, and ACDSee Photo Studio: commercial benchmark for assisted culling, face sharpness, people grouping, AI keyword/search, duplicate review, XMP handoff, and C2PA export. Images should implement explainable, local scoring and provenance outcomes; avoid cloud, subscription, or opaque auto-decision defaults.
- Mylio, Eagle, PowerToys Peek, and Windows shell integrations: adjacent references for local-first positioning, asset-library polish, and Explorer preview entry points. Images should keep `--peek` and file associations lightweight, with any preview/thumbnail handler gated by signing and rollback evidence.

## Security, Privacy, and Reliability
- Verified: recent commits fixed prior high-priority risks: semantic fallback diagnostics (`4ee3432`), trust-path logging (`3f49a0b`), dispatcher fatal termination (`a35172c`), ExifTool async output drain (`f1a35ab`), tile build serialization (`b44a7f8`), and GPS-strip rollback (`5be91e2`).
- Verified: `src/Images/App.xaml.cs` sets Magick.NET `ResourceLimits` for memory, width, height, area, and list length, but there is no explicit app-level ImageMagick security-policy audit or test proving high-risk delegate/format behavior matches Images' codec policy. ImageMagick documents policy tradeoffs for untrusted inputs; this is the next codec-boundary hardening layer.
- Verified: release/trust docs still contain stale workflow and Dependabot claims after `.github` removal. `scripts/Test-ReleaseReadiness.ps1` runs version sync, restore, build, and vulnerable-package scan, but existing roadmap items correctly require local tests, diagnostics, package manifest validation, checksums, SBOM/provenance output, and doc cleanup.
- Verified: `tests/Images.Tests/WpfSmokeTests.cs` covers main-window launch, primary image automation, title, navigation button names, toolbar names, and position chip, but does not yet open secondary windows or the now-nested viewport context menu. Existing P1 roadmap items cover those gaps.
- Verified: privacy posture remains a differentiator: update checks are opt-in, model imports are explicit SHA-gated local files, support bundles redact image bytes/private paths, semantic indexes are app-local, and network activity is visible. Preserve this by rejecting automatic cloud analysis and automatic model downloads.
- Likely: Content Credentials expectations are rising. Images can inspect C2PA, but export currently writes images through `ImageExportService` without a clear preserved/written/omitted provenance outcome. The existing C2PA export roadmap item remains valid, with C2PA 2.4 trust and ingredient semantics as the implementation reference.

## Architecture Assessment
- Verified: `src/Images/ViewModels/MainViewModel.cs` and `src/Images/MainWindow.xaml` are still coordination hotspots. New viewer behavior should land in focused services/controllers and keep WPF bindings thin.
- Verified: `DirectoryNavigator` has persisted sort modes for natural name, name descending, modified, created, size, and extension, but no Explorer-current-order mode or sibling-directory auto-switch behavior. ImageGlass 10 beta explicitly calls out Explorer sort order, and ImageGlass 9.5 added sibling directory auto-switch, making this a Windows-viewer parity gap.
- Verified: `BackgroundJobsService` exists and About exposes job state, but existing roadmap still correctly asks for a primary activity surface because indexing, batch, contact sheet, model validation, and scans are long-running workflows.
- Verified: `ModelManagerService` and product copy still need a backend contract decision: the repo packages `Microsoft.ML.OnnxRuntime.DirectML`, while Microsoft now positions DirectML as sustained engineering and Windows ML as the Windows-supported ONNX Runtime path. Existing P3 roadmap item remains valid.
- Verified: `README.md` has private `person:` tag namespaces and Picasa face-region migration is already planned, but there is no native local face-region detection/review workflow. Competitors show this is table-stakes for photo organization; for Images it should be optional, local-only, explainable, and never auto-write names without review.
- Verified: SVG/vector preview currently flows through existing image decode paths; there is no fixture-backed zoom fidelity decision comparable to the existing HDR/color-management roadmap item. ImageGlass 10 beta's native vector renderer makes this worth a bounded evidence pass before any renderer rewrite.

## Rejected Ideas
- Reintroduce GitHub Actions or Dependabot as the default plan: sourced from stale docs and older changelog entries, rejected because commit `55fabf2` deliberately removed hosted workflows; restore local parity first.
- Cross-platform rewrite: sourced from ImageGlass 10/qView, rejected because Images' advantage is deep Windows/WPF integration, installer, file associations, WIC/OCR, and local diagnostics.
- Multi-user server, cloud library, or sync service: sourced from Immich/PhotoPrism/Mylio, rejected because it would invert the local single-user trust model.
- Automatic model downloads or cloud ML: sourced from Excire/Immich/Adobe comparisons, rejected because Images' local model manager intentionally requires user-imported files and SHA-256 verification.
- Public plugin SDK now: sourced from ImageGlass 10 SDK, rejected until extension loading has signing, sandbox/process-boundary, versioning, diagnostics, and removal policy.
- Full renderer rewrite as immediate work: sourced from ImageGlass HDR/vector work and WPF color limits, rejected until fixture-backed HDR/color/vector evidence proves WPF/Magick cannot meet the need.
- Default C2PA signing without identity material: sourced from C2PA/Lightroom research, rejected because unsigned or unverifiable signing would weaken trust; export should clearly say preserved, written with configured signing, or omitted.

## Sources
OSS and adjacent projects:
- https://imageglass.org/news/announcing-imageglass-10-beta-2-101
- https://imageglass.org/news/imageglass-10-beta-1-is-here-99
- https://imageglass.org/news/announcing-imageglass-9-5-100
- https://github.com/Ruben2776/PicView
- https://github.com/jurplel/qView
- https://github.com/neelabo/NeeView
- https://www.digikam.org/about/features/
- https://docs.photoprism.app/developer-guide/vision/face-recognition/
- https://docs.immich.app/features/facial-recognition
- https://docs.immich.app/features/searching/

Commercial and community:
- https://helpx.adobe.com/lightroom-classic/help/assisted-culling.html
- https://helpx.adobe.com/lightroom-classic/help/content-credentials.html
- https://photofocus.com/news/excire-search-2026-brings-ai-culling-search-panel-and-focus-detection-to-lightroom-classic/
- https://excire.com/en/excire-search/
- https://www.faststone.org/FSViewerDetail.htm
- https://www.xnview.com/en/faq/
- https://www.acdsee.com/en/products/photo-studio-ultimate/
- https://www.reddit.com/r/digiKam/comments/1qztapi/beginner_with_digikam_family_photo_archive_face/

Standards, platform, and dependencies:
- https://spec.c2pa.org/specifications/specifications/2.4/specs/C2PA_Specification.html
- https://opensource.contentauthenticity.org/docs/c2patool/docs/usage/
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview
- https://learn.microsoft.com/en-us/windows/ai/directml/dml-get-started
- https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html
- https://learn.microsoft.com/en-us/windows/win32/shell/preview-handlers
- https://github.com/CycloneDX/cyclonedx-dotnet
- https://cyclonedx.org/capabilities/mlbom/
- https://imagemagick.org/security-policy/
- https://github.com/dlemstra/Magick.NET/releases
- https://github.com/adamhathcock/sharpcompress/releases
- https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML

## Open Questions
- Needs live validation: what publisher identity and signing channel will be available for public release artifacts?
- Needs product decision: should C2PA export remain inspect-only, preserve existing manifests only, or write new action manifests when a configured signing identity exists?
- Needs implementation decision: should local AI stay ONNX Runtime DirectML-first for bundled predictability, or migrate to Windows ML for smaller/evergreen Windows deployments?
