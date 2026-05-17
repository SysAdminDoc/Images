# Images Project Context

Last reconciled: 2026-05-17

## Current Identity

Images is a local-first Windows 10/11 image viewer and lightweight image workflow tool built with WPF and .NET 9. It keeps the first screen image-focused and keyboard-friendly while adding power features around codec diagnostics, archive books, gallery filtering, local cleanup, non-destructive edits, OCR, and privacy-visible integrations.

The project philosophy is stable across `README.md`, `CLAUDE.md`, `docs/integration-policy.md`, and the prior roadmap:

- Local-first by default. No silent network behavior.
- Preserve originals unless the user explicitly chooses a destructive action.
- Prefer WIC for normal decode paths, then Magick.NET fallback for broad format coverage.
- Keep optional runtimes visible, provenance-tracked, and test-gated.
- Ship a self-contained Windows release plus installer with checksum and version sync gates.
- Keep advanced workflows discoverable without making the default viewer feel like a catalog app.

## Verified Repository State

- Branch: `main`, tracking `origin/main`.
- Initial 2026-05-17 research baseline: `6da0641aa2b241c1b108f28ed01be69836ec56a3` (`feat: add send print clipboard actions`, 2026-05-14). Later autonomous roadmap commits on 2026-05-17 closed V7-02 through V7-07 and V7-10.
- Latest published tag found locally and on GitHub Releases: `v0.2.11`, commit `0abf855e109016b3e2279a99cdf43243d3efa35b`, published 2026-05-05.
- Root version surfaces still show `0.2.11` in `src/Images/Images.csproj`, `README.md`, and installer docs.
- `CHANGELOG.md` has substantial Unreleased work after `v0.2.11`: crop/writeback refinement, selection, jpegtran provenance, lossless JPEG writeback and trim confirmation, inpaint runtime decision, compare/overlay mode, editor workbenches, wallpaper modes, send/print/clipboard actions.
- Pre-existing unrelated untracked file during this run: `assets/banner.png.xmp`.

## Architecture Map

Primary app:

- `src/Images/App.xaml.cs` - startup, exception handling, app lifecycle.
- `src/Images/MainWindow.xaml` - main WPF shell.
- `src/Images/ViewModels/MainViewModel.cs` - main interaction coordinator. It is still large but now delegates many workflows to controllers/services.
- `src/Images/Controls/ZoomPanImage.cs` - image viewport interaction.
- `src/Images/DarkTheme.xaml` - Catppuccin Mocha resource dictionary.

Important services and controllers:

- `Services/ImageLoader.cs` - WIC-first and Magick.NET fallback decode.
- `Services/ImageExportService.cs` - codec-aware Save a copy/export path.
- `Services/DirectoryNavigator.cs` - natural-sort folder navigation and file watching.
- `Services/SettingsService.cs` - SQLite-backed app settings and local state.
- `Services/ArchiveBookService.cs` - read-only ZIP/CBZ plus SharpCompress-backed RAR/CBR and 7z/CB7 book pages.
- `Services/DuplicateCleanupService.cs` - exact and perceptual duplicate cleanup.
- `Services/FileHealthScanService.cs` - extension mismatch, corrupt file, zero-byte, and temp-file review.
- `Services/NonDestructiveEditService.cs` - XMP sidecar edit stack.
- `Services/CodecRuntime.cs` - Ghostscript and optional runtime discovery.
- `Services/JpegtranRuntime.cs` and JPEG transform helpers - optional lossless JPEG runtime path.

Tests:

- `tests/Images.Tests` contains regression coverage for services, controllers, view-model slices, and generated image fixtures.
- The repo currently uses generated fixtures rather than checked-in binary sample corpora for most decode/export tests.

## Build, Test, And Diagnostic Commands

Use PowerShell from the repository root.

```powershell
dotnet restore Images.sln
dotnet build Images.sln -c Release
dotnet test Images.sln -c Release --no-build
dotnet list Images.sln package --vulnerable --include-transitive
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Test-VersionSync.ps1
src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.exe --system-info
src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.exe --codec-report
```

Release packaging uses `dotnet publish`, `scripts/Prepare-GhostscriptBundle.ps1`, Inno Setup, GitHub Releases, and version sync checks. See `README.md`, `CLAUDE.md`, `.github/workflows/release.yml`, `installer/Images.iss`, and `docs/codec-bundling.md`.

## Current Capability Summary

Shipped or present in current docs and changelog:

