# Research - Images

## Executive Summary
Images is a local-first Windows WPF image viewer and editor with unusually strong privacy, metadata, archive, slideshow, format-validation, and AI-search foundations for a small desktop app. Its strongest current shape is "power-user Windows Photos replacement": fast viewing, local file control, metadata safety, batch operations, and optional local AI. The highest-value direction is not a broad rewrite; it is making the already ambitious Windows-native product reliable enough to ship. Top opportunities, in order: fix the invalid ONNX Runtime DirectML package pin that currently prevents restore; make dependency and release-readiness checks mechanically catch that failure; centralize the AI runtime/provider boundary around Windows ML plus valid ONNX fallbacks; add automated WPF smoke coverage for open, navigation, rename, export, settings, and batch flows; turn the batch/export system into an operation-chain workflow; add Squoosh/FastStone-style A/B export preview without blocked quality-metric dependencies; keep public docs aligned with .NET 10 and actual package state; add a Catppuccin Latte/system light theme; improve folder/book history navigation.

## Product Map
- Core workflows: open and navigate local image folders; inspect and edit metadata; rename and organize files; compare, crop, rotate, resize, export, batch process, and strip metadata; view archives/books and extract Motion Photo/Live Photo embedded video.
- User personas: privacy-conscious Windows photographers; technical users managing large local image sets; users replacing slow default viewers; archivists/comic readers; local-AI users who want semantic search without cloud upload.
- Platforms and distribution: Windows desktop WPF on `net10.0-windows10.0.22621.0`; local builds via `dotnet`; docs mention future WinGet/MSIX/signing tracks that remain blocked in `Roadmap_Blocked.md`.
- Key integrations and data flows: WIC and Magick.NET for image decoding; ExifTool-style metadata workflows; SQLite catalog/cache storage; SharpCompress archives; Ghostscript helper path for PDF/PS-family formats; ONNX Runtime/DirectML references for local AI; Serilog file logging; XMP sidecars as recoverable metadata state.

## Competitive Landscape
- ImageGlass: Does well at format breadth, HDR/SVG parity, signed installers, MSIX/Flatpak/DMG distribution, Motion Photos, and plugin boundaries. Images should learn from its capability matrix, plugin separation, and installer trust posture. Avoid copying its broad platform/distribution scope before the Windows restore/build path is dependable.
- NeeView: Does well at book/archive viewing, browsing history, and keyboard-heavy image navigation. Images should learn from its archive-as-book ergonomics, bookmark/history affordances, and thumbnail naming options. Avoid turning the main viewer into a file-manager clone that dilutes image-first focus.
- Czkawka/Krokiet: Does well at duplicate/similar-image workflows, bad-extension detection, broken-file detection, EXIF removal, and progress-heavy batch tasks. Images should learn from its data-safety and preview-state discipline, especially for long scans and large previews. Avoid adding destructive cleanup flows without reversible confirmation and clear dry-run output.
- FastStone Image Viewer and IrfanView: Do well at quick launch, batch conversion, slideshow, comparison, EXIF/GPS display, and side-by-side conversion previews. Images should learn from their dense power-user workflows and fast A/B export preview. Avoid legacy menu sprawl and unstructured settings growth.
- PicView and PhotoDemon: Do well at portable/local operation, rich image effects, batch resize/compression, archive/comic viewing, and modern lightweight UX. Images should learn from portable trust signals and simple effect discoverability. Avoid adding large editing surfaces that compete with dedicated raster editors.
- Eagle: Does well at local/offline AI search, reverse image search, and natural-language image lookup. Images should learn from the "no upload" positioning and model availability feedback. Avoid automatic model downloads or opaque inference unless users explicitly opt in.
- Adobe Lightroom and Capture One: Do well at assisted culling, focus/exposure/closed-eye review, smart albums, and third-party workflow actions. Images should learn from quality-signal ranking and review filtering. Avoid cloud/library assumptions, subscription-oriented UX, and database ownership that conflict with local-file-first philosophy.
- Windows Photos: Does well at being preinstalled and visually simple, but community complaints center on speed and control. Images should keep optimizing launch/open confidence and avoid hiding file-level operations that power users expect.

