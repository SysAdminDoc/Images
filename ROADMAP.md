# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, external model downloads, signing identity, Store/WinGet account setup, or unresolved renderer/runtime decisions stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

The feature set already rivals ImageGlass and nomacs. What's missing for a `1.0` label is release infrastructure and quality gates, not features. These items define the boundary:

1. Code signing (D-05 in Roadmap_Blocked.md — needs Azure Artifact Signing credentials)
2. WinGet + Scoop publication (D-02 in Roadmap_Blocked.md — needs first manual submission)

Promote to `1.0.0` when unblocked **and** the P1 items in the audit backlog below are drained.

## Deep Audit Backlog (2026-07-07)

Findings from a 6-agent parallel deep audit (services/file-IO, security/process, concurrency/caching, ViewModel, window code-behind, XAML/theme/a11y). Every item was verified against the code by a read-only auditor; confidence is noted where it matters. None are fixed yet.

### Instructions for the AI working this backlog

1. Read `CLAUDE.md` and `AGENTS.md` first. Baseline at handoff: `dotnet build Images.sln -c Release` green (0 errors, 4 warnings — see AUD-18); re-run `dotnet test Images.sln -c Release` to confirm the test baseline before changing anything.
2. Work top-down by priority. Fix in logical batches (one commit per theme, e.g. "sidecar carry", "semantic-search hardening"), conventional-commit messages, no AI authorship anywhere in git.
3. For every fix: add a focused regression test in `tests/Images.Tests` where the seam allows it, run the full suite, then update `CHANGELOG.md` and delete the item from this file. Git history is the record of completed work — never mark items `[x]`.
4. Never create new markdown files (no audit reports/summaries). Only `README.md` and `CHANGELOG.md` are git-tracked docs; this file and all other .md are gitignored — never `git add -f` them.
5. UI-affecting fixes must be verified with the serialized WPF smoke suite; never launch the GUI in the foreground (use the background/UIA path per global rules).
6. Do NOT re-flag these dismissed findings (checked and rejected 2026-07-07):
   - SQLite injection anywhere — all queries are parameterized.
   - `WriteAtomically` temp-extension handling, `ReadStableFileBytes` truncation race, ImportInbox move/copy rollback ordering, ImageFileTransferService rollback ordering — all sound.
   - ExifTool/jpegtran/c2patool process runners' stdout/stderr drain — correct (async read before wait). Only the Ghostscript version probe has the anti-pattern (AUD-36).
   - argv accepting UNC paths (`App.xaml.cs:183`) — intentional; users legitimately open images from network shares. Do not port ListenService's UNC rejection to argv.
   - `BoolToVis` treating non-zero ints as true — relied upon by `RecentFolders.Count` bindings; only the `UnsetValue→Visible` arm is a bug (AUD-56).
   - PreloadService CTS choreography, BatchProcessorService semaphore/WhenAll pattern, overlay attach/detach symmetry, `ImageViewportTransform` math (algebraically verified vs the render stack), UpdateCheckService URL pinning, ArchiveBookService zip-slip guards, ListenService auth token — all verified clean.

### P1 — data loss / crash / charter violation

- [ ] P1 AUD-01 — Rename orphans XMP sidecars (metadata loss + wrong re-attachment)
  Why: Ratings, labels, tags, and edit stacks live in `image.ext.xmp` / `stem.xmp` sidecars. `RenameService.Commit`/`Revert` move only the image, so renaming discards all metadata from the UI and the orphan sidecar later re-attaches to any new file with the old name. Same gap in `FileHealthScanService.RenameToSuggestedExtension`/`Quarantine` and `DuplicateCleanupService.Quarantine`.
  Where: `src/Images/Services/RenameService.cs:135-174,180-212`; `src/Images/Services/FileHealthScanService.cs:138-198`; `src/Images/Services/DuplicateCleanupService.cs:215-217`.
  Fix (designed, partially prototyped then reverted for handoff): add `src/Images/Services/SidecarCompanionFiles.cs` — static `TryMoveAlongside(oldImagePath, newImagePath)` that best-effort-moves both sidecar forms (`path + ".xmp"`, `dir/stem.xmp`), never throws (primary move already succeeded), never overwrites an existing destination, and returns `IReadOnlyList<RecoverySidecarMove>` (type already exists in `RecoveryCenterService.cs:28`). Call it after each successful `File.Move` in the four sites. Thread the returned moves into recovery records: `RecoveryCenterService.RecordRename`/`RecordQuarantine` need an optional `sidecars` param (the `AppendNew` plumbing and `RestoreSidecars` restore path already exist and work — `RecordMove` uses them); widen `FileHealthActionResult` and `DuplicateCleanupMoveResult` with a `Sidecars` list and pass through at `DuplicateCleanupWindow.xaml.cs:229` and `FileHealthScanWindow.xaml.cs:139,171`. Test seam: RenameService is directly unit-testable with a temp dir.

- [ ] P1 AUD-02 — Rename commit during in-flight async load corrupts the navigator list
  Why: During a slow decode (RAW/archive), `_nav` already points at file B while `CurrentPath` still holds A. The rename box is enabled (`IsEnabled={Binding HasImage}`, not busy-gated) and `CommitRenameCommand` has no busy guard, so `FlushPendingRename` renames A on disk then calls `_nav.UpdateCurrentPath(newPath)` which writes A's new path into **B's slot** (`_files[CurrentIndex] = fullPath`). B is dropped from the list, the in-flight load fails its stale-path guard, viewer goes blank. `UndoOne` has the same unguarded channel.
  Where: `src/Images/ViewModels/MainViewModel.cs:4869-4899` (FlushPendingRename), `:263,266` (commands), `:3595-3625` (UndoOne); `src/Images/Services/DirectoryNavigator.cs:309-325` (UpdateCurrentPath).
  Fix: give `DirectoryNavigator` an `UpdateCurrentPath(oldPath, newPath)` overload that locates `oldPath`'s index (OrdinalIgnoreCase) instead of trusting `CurrentIndex`, and have the VM pass the pre-rename path; additionally gate `CommitRenameCommand`/rename-timer flush on `!IsOperationBusy` for belt-and-braces. Add a DirectoryNavigator unit test: open folder, move index, update-by-old-path, assert both slots.