- Natural folder navigation, zoom/pan/rotate/flip, inline rename, drag/drop, FSW reload.
- Broad WIC plus Magick.NET format support with About/CLI codec reports.
- Structured runtime/dependency provenance rows in About, `--system-info`, and `--codec-report`, covering NuGet packages, optional runtimes, OS OCR, and future model-runtime placeholders with source, version, path, SHA-256 where available, advisory status, and action copy.
- App-local Ghostscript support for PDF/EPS/PS/AI previews in release artifacts.
- Multi-page documents and layered/page formats.
- Archive book mode for ZIP/CBZ, RAR/CBR, and 7z/CB7 with right-to-left mode, spreads, progress, and clean-scan preview.
- Animated GIF playback and support for animated WebP/APNG when available in the Magick build.
- Gallery workbench with smart filters and current-folder thumbnail grid.
- OCR overlay using Windows.Media.Ocr and installed Windows OCR capabilities.
- Duplicate cleanup and file health scan.
- Viewer compare mode with current+next, chosen local file, and duplicate-cleanup pair entry points; 2-up and opacity-overlay layouts share pan, zoom, rotate, flip, A/B swap, keyboard opacity controls, and Escape exit behavior.
- Batch processor, macro actions, import inbox, private tag relationships.
- Non-destructive resize, adjustments, effects, annotations/redaction, perspective correction, auto enhance, edit history, virtual copies.
- Destructive crop apply for flat raster formats only; crop is intentionally disabled for layered, vector, document, archive, and RAW formats.
- Optional jpegtran runtime discovery and lossless JPEG transform paths when an approved local runtime is available.
- Approved libjpeg-turbo 3.1.4.1 jpegtran release staging with tracked license/provenance files, SHA-256 verification, and portable/installed diagnostics smoke coverage.
- Wallpaper layout modes and send/print/clipboard actions.
- Serilog logging, minidump crash capture, support diagnostics, app storage/cache cleanup, update checks disabled by default.

## Important Gaps

The old `ROADMAP.md` v6 header said there was no editor, organizer, or batch processor. That is stale. The current gap set is more specific:

- Roadmap/status hygiene has release-readiness coverage; continue updating `ROADMAP.md` and `PROJECT_CONTEXT.md` in the same change set as roadmap work.
- The future-dated historical `CHANGELOG.md` entries for `v0.1.8` and `v0.1.9` were repaired on 2026-05-17: `v0.1.9` uses GitHub release publication date 2026-05-04, and `v0.1.8` uses local release commit date 2026-04-25 because no tag or GitHub release exists.
- There is no full local catalog/schema layer yet for library-scale metadata, smart search, long-running indexing, or durable job progress.
- Semantic search, local model management, background removal, upscaling, and inpainting all need a shared model/runtime foundation before feature work.
- Color management is still a gap: ICC awareness, soft-proof/status copy, histogram/channel tools, and output profile handling need design and tests.
- Distribution trust is still checksum-first; code signing, package-manager manifests, and SmartScreen reputation remain future work.
- Large-image/deep-zoom tile architecture is not implemented.

## Dependency And Runtime Notes

- Target framework: `net9.0-windows10.0.22621.0`. Microsoft .NET 9 is STS and supported until 2026-11-10 per the official .NET support policy.
- Magick.NET-Q16-AnyCPU is at 14.13.0.
- SharpCompress was upgraded during the 2026-05-17 research pass from 0.47.4 to 0.48.1 because GHSA-6c8g-7p36-r338 / CVE-2026-44788 flagged `WriteToDirectory()` path traversal. Images does not call that API, but the upgrade restores a clean vulnerability gate.
- Ghostscript 10.07.0 is the current release artifact runtime. Its AGPL license and source archive must remain visible in release outputs.
- `dotnet list package --outdated` shows Microsoft.Data.Sqlite, Microsoft.Extensions.Logging, Serilog packages, coverlet, Microsoft.NET.Test.Sdk, and xUnit runner updates. Treat major package moves as test-gated rather than automatic, especially around .NET 10 package lines.

## Research Artifacts

The 2026-05-17 deep research run lives in `.ai/research/2026-05-17/`:

- `STATE_OF_REPO.md`
- `MEMORY_CONSOLIDATION.md`
- `SOURCE_REGISTER.md`
- `RESEARCH_LOG.md`
- `COMPETITOR_MATRIX.md`
- `FEATURE_BACKLOG.md`
- `PRIORITIZATION_MATRIX.md`
- `SECURITY_AND_DEPENDENCY_REVIEW.md`
- `DATASET_MODEL_INTEGRATION_REVIEW.md`
- `CHANGESET_SUMMARY.md`

Root `ROADMAP.md` now starts with an authoritative 2026-05-17 v7 plan. The older v6 intelligence remains below it as historical source material.

## Recommended Next Work

1. Build the V7-11 visual diff export workbench on top of compare mode and existing export/batch services.
2. Design catalog schema v1 before semantic search, model-backed organization, or library-scale jobs.
3. Add target-format capability warnings for export and batch so users see alpha, animation, page, metadata, and color-profile loss before writing copies.
4. Plan the local model/runtime manager only after catalog and provenance foundations are stable.