## Security, Privacy, and Reliability
- [Verified] Current main cannot restore because `src/Images/Images.csproj` pins `Microsoft.ML.OnnxRuntime.DirectML` to `1.26.0`, while NuGet currently exposes DirectML up to `1.24.4`; local `dotnet list Images.sln package --outdated --include-transitive` and `--vulnerable` both fail before analysis.
- [Verified] The repository is on `net10.0-windows10.0.22621.0`, but public-facing docs still contain stale .NET 9 cues; `README.md` and release/readiness docs need to be checked only after the runtime package path is fixed.
- [Verified] DirectML provider calls are scattered across `ClipEmbeddingProvider`, `BackgroundRemovalService`, `LaMaInpaintService`, and `SuperResolutionService`, which makes provider fallback, status reporting, and security updates hard to reason about.
- [Verified] `ClipEmbeddingProvider` attempts to infer DirectML status from `sessionOptions.AppendExecutionProvider_DML`, which is not an actual runtime provider-status check; users need truthful CPU/GPU/NPU/provider feedback before trusting local AI features.
- [Verified] ONNX Runtime 1.25 and 1.26 release notes include memory-safety fixes and hardening, but DirectML package availability lags base ONNX Runtime. Images needs an explicit version policy instead of silently pinning unavailable versions.
- [Verified] Ghostscript 10.07.1 includes sandbox and temporary-file permission hardening; the repo already tracks Ghostscript staging as blocked, so it should remain a blocked distribution item until the binary provenance path is solved.
- [Likely] Minor package drift exists after the .NET 10 migration (`Microsoft.Data.Sqlite 10.0.0` vs newer 10.0.x, `Serilog.Sinks.File 6.0.0` vs 7.0.0). Refresh only after restore is green, because current package commands cannot complete.
- Missing guardrails: package-version resolution before dependency claims; UI smoke tests for core flows; runtime-provider diagnostics for local AI; visual regression coverage for themes and export preview; rollback notes for runtime package changes.
- Recovery and rollback needs: keep XMP sidecars authoritative for metadata edits; ensure catalog/search caches can rebuild after schema/runtime changes; make AI model/runtime failures degrade to non-AI viewing without breaking open/navigation/export.

## Architecture Assessment
- `src/Images/ViewModels/MainViewModel.cs` and `src/Images/MainWindow.xaml` remain very large, which increases regression risk for shell, command state, and keyboard/navigation polish. Future work should extract command groups, runtime status presentation, and mode-specific panels without changing user-visible behavior.
- The AI runtime boundary should be consolidated behind a small provider factory/service that owns Windows ML, DirectML, CPU fallback, provider labels, and failure telemetry; current direct `AppendExecutionProvider_DML` usage appears in multiple services.
- Batch and export foundations exist, but operation chaining is not yet a first-class workflow. The next useful step is an ordered operation pipeline that reuses existing rotate/crop/resize/metadata/export services and shows dry-run counts before write operations.
- Export preview should evolve before adding blocked quality metrics: a split A/B preview with file-size and format deltas is code-ready and matches Squoosh/FastStone patterns without requiring SSIMULACRA2 or external metric binaries.
- Tests cover many service-level concerns, but gaps remain for WPF launch/open/navigation/settings/export smoke tests, real runtime-provider reporting, theme contrast snapshots, and restore/package availability checks.
- Localization infrastructure exists through `Strings.resx`, `LocExtension`, locale settings, and archive right-to-left page-turn support; the next gap is missing-key prevention, hard-coded-string detection, and RTL visual smoke coverage rather than basic i18n research.
- Documentation gaps are currently operational: `RESEARCH.md` had stale missing-feature claims for features now shipped, and `ROADMAP.md` retained completed historical work. Research and roadmap files must stay short, active, and mechanically tied to current repo state.

