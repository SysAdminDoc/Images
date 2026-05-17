# State Of Repo

Date: 2026-05-17
Repository: `C:\Users\--\repos\Images`

## Git And Release State

- Branch: `main`, tracking `origin/main`.
- HEAD: `6da0641aa2b241c1b108f28ed01be69836ec56a3`.
- HEAD subject: `feat: add send print clipboard actions`.
- Latest local/GitHub release tag inspected: `v0.2.11` at `0abf855e109016b3e2279a99cdf43243d3efa35b`.
- Latest release publication date from `gh release view v0.2.11`: 2026-05-05T21:12:36Z.
- Release assets inspected:
  - `Images-v0.2.11-win-x64.zip`
  - `Images-v0.2.11-setup-win-x64.exe`
  - `Images-v0.2.11-checksums.txt`
  - `ghostscript-10.07.0.tar.xz`
- Pre-existing unrelated untracked file: `assets/banner.png.xmp`.

Recent commits:

- `6da0641 feat: add send print clipboard actions`
- `2d22955 feat: add wallpaper layout modes`
- `b84587a feat: add copy move folder actions`
- `03a13bb feat: add auto enhance edit`
- `7e009b5 feat: add perspective correction workbench`
- `d252683 feat: confirm lossless jpeg trim`
- `810b493 feat: add annotations redaction workbench`
- `e15e000 feat: add rotation writeback`
- `b020ab7 feat: add image effects presets`
- `67b9f90 feat: add lossless jpeg crop writeback`

## Local Inventory

Tracked project files are concentrated under:

- `src/Images` - WPF app, services, view models, windows, controls.
- `tests/Images.Tests` - xUnit regression suite.
- `docs` - policy, design, runtime, distribution, and improvement notes.
- `installer` - Inno Setup script and release packaging.
- `.github/workflows` - CI/release automation.

Fast inventory during this pass found roughly:

- 158 C# files.
- 26 XAML files.
- 36 Markdown files.
- 3 PowerShell scripts.
- 4 workflow/config YAML files.
- 1 Inno Setup script.

## Current Application Shape

Images is no longer only a viewer. Current source and documentation show these shipped or Unreleased capabilities:

- Broad image/document decode via WIC, Magick.NET, and optional Ghostscript.
- Multi-page and archive book navigation.
- Gallery workbench with smart filters.
- Local duplicate cleanup and file health scan.
- Batch processor and macro actions.
- Private tags, import inbox, and XMP sidecar workflows.
- OCR overlay with Windows.Media.Ocr.
- Non-destructive edit stack with resize, adjustments, effects, annotations/redaction, perspective correction, auto enhance, and virtual copies.
- Destructive crop/writeback for flat rasters only.
- Optional jpegtran discovery and lossless JPEG transform path.
- Wallpaper, send, print, and clipboard actions.

This means the older `ROADMAP.md` v6 "No editor, no organizer, no batch processor" line is stale and should not guide new work.

## Architecture Observations

- `MainViewModel.cs` still acts as the main coordinator, but many workflows have been extracted into services and controllers.
- Core IO policies are service-owned rather than spread directly through XAML code-behind.
- The app already has a strong local-only privacy contract and many optional-runtime review docs.
- Test coverage is broad for services and view-model workflow seams, with generated image fixtures preferred over checked-in binaries.
- The current highest-risk areas are not basic viewer features; they are runtime provenance, status/documentation drift, optional model/runtime expansion, and workflow integration across already-shipped surfaces.

## Build And Dependency State

During this research run, SharpCompress was upgraded from 0.47.4 to 0.48.1 after the local vulnerability gate flagged GHSA-6c8g-7p36-r338 / CVE-2026-44788. The affected API is `WriteToDirectory()`, which the app does not call, but the package upgrade is the correct gate-clearing move.

Security scan after the upgrade:

```text
dotnet list Images.sln package --vulnerable --include-transitive
The given project `Images` has no vulnerable packages given the current sources.
The given project `Images.Tests` has no vulnerable packages given the current sources.
```

Known package update opportunities from `dotnet list Images.sln package --outdated`:

- `Microsoft.Data.Sqlite 9.0.0 -> 10.0.8`
- `Microsoft.Extensions.Logging 9.0.0 -> 10.0.8`
- `Serilog 4.2.0 -> 4.3.1`
- `Serilog.Extensions.Logging 9.0.0 -> 10.0.0`
- `Serilog.Sinks.File 6.0.0 -> 7.0.0`
- `coverlet.collector 6.0.2 -> 10.0.0`
- `Microsoft.NET.Test.Sdk 17.12.0 -> 18.5.1`
- `xunit 2.9.2 -> 2.9.3`
- `xunit.runner.visualstudio 2.8.2 -> 3.1.5`

## Local Contradictions And Risks

- `CHANGELOG.md` previously contained `v0.1.8` and `v0.1.9` entries dated 2026-06-02, which was after the 2026-05-17 current date. This was repaired on 2026-05-17 after checking local commits/tags and GitHub release metadata.
- Root `ROADMAP.md` contains excellent source material but its top current-state narrative predates several shipped features.
- `.claude` project memory found through the shared memory index described older Images state around `v0.1.2`; this is historical and must be verified against live repo before use.
- Runtime sidecars and model expansion need checksum/license/release smoke gates before bundling.

## Completion Status For This Run

Required artifacts were created or updated:

- `PROJECT_CONTEXT.md`
- `ROADMAP.md`
- `.ai/research/2026-05-17/STATE_OF_REPO.md`
- `.ai/research/2026-05-17/MEMORY_CONSOLIDATION.md`
- `.ai/research/2026-05-17/SOURCE_REGISTER.md`
- `.ai/research/2026-05-17/RESEARCH_LOG.md`
- `.ai/research/2026-05-17/COMPETITOR_MATRIX.md`
- `.ai/research/2026-05-17/FEATURE_BACKLOG.md`
- `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md`
- `.ai/research/2026-05-17/SECURITY_AND_DEPENDENCY_REVIEW.md`
- `.ai/research/2026-05-17/DATASET_MODEL_INTEGRATION_REVIEW.md`
- `.ai/research/2026-05-17/CHANGESET_SUMMARY.md`

`CONTINUE_FROM_HERE.md` was not created because the completion criteria were met in this session.