- [ ] P1 AUD-03 — ONNX session use-after-dispose when closing Semantic Search mid-index
  Why: `Closed` cancels the CTS then immediately disposes `SemanticSearchService` → native `InferenceSession.Dispose()` while the `Task.Run(Rebuild)` thread is very likely inside `_visionSession.Run(...)` (cancellation only observed between assets). Native use-after-free: worst case AV process crash; best case ObjectDisposedException silently swallowed by the per-asset catch.
  Where: `src/Images/SemanticSearchWindow.xaml.cs:52-60,103`; `src/Images/Services/ClipEmbeddingProvider.cs:123-161,173-179`; `src/Images/Services/SemanticSearchService.cs:127,144`.
  Fix: track the in-flight index task in the window; on `Closed`, cancel, then dispose in a continuation after the task completes (`_indexTask.ContinueWith(_ => service.Dispose())` on the threadpool) instead of synchronously. Inside `ClipEmbeddingProvider`, serialize `Dispose` with active `EmbedImage/EmbedText` calls (a `ReaderWriterLockSlim` or ref-count gate) so disposal can never interleave a native Run.

- [ ] P1 AUD-04 — c2patool inspection can trigger unlogged network egress from untrusted file content
  Why: C2PA inspection auto-runs on every image open when c2patool is available. c2pa-rs fetches **remote manifests** referenced inside the asset by default, so a crafted JPEG causes an HTTP request to an attacker-controlled host the moment it is viewed — a tracking beacon that bypasses the app's "all egress is opt-in and logged" charter. Only trust-anchor env vars are pinned; nothing disables remote fetch and nothing records the spawn in `NetworkEgressService`.
  Where: `src/Images/Services/C2paManifestService.cs:199-249` (spawn, env at :210-211); `src/Images/ViewModels/C2paInspectionController.cs:69-91`; auto-invoke at `MainViewModel.cs:4622`.
  Fix: pass a c2patool settings file / flag that disables remote manifest fetching (verify the exact mechanism against the bundled/documented c2patool version — confidence on default-fetch behavior is medium-high, version-dependent), and wrap the invocation in a `NetworkEgressService` record so the run is visible in the egress log either way. If remote fetch cannot be reliably disabled for the installed tool version, gate auto-inspection behind the existing network opt-in setting.

### P2 — correctness / security / prominent UX

- [ ] P2 AUD-05 — One malformed pre-existing sidecar aborts a whole import batch and loses completed results
  Why: `WriteRatingSidecar` does `XDocument.Load` on an existing sidecar; `XmlException` is not in the per-request catch filter, so `Commit` throws — the `imported` list is discarded even though files were already moved (originals gone with `MoveOriginal: true`), and remaining requests never process.
  Where: `src/Images/Services/ImportInboxService.cs:220` (catch filter), `:466` (WriteRatingSidecar).
  Fix: add `XmlException` to the catch filter so one bad sidecar fails one request, not the batch. Test with a corrupt `.xmp` fixture.

- [ ] P2 AUD-06 — "Location cleared." is a silent no-op
  Why: `WriteState` only touches location attributes inside `if (state.Location.HasAnyField)`; clearing skips attribute removal, sidecar keeps City/State/Country/Location, yet the UI reports success.
  Where: `src/Images/Services/ReviewLabelService.cs:164-172`.
  Fix: when the location is empty, explicitly remove the four attributes (`photoshop:City/State/Country`, `Iptc4xmpCore:Location`). Unit-testable directly.

- [ ] P2 AUD-07 — Picasa face regions with leading-zero rect64 values silently dropped
  Why: Picasa omits leading zeros in `rect64(...)` (documented quirk) so real values are 1-15 hex digits; both `FaceRegex` (`{16}`) and `TryParseRect64` (`Length != 16`) reject them — faces near the top/left edge vanish with no failure count.
  Where: `src/Images/Services/PicasaImportService.cs:44,417-421`.
  Fix: accept 1-16 hex digits and left-pad to 16 before parsing. Add fixture cases.

- [ ] P2 AUD-08 — GPS-strip import contract fails open for unreadable files
  Why: For non-rewritable formats the import gate relies on `PreviewStrip`, which catches all exceptions and returns `RemovedCount 0` — a file Magick can't parse sails through and imports with GPS intact, violating the explicit fail-closed comment.
  Where: `src/Images/Services/ImportInboxService.cs:255-260`; `src/Images/Services/MetadataEditService.cs:143-146`.
  Fix: make `PreviewStrip` distinguish "read failed" from "nothing to strip" (return a tri-state or throw a typed exception) and have the import gate treat read-failure as import-failure when GPS stripping was requested.

- [ ] P2 AUD-09 — Directory junction/symlink cycles cause runaway traversal in three scanners
  Why: Duplicate scan, import-inbox scan, and catalog rebuild recurse without skipping `FileAttributes.ReparsePoint`; a junction cycle re-hashes the same tree at ever-deepening paths until the ~32K path limit errors out. `FileHealthScanService.cs:349-353` already contains the correct guard with a comment describing exactly this hazard — copy it.
  Where: `src/Images/Services/DuplicateCleanupService.cs:356-364`; `src/Images/Services/ImportInboxService.cs:331` (`SearchOption.AllDirectories` — needs manual recursion or `EnumerationOptions` with attribute filter); `src/Images/Services/CatalogService.cs:712-717` (`RecurseSubdirectories = true` — use `EnumerationOptions.AttributesToSkip |= ReparsePoint`).

- [ ] P2 AUD-10 — SemanticSearchWindow: unguarded SQLite/ONNX exceptions crash the app
  Why: Dispatcher unhandled = process death by design (`App.ReportDispatcherUnhandledException` returns unhandled). Three escape paths: `IndexButton_Click` (async void) catches only OCE but `Rebuild`'s SQLite write phase has no guard (locked/corrupt `semantic-index.db` → SqliteException → crash); `ClearIndexButton_Click` calls `Clear()` (raw SQLite, no try) with no catch; `RunSearch` calls `_provider.EmbedText(query)` *before* the service's try block, so a CLIP/ORT failure propagates into a sync click handler.
  Where: `src/Images/SemanticSearchWindow.xaml.cs:83-120,137-144,212-242`; `src/Images/Services/SemanticSearchService.cs:150-160,180,293-302`.
  Fix: broaden the window handlers' catches (SqliteException/InvalidOperationException/OnnxRuntimeException → status text), and move `EmbedText` inside the service's guarded region.

