# Changeset Summary

Date: 2026-05-17

## Files Created

- `PROJECT_CONTEXT.md` - canonical consolidated project memory and current-state handoff.
- `.ai/research/2026-05-17/STATE_OF_REPO.md` - local reconnaissance memo.
- `.ai/research/2026-05-17/MEMORY_CONSOLIDATION.md` - instruction/memory inventory and reconciliation.
- `.ai/research/2026-05-17/SOURCE_REGISTER.md` - local and external source ledger.
- `.ai/research/2026-05-17/RESEARCH_LOG.md` - search strategy, passes, tools, and saturation notes.
- `.ai/research/2026-05-17/COMPETITOR_MATRIX.md` - direct, commercial, adjacent, and specialized competitor comparison.
- `.ai/research/2026-05-17/FEATURE_BACKLOG.md` - raw harvested ideas before prioritization.
- `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md` - scored and tiered candidates.
- `.ai/research/2026-05-17/SECURITY_AND_DEPENDENCY_REVIEW.md` - package/runtime/advisory review and hardening plan.
- `.ai/research/2026-05-17/DATASET_MODEL_INTEGRATION_REVIEW.md` - model/dataset/integration review.

`CONTINUE_FROM_HERE.md` was not created because no hard limit blocked completion.

## Files Modified

- `ROADMAP.md` - added a new authoritative 2026-05-17 v7 roadmap section and preserved the older v6 roadmap below as historical context.
- `src/Images/Images.csproj` - upgraded SharpCompress from 0.47.4 to 0.48.1.
- `CHANGELOG.md` - added Unreleased Security note for the SharpCompress advisory fix.
- `docs/archive-runtime-review.md` - updated SharpCompress version and advisory note.
- `docs/integration-policy.md` - updated the SharpCompress accepted integration row.
- `src/Images/Services/CodecCapabilityService.cs` - added shared dependency provenance rows with source, version, path, SHA-256, advisory status, and action copy for About and CLI reports.
- `src/Images/AboutWindow.xaml.cs` - renders the structured provenance rows in the About runtime provenance card.
- `src/Images/Services/CliReport.cs` - includes the shared provenance rows in `--system-info` and supports redirected stdout/stderr for release diagnostics logs.
- `tests/Images.Tests/CodecCapabilityServiceTests.cs` - covers structured provenance rows and the codec report provenance section.
- `PROJECT_CONTEXT.md`, `README.md`, `CHANGELOG.md`, `docs/improvement-plan.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, and `.ai/research/2026-05-17/SECURITY_AND_DEPENDENCY_REVIEW.md` - updated for V7-05 through V7-07 completion.
- `.github/workflows/release.yml` - stages approved jpegtran before publish, smokes portable and installed diagnostics, and uploads diagnostics logs.
- `.gitignore` - keeps temporary codec artifacts and runtime binaries ignored while allowing tracked jpegtran license/provenance files.
- `scripts/Prepare-JpegTranBundle.ps1` - downloads/extracts the approved libjpeg-turbo 3.1.4.1 artifact and verifies artifact plus executable SHA-256 values.
- `scripts/Test-ReleaseDiagnostics.ps1` - validates portable/installed `--system-info` and `--codec-report` logs for Ghostscript, OCR, jpegtran, and provenance rows.
- `scripts/Test-ReleaseReadiness.ps1`, `docs/release-checklist.md`, `docs/codec-bundling.md`, `docs/lossless-jpeg-transform-policy.md`, `docs/integration-policy.md`, `installer/Images.iss`, and `src/Images/Codecs/JpegTran/*` - updated for approved jpegtran release staging and installed smoke support.
- `src/Images/Controls/ZoomPanImage.cs` - exposes view-state get/set helpers so compare canvases can synchronize pan and zoom.
- `src/Images/ViewModels/MainViewModel.cs`, `src/Images/MainWindow.xaml`, and `src/Images/MainWindow.xaml.cs` - add compare mode entry points, state, commands, 2-up and opacity-overlay layouts, linked transforms, A/B swap, keyboard opacity controls, and Escape exit behavior.
- `src/Images/DuplicateCleanupWindow.xaml` and `src/Images/DuplicateCleanupWindow.xaml.cs` - add a selected-pair compare handoff back to the main viewer.
- `tests/Images.Tests/MainViewModelStateTests.cs` - covers current+next compare, overlay opacity, A/B swap, and pair-based compare entry.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, and `.ai/research/2026-05-17/STATE_OF_REPO.md` - updated for V7-10 completion.

## Preserved

- Pre-existing untracked `assets/banner.png.xmp` was left untouched and unstaged.
- Existing `AGENTS.md` and `CLAUDE.md` were left intact because they are local/tool-specific ignored guidance in this checkout.
- Older roadmap research was preserved rather than deleted.

## Verification

Completed before commit:

- `git diff --check` - passed.
- `dotnet list Images.sln package --vulnerable --include-transitive` - passed; no vulnerable packages for `Images` or `Images.Tests`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Test-VersionSync.ps1` - passed for 0.2.11.
- `dotnet build Images.sln -c Release` - passed with 0 warnings and 0 errors.
- `dotnet test Images.sln -c Release --no-build` - passed 351 tests after V7-10 compare coverage was added.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Test-ReleaseReadiness.ps1 -Version 0.2.11` - passed.
- `src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.exe --system-info` - exited 0 in the local shell smoke.
- `src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.exe --codec-report` - exited 0 in the local shell smoke.
- `dotnet src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.dll --system-info` - printed system/runtime diagnostics including SharpCompress 0.48.1.0 and Ghostscript 10.07.0.
- `dotnet src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.dll --codec-report` - printed the codec capability report, dependency provenance rows, advisory status, and runtime/model action copy.
