# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, GUI/visual validation, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when those are unblocked.

## Actionable Work

The remaining research item is decision-gated (Ghostscript-source AGPL mechanism) and lives in `Roadmap_Blocked.md`. Refill via a research pass (`RESEARCH.md`) when ready.

## Research-Driven Additions

Unblocked, net-new items only. HDR display (V100-06), full color management (V100-05), GPS map overlay (V20-23), and the Explorer thumbnail handler (V70-04) remain correctly parked in `Roadmap_Blocked.md` and are not duplicated here.

### 2026-07-16 research pass (V110 block)

Continues the `V###-##` scheme (V110 is the next free hundred-block above V100). Evidence lives in `RESEARCH.md` (2026-07-16). Most 2024-2026 competitor "gaps" were verified as already shipped (compare, loupe, motion/live photo, continuous webtoon archive reading, perspective correction, PDF export, codec-install prompts, UTF-8 Exif) and are intentionally absent below.

#### P0 — Security servicing

- [ ] P0 — Take the July 2026 .NET/SQLite servicing bump (V110-01)
  Why: `Microsoft.Data.Sqlite` 10.0.9 → 10.0.10 lands with the .NET 10.0.10 wave that fixes 17 CVEs including 3 critical RCEs; the app does HTTPS update checks so the TLS/HTTP surfaces are reachable.
  Evidence: RESEARCH.md Security §; https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-july-2026-servicing-updates/ ; `src/Images/Images.csproj`
  Touches: `src/Images/Images.csproj`, `global.json`, `packages.lock.json`, `scripts/Test-ReleaseReadiness.ps1`
  Acceptance: csproj pins Microsoft.Data.Sqlite 10.0.10, SDK/runtime resolves 10.0.10, lockfile regenerated, `dotnet list --vulnerable` clean, release-readiness green.
  Complexity: S

#### P1 — Data safety, trust, and high-value inspection

- [ ] P1 — Fix catalog shared-cache SQLite lock hazard (V110-02)
  Why: `CatalogService.Open` uses `SqliteCacheMode.Shared` while `SemanticSearchService` uses `Private`; under shared-cache + WAL a background `Rebuild` write transaction can raise table-level `SQLITE_LOCKED` on a concurrent UI read, which `busy_timeout` does not retry, and `GetByPath`/`GetAllAssets` swallow the exception → the catalog silently appears empty.
  Evidence: RESEARCH.md Security §; `CatalogService.cs:89`, `CatalogService.cs:155-171`, `SemanticSearchService.cs:102`
  Touches: `src/Images/Services/CatalogService.cs`
  Acceptance: catalog connections open with `Private` cache; a test that runs a `Rebuild` write transaction concurrent with `GetAllAssets` returns full rows (no empty result, no swallowed `SQLITE_LOCKED`).
  Complexity: S

- [ ] P1 — Fix import rollback that can strand a moved original (V110-03)
  Why: `RollBackFailedTransfer` only restores a moved original when the source path is free; if the source slot is re-occupied and a post-import edit throws, the file is left only at the destination while the UI reports failure — perceived data loss.
  Evidence: RESEARCH.md Security §; `ImportInboxService.cs:325-330`
  Touches: `src/Images/Services/ImportInboxService.cs`
  Acceptance: when the source path is occupied during rollback, the original is restored to a unique sibling and the recovered location is surfaced; a test with an occupied source slot proves no file is stranded.
  Complexity: S

- [ ] P1 — Route animated decode through the native security policy seam (V110-04)
  Why: `TryLoadAnimated` builds `new MagickImageCollection(bytes)` directly instead of via `MagickSafeReader`/`CodecRuntime.Configure()`; a future caller reaching it before `Load`/`Preflight` would decode untrusted bytes before the coder allowlist and resource limits install.
  Evidence: RESEARCH.md Security §; `ImageLoader.cs:689`, `ImageLoader.cs:104`
  Touches: `src/Images/Services/ImageLoader.cs`, `src/Images/Services/MagickSafeReader.cs`
  Acceptance: `TryLoadAnimated` calls `CodecRuntime.Configure()` (or a new `MagickSafeReader.ReadCollection`) before decode; a test asserts the policy is initialized on the animated path.
  Complexity: S