- [ ] P2 AUD-11 — `SqliteCacheMode.Shared` defeats WAL concurrency in SemanticSearchService and SettingsService
  Why: With shared cache, a reader hitting a writer's table lock gets SQLITE_LOCKED immediately — `busy_timeout` does not apply to shared-cache table locks. Search-during-rebuild silently returns "no matches"; a background settings writer can make a UI-thread `GetString` return the default. WAL was chosen precisely for concurrent readers; shared cache defeats it.
  Where: `src/Images/Services/SemanticSearchService.cs:97-104`; `src/Images/Services/SettingsService.cs:54-60`.
  Fix: switch both to `SqliteCacheMode.Private` (default). Run the full suite — settings tests exercise this heavily.

- [ ] P2 AUD-12 — Tile-cache eviction can delete the displayed pyramid (single `_activeCacheDir` slot)
  Why: Any secondary-surface load of a huge image (Duplicate Cleanup/FileHealth/ReferenceBoard/ImportInbox previews, Compare) overwrites the single `_activeCacheDir` and fires eviction; when over the 1 GB cap, eviction deletes the main viewer's pyramid mid-display → blank/gray tiles. Worse: deleting a directory mid-build lets `WritePyramidInfo` publish a manifest over missing tiles, which is then reused as a "valid" broken pyramid until 30-day eviction.
  Where: `src/Images/Services/TileService.cs:48,86,174,522-560`.
  Fix: replace the single slot with a concurrent set of protected cache dirs (add on build start/use, remove on pyramid release or LRU age-out), and have `EvictIfOverCap` skip all members plus any dir holding a live build lock.

- [ ] P2 AUD-13 — `ClipEmbeddingProvider.TryCreate` leaks the text session and SessionOptions on partial failure
  Why: If the vision-session constructor throws (corrupt/partial model — the case users retry), the already-created text session (hundreds of MB native) and `sessionOptions` are never disposed.
  Where: `src/Images/Services/ClipEmbeddingProvider.cs:73-86`.
  Fix: try/catch with disposal of any partially created sessions + options on failure; dispose `sessionOptions` after construction on success too.

- [ ] P2 AUD-14 — Slideshow tick pumps through the delete-confirmation dialog and removes the wrong entry
  Why: `DeleteCurrent` never sets `IsOperationBusy`, so while the modal confirm dialog pumps, a slideshow tick advances navigation; after confirm, `_nav.RemoveCurrent()` removes the *advanced-to* file's entry — deleted file stays listed, live file unlisted, viewer jumps two files. Same pumping window for the lossless-JPEG-trim MessageBox.
  Where: `src/Images/ViewModels/MainViewModel.cs:4965-4994` (DeleteCurrent), `:2486-2559` (tick + its own comment about exactly this), `:5026,5917` (trim confirm).
  Fix: wrap the confirm+delete in `IsOperationBusy` (or pause the slideshow timer around modal confirmations), and make `RemoveCurrent` operate on the captured path, not the current index.

- [ ] P2 AUD-15 — Crop-mode Enter steals Enter from every TextBox (rename editor, command palette)
  Why: `Window_PreviewKeyDown` applies the crop on Enter with no `Keyboard.FocusedElement is TextBox` guard (unlike `Window_KeyDown:837`). With a crop selection active, Enter in the rename box applies the crop and discards the rename; Enter in the command palette crops instead of running the selected command, and `Keyboard.ClearFocus()` yanks focus mid-edit.
  Where: `src/Images/MainWindow.xaml.cs:783-806`; wired at `MainWindow.xaml:17`.
  Fix: early-return when the focused element is a TextBox/editable control, mirroring the `Window_KeyDown` guard.

- [ ] P2 AUD-16 — Documented auto freehand-crop never engages on busy-wrapped load paths
  Why: `CanAutoStartCropMode → CanUseCrop → CanUsePixelEditTools` includes `!IsOperationBusy`, but `CompleteCurrentLoad` runs inside the `BeginOperationStatus` window for Next/Prev/First/Last, slideshow, page turns, Open dialog, and Browse Folder — so the CHANGELOG-documented "loads enter crop mode immediately" only works on sync paths (drag-drop, filmstrip click, CLI). Crop mode flips on/off depending on how you arrived at the image.
  Where: `src/Images/ViewModels/MainViewModel.cs:4683-4694,809-810,4029-4043`. The existing test (`MainViewModelStateTests.cs:941`) only covers the sync path — add an async-path regression.
  Fix: defer the auto-start until `EndOperationStatus` (or exclude the load-status flag from the auto-start gate specifically).

- [ ] P2 AUD-17 — c2patool resolved from system PATH and auto-executed on every image open
  Why: Violates the repo's own trust posture (`JpegTranRuntime.cs:8-11` "intentionally does not search PATH"). Any user-writable PATH dir (Python Scripts, npm, scoop shims) lets a lower-trust process plant `c2patool.exe` that Images then launches automatically and repeatedly. The SHA-256 at `C2paToolRuntime.cs:95-107` is display-only, not a gate.
  Where: `src/Images/Services/C2paToolRuntime.cs:66-68`.
  Fix: resolve only from the app directory / an explicit configured tool path (mirror JpegTranRuntime). Note in the About/diagnostics text how to point at an installed copy so users who relied on PATH aren't silently degraded.

- [ ] P2 AUD-18 — CA2014: stackalloc inside a loop (stack-overflow hazard over large folders)
  Why: `Span<double> luminance = stackalloc double[HashSize*HashSize]` sits inside per-file loops; stackalloc in a loop accumulates stack per iteration for the method's lifetime — thousands of files can overflow the stack. These are the 2 CA2014 build warnings.
  Where: `src/Images/Services/CullingScoreService.cs:256`; `src/Images/Services/DuplicateCleanupService.cs:416`.
  Fix: hoist the stackalloc above the loop (clear/reuse per iteration). Also consider removing the unused-looking `Microsoft.VisualBasic` NU1510 warning is a false positive — it IS used by `RecycleBinDeleteService`; leave the package, optionally suppress NU1510 with a comment.

