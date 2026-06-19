# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## Research-Driven Additions

### P0

- [ ] P0 — Repair release-readiness tracker hygiene checks
  Why: Release validation still requires `PROJECT_CONTEXT.md` and a roadmap reference to it, contradicting current repo hygiene and blocking valid releases.
  Evidence: `scripts/Test-ReleaseReadiness.ps1`; `docs/release-checklist.md`; `AGENTS.md`; live `ROADMAP.md`.
  Touches: `scripts/Test-ReleaseReadiness.ps1`; `docs/release-checklist.md`; tests for release-readiness script behavior.
  Acceptance: Release readiness validates the current roadmap/blocked-file policy without requiring `PROJECT_CONTEXT.md`; stale `net9`/`0.1.x` release-support references are removed or corrected; the script has a regression test or documented local check.
  Complexity: S

### P1

- [ ] P1 — Add SBOM and artifact attestations to release artifacts
  Why: Unsigned ZIP/installer releases have checksums and dependency logs but no cryptographic build provenance or SBOM attestation.
  Evidence: `.github/workflows/release.yml`; `docs/distribution-trust.md`; GitHub artifact attestation docs; ImageGlass unsigned beta/code-signing discussion.
  Touches: `.github/workflows/release.yml`; release diagnostics scripts; package-manifest upload flow.
  Acceptance: Release workflow emits a CycloneDX or SPDX SBOM, uploads it with release diagnostics, and creates GitHub build/SBOM attestations for ZIP, setup EXE, checksums, and package manifests where repository plan support allows.
  Complexity: M

- [ ] P1 — Convert WPF smoke tests into a reliable CI gate
  Why: `tests/Images.Tests/WpfSmokeTests.cs` exists but CI marks the smoke step `continue-on-error`, so rendered regressions do not block merges.
  Evidence: `.github/workflows/ci.yml`; `tests/Images.Tests/WpfSmokeTests.cs`; PicView and nomacs issue evidence around resized dialogs and stale metadata panels.
  Touches: `.github/workflows/ci.yml`; `tests/Images.Tests/WpfSmokeTests.cs`; app launch/test diagnostics helpers.
  Acceptance: A stable smoke subset is required in CI on Windows; failures upload logs/screenshots or UIA dumps; exploratory/flaky smoke scenarios are separated from the required gate.
  Complexity: M

- [ ] P1 — Enforce documented UIA accessibility contracts with FlaUI
  Why: `docs/accessibility.md` documents a rich UIA tree, but tests only cover basic launch/navigation and do not assert screen-reader names, help text, live regions, or secondary windows.
  Evidence: `docs/accessibility.md`; `docs/narrator-test-matrix.md`; `tests/Images.Tests/WpfSmokeTests.cs`; Microsoft WPF accessibility platform.
  Touches: `tests/Images.Tests/WpfSmokeTests.cs`; `MainWindow.xaml`; `SettingsWindow.xaml`; `AboutWindow.xaml`; secondary dialog XAML as needed.
  Acceptance: Automated UIA smoke verifies canvas name/help text, Settings/About controls, command/search surfaces, and at least one live status region; failures identify the missing AutomationProperties contract.
  Complexity: M

- [ ] P1 — Complete XMP folder import write-through for labels, keywords, and location
  Why: The sidecar parser extracts labels, flat keywords, hierarchical keywords, and location, but folder apply currently writes only ratings.
  Evidence: `src/Images/Services/XmpSidecarImportService.cs`; `CHANGELOG.md`; digiKam sidecar settings; XnView metadata FAQ/forum workflows.
  Touches: `XmpSidecarImportService`; `ReviewLabelService`; `TagGraphService`; catalog refresh; import command UI/tests.
  Acceptance: Folder import preview reports ratings, labels, tags, location, unmatched files, and skipped fields; apply writes supported values to Images sidecars/catalog without touching originals; tests cover digiKam and Lightroom/XnView-style sidecars.
  Complexity: M

- [ ] P1 — Add local model runtime validation and explicit fallback reasons
  Why: Model manager verifies hashes, but CLIP provider creation catches all runtime/session/tokenizer/preprocessor failures and silently falls back to deterministic metadata embeddings.
  Evidence: `src/Images/Services/ModelManagerService.cs`; `src/Images/Services/ClipEmbeddingProvider.cs`; Windows ML execution-provider docs; Immich ML failure discussions.
  Touches: `ModelManagerService`; `ClipEmbeddingProvider`; `SemanticSearchService`; `ModelManagerWindow`; semantic-search status UI/tests.
  Acceptance: A Validate action runs deterministic text and image smoke inputs against installed model artifacts, records provider/device/output shape, and surfaces exact failure/fallback reasons in Model Manager and Semantic Search.
  Complexity: M