- [ ] P1 — HDR gain-map read-only inspection (V110-05)
  Why: ISO 21496-1:2025 gain maps are now written by Adobe (LrC v17), Apple (iOS 18/Sequoia), and Google (Android 15 UltraHDR); Windows WIC silently ignores them and no mainstream Windows viewer surfaces them — a clean differentiation win that fits the existing read-only metadata-inspection philosophy.
  Evidence: RESEARCH.md Architecture §; https://www.iso.org/standard/86775.html ; https://en.wikipedia.org/wiki/Ultra_HDR ; grep confirms no gain-map code exists
  Touches: new `src/Images/Services/GainMapInspectionService.cs`, `ImageMetadataService.cs`/metadata panel, `PhotoMetadataController.cs`
  Acceptance: for a UltraHDR/ISO-21496-1/Apple gain-map file, the inspector reports gain-map presence, flavor, and min/max content boost, and renders the gain map as a grayscale layer; files without a gain map report absence cleanly. Read-only; no writeback. (HDR *display* stays blocked in `Roadmap_Blocked.md`.)
  Complexity: L

- [ ] P1 — Upgrade SharpCompress 0.49.1 → 0.50.0 behind an archive regression gate (V110-06)
  Why: 0.50.0 reduces LZMA/RAR decode allocation (direct CBR/CBZ benefit) and fixes Zip64 non-seekable streaming + entry-metadata corruption, but breaks Tar auto-decompress and the Detection API — must not be a blind bump.
  Evidence: RESEARCH.md Security §; https://github.com/adamhathcock/sharpcompress/releases/tag/0.50.0 ; `src/Images/Services/ArchiveBookService.cs`
  Touches: `src/Images/Images.csproj`, `packages.lock.json`, `src/Images/Services/ArchiveBookService.cs`, archive tests
  Acceptance: package at 0.50.0; CBZ, CBR, 7z, and any tar path open correctly under new regression fixtures (explicit-decompress for tar, updated detection); no truncated-CBZ claim added.
  Complexity: M

#### P2 — Scale, decomposition, and confirmed UX gaps

- [ ] P2 — Scale semantic search off the full-table linear scan (V110-07)
  Why: `Search` selects every row for the active model (no SQL `LIMIT`), deserializes each 512-dim blob, and dot-products in managed code on the calling thread on every query — re-reads/re-scores the whole index each time and will not scale past a few thousand assets.
  Evidence: RESEARCH.md Architecture §; `SemanticSearchService.cs:194-235`
  Touches: `src/Images/Services/SemanticSearchService.cs`
  Acceptance: normalized vectors are cached in memory keyed by index generation (or candidate set capped / ANN-indexed); a repeat query on an unchanged index performs zero blob re-reads; results are unchanged.
  Complexity: M

- [ ] P2 — Bound `TileService.BuildLocks` growth (V110-08)
  Why: one lock object is added per distinct huge-image path opened this session and never removed, a slow leak in an otherwise carefully-bounded service.
  Evidence: RESEARCH.md Architecture §; `TileService.cs:58`, `TileService.cs:147`
  Touches: `src/Images/Services/TileService.cs`
  Acceptance: build-lock entries are removed in the build `finally` (or via a size-capped/expiring map) while preserving same-cache mutual exclusion; a test browsing many pyramids shows the map does not grow unbounded.
  Complexity: S

- [ ] P2 — Continue extracting the `MainViewModel` god object (V110-09)
  Why: ~8,300 lines / 318 KB owning 20+ services and 4 timers is a change-magnet where any edit risks unrelated regressions (memory notes confirm parallel-agent work on this tree); the team's existing `*Controller` extraction pattern is the proven path.
  Evidence: RESEARCH.md Architecture §; `ViewModels/MainViewModel.cs`
  Touches: `src/Images/ViewModels/MainViewModel.cs`, new `Slideshow*/ArchiveReader*/RenameEditor*Controller.cs`
  Acceptance: slideshow, archive-reader, and rename-editor state/commands move into dedicated controllers behind the existing `_uiDispatcher`/`() => _isDisposed` convention; behavior and tests unchanged; `MainViewModel` shrinks measurably.
  Complexity: L