- [ ] P2 AUD-19 — Seven buttons render the default Aero2 template (light-blue hover flash in dark theme)
  Why: No style/template on the book page-turn edge zones (112px strips over the viewport — hover paints the system `#FFBEE6FD` highlight over the dark canvas) and the 5 annotation color swatches (hover replaces swatch color entirely).
  Where: `src/Images/MainWindow.xaml:1469,1499`; `src/Images/AnnotationsWindow.xaml:120-124`.
  Fix: give the page-turn zones a minimal template that keeps the `#01000000` hit-test background with a themed subtle hover; give the swatches a template that preserves their fill with a themed selection border.

- [ ] P2 AUD-20 — Command palette is opaque to screen readers
  Why: `CommandPaletteList` has no `AutomationProperties.Name` and items announce as "Images.ViewModels.CommandPaletteItem" (Grid template, no ToString override).
  Where: `src/Images/MainWindow.xaml:2024`; `src/Images/ViewModels/CommandPaletteItem.cs`.
  Fix: add `AutomationProperties.Name` to the ListBox (localized) and override `ToString()` on `CommandPaletteItem` to return the command title (or set AutomationProperties on the item template).

- [ ] P2 AUD-21 — Unlabeled text inputs and sliders (a11y)
  Why: No `AutomationProperties.Name`/`LabeledBy`; adjacent TextBlocks are not programmatically associated. The correct `LabeledBy` pattern already exists at `BatchProcessorWindow.xaml:167/175` — these are stragglers.
  Where: `ExportPreviewWindow.xaml:84,101,104` (QualitySlider, MaxWidthBox, MaxHeightBox); `TagGraphWindow.xaml:111,113,215,217,242,244`; `ReferenceBoardWindow.xaml:199` (ZoomSlider); `CrashDialog.xaml:51` (DetailsBox, read-only, milder).
  Fix: add `AutomationProperties.LabeledBy` (preferred) or localized `.Name` to each.

- [ ] P2 AUD-22 — Hint-tier text fails WCAG AA contrast in both custom themes
  Why: `Text.Hint` (11px, ~108 uses), `SectionLabel` (10.5px), `MetaLabel` (12px) use `OverlayBrush`: Mocha `#6C7086` on `#1E1E2E` ≈ 3.4:1; Latte `#7C7F93` on `#EFF1F5` ≈ 3.5:1 — both under 4.5:1. HC is safe.
  Where: `src/Images/Themes/DarkTheme.xaml:191-211` (+ Latte overrides).
  Fix: introduce a dedicated `HintTextBrush` token (Mocha: Overlay2 `#9399B2` ≈ 5.4:1 or Subtext0 `#A6ADC8`; Latte: Subtext0 `#6C6F85` ≈ 5.5:1; HC: `ControlTextColor`) in all three dictionaries and point the three text styles at it. Keeps `OverlayBrush` for genuine decorative uses.

- [ ] P2 AUD-23 — No themed PasswordBox style (archive password prompt renders stock white)
  Why: The implicit TextBox style doesn't apply to PasswordBox; the archive-password dialog builds one in code and it renders as a stock light system field inside a Mocha-dark dialog.
  Where: theme dictionaries (no PasswordBox style anywhere); consumer at `src/Images/MainWindow.xaml.cs:566-570`.
  Fix: add an implicit `PasswordBox` style to `DarkTheme.xaml` mirroring the TextBox tokens (Dynamic brushes, caret/selection brushes included).

### P3 — reliability / polish / perf / debt

- [ ] P3 AUD-24 — Case-only renames are silently ignored
  Why: `IsSame` uses OrdinalIgnoreCase, so `img.jpg → IMG.jpg` returns without `File.Move` (which supports case-only renames on NTFS); UI reports no-op.
  Where: `src/Images/Services/RenameService.cs:106,151`.
  Fix: in `Commit`, treat ordinal-equal as no-op but ignore-case-equal-with-different-casing as a real move (the collision loop already exits correctly for this case because `IsSame(candidate, currentPath)` is true).

- [ ] P3 AUD-25 — GIF frame-delay clamp off-by-one slows 50 fps GIFs 5x
  Why: Comment says "clamp anything under 20 ms" but code is `if (ms <= 20) ms = 100;` — a legitimate 20 ms (2 cs) delay, the most common fast-GIF timing, becomes 100 ms.
  Where: `src/Images/Services/ImageLoader.cs:611-613`.
  Fix: `< 20` (browser convention promotes ≤10 ms; 20 ms is valid). Add a frame-delay unit test.

- [ ] P3 AUD-26 — FSW rename to unsupported extension leaves a ghost entry
  Why: `OnFsEvent` filters on the event's `Name` (the NEW name for `Renamed`); renaming `photo.jpg → photo.jpg.bak` externally early-returns and the navigator keeps listing the vanished `photo.jpg`.
  Where: `src/Images/Services/DirectoryNavigator.cs:521-522`.
  Fix: for `RenamedEventArgs`, also check `OldName`'s extension before the early return.

- [ ] P3 AUD-27 — Catalog incremental rescan never detects sidecar-only changes
  Why: `IsUnchanged` compares only image size+mtime; rating/tag edits in the `.xmp` leave the cached row stale forever despite `sidecar_modified_utc` being stored for exactly this purpose.
  Where: `src/Images/Services/CatalogService.cs:155-169`.
  Fix: include sidecar mtime in the unchanged check (probe both sidecar forms; treat appearance/disappearance as change).

- [ ] P3 AUD-28 — Import reports success when tag export fails; in-place branch doesn't roll back tags sidecar
  Why: `ExportSidecarTags`' failure result is discarded at `ImportInboxService.cs:234` (TagGraphService swallows Xml/IO internally), so imports with failed tag writes are recorded fully imported. In the SamePath branch (:190-194) an already-written tags sidecar is not rolled back when a later rating/GPS step fails (move/copy branch does roll back).
  Where: `src/Images/Services/ImportInboxService.cs:190-194,234`; `src/Images/Services/TagGraphService.cs:325-328`.
  Fix: check the export result and fail the request (with rollback where possible); align in-place rollback with the move/copy branch.

