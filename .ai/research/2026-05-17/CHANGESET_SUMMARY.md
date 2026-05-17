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
- `src/Images/Services/RecoveryCenterService.cs` - durable destructive-action ledger plus restore service for moves, renames, quarantines, and sidecars.
- `src/Images/RecoveryCenterWindow.xaml` and `src/Images/RecoveryCenterWindow.xaml.cs` - user-facing Recovery Center to review, reveal, and restore recorded operations.
- `tests/Images.Tests/RecoveryCenterServiceTests.cs` - focused coverage for restore, conflict-safe targets, sidecar recovery, missing recovery sources, and non-restorable writebacks.
- `src/Images/Services/ModelManagerService.cs` - approved local model registry, app-local grouped storage, runtime status, manual import/delete/reveal support, and SHA-256 validation for future model-backed tools.
- `src/Images/ModelManagerWindow.xaml` and `src/Images/ModelManagerWindow.xaml.cs` - user-facing Model manager for inspecting approved definitions, importing local ONNX files, revealing storage, opening source pages, and deleting local model files.
- `tests/Images.Tests/ModelManagerServiceTests.cs` - focused coverage for approved definitions, import/hash readiness, mismatched hashes, delete, and unknown model rejection.
- `src/Images/Services/SemanticSearchService.cs` - V7-31 local semantic-index foundation with an embedding-provider seam, app-local SQLite storage, deterministic metadata embeddings, exact cosine search, cancellation-safe rebuild, and delete-index support.
- `src/Images/SemanticSearchWindow.xaml` and `src/Images/SemanticSearchWindow.xaml.cs` - user-facing semantic search surface for explicit folder indexing, query/folder-filter search, result open/reveal, cancellation, and derived-data deletion.
- `tests/Images.Tests/SemanticSearchServiceTests.cs` - focused coverage for indexing/search ordering, folder filtering, cancellation preserving the previous index, and delete-index behavior.

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
- `src/Images/Services/ExportPreviewService.cs`, `src/Images/ExportPreviewWindow.xaml`, and `src/Images/ExportPreviewWindow.xaml.cs` - add the V7-11 export workbench with original versus encoded preview, JPEG/PNG/WebP/AVIF/JXL presets, quality and resize controls, size deltas, warning copy, and resize-aware saves.
- `src/Images/Services/ImageExportService.cs` - adds quality/max-dimension Save overloads plus internal preview helpers for in-memory export estimation.
- `src/Images/Services/BatchProcessorService.cs` and `src/Images/BatchProcessorWindow.xaml` - add dry-run estimated output size, byte delta, and warning rows using the export preview estimator.
- `tests/Images.Tests/ExportPreviewServiceTests.cs`, `tests/Images.Tests/ImageExportServiceTests.cs`, and `tests/Images.Tests/BatchProcessorServiceTests.cs` - cover in-memory preview encoding, request normalization, resize-aware saves, and batch dry-run estimates.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, `.ai/research/2026-05-17/FEATURE_BACKLOG.md`, and `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md` - updated for V7-11 completion.
- `src/Images/Services/CatalogService.cs` - adds the V7-12 rebuildable app-local catalog cache with schema migration v1, root rebuild, SHA-256 fingerprints, dimensions, dates, codec/format metadata, XMP sidecar path/modified time, rating, tags, and scan timestamps.
- `tests/Images.Tests/CatalogServiceTests.cs` - covers catalog rebuild/query behavior, sidecar rating/tag indexing, fingerprint/dimension storage, unsupported-file skipping, and cache clearing on rebuild.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, `.ai/research/2026-05-17/FEATURE_BACKLOG.md`, and `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md` - updated for V7-12 completion.
- `src/Images/Services/ReviewLabelService.cs` - adds XMP-backed star ratings, pick/reject labels, sidecar reading/writing, and restoreable previous state for culling review mode.
- `src/Images/ViewModels/MainViewModel.cs`, `src/Images/MainWindow.xaml`, and `src/Images/MainWindow.xaml.cs` - add review mode state, commands, side-panel controls, keyboard flow, gallery smart-filter refresh after sidecar writes, and undo routing.
- `tests/Images.Tests/ReviewLabelServiceTests.cs` and `tests/Images.Tests/MainViewModelStateTests.cs` - cover review sidecar mutations and ViewModel undo behavior.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, `.ai/research/2026-05-17/FEATURE_BACKLOG.md`, and `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md` - updated for V7-13 completion.
- `src/Images/Services/ExportCapabilityWarningService.cs` - adds shared source/target inspection and warning text for alpha flattening, animation frame loss, page/layer flattening, metadata loss, ICC profile risk, and lossy settings.
- `src/Images/Services/ExportPreviewService.cs` and `src/Images/Services/MacroActionService.cs` - route export preview, batch preview, and macro dry-run warning copy through the shared capability-warning service.
- `tests/Images.Tests/ExportCapabilityWarningServiceTests.cs` - covers alpha, metadata, ICC, animation, pages/layers, and dry-run warning paths.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, `.ai/research/2026-05-17/FEATURE_BACKLOG.md`, and `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md` - updated for V7-14 completion.
- `src/Images/Services/ImageColorAnalysisService.cs` - adds read-only embedded ICC/profile status, decoded color space, sampled luma/RGB channel stats, shadow/midtone/highlight histogram percentages, and alpha transparency stats.
- `src/Images/ViewModels/ColorAnalysisController.cs`, `src/Images/ViewModels/MainViewModel.cs`, and `src/Images/MainWindow.xaml` - add an asynchronous side-panel color/histogram section with safe unmanaged-color warnings that do not transform pixels.
- `tests/Images.Tests/ImageColorAnalysisServiceTests.cs` and `tests/Images.Tests/ColorAnalysisControllerTests.cs` - cover profiled, unprofiled, transparent, and controller supersession behavior.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, `.ai/research/2026-05-17/FEATURE_BACKLOG.md`, and `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md` - updated for V7-15 completion.
- `src/Images/ViewModels/MainViewModel.cs`, `src/Images/MainWindow.xaml`, `src/Images/DuplicateCleanupWindow.xaml.cs`, and `src/Images/FileHealthScanWindow.xaml.cs` - add Recovery Center entry points and record move, rename, quarantine, writeback, and Recycle Bin operations.
- `src/Images/Services/ImageColorAnalysisService.cs` and `src/Images/Services/ImageMetadataService.cs` - open background Magick.NET inspection streams with delete-sharing so read-only panels do not block move/delete flows.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, `.ai/research/2026-05-17/SOURCE_REGISTER.md`, `.ai/research/2026-05-17/FEATURE_BACKLOG.md`, and `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md` - updated for V7-16 completion.
- `src/Images/ViewModels/MainViewModel.cs` and `src/Images/MainWindow.xaml` - add Model manager entry points from the context menu and Automation card.
- `src/Images/Services/CodecCapabilityService.cs` and `tests/Images.Tests/CodecCapabilityServiceTests.cs` - connect runtime/model provenance rows to the local model manager snapshot and verify the visible model-manager action copy.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, `docs/inpaint-runtime-decision.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, `.ai/research/2026-05-17/SOURCE_REGISTER.md`, `.ai/research/2026-05-17/RESEARCH_LOG.md`, `.ai/research/2026-05-17/FEATURE_BACKLOG.md`, `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md`, and `.ai/research/2026-05-17/DATASET_MODEL_INTEGRATION_REVIEW.md` - updated for V7-30 completion.
- `src/Images/ViewModels/MainViewModel.cs` and `src/Images/MainWindow.xaml` - add Semantic search entry points and current-folder seeding for the new window.
- `src/Images/Services/ModelManagerService.cs` - adds pinned Qdrant CLIP ViT-B/32 text and vision ONNX candidates for the next semantic-search runtime slice.
- `README.md`, `CHANGELOG.md`, `ROADMAP.md`, `PROJECT_CONTEXT.md`, `docs/improvement-plan.md`, `docs/design-product-differentiators.md`, `.ai/research/2026-05-17/STATE_OF_REPO.md`, `.ai/research/2026-05-17/SOURCE_REGISTER.md`, `.ai/research/2026-05-17/FEATURE_BACKLOG.md`, `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md`, and `.ai/research/2026-05-17/DATASET_MODEL_INTEGRATION_REVIEW.md` - updated for the V7-31 foundation, while keeping V7-31 unchecked until an approved ONNX image/text embedding provider and runtime validation exist.

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
- `dotnet test Images.sln -c Release --no-build` - passed 387 tests after the V7-31 semantic-search foundation coverage was added.
- `dotnet test Images.sln -c Release --filter ExportCapabilityWarningServiceTests` - passed 5 focused V7-14 capability-warning tests.
- `dotnet test Images.sln -c Release --filter "ImageColorAnalysisServiceTests|ColorAnalysisControllerTests"` - passed 5 focused V7-15 color-analysis tests.
- `dotnet test Images.sln -c Release --filter RecoveryCenterServiceTests` - passed 5 focused V7-16 recovery-center tests.
- `dotnet test Images.sln -c Release --filter "ImageCommands_AreDisabledUntilImageIsLoaded|RecoveryCenterServiceTests|ImageColorAnalysisServiceTests|ColorAnalysisControllerTests"` - passed 11 focused tests after delete-sharing hardening.
- `dotnet test Images.sln -c Release --filter "ModelManagerServiceTests|CodecCapabilityServiceTests"` - passed 7 focused V7-30 model-manager and provenance tests.
- `dotnet test Images.sln -c Release --filter SemanticSearchServiceTests` - passed 4 focused V7-31 semantic-index foundation tests.
- `dotnet test Images.sln -c Release --filter "ModelManagerServiceTests|SemanticSearchServiceTests|CodecCapabilityServiceTests"` - passed 11 focused tests after adding Qdrant CLIP registry pins.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Test-ReleaseReadiness.ps1 -Version 0.2.11` - passed.
- `src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.exe --system-info` - exited 0 in the local shell smoke.
- `src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.exe --codec-report` - exited 0 in the local shell smoke.
- `dotnet src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.dll --system-info` - printed system/runtime diagnostics including SharpCompress 0.48.1.0 and Ghostscript 10.07.0.
- `dotnet src\Images\bin\Release\net9.0-windows10.0.22621.0\Images.dll --codec-report` - printed the codec capability report, dependency provenance rows, advisory status, and runtime/model action copy.