- [ ] P2 — Detect JXL lossless-JPEG transcode in the inspector (V110-10)
  Why: JPEG-to-JXL lossless recompression is a headline JXL capability no viewer surfaces; distinguishing transcoded vs native codestream vs ISOBMFF container is a cheap read-only add on top of the existing metadata panel.
  Evidence: RESEARCH.md Architecture §; https://en.wikipedia.org/wiki/JPEG_XL ; `src/Images/Services/Exif31MetadataReader.cs`
  Touches: `src/Images/Services/ImageMetadataService.cs`/metadata panel, `SupportedImageFormats.cs`
  Acceptance: opening a `.jxl` reports whether it is a JPEG-transcode, a native lossy/lossless codestream, or an ISOBMFF container; non-JXL files are unaffected.
  Complexity: M

- [ ] P2 — Invert-colors display toggle (V110-11)
  Why: a quick invert toggle is both an inspection aid (reading low-contrast scans/negatives) and an accessibility affordance; ImageGlass ships it and Images has no display-time color inversion (only transform-matrix math).
  Evidence: RESEARCH.md Competitive §; https://github.com/d2phap/ImageGlass/releases ; grep shows no invert-display path
  Touches: `src/Images/Controls/ZoomPanImage.cs`, `src/Images/ViewModels/MainViewModel.cs`, `CommandShortcutService.cs`
  Acceptance: a command/hotkey toggles a non-destructive inverted rendering of the current image; state clears on navigation; export is unaffected.
  Complexity: S

- [ ] P2 — Jump to next/previous archive without leaving the reader (V110-12)
  Why: readers browsing a folder of CBZ/CBR want to roll into the next archive at the last page (and previous at the first) without returning to the folder; PicView ships this and continuous archive reading already exists here.
  Evidence: RESEARCH.md Competitive §; https://github.com/Ruben2776/PicView/releases ; `src/Images/Services/ArchiveBookService.cs`, `src/Images/Services/DirectoryNavigator.cs`
  Touches: `src/Images/Services/ArchiveBookService.cs`, `src/Images/ViewModels/MainViewModel.cs`, `DirectoryNavigator.cs`
  Acceptance: at the last page of an archive, next advances into the first page of the next archive in natural folder order (and symmetrically for previous); wraps or stops per existing navigation semantics; non-archive navigation unchanged.
  Complexity: M

- [ ] P2 — Hold-to-scrub rapid folder fly-through (V110-13)
  Why: holding an arrow to fly through a large folder with instant decode is repeatedly praised (FlyPhotos) and directly answers the "per-image reload stutter" complaint; Images has preload but no explicit accelerated scrub mode.
  Evidence: RESEARCH.md Competitive §; https://www.makeuseof.com/free-windows-photos-app-replacement/
  Touches: `src/Images/MainWindow.xaml.cs`, `src/Images/ViewModels/MainViewModel.cs`, `src/Images/Services/PreloadService.cs`
  Acceptance: holding Next/Prev accelerates navigation using thumbnail/preloaded frames without full-decode stutter and settles on a full decode when the key is released; single presses behave as before.
  Complexity: M

#### P3 — Hardening and hygiene

- [ ] P3 — Constant-time listen-mode token compare + tighten the connection cap (V110-14)
  Why: `ListenService` uses byte-by-byte `string.Equals` for the session token (theoretical timing side-channel) and increments `_activeConnections` inside the handler so a burst can transiently exceed the cap of 8; both are low-risk given loopback-only bind + rate limiting but are cheap to harden.
  Evidence: RESEARCH.md Security §; `ListenService.cs:141`, `ListenService.cs:89-111`
  Touches: `src/Images/Services/ListenService.cs`
  Acceptance: token comparison uses `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes; the active-connection counter is incremented in the accept loop before the handler is spawned and decremented in the handler's finally.
  Complexity: S

- [ ] P3 — Dispose the continuous-archive decode gate + Picasa tag-drift prune (V110-15)
  Why: `_continuousArchiveDecodeGate` (`MainViewModel.cs:59`) is the one `SemaphoreSlim` missed by an otherwise thorough `Dispose`; separately, `PicasaImportService` unions `dc:subject`/`lr:hierarchicalSubject` on re-import so renamed albums accumulate stale tags. Both are small correctness/hygiene fixes.
  Evidence: RESEARCH.md Architecture §; `MainViewModel.cs:59`, `PicasaImportService.cs:519-540`
  Touches: `src/Images/ViewModels/MainViewModel.cs`, `src/Images/Services/PicasaImportService.cs`
  Acceptance: `MainViewModel.Dispose` disposes the semaphore; Picasa-authored tags are namespaced so a re-import prunes its own prior additions; a re-import-after-rename test shows no stale-tag accumulation.
  Complexity: S