- [ ] P3 AUD-29 — Recovery records beyond the newest 200 become unrestorable; JSONL grows unbounded
  Why: `Restore`/`ResolveRevealPath` search `ListRecent()` with default `maxCount=200`; older quarantine entries return NotFound even though the file still exists. Each restore appends another record, accelerating aging-out.
  Where: `src/Images/Services/RecoveryCenterService.cs:233,255` (+ log growth at :348-363).
  Fix: `Restore`/`ResolveRevealPath` should search unbounded (or look up by id directly); add log compaction (rewrite keeping the last N per id or records younger than a retention window).

- [ ] P3 AUD-30 — Namespace-blind "Rating" fallback misreads MicrosoftPhoto:Rating (0-99 scale); rating clear misses element form
  Why: The fallback matches any element/attribute with local-name "Rating" in any namespace — `MicrosoftPhoto:Rating="50"` (3 stars) clamps to 5 stars. Clearing via `SetAttributeValue(xmp+"Rating", null)` never removes an element-form `<xmp:Rating>` from other tools.
  Where: `src/Images/Services/XmpSidecarImportService.cs:305-316`; `src/Images/Services/CatalogService.cs:927-948`; `src/Images/Services/ReviewLabelService.cs:154,175-196`.
  Fix: when the matched namespace is MicrosoftPhoto, map 0-99 → 0-5 (Microsoft's documented bands: 1→1, 25→2, 50→3, 75→4, 99→5); on clear, also remove element-form ratings.

- [ ] P3 AUD-31 — Wrong archive password surfaces as an unhandled CryptographicException
  Why: `IsArchiveReadException` covers InvalidDataException/SharpCompressException, but SharpCompress throws `CryptographicException` for a bad password — bypassing both the friendly wrapper and the re-prompt path.
  Where: `src/Images/Services/ArchiveBookService.cs:284-285`.
  Fix: include `CryptographicException` and map it to `ArchivePasswordRequiredException` so the prompt re-opens.

- [ ] P3 AUD-32 — Per-root catalog stats store global totals
  Why: Every root in a multi-root rebuild is upserted with the combined `reused+updated`/`failed` counts.
  Where: `src/Images/Services/CatalogService.cs:141-142`.
  Fix: track counts per root during the scan loop and upsert each root's own numbers.

- [ ] P3 AUD-33 — Dead back-history entries permanently block GoBack
  Why: If the top back-stack target was deleted, `Open` fails, the entry is never popped, every GoBack retries the same dead path while `CanGoBack` stays true.
  Where: `src/Images/Services/DirectoryNavigator.cs:244-262`.
  Fix: pop the entry on failed open and fall through to the next one (loop until success or empty).

- [ ] P3 AUD-34 — c2patool stderr "not found" substring maps real errors to "no manifest"
  Why: Any failure whose stderr contains "not found" ("trust anchor not found", transient file errors) is classified `NoManifest`; `PlanExportHandoff` then reports `SourceHasNoManifest` and exports proceed without the `RequiresAttention` flag a real failure would raise.
  Where: `src/Images/Services/C2paManifestService.cs:253-258`.
  Fix: tighten the classifier to c2patool's actual no-manifest message ("No claim found" — verify against the tool version) and treat other stderr as inspection failure.

- [ ] P3 AUD-35 — ModelManager reports "SHA-256 verified" from a stale cached hash
  Why: `InspectModel` trusts the import-time hash if file *length* matches; a same-length in-place modification still reports "Imported and SHA-256 verified"/Ready. Not a security boundary (same-user write access), but the UI claim is false.
  Where: `src/Images/Services/ModelManagerService.cs:526-543,618`.
  Fix: also compare last-write-time (store it at import); recompute the hash when it differs and update or flag.

- [ ] P3 AUD-36 — Ghostscript version probe has the stdout/stderr drain anti-pattern
  Why: Neither redirected stream is read until after `WaitForExit(1500)`; a chatty gs build can fill the pipe buffer and deadlock into the timeout-kill path. Only remaining spawn in the codebase with this pattern.
  Where: `src/Images/Services/CodecRuntime.cs:230-249`.
  Fix: start `ReadToEndAsync` on both streams before waiting, like ExifTool/jpegtran/c2patool.

- [ ] P3 AUD-37 — Listen-mode socket not bound exclusively
  Why: `TcpListener` without `ExclusiveAddressUse = true`; same-user SO_REUSEADDR hijack only, but it's a one-line hardening on a security-sensitive listener.
  Where: `src/Images/Services/ListenService.cs:44-47`.

- [ ] P3 AUD-38 — NetworkEgressService rewrites/reads the whole JSONL on every recorded entry
  Why: `RotateIfNeeded` does `File.ReadAllLines` after each append inside `_persistLock` — O(file) per event on the caller's thread during listen-mode sessions.
  Where: `src/Images/Services/NetworkEgressService.cs:244-252`.
  Fix: track the line count in memory (initialize once at startup), or only check rotation when file size crosses a threshold.

- [ ] P3 AUD-39 — Support-bundle redaction is prefix-match only
  Why: Only the exact `%USERPROFILE%` string (plus forward-slash variant) is redacted; 8.3 short forms (`C:\Users\MATTHE~1\...`) and `file:///C:/Users/...` URI forms survive, while the manifest promises profile paths are redacted throughout.
  Where: `src/Images/Services/SupportBundleService.cs:268-297`.
  Fix: add the URI form and the 8.3 short form (via `GetShortPathName` of the profile dir) to the redaction set, or switch to a regex over `[A-Z]:[\\/]Users[\\/][^\\/]+`.

- [ ] P3 AUD-40 — `explorer.exe /select,<path>` mis-parses paths containing commas
  Why: Explorer splits its own command line on commas regardless of argv quoting; `C:\pics\a,b.jpg` opens the wrong location. Not injection — wrong-folder UX bug.
  Where: `src/Images/Services/ShellIntegration.cs:60-65`.
  Fix: use `SHOpenFolderAndSelectItems` (P/Invoke: `SHParseDisplayName` + `SHOpenFolderAndSelectItems`) with a fallback to the current approach.

- [ ] P3 AUD-41 — Email drafts leave full image copies in AppData indefinitely
  Why: Each `.eml` embeds the complete image under `%LOCALAPPDATA%\Images\email-drafts`; `PruneOldDrafts` only runs when creating the *next* draft, so a one-off share persists forever. Surprising residual copy for a privacy-first app.
  Where: `src/Images/Services/EmailShareService.cs:34-45,92-108`.
  Fix: also prune on app startup (cheap, bounded) and document the retention in the share dialog microcopy.

- [ ] P3 AUD-42 — `IsWriteFormatAllowed` narrower than the extension blocklist (policy drift)
  Why: Blocks only Pdf/Pdfa/Eps/Svg while `DisallowedWriteExtensions` also covers PS/PS2/PS3/EPSF/EPSI/AI/SVGZ/MVG/MSL/HTTP(S). Currently benign (export allowlist never yields these) but any future caller gets weaker enforcement than documented.
  Where: `src/Images/Services/MagickSecurityPolicy.cs:24-28,121-122`.
  Fix: derive the format check from the same list as the extension check.

- [ ] P3 AUD-43 — BackgroundTaskTracker remove-on-zero races MarkStarted on the same name
  Why: T1 decrements to 0, T2 GetOrAdds the same CounterSet and increments, T1 TryRemoves the live entry → dropped/orphaned running counts in diagnostics.
  Where: `src/Images/Services/BackgroundTaskTracker.cs:137-143,148-155`.
  Fix: `TryRemove(KeyValuePair)` with the exact CounterSet reference, or only remove under a per-name lock when count==0 re-checked.

- [ ] P3 AUD-44 — TileService.ClearCache/ClearAll remove BuildLocks another thread may hold
  Why: After `BuildLocks.TryRemove`, a new `BuildPyramid` GetOrAdds a fresh lock → two threads can build into the same directory concurrently, violating the mutual-exclusion invariant.
  Where: `src/Images/Services/TileService.cs:324,509`.
  Fix: don't remove lock objects on clear (they're cheap); or coordinate clears through the same lock. Fold into the AUD-12 rework.