- [ ] P1 — Add unified local data management and privacy controls
  Why: Privacy docs list many local stores, but users can only open a few folders and clear some caches from scattered surfaces.
  Evidence: `docs/privacy-policy.md`; `SettingsWindow.xaml`; `AboutWindow.xaml`; Mylio local-first positioning; Squoosh local-processing privacy model.
  Touches: `SettingsViewModel`; `SettingsWindow.xaml`; storage services for logs, thumbnails, catalog, semantic index, models, recovery, wallpaper, and email drafts.
  Acceptance: Settings Diagnostics shows per-store size/path/purpose and offers reveal, clear, and export actions with confirmations; factory reset is separate from cache/log deletion; no image originals are deleted.
  Complexity: M

- [ ] P1 — Add redacted support bundle export
  Why: Diagnostics can copy system info and codec reports, but support/debugging still requires users to manually gather logs, provenance, network history, and recovery state.
  Evidence: `AboutWindow.xaml`; `DiagnosticsStatusService`; `RecoveryCenterService`; commercial support/trust patterns.
  Touches: `AboutWindow`; `DiagnosticsStatusService`; `NetworkEgressService`; `RecoveryCenterService`; CLI report helpers.
  Acceptance: A single action writes a temp ZIP/text bundle containing system info, codec report, runtime provenance, redacted settings, recent network log summary, recovery records, crash-log index, and no image bytes; the result is revealed in Explorer.
  Complexity: M

### P2

- [ ] P2 — Add incremental catalog and semantic rescan staging
  Why: Catalog rebuild currently recursively hashes all candidates, clears the catalog, and inserts current rows, which can be slow and brittle for large libraries.
  Evidence: `src/Images/Services/CatalogService.cs`; `src/Images/Services/SemanticSearchService.cs`; nomacs large-folder startup issue; Mylio offline dynamic-search model.
  Touches: `CatalogService`; `CatalogQueryService`; `SemanticSearchService`; catalog schema/tests; semantic search UI status.
  Acceptance: Rescan reuses unchanged path/size/mtime/fingerprint rows, stages changes before swap, reports reused/updated/failed counts, and cancellation preserves the last good catalog/index.
  Complexity: L

- [ ] P2 — Replace nonsemantic XAML color literals with theme tokens
  Why: Hardcoded alpha colors remain in main and secondary windows, increasing light/high-contrast drift despite strong theme dictionaries.
  Evidence: `MainWindow.xaml`; `SettingsWindow.xaml`; `AboutWindow.xaml`; `FileHealthScanWindow.xaml`; `ImportInboxWindow.xaml`; `DuplicateCleanupWindow.xaml`; `RecoveryCenterWindow.xaml`; `ModelManagerWindow.xaml`; WPF high-contrast theme behavior.
  Touches: theme dictionaries; shared styles; affected XAML surfaces; theme tests/smoke.
  Acceptance: Raw hex outside theme dictionaries is limited to intentional annotation swatches and transparent hit-test overlays; light/dark/high-contrast smoke opens Settings, About, and one status-heavy tool without unreadable text or invisible icons.
  Complexity: M

- [ ] P2 — Validate generated WinGet and Scoop manifests in release workflow
  Why: The release workflow generates package-manager manifests but only uploads them; distribution trust docs still call out validation and install smoke as future follow-ups.
  Evidence: `.github/workflows/release.yml`; `scripts/New-PackageManifests.ps1`; `docs/distribution-trust.md`; WinGet/Scoop submission docs.
  Touches: release workflow; package manifest script/tests; release diagnostics logs.
  Acceptance: Release workflow validates generated WinGet manifests when tooling is available, validates the Scoop JSON schema/URL/hash fields, runs a portable install/launch smoke from the generated Scoop manifest or an equivalent local check, and uploads validation logs.
  Complexity: M

- [ ] P2 — Add deprecated/outdated package maintenance gate
  Why: Live NuGet checks show no vulnerable packages, but xUnit v2 is deprecated and transitive runtime packages have available updates; CI only gates vulnerabilities.
  Evidence: `tests/Images.Tests/Images.Tests.csproj`; live `dotnet list package --deprecated`; live `dotnet list package --outdated --include-transitive`; xUnit v3 guidance.
  Touches: test project package references; `.github/workflows/ci.yml`; `.github/workflows/security.yml`; dependency audit docs/scripts.
  Acceptance: CI reports deprecated/outdated packages separately from CVEs; xUnit is migrated to v3 or explicitly exempted with a tracked reason; release diagnostics include the maintenance report without blocking security-only patches unnecessarily.
  Complexity: M

### P3

- [ ] P3 — Support multi-path launch sessions from the command line
  Why: Power users and external tools expect a viewer to open an explicit ad hoc set of images, not only infer siblings from one folder.
  Evidence: ImageGlass multi-path issue; qView minimal viewer behavior; current `DirectoryNavigator` folder-root model.
  Touches: app launch parsing; `DirectoryNavigator`; recent session/navigation state; smoke tests.
  Acceptance: `Images.exe a.jpg b.png c.webp` opens a session containing exactly those files in argument order, supports next/previous/Home/End, and falls back to current folder navigation for a single path.
  Complexity: M