## Rejected Ideas
- Cross-platform UI rewrite: ImageGlass, PicView, and qView show cross-platform demand, but Images is explicitly Windows/WPF-native and already benefits from WIC, Windows ML, and Windows shell conventions.
- Cloud sync, accounts, or hosted libraries: Lightroom-style cloud/library behavior conflicts with the local-first privacy position and has no evidence in the repo architecture.
- Automatic model downloads by default: Eagle proves local AI demand, but Images should not fetch large models or change network behavior without explicit opt-in and a provenance/consent design.
- Full photo-management suite parity with digiKam or Lightroom: smart albums and culling are useful later, but the immediate gap is reliable viewer/editor/runtime infrastructure.
- Plugin marketplace now: ImageGlass validates plugin boundaries, but Images should finish the runtime/package/test foundation before loading third-party code.
- MSIX-only distribution: signed packaged distribution is desirable, but blocking account/signing work already lives in `Roadmap_Blocked.md`; portable/dev builds must remain viable.
- MCP integration before catalog/query boundaries: read-only automation could be valuable later, but it depends on a stable catalog API, privacy policy, and plugin/security boundary.
- HDR display implementation as a quick win: ImageGlass has HDR tone mapping, but Images already tracks HDR/color work as blocked by rendering/color-management decisions; do not duplicate it in the active roadmap.
- Mobile companion app: the codebase, package targets, and differentiators are Windows desktop/WPF-specific; mobile work would fragment the product before the core release/runtime path is reliable.
- Multi-user collaboration: no account, cloud, or shared-library architecture exists, and it would conflict with the local-file-first privacy posture.

## Sources
Competitors and adjacent projects:
- https://github.com/d2phap/ImageGlass/releases/tag/10.0.2.66-beta-2
- https://imageglass.org/docs/features
- https://github.com/neelabo/NeeView/releases
- https://github.com/qarmin/czkawka/releases/tag/11.0.1
- https://github.com/qarmin/czkawka
- https://github.com/Ruben2776/PicView/releases
- https://picview.org/
- https://github.com/nomacs/nomacs/releases
- https://photodemon.org/
- https://www.faststone.org/FSViewerDetail.htm
- https://www.irfanview.com/main_history.htm
- https://www.irfanview.com/64bit.htm

Commercial and product research:
- https://www.adobe.com/learn/lightroom-cc/web/ai-assisted-culling-lightroom
- https://support.captureone.com/hc/en-us/articles/35747427882653-Capture-One-16-8-release-notes
- https://en.eagle.cool/blog/post/eagle-plugin-ai-search

Platform, standards, and dependencies:
- https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html
- https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML
- https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime
- https://github.com/microsoft/onnxruntime/releases/tag/v1.25.0
- https://github.com/microsoft/onnxruntime/releases/tag/v1.26.0
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/supported-execution-providers
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://github.com/dlemstra/Magick.NET/releases
- https://ghostscript.readthedocs.io/en/gs10.07.1/News.html
- https://spec.c2pa.org/specifications/specifications/2.4/specs/C2PA_Specification.html

Community and ecosystem signal:
- https://github.com/ibaaj/awesome-OpenSourcePhotography
- https://github.com/meichthys/foss_photo_libraries

## Open Questions
- Which runtime policy should Images choose now: keep DirectML at the latest available package, switch local AI to Windows ML plus base ONNX Runtime, or ship CPU-only until DirectML catches up?
- Are external model downloads allowed in a future opt-in flow, and where should model provenance and storage policy be documented?
- Is there an available signing identity, Store account, or WinGet publisher path for distribution work currently parked in `Roadmap_Blocked.md`?
- Which Windows UI automation stack should be standardized for CI: FlaUI, WinAppDriver/Appium, or a repo-local smoke runner around the current WPF shell?