- [ ] P3 AUD-45 — CLIP attention mask truncates at token id 0 (a valid vocab token: "!")
  Why: Padding uses 0 and the mask loop stops at the first 0 — queries containing "!" cut the attention mask short, degrading text embeddings on 2-input models.
  Where: `src/Images/Services/ClipEmbeddingProvider.cs:103-105`; `src/Images/Services/ClipTokenizer.cs:178-184`.
  Fix: compute mask from the tokenizer's reported real length, not by scanning for 0.

- [ ] P3 AUD-46 — Fixed `.tmp` temp path collides across processes (mutation silently lost)
  Why: Two instances saving simultaneously write the same `smart-collections.json.tmp`; the loser's `File.Move` throws IOException, caught and logged → user's change silently dropped. `TagGraphService.TrySave` has the same fixed-`.tmp` pattern.
  Where: `src/Images/Services/SmartCollectionService.cs:208-210`; `src/Images/Services/TagGraphService.cs:419`.
  Fix: GUID temp names (pattern already correct in `TileService.WritePyramidInfo`/`ThumbnailCache.WriteAtomically`).

- [ ] P3 AUD-47 — ThumbnailCache eviction aborts entirely if one file vanishes mid-sweep
  Why: `FileInfo.Length` throws for a concurrently deleted thumb; the outer catch abandons the sweep and the next attempt is 5 minutes away — cache can sit over cap indefinitely under churn.
  Where: `src/Images/Services/ThumbnailCache.cs:394-411`.
  Fix: per-file try/catch inside the size loop; skip vanished files.

- [ ] P3 AUD-48 — OCR busy flag killed by a stale extraction's finally (E→E→E)
  Why: Cancel + quick restart: the first canceled extraction's `finally` runs `IsOcrBusy = false` unconditionally while the second runs — busy panel hides, tooltip flips. Generation guard keeps results correct; state only.
  Where: `src/Images/ViewModels/OcrWorkflowController.cs:187-194`.
  Fix: only clear busy when the finishing extraction is the current generation.

- [ ] P3 AUD-49 — External-edit reload: queued debounce survives re-Arm and reloads the wrong file; atomic-save editors never detected
  Why: (a) A queued `BeginInvoke(RestartDebounce)` from the FSW thread runs after navigation re-arms the controller — 800 ms later the *current*, never-edited file reloads with a false toast. (b) `NotifyFilter = LastWrite|Size` + `Changed`-only subscription misses temp-file+rename-over saves (Photoshop, GIMP, VS Code) — the headline reload feature silently no-ops for them.
  Where: `src/Images/ViewModels/ExternalEditReloadController.cs:69,118-157`.
  Fix: validate `WatchedPath` in the debounce tick; add `Renamed` subscription + `FileName` notify filter for rename-over detection.

- [ ] P3 AUD-50 — Slideshow wedges in "Playing" with a dead timer; single-file re-decode; shuffle re-enumerates folder every tick
  Why: (a) When the folder empties, the tick returns past the trailing `_slideshowTimer?.Start()` leaving `IsSlideshowActive=true` with a stopped timer; `ClearCurrentState` never stops the slideshow either. (b) Single-file slideshows re-decode the same image every tick. (c) Shuffle mode calls `_nav.Open(targetPath)` per tick — full directory re-enumeration + sort + history push.
  Where: `src/Images/ViewModels/MainViewModel.cs:2486-2559` (all three), `:7517` (ClearCurrentState).
  Fix: stop-and-clear slideshow state on empty/last-file; short-circuit single-file ticks; give the navigator an index-jump API for shuffle.

- [ ] P3 AUD-51 — Inpaint feature is unwired and its latent state machine has destructive-write bugs
  Why: No XAML/code-behind references any Inpaint member (verified repo-wide). Latent: mask regions and `IsInpaintMode` are never reset on navigation/clear, so a mask painted on image A would overwrite image B via `ApplyInpaintAsync` (gated only on `HasInpaintMaskRegions && !IsOperationBusy`) the day someone wires the UI.
  Where: `src/Images/ViewModels/MainViewModel.cs:6338-6468`, resets missing at `:4544-4560,7517-7555`.
  Fix (defensive, cheap): reset inpaint state in `CompleteCurrentLoad`/`ApplyLoadFailure`/`ClearCurrentState` and add a `CanUseInpaint` gate to `ApplyInpaintAsync`. Decide separately whether to wire or remove the feature.

- [ ] P3 AUD-52 — AsyncRelayCommand has no reentrancy guard; motion-photo commands can run concurrently
  Why: Fire-and-forget async void with no `_isExecuting`; `ExtractMotionVideoCommand`/`PlayMotionVideoCommand` bodies never set busy — double-invoke launches two parallel extractions to the same output.
  Where: `src/Images/ViewModels/AsyncRelayCommand.cs:30-53`; `src/Images/ViewModels/MainViewModel.cs:322-323,7420-7474`.
  Fix: add an executing flag to AsyncRelayCommand that disables CanExecute while the body runs (raises requery on completion); it protects every async command at once.

- [ ] P3 AUD-53 — `File.Exists` inside CanExecute predicates → disk I/O storm on every requery
  Why: `HasImage`/`CanUseImageCommands` (~30 commands)/per-item culling checks probe the disk on every `CommandManager.RequerySuggested` (every keystroke/focus change); on a disconnected UNC path each probe can block the UI thread for seconds.
  Where: `src/Images/ViewModels/MainViewModel.cs:804,828,1659,3377`; multiplied by `RefreshCommandPaletteItems:1050-1068`.
  Fix: cache file-existence on load/navigation (invalidate via FSW + explicit operations) instead of probing in predicates.

- [ ] P3 AUD-54 — Synchronous decodes/transfers freeze the UI on secondary paths
  Why: (a) `OnDirectoryListChanged → LoadCurrent()`, `AdvanceAfterRemovedCurrent`, external-edit debounce reload, `OpenFileList` first decode, and `TransferCurrentImage` (busy status never renders during a multi-GB sync move) all decode/copy synchronously on the dispatcher. (b) Four secondary windows (`DuplicateCleanupWindow.xaml.cs:354`, `FileHealthScanWindow.xaml.cs:213`, `ImportInboxWindow.xaml.cs:346`, `ReferenceBoardWindow.xaml.cs:223`) run full-size `ImageLoader.Load` on selection change with no decode-width cap — seconds-long freezes on 100 MP files, and FileHealthScan deliberately targets damaged files. (c) Tile bitmaps decode synchronously on the dispatcher during pan/zoom (`ZoomPanImage.cs:590-612`). (d) `Window_DragOver` enumerates directories per mouse-move tick (`MainWindow.xaml.cs:1557-1612`).
  Fix: background decode with a width cap for the four previews (ThumbnailCache or `DecodePixelWidth`); async-ify the VM paths that have async siblings; move tile decode off-thread with placeholder; cache the last DragOver path verdict.

- [ ] P3 AUD-55 — Gallery rebuild churn + synchronous smart-filter index on gallery open
  Why: With the gallery open, every navigation clears and re-adds the entire `GalleryItems` collection; `EnsureGallerySmartFilterIndex` runs `BuildIndex` (64×64 decode per file) synchronously on the UI thread for large folders.
  Where: `src/Images/ViewModels/MainViewModel.cs:3488-3561`.
  Fix: diff-update the collection instead of clear+re-add; move the index build to a background task with a busy chip (it already runs off-thread in the v0.2.16 smart-filter path — align this one).

- [ ] P3 AUD-56 — `BooleanToVisibilityConverter` maps `DependencyProperty.UnsetValue` to Visible
  Why: The `_ => true` arm shows elements for unresolved bindings. Do NOT change int handling (non-zero-int→true is relied upon by `RecentFolders.Count` bindings); only special-case `UnsetValue`/`null` → false.
  Where: `src/Images/ViewModels/Converters.cs:20-27`.

- [ ] P3 AUD-57 — Multi-file drop opens only the first file
  Why: `Window_Drop` uses `paths[0]` while `OpenPathList` exists and App uses it for multi-file argv — dropping 10 files silently loses 9.
  Where: `src/Images/MainWindow.xaml.cs:1581-1600`; list API at `:285`, argv precedent `App.xaml.cs:155-158`.
  Fix: route multi-file drops through `OpenPathList`.

- [ ] P3 AUD-58 — ZoomPanImage resets zoom/pan on every SizeChanged
  Why: Any layout change — fullscreen edge-reveal of the side panel, filmstrip/gallery toggle, window resize — calls `ResetView()`, discarding user zoom/pan. In fullscreen, merely moving the mouse to the right edge resets zoom.
  Where: `src/Images/Controls/ZoomPanImage.cs:189-194`; edge-reveal at `MainWindow.xaml.cs:716-720`.
  Fix: only reset when the view is still in an untouched fit mode; preserve zoom/pan (re-anchored to center) for user-modified views.

- [ ] P3 AUD-59 — Double right-click resets zoom while also opening the context menu
  Why: WPF `MouseDoubleClick` fires for right-button too; `OnDouble` doesn't check `e.ChangedButton == MouseButton.Left`.
  Where: `src/Images/Controls/ZoomPanImage.cs:429-436`; menu at `MainWindow.xaml.cs:108-115`.

- [ ] P3 AUD-60 — AnnotationsWindow window-level Enter/Escape steal text-input keys; load-failure status overwritten
  Why: (a) Enter while typing annotation text applies-all and closes; Escape silently discards all work — no focused-TextBox guard (PerspectiveCorrection/Effects/Adjustments share the pattern but have no text inputs). (b) The constructor's `UpdateStatus()` unconditionally overwrites the "missing file" status set by `LoadPreview()`.
  Where: `src/Images/AnnotationsWindow.xaml.cs:195-207,41-44,595-601`.
  Fix: guard the key handler on focused TextBox; make `UpdateStatus` not clobber an error status (or set the error after).

- [ ] P3 AUD-61 — BatchProcessor: exotic per-item fault crashes; partial-cancel nulls
  Why: If `RunOne` throws outside its caught set, `Task.WhenAll` throws through `RunButton_Click` (catches only OCE → fatal); on partial cancellation `results[]` can contain nulls → NRE iterating `result.Items`. Low likelihood (RunOne's catch is broad) but the crash path is real.
  Where: `src/Images/Services/BatchProcessorService.cs:190-224,317`; `src/Images/BatchProcessorWindow.xaml.cs:158-203`.
  Fix: broaden the window catch; filter nulls before iterating.

- [ ] P3 AUD-62 — ImportInboxWindow: ChooseDestinationButton missing from SetBusy → overlapping reloads
  Why: Every trigger except `ChooseDestinationButton` is disabled while busy; clicking it mid-reload awaits a second `ReloadAsync`, and the superseded reload's finally re-enables the whole UI while the second scan still runs.
  Where: `src/Images/ImportInboxWindow.xaml.cs:103-108,175-188,356-366`.
  Fix: include the button in SetBusy (or guard ReloadAsync with a generation token).

- [ ] P3 AUD-63 — ReferenceBoardWindow drag state lacks LostMouseCapture recovery; handle mouse-up always swallows clicks
  Why: If capture is stolen mid-drag (popup/system dialog), `_dragElement`/`_dragHandle` stay set and the next press-move teleports the card from the stale origin; `e.Handled=true` in MouseLeftButtonUp fires even when no drag is active.
  Where: `src/Images/ReferenceBoardWindow.xaml.cs:440-481`.
  Fix: subscribe LostMouseCapture to clear drag state; only handle mouse-up when a drag was active.

- [ ] P3 AUD-64 — ExportPreviewWindow has no cancellation for preview encoding
  Why: Closing mid-encode leaves the Magick re-encode running to completion; rapid setting changes queue full encodes with no supersede token.
  Where: `src/Images/ExportPreviewWindow.xaml.cs:165-196`.
  Fix: add a CTS + generation counter like the other preview controllers.

- [ ] P3 AUD-65 — Code-behind brush snapshots go stale after a live theme switch
  Why: ~15 windows resolve theme brushes once via `TryFindResource` and assign to elements (AboutWindow, ReferenceBoard, PerspectiveCorrection, CrashDialog, TagGraph, + the `StatusDot.Fill` pattern in 8 more); no theme-changed repaint, so an open secondary window keeps mixed colors after Dark↔Latte switch until a data-driven repaint.
  Where: e.g. `AboutWindow.xaml.cs:314-416`, `ReferenceBoardWindow.xaml.cs:629`, `PerspectiveCorrectionWindow.xaml.cs:386`, `CrashDialog.xaml.cs:150-158`, `TagGraphWindow.xaml.cs:193`.
  Fix: use `SetResourceReference(Shape.FillProperty, "TokenName")` instead of one-shot brush assignment (systemic sweep), or subscribe windows to a ThemeService changed event.

- [ ] P3 AUD-66 — Latte filled-button text contrast under AA; HC uses ControlColor instead of HighlightTextColor
  Why: `PrimaryButton`/`DangerButton` use `Foreground=CrustBrush`: Latte `#DCE0E8` on accent `#1E66F5` ≈ 3.7:1 / on red `#D20F39` ≈ 4.1:1 (Mocha is ~9:1, fine). HC text-on-highlight should be `SystemColors.HighlightTextColor`.
  Where: `src/Images/Themes/DarkTheme.xaml:280,327` + Latte/HC dictionaries.
  Fix: introduce an `OnAccentBrush` token per theme (Mocha: Crust; Latte: `#FFFFFF` or Base; HC: HighlightTextColor) and point both button styles at it.

- [ ] P3 AUD-67 — Latte caption color constant is Mantle, not Base
  Why: `LatteBaseColorRef = 0x00EFE9E6` is #E6E9EF (Mantle) in BGR; Latte Base is #EFF1F5 → `0x00F5F1EF`. Mocha's constant correctly uses Base, so the Latte caption is one step darker than the window body.
  Where: `src/Images/Services/WindowChrome.cs:22`.
  Fix: correct the constant (or rename it if Mantle is intentional — but match Mocha's precedent).

- [ ] P3 AUD-68 — `Elevation.Focus` is a dead, non-adaptive token
  Why: Defined with hardcoded Mocha blue, referenced nowhere, not overridden per theme — first future use silently breaks Latte/HC.
  Where: `src/Images/Themes/DarkTheme.xaml:111-113`.
  Fix: delete it (dead code), or make it per-theme if kept.

- [ ] P3 AUD-69 — Half the accessible names are hardcoded English
  Why: 220 literal `AutomationProperties.Name` strings (195 in MainWindow.xaml) vs 218 localized via `{x:Static loc:Strings.*}` — screen-reader users get untranslated UI despite full localization infrastructure.
  Where: `MainWindow.xaml` (bulk), `AboutWindow.xaml` (12), `AnnotationsWindow.xaml`/`SettingsWindow.xaml` (5 each), remainder scattered.
  Fix: mechanical sweep to `Strings.resx` entries; the localization parity script will then gate them.

- [ ] P3 AUD-70 — 1px GridSplitters: keyboard-focusable with invisible focus, 1px mouse target
  Why: 11 windows use `Width="1"` splitters — the shared FocusVisual ring on a 1px element is effectively invisible and the mouse target is sub-Fitts.
  Where: e.g. `BatchProcessorWindow.xaml:246-249` (+10 similar).
  Fix: widen to 4-6px with a transparent background and a 1px themed hairline drawn inside (keeps the visual), preserving keyboard resize.

### Suggested batching order

1. **Sidecar integrity** (AUD-01, 24) — shared helper + recovery round-trip + tests.
2. **Navigator/rename race** (AUD-02, 26, 33) — DirectoryNavigator API + VM guards + tests.
3. **Semantic search hardening** (AUD-03, 10, 11, 13, 45) — dispose choreography, exception guards, cache mode, leak.
4. **Import/export contracts** (AUD-05, 07, 08, 28, 30) — ImportInbox + Picasa + XMP fixes + fixtures.
5. **Cache/eviction** (AUD-12, 44, 46, 47, 18) — TileService rework, atomic temp names, thumbnail sweep, stackalloc.
6. **Slideshow/modal pumping** (AUD-14, 50) — busy discipline around modals + timer state machine.
7. **Input routing** (AUD-15, 57, 59, 60) — key/mouse guards, multi-drop.
8. **Process/egress posture** (AUD-04, 17, 34, 35, 36, 37, 42) — c2patool containment first.
9. **Theme/a11y wave** (AUD-19—23, 65—70) — tokens first (HintTextBrush, OnAccentBrush), then consumers, then labels.
10. **Perf wave** (AUD-38, 52—55, 58, 61—64) — highest-leverage: preview decode caps + CanExecute I/O.

Re-verify each agent claim against current code before editing (line numbers drift). After each batch: build, full tests, CHANGELOG entry, delete items here, commit, push.
