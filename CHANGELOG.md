# Changelog

All notable changes to **Images** are documented here.

## Unreleased

- Added a branch-aware WIC JPEG safety gate for CVE-2025-50165. Images now probes the numeric `WindowsCodecs.dll` file version against Microsoft’s August 2025 servicing floors and routes every JPEG variant through the hardened Magick.NET fallback when the codec branch is unknown or unpatched; main decode, large memory-mapped decode, previews, and quick dimensions all share the decision. The once-per-session toast recommends Windows Update without blocking the image, while the existing Magick.NET thumbnail cache remains unchanged because it never used WIC.
- Added a DPI-pinned Skia golden-render regression lane. A checked-in `tests/render` fixture is decoded through ImageSharp and compared against the offscreen Skia surface channel by channel in RGBA space with an explicit one-level tolerance, complementing the existing alpha-conversion and transparent-letterbox pixel contracts without launching WPF.
- Reworked the visual system against the image-generated `premium-viewer-v0.3.2-concept.png` target. All 23 WPF windows now inherit readable 15 px body type and 13 px minimum literal text, sentence-case section labels, compact 48 px tool headers, tighter workspaces, flatter status treatments, underline tabs, and unboxed content groups; outlines remain for input fields, focus, selection, transient overlays, and major workspace boundaries. The main viewer now gives more height to the image through a shorter context bar, filmstrip, and transport dock while retaining the 316 px aligned Details inspector. Settings copy is substantially shorter, Settings and About move their controls higher, and reduced section padding reaches every auxiliary workflow. Visual-system contracts protect the shared hierarchy, minimum readable text size, and mockup structure.
- Added face-aware culling signals to the face-review workbench without adding another model or automatic decisions. Every detected face gets a resolution-normalized local Laplacian-variance sharpness score; sufficiently sharp faces with low horizontal-to-vertical texture ratios around both YuNet eye landmarks get a conservative possible-closed-eyes hint. The UI exposes the raw local-sharpness value and labels both signals as fallible review hints. They never alter accept/reject review state, Pick/Reject labels, ratings, metadata, files, or sidecars.
- Added the face-region review workbench over the shipped YuNet/SFace pipeline. It can analyze the current image or an explicitly requested, 100-image-bounded folder batch, groups face crops by local SFace cluster without retaining vectors in UI state, and overlays amber/green/red detection bounds for pending/accepted/rejected review. XMP merge remains disabled until every region is accepted or rejected and every accepted region has a person name; the atomic writer then replaces only prior Images-owned MWG-rs entries while preserving third-party face regions and unrelated sidecar metadata. Detection and clustering remain read-only until that final explicit merge action.
- Completed the optional local safety-classification slice with a staged Apache-2.0 Marqo ViT-Tiny model, exact immutable checkpoint/config/license/hash provenance, and a byte-reproducible PyTorch-to-ONNX conversion whose logits match within `5.98e-7`. The classifier is default-off and runs only through explicit `--safety-classify <imagePath> [imagePath ...]`; it reuses one local inference session, exports the two NSFW/SFW probabilities only to stdout, imposes no moderation threshold, and never writes results to files, source metadata, catalog labels, or logs. The model card's benign pastry fixture validated as SFW at 0.938259 through DirectML; no explicit-content fixture is stored or fetched.
- Completed local scene classification with the official Places365 ResNet-18 checkpoint, reproducible PyTorch-to-ONNX conversion, aligned 365-category and indoor/outdoor labels, and explicit CC-BY model attribution alongside the MIT code notice. `--scene-classify <imagePath> [imagePath ...]` reuses one shared inference session, prints top-five probabilities plus a probability-weighted environment assessment, and emits conservative review-only `scene:`/`environment:` smart-album keywords without modifying files or metadata. The official food-court CAM fixture was validated through the converted graph before integration.
- Completed the local NIMA aesthetic-culling slice with a staged Apache-2.0 idealo MobileNet model, exact source-checkpoint/license/hash provenance, and a tracked TensorFlow-to-ONNX conversion script that verifies probability parity. `--aesthetic-score <imagePath> [imagePath ...]` reuses one shared Windows ML/DirectML/CPU session, validates each ten-bin opinion distribution, reports mean/uncertainty and relative batch rank, preserves partial results, and never writes Pick/Reject labels or metadata. Live DirectML validation ranked the six-image upstream panel with scores from 4.70 to 6.44.
- Added opt-in local document-orientation suggestions with a revision/size/SHA-256-pinned MIT ConvNeXtV2 INT8 ONNX model and shared Windows ML/DirectML/CPU routing. `--orientation-suggest` applies existing EXIF orientation, reports only a clockwise correction hint, withholds ambiguous results below 0.80 confidence or a 0.20 runner-up margin, and never rotates or writes a file. Four rotations of an external receipt fixture validated the reviewed class mapping at 0.9979–0.9987 confidence.
- Completed the local object-detection core with the revision/size/SHA-256-pinned Apache-2.0 OpenCV YOLOX-S model, official RGB/114 letterbox preprocessing, 8400x85 COCO decoding, class-aware non-maximum suppression, and shared Windows ML/DirectML/CPU routing. `--object-detect` emits boxes and deduplicated `object:` catalog-keyword suggestions; `--object-xmp` prints a reviewable `dc:subject` draft, and neither command writes files. Live DirectML validation detected the external dog fixture as `object:dog` at 0.8518 confidence.
- Completed the local face-analysis core with SFace similarity alignment, a 40 px minimum-face quality gate, unit-normalized 128-value private embeddings, and deterministic cosine clustering at the model card's published 0.363 threshold. `--face-cluster` reports only membership and rejection reasons, while `--face-xmp` prints a literal MWG-rs draft with unassigned regions; neither exposes vectors or modifies files. Live DirectML validation produced a normalized embedding and clustered two independent runs of the official sample portrait together.
- Added a window-free local YuNet face-detection consumer. `--face-detect <imagePath>` uses the verified Model Manager artifact and shared hardware router, preserves aspect ratio, decodes confidence/box/five-landmark outputs, applies non-maximum suppression, and prints pixel plus MWG-compatible normalized region JSON without modifying the image or sidecar. The pinned model was smoke-tested against OpenCV's sample portrait through DirectML.
- Added SHA-256-pinned, revision-pinned OpenCV YuNet and SFace artifacts to Model Manager for the face workflow. The exact official permissively licensed model bytes were downloaded and inspected locally (YuNet/MIT 232,589 bytes; SFace/Apache-2.0 38,696,353 bytes), their ONNX input/output contracts were verified, and optional-model SBOM provenance now includes both without bundling or automatic download.
- Completed the Windows ML inference foundation. The self-contained `Microsoft.Windows.AI.MachineLearning` 2.1.74 runtime now discovers and registers already-ready certified Windows ML providers on Windows 11 24H2+, explicitly prefers NPU then GPU devices, and falls back per model to bundled DirectML then CPU. Every AI consumer shares the same session router; diagnostics report the hardware actually selected as `NPU`, `GPU`, or `CPU` plus provider/vendor detail. ONNX telemetry is disabled, provider acquisition never runs silently, and a SHA-256-pinned 129-byte ONNX Add fixture executes through every detected path in tests.
- Replaced ordinary static-image presentation with a live software SkiaSharp WPF surface, including premultiplied-alpha, centered-uniform fit, inversion, and nearest-neighbor parity. Deterministic pixel fixtures cover alpha conversion and exact nearest-neighbor output; the existing transformed WPF layer remains the explicit fallback for animation and tile pyramids and continues to supply loupe, checkerboard, and analysis-overlay compatibility. The six-case non-activating app lane exercises the packaged native runtime offscreen.
- Added window-free catalog CLI consumers: `--catalog-search "<terms>"` prints assets matching every filename/format/codec/rating/palette/camera/lens/tag term, while `--catalog-near <lat> <lon> <radiusKm>` prints assets inside an exact great-circle radius. Catalog folder, term, and geo predicates now run in SQLite instead of materializing up to 50,000 records for LINQ filtering; focused tests cover multi-term metadata matching, radius exclusion, parsing, and process-friendly path output.
- Added a safe WPF background-smoke lane. `--uia-background` launches the real app offscreen with isolated mutable storage, no taskbar entry, `ShowActivated=false`, native `WS_EX_NOACTIVATE`, no startup focus/update check, and no persisted test geometry. Six FlaUI cases verify process lifecycle, fixture decode, title, canvas automation, navigation controls, and a two-item filmstrip without global keyboard/mouse input; `scripts/Test-WpfBackgroundSmoke.ps1` runs the lane locally and a Windows GitHub Actions job publishes its TRX result.
- Reframed the v1.0 milestone around reachable unsigned quality gates: GitHub Releases, checksum-pinned WinGet/Scoop manifests, and the non-activating WPF smoke lane. Code-signing and signing-dependent C2PA-write items are permanently retired by owner policy. The Skia canvas and remaining Windows ML/NPU runtime front-end are now scheduled as actionable keystone efforts instead of circular blocked chains.

## v0.2.29 - 2026-07-17

- The folder catalog now indexes geo/time/camera EXIF (catalog schema v3): GPS latitude/longitude (decimal degrees, paired and range-validated), capture time (`DateTimeOriginal` + offset, normalized to UTC), camera make/model, lens, ISO, focal length, aperture, and shutter. Existing catalogs migrate in place — the new columns are nullable and backfill on the next rescan — and a new `CatalogService.FindWithinBounds` geo bounding-box query (antimeridian-aware) backs future map, trip, and near-duplicate features. Extraction reuses the details panel's GPS/ISO logic via the new `CatalogExifExtractor`.
- Rewrote the privacy policy's stored-data table directly from the `LocalDataStoreRegistry` inventory (23 stores instead of a stale 8), listing each store's path, class, and clear-on-privacy-reset behavior, and corrected the analysis note to reflect that OCR and semantic search run only on explicit user action against locally imported models.

## v0.2.28 - 2026-07-16

- Re-importing a Picasa library after renaming an album or person no longer accumulates stale keywords: the importer prunes its own previously written `album:`/`person:`/`Picasa|Albums|`/`Picasa|People|` tags before writing the current set, while preserving user-authored keywords. `MainViewModel` now also disposes its continuous-archive decode gate.
- Listen mode now compares its loopback session token in constant time and counts a connection against the concurrency cap before the handler task is scheduled, closing a theoretical token-timing side channel and a transient cap overshoot under bursts.
- Semantic search now caches the model's normalized vectors in memory keyed by the index generation. Repeat queries on an unchanged index no longer re-read and re-deserialize the entire embedding table (up to ~100 MB of blob I/O) per search; a Rebuild or Clear invalidates the cache.
- The details panel now reports JPEG XL structure: bare codestream, ISOBMFF container, or a lossless JPEG-to-JXL transcode (`jbrd` reconstruction box) — a headline JXL trait no mainstream Windows viewer surfaces.
- Added an "Invert colours" view toggle (command palette, rebindable) that non-destructively inverts the displayed still image — an aid for reading negatives, low-contrast scans, and for accessibility. The source file and exports are untouched, and the toggle resets on navigation. Animated and tile-backed images keep their normal rendering.
- Tile-pyramid build locks are now ref-counted and released when the last waiter for a cache directory finishes, so browsing many huge images in one session no longer accumulates lock objects for the process lifetime. Concurrent builds of the same image still share one gate, preserving mutual exclusion.
- Upgraded SharpCompress 0.49.1 -> 0.50.0 for reduced LZMA/RAR decode allocation (comic-archive pages) and Zip64 non-seekable-stream / entry-metadata-corruption fixes. The 0.50.0 Tar auto-decompress and Detection API breaking changes do not touch the read-only `ArchiveFactory.OpenArchive` path Images uses; the CBZ/CBR/7z/CB7 regression suite passes unchanged.
- The details panel now surfaces HDR gain maps that Windows silently ignores: it detects Google Ultra HDR, Adobe/ISO `hdrgm` metadata, Apple HDR gain maps, and ISO 21496-1, and reports the flavor, version, and content-boost range (in stops). Read-only inspection — no HDR display or writeback.
- Animated multi-frame decode now routes through `MagickSafeReader.ReadCollection(bytes)`, which installs the native coder allowlist and resource limits before decoding. Previously the animated path constructed a `MagickImageCollection` directly, so a future caller reaching it before the main load preflight could decode untrusted bytes with the security policy uninitialized.
- Import Inbox rollback no longer strands a moved original at the destination when the source path is re-occupied mid-import: the original is restored to a unique `(recovered)` sibling of the source and the failure message reports where it landed, so a failed move is always recoverable.
- Catalog SQLite connections now open with a private cache under WAL, matching the semantic index. A background `Rebuild` write transaction can no longer raise shared-cache `SQLITE_LOCKED` on a concurrent UI read (which `GetByPath`/`GetAllAssets` would swallow into an empty catalog); readers observe the last committed snapshot instead.
- Took the July 2026 servicing bump: `Microsoft.Data.Sqlite` and `Microsoft.Extensions.Logging` 10.0.9 -> 10.0.10, aligning with the .NET 10.0.10 wave (17 CVEs, 3 critical RCE). Lockfile regenerated, vulnerable-package scan clean, runtime-provenance doc synchronized.

## v0.2.27 - 2026-07-14

- Archive books now offer a persisted continuous vertical-reading mode for webtoons and long strips; the virtualized reader lazily decodes nearby pages, recycles offscreen pixels, preserves per-book position, exposes calm retry states, and remains mutually exclusive with two-page spreads.
- Diagnostics, About provenance, and startup logs now report the loaded `MagickNET.Version`, native ImageMagick version, and `SELECT sqlite_version()` result; versions below ImageMagick 7.1.2-2 or SQLite 3.53.2 raise an explicit warning instead of letting native payload drift stay invisible.
- Archive pages now independently verify advertised ZIP/RAR/7z CRCs before returning decoded bytes; a corrupt-entry regression proves damaged page data fails as an archive error instead of silently reaching the image decoder.
- Focus peaking and exposure-clipping overlays now provide bounded, cancellable RAW/photo culling analysis directly on the transformed image surface; both are localized, persisted, rebindable, command-palette discoverable, and use a text legend alongside green/red/blue markers.
- Active-monitor DXGI diagnostics now report HDR/SDR color space and peak/full-frame luminance; when HDR-class content is tonemapped for the WPF SDR surface on an HDR desktop, the Details panel shows an explicit localized status badge.
- Exif 3.1 learning-use preferences, development provenance, and correction/noise-reduction state now appear as localized read-only metadata; strict TIFF parsing preserves the new UTF-8 fields, labels reserved values explicitly, and rejects legacy Samsung/GE tag collisions without modifying the image.
- Settings can now export and transactionally import a bounded, versioned JSON allowlist of portable preferences and custom shortcuts; previews report applied/ignored counts, while private paths, window placement, history, and update-check consent never leave the local database.
- Service-generated metadata, color analysis, diagnostics, OCR, content-credential, model-management, network-export, and asynchronous loading/error copy now follows the active UI culture; the pseudo-locale test covers real service outputs instead of XAML alone.
- Native SQLite is pinned to 3.53.3, every app-owned database disables and verifies trusted-schema execution on open, and release readiness rejects runtimes below the reviewed 3.53.2 security floor.
- Macro and batch folder collection now shares one reparse-aware traversal that skips nested junctions/symbolic links, inaccessible paths, unsupported files, and duplicate sources instead of walking outside the selected tree.
- User-selected macro and batch-preset JSON imports are now capped at 1 MiB and 128 operations, reject null action arrays cleanly, and read through replacement-friendly bounded streams. App-owned collection, tag, model-manifest, tile, and update-state JSON is likewise size-bounded so damaged local state cannot cause unbounded memory use.
- Background thumbnail, metadata, catalog, and paged-image decoders now open source images with delete sharing, so navigation-time reads no longer intermittently block Recycle Bin deletes or atomic replacements.

### Added

- **Refined viewer design target** - A new image-generated concept at `assets/mockups/premium-viewer-v0.3-concept.png` captures the intended hierarchy for the contextual file bar, inset image stage, progressive Details inspector, filmstrip, and transport dock.
- **Monitor-aware legacy ICC output** - The opt-in color-managed display path now resolves the active monitor profile with Windows ICM, verifies Advanced Color state, converts tagged images to a non-sRGB monitor profile only in legacy SDR mode, and safely falls back to sRGB otherwise. Moving the window between monitors invalidates preloads and re-decodes the current image; About diagnostics report the active destination and fallback reason.
- **Resource-bounded adversarial codec corpus** - A dedicated child-process probe now exercises corrupt/truncated PNG, JPEG, WebP, TIFF, GIF, and SVG inputs, declared-dimension bombs, and archive entry/compression floods under a 20-second wall timeout and 768 MiB Windows Job Object ceiling. Hash- and license-verified upstream fixtures prove AVIF, HEIC, JXL, DNG RAW, and PSD decoding without risking the xUnit host.
- **Screen-reader announcements for asynchronous status changes** - Every dynamic status surface across all 20 WPF windows now raises coalesced `LiveRegionChanged` events with polite semantics, including scans, imports, exports, recovery, model work, background activity, and errors. Static contracts cover every named status surface, while the opt-in rendered matrix now constructs every secondary window under Dark, Latte, High Contrast, and the pseudo-locale.
- **HDRI decode-time tonemapping** - EXR, Radiance HDR, RAW, AVIF/HEIC/JXL, and high-bit-depth PNG/TIFF content now retains floating-point highlights until Reinhard, Hable, or ACES maps them into the SDR display range. Reinhard is the default, Settings exposes the operator, and decoder provenance identifies when tonemapping was applied.
- **Authoritative local-data registry and privacy reset** - Settings, `--system-info`, and support bundles now share one inventory covering the real catalog and semantic-index files, caches, models, diagnostics, histories, drafts, backups, and temporary media. A confirmed reset clears registered rebuildable/diagnostic stores while preserving imported models and user-owned content.
- **Adjustable loupe magnification** - Appearance settings now offer a loupe magnification choice (2×, 3×, 4×, 6×) so the middle-button magnifier can zoom in further than the previous fixed 2×.
- **Keyboard-invocable loupe** - A new "Toggle loupe magnifier" command (default `L`, rebindable, in the command palette) shows a viewport-centered magnifier with no mouse needed; pan the image to move it beneath the lens. Holding the middle button still temporarily overrides the lens to follow the cursor.

### Fixed

- **Concurrent diagnostics and recovery logs are lossless** - Recovery Center instances now serialize append/compaction/read operations so overlapping destructive workflows cannot drop durable records, and deferred network-log collection mutations now honor the same lock used by snapshot readers.
- **XMP/XML imports are resource-bounded** - Sidecar, catalog, tag, edit-stack, inbox, and Picasa XML readers now reject documents over 16 MiB, prohibit DTD/entity expansion, and use replacement-friendly file sharing before parsing user-controlled metadata.
- **Atomic exports tolerate brief file locks** - Final temp-file swaps now retry a bounded number of times when an image reader, thumbnailer, or security scanner briefly locks the destination, preventing intermittent crop/writeback failures without hiding permanent permission errors.
- **Optional tools cannot exhaust memory through process output** - ExifTool, c2patool, jpegtran, and Ghostscript now share a concurrent bounded process runner: version probes retain at most 256 KiB per stream, operations retain at most 4 MiB, and exceeding either limit terminates the child process tree with a distinct failure from timeout or nonzero exit.
- **Secondary failures no longer expose exception internals** - Rename, delete, rotate, crop, copy, export, repair, wallpaper, Explorer, transfer, save, print, metadata, email, clipboard, and edit-stack failures now show stable localized recovery copy; full exception and service-result details remain in diagnostics logs.
- **ImageMagick now denies unadvertised native decoders** - Startup injects a deny-all-then-permit coder policy, disables filters and `@` path indirection, and default-denies delegates except the four Ghostscript raster-preview paths when that approved runtime is present. SVG/SVGZ use the in-process MSVG pipeline; hazardous MNG, TIM, MSL, and direct MVG inputs are no longer advertised.
- **Inline rename is transactional and restart-recoverable** - Image and XMP moves now succeed as one unit or roll back together, successful renames persist their complete sidecar map in Recovery Center, and an incomplete rollback creates an explicit partial-state record instead of silently orphaning metadata.
- **Support bundles remove all rooted paths** - Every textual ZIP entry now passes one sanitizer that replaces drive-rooted, UNC/device, file-URI, mixed-separator, quoted, and POSIX absolute paths with `%PATH%`, including exception text and bundle failure details.
- **Archive books enforce aggregate safety budgets** - Archive enumeration now stops at 10,000 entries, 4 MiB of entry-name metadata, 4 GiB of declared image data, or a known compression ratio above 1000:1, before page names are materialized or image data is decoded.
- **Malformed image dimensions now fail closed** - Raster loads classify dimension preflight as small, large, rejected, or unknown; rejected and inconclusive probes are refused before WPF or managed full-frame decode can allocate from untrusted headers.
- **Session restore no longer reopens a broken or peeked file** - A file that exists but fails to decode, or a transient `--peek` target, is no longer saved as the "last image", so opt-in session restore won't reopen a known-bad file or promote a peek into a full session.
- **Large archive spreads fail cleanly** - A two-page spread whose combined width overflowed a 32-bit integer produced an opaque render error; it now reports the same "too large to render" message as other decode paths.
- **Animated WebP/APNG frame delays can't overflow** - Very large per-frame delay fields (32-bit for WebP/APNG) no longer overflow into a negative value and silently reset to 100 ms; delays are computed in 64-bit and capped at 60 s.
- **Batch preview shows the real output names** - When several source files rename to the same stem, the batch preview now reports the distinct destinations the run will actually write instead of repeating one identical path.
- **No crash if a command faults during shutdown** - An async command that faulted after its view model was disposed could rethrow on a background continuation and terminate the app; the fault is now logged instead.
- **Calmer decode-failure message** - The "couldn't be displayed" card no longer appends raw decoder exception text; it shows a clear localized sentence and logs the technical detail for diagnostics.

### Changed

- **Viewer workspace has a clearer visual hierarchy** - The active file now gets a quiet breadcrumb/action bar and inset stage, the Details panel leads with four scannable facts and collapses duplicate metadata, the filmstrip prioritizes larger image previews, and navigation/view/file actions sit in three coherent transport groups that wrap intact on narrow windows. Peek and fullscreen modes remain edge-to-edge.
- **Deterministic image-runtime tests** - Test collection parallelism now has a fixed four-worker ceiling, WPF/settings tests that share process-wide theme and decoder state are serialized together, and async writeback assertions wait for operation completion before checking terminal state.
- **Reproducible local and release builds** - `global.json` pins the .NET 10.0.3xx feature band with patch-only roll-forward, app/test NuGet graphs are committed as lock files, and release readiness restores in locked mode. CycloneDX 6.2.0 is a repo-local tool restored explicitly before SBOM generation, removing dependence on machine-global tools.
- **Primary windows are fully localization-backed** - MainWindow menus, tooltips, empty/error states, gallery, inspector, controls, shortcuts help, and AboutWindow visible copy now resolve from resources. The localization gate rejects new literal visible text in either window and missing XAML resource keys; the pseudo-locale covers every addition.
- **Consistent microcopy** - "Auto enhance" menu casing, and the duplicate-cleanup buttons read "Quarantine extras"/"Recycle extras" instead of the awkward "extra(s)".

### Performance

- **Transient sidecar failures no longer re-hash unchanged images** - Incremental catalog scans retain the cached row and retry an inaccessible XMP metadata probe on the next scan, avoiding repeated SHA-256 work while still applying a real sidecar change once it becomes readable.
- **Navigation no longer waits on dispatcher-thread disk probes** - Edit-sidecar reads and file-size checks now run alongside decode, while neighbour preload size/dimension/staleness probes stay entirely on workers. RAW embedded previews update only the pixels, so folder thumbnails and metadata, color, and C2PA analysis start once for the final decode instead of being canceled and restarted.
- **Gallery workbench stays responsive on large folders** - Its wrapping tile grid now recycles only the visible rows plus a small scroll cache instead of constructing every thumbnail card and context menu at once.
- **Faster folder sort by date/size** - Sorting a large folder by modified/created/size now stats each file once instead of repeatedly inside the comparator, removing multi-second stalls on big or network folders.

## v0.2.26 - 2026-07-12

### Changed

- **Loupe and zoom-to-selection are documented** - The `?` keyboard-shortcuts overlay now lists the middle-button loupe and Ctrl+Shift+drag zoom-to-selection gestures so they are discoverable.
- **Serilog updated to 4.4.0** - Routine logging dependency refresh.

### Fixed

- **Save a copy tells you when it flattens frames** - Copying an animated GIF or multi-page source now notes "First frame only" in the confirmation so frame loss is not silent.
- **Color management signals when it can't apply** - Very large (memory-mapped) and tile-backed images that bypass the ICC transform now note "color management not applied" in the decoder status when the setting is on, instead of silently rendering uncorrected.
- **Color management applies immediately and safely** - Toggling color-managed display now re-decodes the current image and clears preloaded neighbors so the change is visible right away instead of only affecting the next opened file; the setting is also read safely across decode threads.
- **Loupe no longer conflicts with panning or sticks open** - The magnifier now captures the pointer (so a release anywhere hides it), is suppressed while a pan or zoom-to-selection gesture is active, and no longer freezes an in-progress pan; capture loss dismisses it cleanly.
- **Loupe and pixel readout skip gigapixel placeholders** - On very large tile-backed images the loupe no longer shows a solid block and the metadata HUD readout stays blank instead of reporting a placeholder pixel value for every position.
- **One pixel sample per mouse move** - Hover readout and inspector mode now share a single per-move pixel sample instead of sampling the image twice.
- **Session restore validates the persisted path** - Reopening the last image on launch now routes the stored path through the same guard as command-line arguments, rejecting device-namespace shapes and canonicalizing before opening, so a tampered settings value cannot reach the file-open path.
- **End-of-folder nudge no longer lingers after a jump** - The "stopped at end" flag is now cleared on first/last/jump/open navigation, so the nudge only appears on an actual stopped Prev/Next.

### Added

- **Stop at first/last image** - A new setting makes Prev/Next stop at the ends of the folder with a brief "End of folder" nudge instead of wrapping or switching folders; it takes precedence over sibling-folder auto-switch. Together with the existing wrap and sibling-folder options, all three end-of-list behaviors are now selectable.
- **Live pixel readout** - When the metadata HUD is open, hovering over the image now shows the pixel under the cursor (coordinate, hex, and RGB) in the HUD; it clears when the pointer leaves the image.
- **Loupe magnifier** - Hold the middle mouse button over the image to show a circular lens that magnifies the source pixels under the cursor (2x by default) without changing the base zoom; release or leave the image to dismiss it.
- **Color-managed display (opt-in)** - A new Appearance setting (off by default) honors an image's embedded ICC profile and converts wide-gamut colors (Adobe RGB, Display P3) to sRGB on decode, so tagged photos no longer render over-saturated. Applies to newly opened raster images via Magick.NET; untagged and huge/tiled images are unaffected.
- **Zoom to selection** - Hold Ctrl+Shift and drag a box over the image to zoom directly to that region; the marquee fills the viewport and re-centers. Escape or losing focus cancels the drag; click-sized boxes are ignored.
- **Reopen last image on launch** - A new General setting (off by default) makes Images reopen the last image you viewed when it starts with no file passed on the command line; if that file is gone, it opens to the empty state.
- **Zoom lock across image navigation** - A new toggle (command palette: "Toggle zoom lock") keeps the current zoom factor when moving to the next or previous image, re-anchoring the pan to center, so a series can be pixel-peeped at a fixed magnification. The choice persists.
- **Transparency checkerboard** - A new toggle (command palette: "Toggle transparency grid") draws a checkerboard behind images that carry an alpha channel so transparent regions read as transparent; the pattern scales with zoom and the choice persists.

### Fixed

- **Network egress log reads are lock-synchronized** - `TotalBytes` and the clipboard summary now snapshot the entry list under the same lock that guards recording, closing a narrow race reachable when dispatcher marshalling is bypassed (tests, startup/shutdown).
- **Super-resolution guards against dimension overflow** - The upscaled canvas size is now computed with 64-bit multiplication and clamped, so an extreme model scale factor cannot overflow and wrap to a bogus dimension.
- **Save a copy preserves image metadata** - Saving an unedited copy now reloads the source file through Magick.NET, so embedded EXIF, IPTC, XMP, and ICC color profiles survive the copy instead of being discarded by the in-memory pixel re-encode. Clipboard pastes and non-raster sources still use the pixel path.

### Security

- **Magick.NET upgraded to 14.15.0** - Bundles ImageMagick 7.1.2-27 and libheif >=1.22.0, addressing CVE-2026-32740 (heap write decoding crafted HEIF/AVIF grid tiles) and the 2026 ImageMagick heap/OOB-write CVEs on the untrusted-file fallback decode path Images uses for HEIF/AVIF/RAW/etc. Runtime-provenance docs and the vulnerable-package gate stay in sync.

### Changed

- **Theme selection is now available in Settings** - Appearance exposes Dark, Light, and Follow Windows choices that apply immediately, while the accessibility high-contrast override remains independent.
- **Shared chrome uses one premium visual language** - Dark and light surfaces now use neutral charcoal/cool-gray layering with a warm accent across buttons, tabs, selection, focus, and tool windows instead of mixing the new workspace with legacy purple and blue controls.
- **Keyboard focus no longer shifts layouts** - Shared buttons, tabs, checkboxes, text fields, expanders, and rail controls keep a stable one-pixel border while changing to the focus color, eliminating subtle size jitter during keyboard navigation.
- **Accessibility descriptions match current behavior** - Reduce Motion and high-contrast settings now describe their app-wide effects instead of older partial or future-tense behavior.

## v0.2.25 - 2026-07-09

### Changed

- **Viewer workspace reimagined around the image** - The main shell now uses a 52px navigation rail, neutral charcoal canvas, 300px contextual inspector, shorter filmstrip, warm focus/selection accents, and a compact navigation/fit/zoom command row with advanced actions removed from permanent chrome.
- **Inspector and empty states debloated** - Duplicate recent-folder/history sections, routine metadata warnings, advanced tool cards, marketing-style capability copy, and redundant settings/diagnostics actions no longer compete with the active image; deeper color analysis remains available under More details.
- **Gallery and settings hierarchy tightened** - Gallery now shares the viewer canvas, keeps search plus common sort modes visible, and uses warm tile selection; Settings uses a smaller title hierarchy and concise automatic-save status instead of a decorative header tile and feature inventory.
- **Premium viewer mockup captured** - The image-generated design target is preserved at `assets/mockups/premium-viewer-v0.2.25.png` for visual parity checks.

## 0.2.24

### Fixed

- **Motion photo state clears on load failure** - Load failures and empty-viewer transitions now clear motion-photo, companion-video, and motion-video-playback state so stale extract/play buttons from a previous file cannot appear on an error screen or empty viewer.
- **Annotation number baking matches overlay contrast** - The Magick.NET annotation baking path now picks black or white number text from the marker fill luminance, matching the WPF overlay that already did this, so saved images with light-colored number markers are readable.
- **Contact sheet colors are parameterized** - ContactSheetService no longer hardcodes Catppuccin Mocha hex values for text, placeholder, and watermark colors; callers can pass theme-appropriate colors via ContactSheetOptions.
- **Macro rename carries sidecar files** - MacroActionService rename-pattern steps now call SidecarCompanionFiles.TryMoveAlongside after File.Move, matching the sidecar-carry pattern in RenameService, DuplicateCleanupService, and FileHealthScanService.
- **Inpaint mode setter uses change guard** - IsInpaintMode and InpaintBrushRadius setters now use the standard Set(ref) change-detection pattern, eliminating redundant PropertyChanged notifications on same-value assignments.
- **Background removal clone is exception-safe** - BackgroundRemovalService.RunInference now guards the clone-then-composite sequence with try/catch so the cloned MagickImage is disposed if Composite throws.

## 0.2.23

### Fixed

- **Removed-file navigation updates immediately** - When the current image is deleted or removed from the navigator, the viewer now publishes the next path immediately while the async decode continues, so stale filenames do not linger in the shell.
- **Theme overlays load reliably from tests and secondary entry points** - Latte and high-contrast overlays now use assembly-qualified pack URIs, preventing resource lookup failures outside the main app startup path.
- **Catalog folder queries tolerate path normalization drift** - Folder queries now normalize both the requested folder and stored asset folders before comparison, so persisted relative or untrimmed paths still match.
- **Dialog owner lookup is dispatcher-safe** - Delete confirmations and secondary tool windows no longer read `Application.Current.MainWindow` from the wrong dispatcher thread.
- **Network egress entries survive dispatcher shutdown** - Network activity recorded after a WPF dispatcher is shutting down now falls back to direct insertion instead of disappearing behind a never-pumped dispatcher queue.
- **Store codec launch failures are honest** - If Windows cannot open the Microsoft Store codec extension link, Images now shows a failure toast instead of claiming the Store page opened.
- **Annotation marker labels stay readable** - Number annotations now choose black or white label text from the marker fill color, so white and yellow markers are no longer unreadable; annotation drags also recover cleanly from mouse-capture loss.
- **Canvas tools recover from mouse-capture loss** - Retouch, red-eye, exposure brush, crop, selection, and inspector drags now clear transient pointer state when WPF mouse capture is lost, preventing stale brush or drag state from leaking into the next pointer move.
- **Clipboard paste is not blocked by temp cleanup** - Clipboard image paste now continues if best-effort temp-image pruning cannot be queued, and paste feedback uses clearer status text.
- **Update-check network bytes are accurate** - Successful update checks now record the actual JSON bytes read when the server does not provide `Content-Length`, and oversized responses fail as a completed update-check error instead of an unexpected fault.
- **Session tray saves are collision-safe** - Saving a session tray now uses a unique sibling temp file instead of clobbering `*.tmp` files, and loading a session reports only entries that were actually added.

## 0.2.22

### Changed

- **Command palette taxonomy tightened** - Common viewer commands now rank first, heavyweight utilities appear under an Advanced tools category, task-oriented synonyms make advanced commands searchable, and Review-related queries no longer surface retired workflow entries.
- **Explorer-like folder sort fallback added** - Folder sorting now includes an explicit Explorer-like name order that uses a deterministic Shell-style fallback when Images cannot read a live Explorer window's private sort/grouping state.
- **Viewer shell debloated and premium-polished** - The default rail is now compact icon-only chrome, advanced Search/Batch/Export surfaces no longer occupy the primary rail, the right panel is renamed to Details, and advanced tool cards are removed from the default inspector surface.
- **Default viewer controls are quieter** - Compare, Export, Print, Save-copy, and GPS-strip actions no longer sit in the bottom viewer toolbar; those power tools remain available through the context menu and command palette.
- **Command palette mode noise reduced** - Legacy workflow-mode entries are no longer advertised in command search after the Review workflow removal.
- **Workflow modes collapsed to the default viewer** - Organize/Edit/Book/Diagnostics mode state is no longer restored or advertised; legacy stored values normalize back to Viewer.
- **Review documentation purged from live guidance** - Accessibility and product-planning docs no longer present the retired Review/rating workflow as current UI, and the manual screen-reader matrix now covers active viewer surfaces instead.
- **Runtime provenance docs are gated** - Release readiness now fails when current runtime/package documentation drifts from Magick.NET, SharpCompress, Ghostscript, jpegtran, or SQLite package provenance, and archive docs now match SharpCompress 0.49.1.
- **Manual dependency sweep completed** - `Microsoft.NET.Test.Sdk` moved to 18.7.0; NuGet vulnerable/deprecated scans are clean. Remaining outdated entries are transitive packages held by current ONNX DirectML and SQLitePCLRaw package graphs.

### Fixed

- **Recycle Bin dependency warning removed** - Delete-to-Recycle-Bin still uses the framework VisualBasic file-operation API, but the unnecessary `Microsoft.VisualBasic` NuGet package reference is gone and Release builds no longer emit `NU1510`.
- **Release readiness rejects placeholder package hashes** - The no-assets readiness path now validates committed package manifests for the current version, manifest generation rejects placeholder checksum entries, and SBOM generation works under Windows PowerShell 5.1.
- **Semantic Search Reveal selects files in Explorer** - Reveal now uses the Explorer selection helper instead of launching the result file's associated app, and missing results stay in-window with a warning.
- **External edit reload exceptions stay contained** - Exceptions thrown by the external-edit reload callback are now logged and surfaced through the existing toast path instead of escaping the dispatcher timer.
- **Sidecar writes are collision-safe** - XMP sidecar saves now use unique temp files and serialize final swaps so parallel tag/import/Picasa writes cannot collide on a shared temp path or leave stale temp files behind.
- **Import duplicate scans stay inside the destination tree** - Import Inbox destination hashing now skips reparse-point directories, matching the source scan guard so linked folders outside the library cannot be traversed for duplicate detection.
- **Import sidecar rollback is bounded** - Existing XMP sidecars larger than the rollback snapshot cap now fail the import before metadata edits, with copied/moved files rolled back and the oversized sidecar left untouched.

## 0.2.20

### Removed

- **Viewer Review workflow removed** — The Review rail button, side-panel rating/pick/reject card, assisted-culling scorer, Review workflow mode, Review command-palette entry, and Review hotkeys have been removed to keep the viewer lean.
- **XMP rating folder import removed from the main viewer** — The command-palette action that wrote imported sidecar ratings through the old Review path is gone; tag-sidecar import/export remains under Tag relationships.

## 0.2.19

### Fixed

- **Viewer top command bar removed** — The floating image command bar with Compare and Export actions no longer covers the top of the image viewport; the existing position/page/HUD chips now sit closer to the top edge.

## 0.2.18

### Fixed

- **XMP sidecars follow rename and quarantine moves** — Rename, undo, file-health rename/quarantine, and duplicate-cleanup quarantine now carry both `image.ext.xmp` and `stem.xmp` companion files without overwriting existing sidecars, and recovery records can restore moved sidecars with the image.
- **Case-only renames are real renames** — Renaming `photo.jpg` to `PHOTO.jpg` now performs the NTFS case-only move and updates the viewer instead of reporting a no-op.
- **Rename/navigation races no longer corrupt the folder list** — Rename and undo now update the navigator by captured source path instead of the current index, rename commits are busy-gated during in-flight loads, dead back-history entries are dropped, and external renames from supported to unsupported extensions refresh the folder list.
- **Semantic Search shutdown and CLIP inference are hardened** — Closing the window during indexing now cancels first and disposes ONNX sessions after the in-flight task finishes, CLIP inference is serialized against disposal, partial provider creation cleans up native sessions, text attention masks no longer truncate at token id `0`, and semantic/settings SQLite connections use private WAL cache mode.
- **C2PA inspection cannot fetch remote manifests** — c2patool reads now pass a no-network settings file, the child-process inspection is recorded in the network-egress log, system PATH is no longer searched for auto-run c2patool binaries, and only actual no-manifest output is classified as no-manifest.
- **Import Inbox keeps processing after malformed sidecars** — A corrupt pre-existing XMP sidecar now fails only that import request, rolls back any moved original, and preserves the rest of the batch result.
- **Review location clears remove stale XMP values** — Clearing a review location now removes the Photoshop City/State/Country and IPTC Location attributes instead of leaving old place metadata behind while reporting success.
- **Picasa face regions accept omitted leading zeros** — `rect64(...)` values with 1-15 hex digits are left-padded before parsing, so faces near the top or left edge are no longer dropped.
- **GPS-strip import fails closed on unreadable files** — Metadata preview read failures are now surfaced separately from "no GPS fields," so Import Inbox rolls back unreadable non-rewritable files when GPS stripping was requested.
- **Recursive scanners skip reparse-point directories** — Duplicate Cleanup, Import Inbox, and Catalog rebuild now avoid junction/symlink directories so cyclic folder links cannot trigger runaway traversal.
- **Tile cache eviction protects every active pyramid** — Eviction now skips all tile cache directories in active use or mid-build, so secondary huge-image previews cannot evict the main viewer's displayed pyramid.
- **Delete confirmations cannot remove the wrong file during slideshow** — Delete and lossless-JPEG trim confirmations now run under the operation-busy guard, and deletes remove the captured path instead of the current index if navigation changes while a modal dialog is open.
- **Crop Enter respects text entry focus** — Pressing Enter while a text box, password box, rich text box, or editable combo box has focus no longer applies an active crop selection.
- **Auto crop starts after busy-wrapped loads** — Freehand crop mode now starts after async/open-dialog/page-turn/slideshow loads clear their operation-busy status instead of only on synchronous open paths.
- **Loop-local stack buffers are hoisted** — Culling ranking and duplicate cleanup now allocate their 8x8 average-hash luminance buffer once per scan/rank operation instead of once per image, and the email header encoder reuses its UTF-8 rune buffer across header characters.
- **Dark-theme hover chrome is consistent on edge controls** — Archive book page-turn zones and annotation color swatches now use custom themed button templates instead of falling back to the default Aero2 hover paint.
- **Command palette results are announced by name** — The command palette list now has a localized automation name, and command items expose their display title instead of the view-model type name to assistive technologies.
- **Tool-window inputs have accessible labels** — Export preview, tag graph, reference board zoom, and crash details controls now expose localized automation names or `LabeledBy` associations.
- **Hint-tier text clears AA contrast** — `Text.Hint`, `SectionLabel`, and `MetaLabel` now use a dedicated per-theme `HintTextBrush` instead of decorative overlay colors.
- **Archive password prompts are themed** — The implicit `PasswordBox` style now mirrors the app's TextBox chrome, including dark surfaces, accent focus, caret, and selection brushes.
- **20 ms GIF frame delays stay fast** — Animated GIF playback now preserves valid 2-centisecond frame delays while still clamping shorter hostile delays.
- **Catalog rescans notice sidecar-only edits** — Incremental catalog rebuilds now compare cached sidecar path and modified time, so XMP rating/tag creation, edits, or removal refresh stale rows even when the image file itself is unchanged.
- **Import Inbox fails cleanly when tag sidecars cannot be written** — Tag export failures now fail the individual import, transfer rollbacks preserve any pre-existing destination sidecar, and in-place imports remove sidecars they created if a later post-import step fails.
- **Recovery restores are no longer limited to the newest 200 records** — Restore and reveal now look up the requested recovery id across the full log, while repeated status updates compact duplicate JSONL rows to the latest record state.
- **MicrosoftPhoto ratings map to the correct star value** — XMP imports, catalog sidecar reads, and review labels now interpret `MicrosoftPhoto:Rating` as the 0-99 Windows scale, and clearing a review rating removes stale element-form rating nodes too.
- **Wrong archive passwords reopen the password prompt** — SharpCompress cryptographic failures now map to `ArchivePasswordRequiredException` instead of escaping as generic archive read errors.
- **Catalog root stats are per-root again** — Multi-root catalog rebuilds now store each root's own indexed and failed counts instead of writing global totals into every `catalog_roots` row.
- **Model Manager no longer trusts stale same-length hashes** — Imported model manifests now store file modified time, and snapshot inspection rehashes modified files before reporting readiness.
- **Ghostscript version probing drains process output while waiting** — The version probe now reads stdout and stderr asynchronously before `WaitForExit`, avoiding pipe-buffer stalls from chatty `gs` builds.
- **Listen mode binds its socket exclusively** — The loopback TCP listener now sets `ExclusiveAddressUse` before binding so another same-user socket cannot reuse the listen port.
- **Network egress log rotation avoids per-entry full-file reads** — The JSONL writer now tracks persisted line count in memory and only reads/trims the log when the cached count exceeds the cap.
- **Support bundles redact more profile path forms** — Redaction now catches Windows short-name profile paths and `file:///C:/Users/...` URI forms in logs and manifest text.
- **Reveal in Explorer handles comma paths** — File selection now uses `SHParseDisplayName` plus `SHOpenFolderAndSelectItems` before falling back to Explorer's `/select,` command line.
- **Email draft cleanup on startup** — Local `.eml` share drafts are now pruned on app startup as well as draft creation, and the share status explains the seven-day local attachment-copy retention.
- **Magick write policy alignment** — Magick format writes now derive from the same high-risk delegate blocklist as extension checks, preventing future policy drift for PS/AI/SVGZ/MVG/MSL-style targets.
- **Background task diagnostics race fix** — Named background counters now serialize start/finish eviction, preventing same-name restarts from losing their running diagnostics entry.
- **Tile cache build-lock hardening** — Cache clears and eviction no longer drop per-directory build locks, preserving mutual exclusion for rebuilds that overlap cache deletion.
- **Collision-safe local JSON saves** — Smart collections and tag graph persistence now use GUID temp files with cleanup instead of fixed `.tmp` paths, avoiding cross-instance save collisions.
- **Thumbnail eviction resilience** — Cache eviction now skips vanished or inaccessible files during sizing instead of aborting the whole sweep.
- **OCR restart busy-state fix** — A stale canceled OCR extraction can no longer clear the busy panel while a newer extraction is still running.
- **External edit reload hardening** — Reload debounce work is now tied to the watched path and the watcher listens for filename changes so atomic rename-over saves trigger reloads without stale-file false toasts.
- **Slideshow state-machine hardening** — Empty or cleared image lists now stop slideshow state, single-image slideshows skip redundant reloads, and shuffle jumps within the current navigator list without folder re-enumeration.
- **Inpaint state guard** — Latent inpaint mask state now resets on navigation, load failure, and clear, and apply is gated by the same current-image capability check as entering inpaint mode.
- **Async command reentrancy guard** — Async commands now disable while their current execution is running, preventing duplicate motion-photo extraction or playback launches from racing to the same output.
- **Command predicate file-state cache** — Hot command and palette predicates now use cached current, compare, and culling-item availability instead of probing the file system on every WPF requery.
- **Main-view long operations yield to the busy panel** — Explicit file-list opens, external-edit reloads, delete advancement, and copy/move transfers now use async decode or worker-thread transfer paths so the dispatcher can render status before heavy work starts.
- **Secondary previews are capped and asynchronous** — Duplicate Cleanup, File Health Scan, Import Inbox, and Reference Board now load bounded preview images off the dispatcher instead of decoding full-size images during selection or card creation.
- **Tile and drag-over UI stalls reduced** — Huge-image tile bitmaps now decode on worker tasks before being attached to the canvas, and drag-over file verdicts are cached for the current file list instead of re-enumerating directories per mouse move.
- **Gallery refresh no longer blocks on smart filters** — Gallery navigation now reconciles thumbnails in place, and smart-filter indexing runs on a worker task with an indexing chip instead of synchronously rebuilding the collection on the dispatcher.
- **Unresolved visibility bindings collapse safely** — `BoolToVis` now treats `DependencyProperty.UnsetValue` like `null` while preserving the existing nonzero integer behavior used by count bindings.
- **Multi-file drops open the full supported set** — Dropping multiple image files now routes through the same file-list open path as multi-file argv instead of silently opening only the first dropped file.
- **Zoom survives layout changes** — `ZoomPanImage` now preserves user-modified zoom and pan through resize, fullscreen edge reveal, and gallery/filmstrip layout changes while untouched fit views still stay fitted.
- **Right-click double-clicks no longer reset zoom** — The viewer's double-click zoom toggle now only handles the left mouse button, leaving right-button context-menu gestures alone.
- **Annotation editing keeps text input and load errors intact** — Enter/Escape no longer apply or close the annotation window while text-entry controls have focus, and missing-file preview failures keep their error status instead of being overwritten by the empty-state message.
- **Batch Processor reports unexpected item faults** — Non-cancellation item failures now become failed run rows, partial result slots are compacted before summary/reporting, and the window shows a failed status instead of crashing on unexpected run exceptions.
- **Import Inbox destination changes wait for scans** — The destination chooser is now disabled during busy scans/import work, preventing overlapping reloads from re-enabling the window early.
- **Reference Board drag state recovers after capture loss** — Drag handles now clear stale state when mouse capture is lost, and mouse-up events are only handled for an active drag.
- **Export Preview cancels superseded encodes** — Preview rebuilds now cancel prior work, ignore stale completions, cancel on close, and suppress constructor-time setting events until the window has finished loading.
- **Main workspace shell feels more premium** — The viewer now has a persistent workflow rail, warmer accent hierarchy, and a denser inspector surface aligned with the generated premium mockup.
- **Filled-button contrast is theme-safe** — Primary and danger buttons now use a per-theme `OnAccentBrush`, Latte clears AA contrast, and high-contrast mode uses system highlight text.
- **Latte native caption matches the window body** — The DWM caption color now uses Latte Base instead of Mantle, and the dead non-adaptive focus elevation token was removed.
- **Open tool windows repaint on theme changes** — Status dots, status cards, reference-board cards, perspective handles, and the archive-password dialog now bind code-behind theme brushes through live resource references instead of stale brush snapshots.
- **Screen-reader names are localizable** — Remaining literal `AutomationProperties.Name` values in the main shell and secondary windows now resolve through localization resources, and the localization script fails on new hardcoded automation names.
- **Grid splitters have usable hit targets** — Tool-window splitters now use a shared 6px transparent hit area with a centered 1px themed hairline and visible keyboard focus styling.

## 0.2.17

Roadmap drain: resolves the remaining code-ready items from the v0.2.16 deep audit.

### Fixed

- **Email drafts encode non-ASCII correctly** — Subject headers use RFC 2047 encoded-words, the attachment name/filename parameters use the RFC 2231 extended syntax, and the quoted-printable-declared body is now actually QP-encoded with soft line breaks. Non-ASCII filenames and source paths previously garbled in mail clients; ASCII values are unchanged.
- **Super-resolution tiling uses the model's true scale** — The tiled upscale path sized its canvas and positioned composited tiles by the caller's assumed scale factor; a model whose real output ratio differed produced gaps or overlaps. The scale is now derived from the model's output/input dimension ratio. (Staged path; no live caller yet.)
- **Tile cache never evicts the active pyramid** — `EvictIfOverCap` could prune a single pyramid larger than the 1 GB cap out from under the viewer. The directory backing the current display is now tracked and skipped during eviction.

### Internal

- **Deterministic update-check tracker test** — Observes the per-name `update-check:manual` counter mid-run instead of the process-wide totals, removing a parallel-execution flake.

## 0.2.16

Deep audit release: 40+ correctness, security, privacy, and quality fixes across the viewer, editing pipeline, import/export, AI services, theming, and accessibility.

### Security & privacy

- **GPS strip now removes XMP and IPTC location** — The privacy strip only removed EXIF GPS tags, leaving `exif:GPS*` in the XMP packet and IPTC City/Country records intact while reporting success. It now scrubs XMP/IPTC location for the GPS category and drops the XMP/IPTC/8BIM profiles for the All category.
- **Import Inbox honors the GPS-strip contract for all formats** — The strip silently skipped non-JPEG/TIFF; it now covers PNG/WebP and fails the import when a format it cannot rewrite still carries location data instead of importing it.
- **Listen-mode UNC bypass closed** — Forward-slash UNC paths (`//host/share`) slipped past the block and triggered an outbound SMB connection; the check now runs on the canonicalized path. Added a 10-second pre-auth deadline and an 8-connection concurrency cap.
- **Support bundle redacts log paths** — Raw logs (which contain full image paths) are now streamed through `%USERPROFILE%` redaction, matching the bundle's own privacy claim.
- **Model integrity re-verified on drift** — `ModelManagerService` rehashes when the on-disk length differs from the manifest, so post-import corruption no longer reports "SHA-256 verified".

### Correctness

- **"Max dimensions" resize is shrink-only** — Every max-dimension resize path (export, export preview, batch, macro, non-destructive edit) upscaled images smaller than the bound; all now use `Greater = true`.
- **Q16 tensor scaling fixed** — The ONNX tensor converters divided saturated blue/green pixels by 255 instead of 65535 on the Q16 build, corrupting inpaint patches and CLIP embeddings. All four converters divide by `Quantum.Max`.
- **Encrypted zip/cbz password prompt** — Password-protected archives went through a reader that cannot detect encryption; routing through SharpCompress restores the prompt.
- **Preload cache leak and staleness** — Size/megapixel-skipped files leaked LRU entries that let the decoded cache exceed its cap; cached decodes are now invalidated when the source file changes on disk.
- **Reload bypasses the preload cache** — Reload could serve a stale pre-edit decode; it now forces a fresh decode.
- **Rename undo and cancel** — Undo "follow the file" compared the wrong path (dead code); a canceled extension edit was committed on the next navigation. Both fixed, and the external-edit watcher re-arms after rename/undo.
- **Recycle-bin cancel no longer reports success** — A canceled shell error dialog returned as "deleted" and removed a still-present file from the list with a phantom recovery record.
- **Explicit-list open matches by full path** — Opening a same-named file from another directory in a cross-directory list no longer silently shows the wrong image.
- **Motion-photo detection scans the whole trailer** — Only the last 256 KB was scanned, missing real 1–4 MB Samsung/Pixel videos; it now walks backward in 1 MB chunks up to 128 MB.
- **Picasa import** — Structural sections (`[Picasa]`, `[Contacts2]`, `[.album:*]`) are no longer reported as missing images; album IDs resolve to names; one unreadable sidecar no longer aborts the whole folder.
- **Import-into-source-folder** no longer duplicates/renames the original (dead `SamePath` guard).

### Crash prevention

- **PerspectiveCorrectionWindow** guards its preview decode (WIC-rejected but Magick-decodable images crashed on open).
- **NonDestructiveEditService** tolerates a malformed sidecar on the write path, matching the read path.
- **EditStackWindow** copy-summary handles a busy clipboard instead of crashing.
- **BatchProcessorWindow** cancels its run on window close.

### Reliability

- **ApplyInpaint hardened** — Now runs behind the operation-busy guard on `AsyncRelayCommand` with a writeback backup, recovery record, and preload reset.
- **Writeback backup failures abort the write** — A failed backup under a configured policy no longer lets the lossy in-place write proceed silently.
- **XMP sidecar writes are atomic** — Picasa, review-label, tag, and import-rating sidecars write via temp-then-swap so a crash can't truncate a merged sidecar.
- **File-health scan skips reparse points** — A junction cycle no longer grows the scan unbounded.
- **EML attachment** fills its buffer across short reads to avoid base64 corruption; **ExifTool** argfile sets `-charset filename=UTF8` for non-ANSI paths.
- **Slideshow** skips ticks while an operation is busy; **OCR** extraction is canceled when switching modes so it can't resurrect its overlay.
- **CLIP provider disposed** — Semantic Search disposes its ~600 MB of native ONNX sessions on window close.
- **Network-egress log** caps in-memory entries and serializes JSONL appends.

### Performance

- **Catalog uses its NOCASE index** — Hot lookups wrapped both sides in `lower()`, defeating the unique index and making rebuild O(n²); they now compare the collated column directly.
- **Gallery smart-filter uses a 64×64 decode hint** instead of decoding every folder image at full resolution just to derive a palette color — the dominant cost behind the gallery-open freeze.

### Accessibility & UX

- **Zoom-at-cursor honors rotation and flip** — Wheel/pinch zoom no longer lurches away from the cursor on rotated or flipped images.
- **Accessible names** added for unlabeled Batch Processor inputs, Import Inbox per-file checkboxes, and the archive password box.
- **High Contrast** refreshes on Windows scheme change, hands the caption to the system scheme, and remaps `CrustColor` to a surface color; editing-overlay tints are now per-theme tokens.
- **Microcopy** — localized the password-dialog "OK", `cancelled`→`canceled`, `directory`→`folder`, and title-cased the crash-dialog title.

## 0.2.15

### Internal

- **AsyncRelayCommand catches non-cancellation exceptions** — Exceptions from command methods now log + toast instead of propagating through `async void` to `DispatcherUnhandledException` and terminating the app. Static `CommandFaulted` event enables graceful error reporting.
- **ApplyRotationToFile and ApplyCropSelection converted to async** — Both now yield to the dispatcher after `BeginOperationStatus` so the operation status overlay renders before the heavy I/O begins. Previously the UI thread never yielded, causing a visible freeze with no feedback on large images.
- **BackgroundTaskTracker evicts idle per-name entries** — Completed task entries are removed from `_byName` when Running reaches 0, preventing unbounded dictionary growth during long sessions with many file operations.

### Theme

- **PerspectiveCorrectionWindow uses themed overlay brushes** — Handle strokes, polygon fills, and label backgrounds now use `AccentBrush`, `AccentSelectionBrush`, `TextBrush`, and `FloatingChromeBrush` instead of hardcoded Mocha hex values. Overlays now adapt correctly to Latte and HighContrast themes.

## 0.2.14

### Correctness

- **GoBack/GoForward no longer corrupt history on failed navigation** — Stack mutations now deferred until `Open` succeeds. Previously, navigating to a deleted file permanently lost the history entry and created a self-referencing forward-navigation loop.
- **OpenRecentArchiveCommand converted to AsyncRelayCommand** — Last remaining `RelayCommand(async ...)` pattern; any non-cancellation exception from archive open would crash the app.
- **SaveAsCopyAsync captures source path before yield** — `CurrentPath` was re-read after `YieldForOperationStatusAsync`, creating a TOCTOU race if a FileSystemWatcher event changed the current file during the dispatcher yield.
- **AnnotationsWindow.LoadPreview handles corrupt/unsupported files** — Added `UriKind.Absolute` (prevents fragment truncation on filenames containing `#`) and try-catch for graceful failure on bad image data.
- **MainWindow MotionVideoPlayer adds UriKind.Absolute** — Same fragment-truncation fix for motion photo video playback.
- **QuarantineExtrasButton_Click catches I/O exceptions** — Only async-void handler with no catch; disk errors during quarantine would crash the app.
- **Window_Drop removes dead async keyword** — No awaits, generated CS1998 warning.

### Theme

- **ViewerSurfaceBrush added to all three theme dictionaries** — Referenced by 4 tool windows (Annotations, Adjustments, Effects, PerspectiveCorrection) but never defined; DynamicResource silently resolved to null, making preview canvases transparent.
- **CrashDialog success state uses themed SuccessPanelBrush** — Was the only code path hardcoding Mocha green instead of using the theme resource.
- **ReferenceBoardWindow uses themed AccentPanelBrush and SurfacePanelBrush** — Group frame and header backgrounds now adapt to Latte and HighContrast themes.

### Internal

- **CancellationTokenSource disposed on window close** — DuplicateCleanupWindow, FileHealthScanWindow, ImportInboxWindow, and SemanticSearchWindow now dispose their CTS in the Closed handler instead of only cancelling it.
- **NetworkEgressService.LoadPersistedEntries dispatches to UI thread** — Called via `Task.Run` at startup; `_entries.Add` on the thread pool thread would throw `NotSupportedException` if the About panel was already bound.
- **RestartSlideshowTimer reuses existing DispatcherTimer** — Previously allocated a new timer on every restart without unsubscribing the old Tick handler, causing redundant allocations during slideshows.

## 0.2.13

### Theme

- **Runtime theme switching now updates all windows** — Converted 488 `StaticResource` brush references to `DynamicResource` across MainWindow and 27 secondary window/overlay XAML files so theme changes via Settings take effect immediately without restarting or re-opening windows.

### Internal

- **AsyncRelayCommand replaces async void command pattern** — 25 `RelayCommand(async () => await …)` usages in MainViewModel now use `AsyncRelayCommand` which accepts `Func<Task>`, catches `OperationCanceledException` (expected during navigation), and routes other exceptions through the dispatcher crash handler instead of terminating the process.
- **Preload eviction now cancels in-flight decodes** — PreloadService uses per-entry linked `CancellationTokenSource` so evicted cache entries cancel their background decode immediately instead of running to completion.
- **Navigation history uses O(1) bounded collection** — DirectoryNavigator back/forward stacks replaced with `LinkedList<string>` so push-past-cap drops the oldest entry in O(1) instead of O(n) stack-to-array-to-stack rebuild.
- **Monitor work area fallback returns physical pixels** — `GetCurrentMonitorWorkArea` fallback paths now convert `SystemParameters.WorkArea` from logical to physical pixels, matching the method's contract and preventing double-conversion by callers.

### UX

- **Premium main-viewer command polish** — Viewport context menus are now scroll-bounded, sectioned, and submenu-capable; first-run and side-panel launch actions use consistent ellipsis copy, icon semantics, and left-aligned compact tool buttons.
- **Shared premium tool-window shell** — Secondary workbenches now use consistent header, sidebar, workspace, status-bar, icon-tile, and empty-state treatments so batch export, cleanup, recovery, model, semantic search, and edit tools feel like one coherent product surface.

### Fixes

- **Viewport context menu is smoke-covered** — Right-clicks from the image viewport now open the existing context menu, and the smoke gate verifies the constrained-window menu stays bounded or scrollable, exposes grouped root commands, opens the Compare submenu, and reaches `Compare with…` by keyboard.
- **Secondary window resource crashes are covered** — shared path converters are now registered in the theme dictionary and About's background-jobs card no longer references Settings-only resources, preventing startup/About XAML crashes from missing `StaticResource` keys.
- **Semantic search fallback is explicit** — CLIP provider creation/preprocessing failures now log warning context, semantic status reports the active provider plus fallback reason, the search window shows deterministic fallback copy, and fixture tests pin deterministic query ranking.
- **Trust-path diagnostics no longer disappear silently** — C2PA runtime/manifest failures, contact-sheet degraded reads, listen-mode client errors, ExifTool cleanup failures, and performance-report storage failures now log contextual diagnostics; About diagnostics now shows C2PA runtime degraded/ready status.
- **C2PA trust environment setup no longer throws** — c2patool inspection now initializes empty trust-anchor/config variables only when absent, preventing missing environment keys from turning a valid no-manifest read into an inspection error.
- **Dispatcher fatal exceptions no longer resume the WPF app** — the dispatcher crash path now logs, writes the crash record/minidump, shows the crash dialog, flushes logs, and returns unhandled so WPF terminates instead of continuing in an undefined state.
- **ExifTool process output no longer risks pipe-buffer stalls** — the process runner now waits asynchronously while stdout and stderr drain, kills timed-out process trees, and preserves any completed output for diagnostics.
- **Tile pyramid cache builds are serialized per image** — concurrent requests for the same deep-zoom pyramid now reuse a per-cache-key build lock, re-check the completed manifest inside the lock, and publish `pyramid.json` atomically after tiles are written.
- **Import inbox GPS-strip failures are no longer silent** — when a requested GPS metadata strip fails after a copy or move, the import is reported as failed and the transferred file is rolled back where possible so a GPS-bearing file is not silently accepted into the destination.
- **APNG export capability correction** — Save/export no longer advertises APNG as a writable target when the runtime cannot encode it reliably. APNG files still open and inspect as before; `.apng` export targets resolve to PNG.
- **Viewport context menu hit target restored** — the image viewport is explicitly hit-testable, so right-click opens the bounded context menu even when the pointer lands on empty viewport space around the rendered image.

- **Parallel batch filename collision** — `RunAsync` now reserves output filenames via `ConcurrentDictionary` across parallel tasks, preventing TOCTOU races where two concurrent writes could silently overwrite the same output file. Also removed a pre-loop cancellation check that could leave null entries in the task array, causing `Task.WhenAll` to throw `ArgumentException` instead of `OperationCanceledException`.
- **ListenService path canonicalization** — incoming TCP paths are now canonicalized with `Path.GetFullPath` before the existence check, preventing `..` segment traversal.
- **Catalog LoadAllAssets missing palette** — `ReadAssetRecord` now reads the `palette` column, so `CatalogRebuildResult.Assets` returns palette data after a scan.
- **External-edit watcher re-armed after navigation** — `ExternalEditReloadController.Arm` is now called in `CompleteCurrentLoad`, so FileSystemWatcher detects external edits to the current file after arrow-key navigation, not just after initial open.
- **CurrentPath set before controller refreshes** — `CurrentPath` is now assigned before metadata/color/C2PA controller refreshes fire, closing a brief window where controllers could compare against the previous path.
- **Activity panel faulted count asymmetry** — Background activity running/faulted counts now derive from the same job set displayed in the panel, preventing the panel from hiding while faulted jobs are still in the visible list.
- **Slideshow timer crash resilience** — Slideshow timer tick is now wrapped in a top-level try-catch to prevent silent slideshow stops on unexpected decode errors.
- **ListenService broken state on port bind failure** — `Start()` no longer leaves the service permanently broken when the requested port is in use; a subsequent call with a different port can succeed.
- **Thumbnail cache quota tracking** — LRU eviction no longer decrements the tracked total before confirming the file delete succeeded, preventing cache growth past the configured cap when files are locked by other processes.
- **NonDestructiveEditService temp file collision** — Atomic sidecar writes now use GUID-based temp filenames to prevent concurrent saves of the same sidecar from clobbering each other.
- **Catalog rebuild crash on inaccessible directories** — Directory enumeration during catalog rebuild now uses `IgnoreInaccessible` to skip permission-denied subdirectories instead of aborting the entire scan.
- **Duplicate cleanup IndexOutOfRange guard** — Similar-image finding summaries and sort now safely handle findings with fewer than two candidates.
- **Tile cache lock memory leak** — Build lock entries are now removed when tile pyramid caches are cleared, preventing unbounded dictionary growth in long sessions.
- **Import inbox GPS strip ordering** — Post-import edits now write sidecar tags and ratings before stripping GPS metadata, so rollback on failure preserves the original file's GPS coordinates.
- **Atomic write TOCTOU race** — Export and batch processor atomic writes now use `File.Move(overwrite: true)` instead of `File.Exists` + `File.Replace/Move`, eliminating a race where a concurrent write could cause data loss.
- **MenuItem hardcoded checked-highlight color** — Replaced a Mocha-specific `#1A89B4FA` in the global MenuItem template trigger with the theme-aware `AccentPanelBrush`.
- **DarkTheme missing theme name marker** — Added `Images.Theme.Name` resource key to DarkTheme for consistency with Latte and HighContrast dictionaries.
- **Gallery focus trap** — Gallery ListBoxItem now sets `Focusable="False"` to delegate keyboard focus to child buttons, matching the filmstrip pattern.
- **Annotation swatch accessibility** — Color palette buttons now have `AutomationProperties.Name` and `ToolTip` so screen readers can identify each color.
- **Sibling folder settings localization** — The sibling folder auto-switch toggle in Settings now uses localized resource strings instead of hardcoded English.
- **RAW preview loading state** — `IsImageLoading` is now re-set to `true` after displaying the embedded JPEG preview, so the loading indicator stays visible while the full RAW decode runs.
- **ApplyInpaint busy guard** — `ApplyInpaintCommand.CanExecute` now checks `!IsOperationBusy`, preventing double-invoke of concurrent inpaint operations.
- **Slideshow flush pending rename** — `SlideshowTimer_Tick` now calls `FlushPendingRename()` before navigating, so a rename in progress is committed rather than silently discarded.
- **CatalogService SHA-256 file sharing** — `ComputeSha256` now opens files with `FileShare.ReadWrite | FileShare.Delete`, matching the rest of the codebase and preventing failures on files being edited by another application.
- **Theme-aware window captions** — `WindowChrome.ApplyDarkCaption` now checks `ThemeService.CurrentMode` and applies Catppuccin Latte light caption colors when the light theme is active, preventing a dark title bar clashing with a light window body.
- **Controller error logging** — `ExternalEditReloadController`, `PhotoMetadataController`, and `ColorAnalysisController` now log exceptions instead of silently swallowing them in catch blocks. `ExternalEditReloadController.Arm` now catches specific exception types instead of bare `catch`.
- **ListenService token truncation** — session token is now logged as the first 8 characters only, preventing full token exposure in log files or support bundles.
- **ImageLoader QuickDimensions reliability** — `BitmapCacheOption.OnLoad` ensures all metadata is read before the stream closes, preventing silent failures on codecs that defer dimension reading.
- **Fullscreen edge-hover panel bugs** — fixed two issues: (1) hiding logic no longer depends on the other panel's state, so each panel hides independently when the mouse leaves its edge zone, (2) `HideFullscreenPanels` now explicitly collapses the right side panel, preventing a WPF binding-cleared bug where the side panel would stay visible on the second fullscreen entry.
- **Stabilized STA supersession controller tests** — `PhotoMetadataControllerTests` and `ColorAnalysisControllerTests` supersession tests no longer intermittently fail during parallel full-suite runs. Root cause was thread pool starvation; fixed by releasing the blocked reader immediately after supersession, widening `PumpUntil` deadline to 5s, and extending the dispatcher drain interval.
- **Release-readiness script repaired** — removed stale `PROJECT_CONTEXT.md` requirement that blocked valid releases; script now validates the current `ROADMAP.md`/`Roadmap_Blocked.md` policy instead.
- **Stale version references corrected** — release support policy updated from `0.1.x`/`net9.0` to `0.2.x`/`net10.0`; release checklist updated to match current roadmap hygiene rules.
- **23 hardcoded XAML hex colors replaced with semantic theme tokens** — status chips, selected-item highlights, overlay dimmers, and subtle surface backgrounds across 8 windows now consume `DynamicResource` brushes from the theme dictionaries. New tokens: `OverlayDimmerBrush`, `SubtleSurfaceBrush`. Light/dark/high-contrast themes all updated.
- **Async void crash prevention (3 handlers)** — `EditStackWindow.ExportButton_Click`, `ImportInboxWindow.ImportButton_Click`, and `ImportInboxWindow.ImportPicasaButton_Click` now catch I/O exceptions instead of crashing the app on disk-full or file-locked errors.
- **FindResource → TryFindResource (14 call sites)** — Programmatic resource lookups in `ReferenceBoardWindow`, `AboutWindow`, `CrashDialog`, and `MainWindow` now use `TryFindResource` with fallbacks, preventing crashes on missing theme resources.
- **Cross-monitor DPI correction** — `MonitorService.MoveWindowToMonitor` now queries the target monitor's DPI via `GetDpiForMonitor` instead of using the source monitor's DPI, fixing incorrect window sizing when moving between monitors with different DPI scaling.
- **CancellationTokenSource disposal leaks (4 windows)** — `DuplicateCleanupWindow`, `FileHealthScanWindow`, `SemanticSearchWindow`, and `ImportInboxWindow` now dispose the old CTS before creating a new one on re-scan.
- **FolderPreviewController semaphore disposal race** — Removed premature `SemaphoreSlim.Dispose()` call that raced with in-flight thumbnail decode tasks during shutdown, causing misleading error log entries.
- **CurrentPath data race in SaveAsCopy and PlayMotionVideo** — `CurrentPath` is now captured into a local variable before `Task.Run` lambdas in `SaveAsCopyAsync` and `PlayMotionVideoAsync`, preventing a race where the rename debounce timer could change the path between the null-check and lambda execution.
- **ListenService shutdown hang** — Replaced synchronous `Dispatcher.Invoke` with `BeginInvoke` in the TCP listen callback to prevent hangs when the dispatcher message pump has stopped.
- **Loading indicator storyboard leak** — Added `StopStoryboard` exit action to the loading ellipse pulse animation so each load cycle does not accumulate orphaned composition-thread clocks.
- **STA test stability** — Added `MainViewModelStateTests` to the serialized `WpfSmoke` collection to prevent intermittent failures from STA thread pollution.

### Features

- **Local assisted culling lane** — Review mode can score the current folder with local-only sharpness, exposure clipping, similarity, and existing XMP rating/pick/reject signals, show reasons per ranked file, and apply keep/reject labels without network access.
- **C2PA export provenance handoff** — Export preview and Save a copy now report whether Content Credentials will be preserved, written through an approved C2PA writer, or omitted; current re-encoded exports explicitly omit credentials unless a future approved writer is configured.
- **Picasa metadata migration importer** — Import Inbox can parse folder-level `.picasa.ini` files plus optional `contacts.xml`, then write XMP sidecars for star ratings, album tags, and face regions without modifying source images.
- **Multi-path launch sessions** — `Images.exe a.jpg b.png c.webp` opens an explicit ad hoc set spanning different folders. Next/previous/Home/End navigate the argument list in order; single-path launch falls back to the existing folder scan. `DirectoryNavigator.OpenExplicitList` supports the feature with 3 new regression tests.
- **Redacted support bundle export** — About window now offers a one-click "Export support bundle" that writes a ZIP containing system info, codec report, network activity summary, diagnostics status, recent logs, crash log, recovery records, redacted settings, and cache health. No image bytes or private paths are included.
- **Local data management panel** — Settings Diagnostics now shows per-store sizes (thumbnails, logs, recovery, semantic index, models, network log, settings DB) and offers individual clear actions for thumbnails, logs, recovery log, and network activity with a refresh button.
- **UIA accessibility contract assertions** — FlaUI smoke tests now verify canvas automation name/help text pattern, window title with filename, navigation button names, toolbar button names, and folder position chip per the documented UIA tree.
- **XMP write-through for color labels, keywords, and location** — `ReviewLabelService` now writes `xmp:Label` (color labels) and IPTC/Photoshop location fields to sidecars. `XmpSidecarImportService.ApplyFolder` applies ratings, color labels, keywords, and location in one pass with per-field counts. 4 new regression tests.
- **CLIP pipeline validation with explicit fallback reasons** — Model Manager exposes a Validate button that runs step-by-step checks (file availability, tokenizer load, preprocessor config, ONNX session creation, text embedding smoke) and surfaces exact failure reasons. `SemanticSearchService.ClipFallbackReason` explains why the deterministic provider was used.
- **Start surface with recent folders, books, and import inbox** — the no-image launch panel now shows recent folders (clickable, bound to MRU), recent archive books with page progress, and an Import Inbox button alongside the existing Open/Paste/Settings/Diagnostics actions.
- **Background jobs center** — `BackgroundJobsService` tracks running and recent async tasks with name, state, duration, error, and affected count. The About window surfaces a Jobs panel with session summary and per-job details.
- **Workflow modes** — Viewer, Review, Organize, Edit, Book, and Diagnostics modes switch between chrome presets (filmstrip, metadata HUD, gallery, review labels). Modes are accessible via the command palette ("Mode: Review", etc.), persisted across sessions, and respect peek/fullscreen isolation.

- **Quick keyword sets** — `KeywordSetService` persists named keyword presets to `keyword-sets.json`, supports add/remove/rename/apply to image sidecars via `TagGraphService`, and exports/imports definitions as portable JSON. 7 regression tests.
- **Performance budget CLI** — `Images.exe --perf-report` measures process-to-CLI time, directory scan, thumbnail cache health, settings DB access, and memory working set against configurable thresholds with pass/warn status.
- **Incremental catalog rescan** — `CatalogService.Rebuild` now skips unchanged files (same size + modified time), removes stale paths, and reports reused/updated/removed counts. Repeat scans avoid re-hashing ~90% of files for large libraries. 2 regression tests.
- **Cross-folder session tray** — `SessionTrayService` manages an ordered, deduplicated list of image paths from any folder. Add, remove, reorder, save/load plain text file lists (with comment and blank line tolerance), and filter to valid entries. 7 regression tests.
- **Saved smart collections** — `SmartCollectionService` persists named filter collections (rating, tags, format, orientation, dimensions, date, duplicate status) to `smart-collections.json`. Collections can be added, removed, renamed, reordered, and applied against catalog/gallery items. 11 regression tests.
- **Contact sheet export** — `ContactSheetService` plans grid layouts from image lists and renders PNG contact sheets with Magick.NET, thumbnail fitting, filename captions, unreadable-file fallback, and optional watermark. 6 regression tests.

### Infrastructure

- **Pseudo-locale overflow gate** - `Strings.qps-ploc.resx` can be regenerated locally, localization validation now requires and checks the pseudo-locale by default, and WPF smoke coverage opens key secondary windows under expanded strings to catch critical control clipping.
- **Magick.NET security policy gate** - Codec runtime startup now applies and reports app-level Magick.NET resource limits, Ghostscript-gated document previews, huge-dimension rendering guards, and blocked PDF/EPS/SVG/MVG/MSL/URL-style write targets; release diagnostics now fail if the policy is not enforced.

- **Local release readiness parity restored** — `scripts\Test-ReleaseReadiness.ps1` now runs version sync, restore, Release build, tests, high/critical vulnerability blocking, localization parity, release diagnostics, checksum generation, and WinGet/Scoop manifest validation from one local command; release/trust docs now describe local-only gates instead of removed hosted workflows or Dependabot.
- **Secondary tool-window UIA smoke coverage** — Smoke-gate tests now open Settings, About/Diagnostics, Duplicate Cleanup, Semantic Search, Model Manager, and Import Inbox with app theme resources, then verify titles, named controls, keyboard focusability, and critical UIA help text hygiene.
- **Edge-hover contextual panels in fullscreen** — in fullscreen mode (F11), the bottom toolbar and right side panel auto-hide for zero-chrome image viewing. Moving the mouse to the bottom or right edge of the screen reveals the respective panel; panels auto-hide after 2 seconds when the mouse leaves the edge zone.
- **Draggable comparison divider in export preview** — the side-by-side export preview now has a draggable GridSplitter between original and encoded output. Drag the divider to adjust comparison proportions; the splitter hides in difference-view mode.
- **Color palette extraction in catalog** — catalog scans now extract the dominant color palette (red/orange/yellow/green/cyan/blue/purple/pink/dark/light/gray) from each image and persist it in the catalog DB. Gallery `palette:` filter tokens query catalog-backed palette data. Schema migrated v1→v2 with `ALTER TABLE ADD COLUMN palette`.
- **Embedded-JPEG-first RAW preview** — RAW files (DNG/NEF/CR2/CR3/ARW/RAF/ORF/PEF and 25+ formats) now show the embedded EXIF thumbnail immediately while the full demosaic decode runs in the background. The async load path extracts the preview via `MagickImage.Ping` + `ExifProfile.CreateThumbnail` without reading the full RAW data. `LoadResult.IsPreview` flag distinguishes preview from full decode.
- **Parallel batch processing** — `BatchProcessorService.RunAsync` processes files concurrently with `SemaphoreSlim`-bounded parallelism (default `ProcessorCount - 1`). The batch window now reports per-file progress during runs. Throughput scales with available cores on large batches. 3 new regression tests.
- **WCAG 2.5.7/2.5.8 accessibility audit** — fixed two undersized interactive chips (channel isolation and slideshow indicator) with `MinHeight="24"`. Documented drag-operation alternatives and known limitations in `docs/accessibility.md`.
- **Test coverage for 11 previously untested services** — 89 new regression tests covering BackgroundJobsService, SupportedImageFormats, WritebackGuardService, WorkflowModeService, DirectorySortMode, PerformanceBudgetService, MetadataEditService, CatalogQueryService, AppInfo, LaunchTiming, and SupportBundleService. Total test count: 708 passed.
- **CI runner pinned to `windows-2025`** — CI, release, and security workflows now use `windows-2025` instead of `windows-latest` to avoid breakage from the June 2026 runner-image migration to Windows Server 2025 + VS 2026.
- **WinGet and Scoop manifest validation** — the release workflow now validates generated Scoop JSON (version, URL, SHA-256 fields) and WinGet YAML (PackageIdentifier, version match) before uploading, with optional `wingetcreate validate` when available.

### Infrastructure

- **WPF smoke tests split into required gate and exploratory tiers** — `LaunchAndClose` and `OpenFixtureImage` now run as a required CI gate (`SmokeGate` category); keyboard-driven interactive tests remain `continue-on-error`.
- **Migrated xUnit v2.9.3 → v3.2.2** — dropped deprecated `Xunit.SkippableFact` in favor of native `Assert.Skip()`; updated test runner to v3.1.5.
- **Deprecated/outdated package CI reporting** — CI and daily security workflows now report deprecated and outdated NuGet packages alongside existing CVE gates, uploading maintenance reports as workflow artifacts.

### Features

- **Operation-chain batch workflow** — Batch Processor now exposes an ordered copy pipeline for resize, rotate, flip, metadata stripping, rename patterns, and export settings. Preview shows output names, dimensions, size deltas, warnings, dry-run remains default, and preview/run work can be canceled.
- **Export preview linked inspection** — Export Preview now uses synchronized pan/zoom canvases for original and encoded output, plus a toggleable difference view generated at encoded dimensions for lossy/export review before saving.
- **Command registry and shortcut rebinding** — `CommandShortcutService` centralizes command IDs, default shortcuts, and user overrides in the `hotkeys` SQLite table. Settings exposes a Hotkeys section with per-command rebinding, conflict detection, and reset-to-default. The command palette, keyboard dispatch, and settings summary now all consume the same registry.
- **Image loading state indicator** — the viewport now clears stale content and shows a pulsing loading indicator while decoding large images. `IsImageLoading` is set true in `PrepareCurrentLoad` and cleared in `CompleteCurrentLoad` (including early-return paths), preventing stale previews after navigation during decode.
- **Centralized ONNX Runtime provider** — one `OnnxRuntimeService` now owns DirectML/CPU probing, SessionOptions creation, and provider label reporting for all AI services (CLIP, background removal, LaMa inpaint, super resolution). Fixes broken provider-name detection.
- **Listen-mode hardening** — per-session cryptographic token authentication, 20/s connection rate limit, 32K char line length cap, inbound/outbound event labeling in the network activity panel, `ClearAll()` to delete persisted logs, and automatic 2000-entry JSONL rotation.
- **Tile-pyramid cache management** — 1 GB cap with LRU eviction for pyramids older than 30 days, `GetHealth()` reporting, `ClearAll()` cache clearing, and automatic post-build eviction.
- **Writeback backup policy** — optional same-folder or app-local backup before destructive crop, rotation, GPS strip, and metadata strip overwrites, configurable via settings.
- **Folder back/forward navigation** — DirectoryNavigator tracks a 50-entry history stack across folder changes, with GoBack()/GoForward() and CanGoBack/CanGoForward properties.
- **XMP sidecar folder import** — command palette action scans a folder for .xmp sidecars and applies ratings to matching images. Keywords reported in toast; write requires future ExifTool integration.
- **Catppuccin Latte light theme** — full Latte palette override with runtime switching. ThemeService supports Dark, Latte, High Contrast, and Follow System modes via the `appearance.theme` setting.
- **WPF smoke test scaffold** — FlaUI-based smoke tests for launch/close, fixture image open, navigation, and Escape exit. CI step runs with `RUN_SMOKE_TESTS=1` on windows-latest.
- **Read-only catalog query boundary** — `CatalogQueryService` wraps the catalog with ListIndexedFolders, QueryByFolder, multi-term Search, and optional path redaction for automation integrations.
- **Localization CI enhancements** — Strings.cs property parity check against resx keys, XAML hard-coded string scan with warnings, and orphaned-property detection.
- **Package-resolution validation** — release readiness script now runs dotnet restore, dotnet build, and a vulnerable-package scan before existing doc/version checks.

- **Command palette (V20-29)** — `Ctrl+Shift+P` opens a VS Code-style fuzzy-search overlay listing 55 commands across 8 categories (Navigate, View, Edit, File, Tools, Review, Compare, Help). Type to filter by name, category, or shortcut; Up/Down to select; Enter or double-click to execute; Escape or click the dimmer to dismiss. No other Windows image viewer ships a command palette.
- **Color-channel isolation (V20-28)** — view individual R, G, B, or A channels as grayscale. Cycle through modes via the command palette or click the channel chip in the bottom toolbar. Mode persists across image navigation. Tile-pyramid (DZI) images skip channel filtering gracefully.
- **Multi-monitor window placement (V20-27)** — window position is now remembered per-monitor so the viewer restores to the correct display across sessions. On multi-monitor setups, the command palette shows "Send to monitor N" commands to move the window between displays. Falls back to primary-monitor clamping when a saved monitor is disconnected.
- **Viewer sort-mode switching (V20-30)** — the main viewer now persists the folder sort order across sessions. Nine sort modes (Name A-Z/Z-A, Modified newest/oldest, Created newest/oldest, Size largest/smallest, Type) are available through the command palette. The selected mode applies to all folder navigation and survives app restarts.
- **Network-listen mode (V20-31)** — `Images.exe --listen <port>` opens the viewer in TCP listen mode on loopback (127.0.0.1). External tools send UTF-8 file paths to open/refresh images live. All received paths are logged in the network activity panel. A green status chip in the toolbar shows the active port.
- **Slideshow (V30-33)** — auto-advances through folder images with configurable 1-60 second interval (default 5s), loop, shuffle, and pause. Start/stop from the command palette; Escape stops; hover or click the green status chip to pause/resume. Manual navigation resets the timer without stopping playback.

### Security

- **Archive reader dependency** — upgraded SharpCompress from 0.47.4 to 0.48.1, clearing the GHSA-6c8g-7p36-r338 / CVE-2026-44788 NuGet vulnerability gate. Images still uses SharpCompress only for read-only archive page streams and does not call the affected `WriteToDirectory()` extraction API.
- **Magick.NET 14.13.0 → 14.14.0** — resolves 12 upstream advisories (2 high, 10 moderate severity) including GHSA-36wm-hprc-mcf5 and GHSA-7gg8-qqx7-92g5. Zero vulnerable NuGet packages remain.
- **Transitive dependency audit** — CI security and release workflows now log the full NuGet transitive dependency tree as an uploadable artifact alongside the vulnerable-package gate, with documented S-09 native decoder floor requirements for future bundled runtimes.
- **ONNX Runtime DirectML 1.24.4** — pinned to 1.24.4 (the latest available release); the previously claimed 1.26.0 version does not exist on NuGet.
- **.NET 10 LTS migration** — moved from .NET 9 STS (EOL November 2026) to .NET 10 LTS (supported through November 2028). All Microsoft.* NuGet packages updated to the 10.0.x track. CI, security, and release workflows updated.
- **Content-based format validation** — files are now probed by magic bytes on open, not just by extension. When the detected content format doesn't match the file extension, an informational toast alerts the user. The same signature detection logic is shared with the file health scanner, eliminating duplicated code.
- **Granular EXIF metadata removal** — the viewport context menu now offers a "Strip metadata" submenu with category choices: device info (make, model, serial numbers), timestamps, software and comments, or all metadata at once. Each category previews what will be removed and writes atomically. The existing GPS-only strip remains as a separate quick action. All strip operations are available through the command palette.
- **Archive password prompt** — password-protected ZIP/CBZ, RAR/CBR, and 7z/CB7 archives now prompt for a password instead of failing with a generic error. The password is cached for the current archive session and cleared when navigating to a non-archive file. SharpCompress handles decryption transparently.
- **Motion Photo / Live Photo detection** — JPEG and HEIC files containing embedded MP4 video segments (Samsung Motion Photos, Google Pixel) are detected by scanning for ftyp boxes near the end of the file. When detected, a context menu and command palette action lets users extract the embedded video to a separate file. Apple Live Photos (.mov companion files alongside JPEGs) are also detected and can be opened directly.
- **Batch metadata strip action** — the macro/batch processor now supports a `strip-metadata` action with a `categories` parameter accepting `gps`, `device`, `timestamps`, `software`, or `all` (comma-separated). Dry-run mode previews how many tags would be removed per file.

### Dependencies

- Microsoft.Data.Sqlite 9.0.0 → 10.0.9
- Microsoft.Extensions.Logging 9.0.0 → 10.0.9
- Microsoft.ML.OnnxRuntime.DirectML pinned at 1.24.4 (latest available)
- Serilog 4.2.0 → 4.3.1
- Serilog.Extensions.Logging 9.0.0 → 10.0.0
- Serilog.Sinks.File 6.0.0 → 7.0.0
- SharpCompress 0.48.1 → 0.49.1
- SQLitePCLRaw.bundle_e_sqlite3 pinned at 3.0.3 (clears GHSA-2m69-gcr7-jv3q High)
- coverlet.collector 6.0.2 → 10.0.1
- Microsoft.NET.Test.Sdk 17.12.0 → 18.6.0
- xunit 2.9.2 → 2.9.3
- Target framework net9.0 → net10.0

### Changed

- **Network activity trust polish** - persisted network activity now restores the newest retained entries first, skips malformed JSONL rows without blocking startup, and the About dialog's Clear log action deletes the local persisted history instead of only clearing the current view.
- **Listen-mode operator polish** - the toolbar chip now refreshes its label/tooltip when listen mode starts, the tooltip/toast/CLI help/README explain the required session-token first line, and the listen-mode path gate has focused tests for local-only file acceptance.
- **XMP import truthfulness** - folder import now matches both `photo.jpg.xmp` and `photo.xmp` sidecars to images, applies only ratings it can actually write, and reports applied, no-rating, unmatched, and failed sidecars separately.
- **Tile-cache cleanup hardening** - failed tile-pyramid builds now remove partial cache directories, and reused/built pyramids refresh their cache timestamp so eviction better reflects recently opened large images.
- **Theme semantic brush hardening** - Latte now overrides the semantic surface/chrome brushes used by shared controls, high-contrast mode now suppresses Latte overlays when active, and selected/accent/status overlays across the viewer resolve through theme tokens instead of dark-only hex values.

- **Settings and collection chrome polish** — shared WPF `DataGrid` and `ListBoxItem` styles now provide intentional row rhythm, hover, selected, focused, and disabled states instead of default Windows chrome. The Hotkeys settings editor now shows quieter shortcut summary copy, inline Default/Custom badges, clearer shortcut edit help text, and a localized live loading indicator in the viewer.
- **Premium desktop polish pass** — refreshed the shared WPF chrome with tighter 8 px radii, calmer elevation, solid accessible focus rings, consistent ComboBox/Tab styles, refined button states, and a tabbed Settings dialog that groups General, Appearance, Accessibility, Advanced, Text extraction, and Diagnostics into a cleaner resizable surface. The main viewer now uses a more forgiving startup size/work-area clamp and a viewport measure guard so large images do not push tool chrome off-screen.
- **Complete jpegtran sidecar staging** — libjpeg-turbo release staging now copies and verifies the required adjacent `jpeg62.dll` alongside `jpegtran.exe`, and the runtime resolver marks incomplete app-local jpegtran bundles unavailable before launching them. This prevents the Windows "jpeg62.dll was not found" loader dialog during diagnostics, startup probing, and release smoke tests.
- **High-contrast runtime theme** — the accessibility high-contrast preference now installs a `SystemColors`-backed theme dictionary immediately, Windows high-contrast mode is honored automatically at startup, and Windows preference/color changes refresh the active dictionary at runtime.
- **Localization foundation** — all user-visible strings across the entire app are now routed through `Strings.resx` resources: MainViewModel (300+ toast/status/error strings), Settings, About, Effects, Adjustments, Model Manager, Duplicate Cleanup, Recovery Center, Batch Processor, Export Preview, File Health Scan, Semantic Search, Tag Graph, Reference Board, Import Inbox, Macro Actions, and all overlays/dialogs. A runtime locale switcher in Settings persists the language preference and applies it on restart.
- **Network egress transparency (P-03)** — every outbound HTTP call now records URL, purpose, bytes, and duration through `NetworkEgressService`. The About window shows a scrollable "Network activity" panel with per-entry cards, copy-to-clipboard, and clear actions. History persists across sessions in a local JSONL file. No competitor ships this.
- **Decode pipeline observability (O-03)** — a custom `Images-Decode` EventSource exposes `images-decoded`, `decode-duration-ms`, `wic-decodes`, `magick-fallback-decodes`, `thumbnail-writes`, and `decode-failures` counters plus `DecodeStarted`/`DecodeCompleted`/`DecodeFailed` events for live `dotnet-counters` monitoring. Recipe in `docs/perf.md`.
- **Store codec extension detection (V20-18)** — when HEIC, AVIF, or JXL decode fails because the Windows Store extension is missing, the load-error panel now names the required extension and shows a one-click "Get" button that opens the Microsoft Store deep-link.
- **Scoop extras manifest (D-03)** — `packaging/scoop/images.json` provides a ready-to-submit Scoop manifest with `checkver` and `autoupdate` sections pointing at GitHub Releases.
- **Migration guide** — `docs/migration-guide.md` documents actionable import steps for digiKam, XnView MP, Apple Photos (via osxphotos), Lightroom Classic, and Picasa users.
- **Metadata capture dates** — EXIF `OffsetTimeOriginal` is now covered by regression tests and rendered with an explicit signed UTC offset when present, while offset-free EXIF dates continue to display without inventing one.
- **WinGet release publisher** — published GitHub releases now have a dedicated WinGet workflow wired to `vedantmgoyal9/winget-releaser@v2`, matching the setup installer asset and cleanly skipping until the required classic `WINGET_TOKEN` secret and `winget-pkgs` fork are configured.
- **Settings information architecture** — Settings now has first-class General, Appearance, Accessibility, Advanced, Hotkeys, Diagnostics, Text extraction, and Privacy sections. New persisted controls cover window-placement restore, reduced viewer motion, high-contrast preference, and archive-book defaults; reduced motion now disables the main viewer's edge-arrow fade animation.
- **Runtime dependency provenance dashboard** — About, `--system-info`, and `--codec-report` now share structured NuGet/runtime/model rows that show source URLs, version, path, SHA-256 where available, advisory status, and setup/release action copy for Magick.NET, SharpCompress, Ghostscript, jpegtran, Windows OCR, and future model runtimes.
- **Local model/runtime manager** — the app now has a Model manager surface and service for approved local model definitions, app-local grouped storage, manual import/delete/reveal actions, pinned SHA-256 validation for OpenCV/Carve LaMa and Qdrant CLIP ViT-B/32 model/tokenizer/preprocessor candidates, Windows ML versus ONNX Runtime DirectML readiness copy, and diagnostics provenance rows without automatic model downloads.
- **Approved jpegtran release staging** — release packaging now stages libjpeg-turbo 3.1.4.1 `jpegtran.exe` from the reviewed `vc-x64` artifact, verifies the installer and extracted executable SHA-256 values, carries the libjpeg-turbo license/readme files, and keeps the executable ignored by git while including it in build/publish output.
- **Release diagnostics smoke** — the release workflow now runs portable and installed `--system-info` / `--codec-report` smoke checks, validates Ghostscript, OCR, jpegtran, and dependency-provenance rows, and uploads the diagnostics logs as workflow artifacts.
- **Compare and overlay review mode** — the viewer can compare the current image with the next folder item, a chosen local file, or the selected duplicate-cleanup pair. Compare mode supports linked pan/zoom/rotate/flip in 2-up and opacity-overlay layouts, A/B swap, keyboard opacity controls, side-panel controls, and Escape exit behavior.
- **Export preview workbench** — `Ctrl+Alt+W` opens an A/B export workbench that encodes the displayed image in memory for JPEG, PNG, WebP, AVIF, and JXL presets, shows original versus encoded preview, estimated output size and byte delta, resize-aware save output, and metadata/transparency/lossy-format warnings before writing a copy.
- **Export capability warnings** — export preview, batch preview, and batch dry-run now share target-format warnings for alpha flattening, animation frame loss, page/layer flattening, EXIF/IPTC/XMP metadata loss, ICC profile risk, and lossy quality settings.
- **Color profile and histogram awareness** — the side panel now reports embedded ICC/profile status, decoded color space, luma and RGB channel stats, shadow/midtone/highlight histogram percentages, alpha transparency stats, and safe unmanaged-color warnings without transforming pixels.
- **Destructive-action recovery center** — the viewer now records move, file-health rename, duplicate/file-health quarantine, crop/rotation/GPS writeback, and Recycle Bin actions in an app-local recovery ledger. The Recovery Center can reveal related paths, restore moves/renames/quarantines with collision-safe targets and sidecar recovery, and explains retention plus non-restorable writeback/Recycling recovery rules.
- **Read-only metadata delete sharing** — photo metadata and color-analysis reads now open Magick.NET streams with delete-sharing so background panels do not briefly block move/delete workflows while they inspect the current file.
- **Catalog schema v1** — the app now has a rebuildable local `catalog.db` foundation that indexes source path, SHA-256 fingerprint, dimensions, file dates, size, codec/format metadata, XMP sidecar path/modified time, rating, tags, and scan timestamps without treating SQLite as the authoritative source of user metadata.
- **Catalog migration guardrails** — catalog schema startup now rejects newer databases instead of downgrading them, runs forward-only SQLite migration hops with integrity checks and WAL checkpointing, creates `catalog.db.bak.v<old>-<new>` backups for existing caches, restores the backup if a hop fails, and verifies a schema canary before the cache is used.
- **Semantic search foundation** — the app now has a local semantic search service and window that explicitly index selected folders, store derived vectors in app-local `semantic-index.db`, search with a deterministic offline metadata embedding provider, filter by folder, open/reveal results, cancel indexing, and delete local search data. Approved ONNX CLIP/SigLIP inference remains gated behind future runtime validation.
- **Culling review mode** — `L` toggles review mode for folder culling. While active, `1`-`5` rate, `0` clears the rating, `P` marks pick, `R` marks reject, and `U` restores the previous review sidecar state. The side panel exposes the same controls and writes local XMP sidecars without requiring a catalog.
- **Automatic freehand crop mode** — normal flat-raster image loads now enter free-aspect crop mode immediately, so dragging on the open image starts a crop without pressing `C`; `C` still toggles crop mode when pan-only canvas control is needed.
- **Crop apply affordances** — Enter now applies an active crop selection from the preview key path, and the crop rectangle shows an on-canvas Apply button anchored to its lower-right edge.
- **Immediate edit preview** — applied crop operations now refresh the displayed image immediately, and the main viewer renders enabled edit-stack operations when loading the current image.
- **Crop file overwrite** — applying a crop now writes the cropped pixels back to the source file, clears baked edit-stack operations, resets stale preload data, and notifies the Windows shell so Explorer thumbnails refresh.
- **Crop format gate** — crop mode now starts only for flat raster image files such as JPEG, PNG, WebP, TIFF, GIF, BMP, HEIC/AVIF/JXL, and similar bitmap formats; PSD/PSB, vector/document previews, archives, and RAW files remain view-only for crop.
- **Pixel selection tool** — `S` now toggles a rectangular on-canvas pixel selection mode with copy/clear controls on the selection edge, side-panel controls, and `Ctrl+C` extraction of the selected pixels to the clipboard.
- **jpegtran runtime provenance** — the app now resolves an optional app-local libjpeg-turbo `jpegtran.exe` sidecar or explicit `IMAGES_JPEGTRAN_EXE` override, then surfaces its path, version, and SHA-256 in About, `--system-info`, and `--codec-report` diagnostics.
- **Lossless JPEG crop writeback** — when an approved local `jpegtran.exe` runtime is available, a single exact MCU-aligned JPEG crop now shells out through a test seam, validates the output, replaces the source atomically with rollback data, and avoids copying stale embedded thumbnails. Missing runtimes, unaligned crops, oriented JPEGs, and multi-operation edit stacks continue through the existing raster overwrite path.
- **Lossless JPEG trim confirmation** — interactive JPEG crop and rotation overwrite now warns before any MCU edge trim, lets the user choose trimmed lossless `jpegtran` writeback or exact raster re-encode, and keeps unattended export/writeback paths exact by default.
- **Inpaint runtime decision** — content-aware repair is now scoped as a future opt-in local LaMa ONNX workflow, using Windows ML first and ONNX Runtime DirectML as fallback, with no bundled model or automatic download in the current viewer/editor.
- **Sharpen, noise reduction, and vignette effects** — `Ctrl+Alt+F` now opens a modeless effects workbench with live Magick.NET previews, Crisp/Clean/Focus presets, Enter-to-apply behavior, XMP edit-stack storage, and Save-a-copy rendering.
- **Rotation writeback** — an explicit Apply rotation to file command now bakes the current right-angle view rotation into flat raster sources, clears baked edit-stack operations, refreshes the displayed image and shell thumbnail, and uses `jpegtran -rotate` for exact aligned JPEGs when the optional runtime is present.
- **Annotations and redaction** — a modeless annotations workbench now supports arrows, boxes, circles, text labels, numbered callouts, freehand pen strokes, blur redaction, and pixelate redaction as non-destructive edit-stack operations rendered on Save a copy.
- **Perspective correction** — `Ctrl+Alt+P` opens a modeless four-corner perspective workbench with draggable handles, keystone nudges, XMP edit-stack storage, and Magick.NET perspective rendering on Save a copy.
- **WinGet and Scoop package manifests** — the release workflow now generates ready-to-submit WinGet multi-file manifests (`SysAdminDoc.Images`) and a Scoop portable manifest (`images.json`) from release checksums, uploads them as workflow artifacts, and prints submission steps. Signing status is documented in `docs/distribution-trust.md`.
- **Deep-zoom tile engine** — huge images (>256 MB or >50 megapixels) now load through a DZI-style WebP tile pyramid under `%LOCALAPPDATA%\Images\tiles\`. The viewer binds the pyramid into the WPF canvas, chooses tile levels from the current zoom, renders only visible cached tiles while panning, supports paged raster frame keys, and disables bitmap-only edit/copy/compare workflows while the display is tile-backed.
- **Background removal** — when an approved segmentation ONNX model (BiRefNet, U-2-Net, or similar) is imported through the Model Manager, background removal runs locally through ONNX Runtime DirectML, produces a transparency mask or a foreground-only RGBA result, and resizes the output back to the original dimensions. Mask-only mode lets users inspect or edit the segmentation before applying.
- **Super-resolution** — when an approved upscaling ONNX model (Real-ESRGAN or similar) is imported through the Model Manager, super-resolution runs locally through ONNX Runtime DirectML. Dynamic-input models can upscale the full image or tile large inputs with overlap; fixed-input models resize through the model dimensions and scale back. The default 4x scale factor is configurable.
- **LaMa ONNX content-aware repair** — when an approved LaMa ONNX model is imported through the Model Manager, a new AI repair mode lets users paint circular mask regions over image areas to fill. The LaMa inference pipeline runs locally through ONNX Runtime DirectML with GPU acceleration and CPU fallback, composites the repaired output back to the original resolution, and writes the result. The Carve LaMa FP32 (512x512 fixed input) and OpenCV LaMa 2025-Jan candidates are both supported.
- **CLIP semantic search MVP** — when approved Qdrant CLIP ViT-B/32 ONNX models are imported through the Model Manager, semantic search automatically upgrades from the deterministic metadata provider to a 512-dimensional CLIP embedding provider backed by ONNX Runtime DirectML. Image and text queries run through local BPE tokenization and CLIP preprocessing with no network calls. The search window, index rebuild, folder management, and cosine similarity ranking all work with the new provider without changes. ONNX Runtime DirectML version and assembly path now appear in About, `--system-info`, and `--codec-report` diagnostics.
- **C2PA content provenance inspection** — when an optional `c2patool` runtime is available (via app-local `Codecs\C2paTool`, system PATH, or `IMAGES_C2PATOOL_EXE`), the side panel shows a read-only Content Credentials section for JPEG, PNG, WebP, AVIF, HEIC, TIFF, and other supported formats. The panel displays the trust badge, claim generator, signature date, assertions, and ingredient provenance from C2PA manifests. Images explicitly communicates that content credentials show provenance (who created or edited a file), not authenticity (whether the content is truthful). Runtime status and SHA-256 appear in About, `--system-info`, and `--codec-report` diagnostics.
- **Auto Enhance** — `Ctrl+Alt+E` now adds a one-click balanced enhancement edit with automatic gamma, white balance, contrast curve, and mild sharpening during Save a copy/export rendering.
- **Copy/Move to folder** — the viewport context menu now copies or moves the current file to a chosen folder, preserves matching XMP sidecars, resolves destination name collisions safely, remembers recent transfer destinations, and refreshes the shell after moves/copies.
- **Wallpaper layout modes** — Set as desktop wallpaper now offers Fill, Fit, Span, and Tile modes and writes the matching Windows wallpaper style before applying the stable app-data wallpaper copy.
- **Send/print/copy actions** — the viewport context menu now supports no-dialog printing to the default printer, local `.eml` email drafts with the current file attached, Copy image, and Copy image and path clipboard payloads.
- **ExifTool safe invocation wrapper** — future metadata-write workflows now have a process-boundary helper that runs ExifTool without shell invocation, sends arguments and target paths through a UTF-8 `-@` argfile, rejects line-break and shell-metacharacter path channels, and cleans temporary argfiles after execution.
- **XMP sidecar import service (M-03, M-04)** — a new `XmpSidecarImportService` reads standard `.xmp` sidecar files and extracts `xmp:Rating` (1-5 or -1 reject), `xmp:Label` (color labels), `dc:subject` (flat keywords), `lr:hierarchicalSubject` (Lightroom/XnView pipe-separated paths), `digiKam:TagsList` (digiKam slash-separated paths), and IPTC/Photoshop location fields. Covers both digiKam "Write metadata to files" and XnView MP "Export to XMP" workflows without reading either app's native database.

### Accessibility

- **A-05 Published UIA tree documentation** — `docs/accessibility.md` documents the full UI Automation tree structure (image canvas, navigation, rename, rating/review, toolbars, editing overlays, dialogs) so screen reader users and accessibility testers know exactly what Narrator, NVDA, and JAWS will announce.
- **A-04 Magnifier caret tracking** — the rename stem TextBox now explicitly raises `TextPatternOnTextSelectionChanged` on every caret move so the Windows Magnifier follows the edit point when "Follow the text insertion point" is active. `AutomationId="StemEditor"` added for reliable UIA element identification.
- **A-06 Screen reader manual test matrix** — `docs/narrator-test-matrix.md` documents a pre-release test script for Narrator, NVDA, and JAWS covering 10 core scenarios (image load, navigation, rename, rating, pick/reject, delete confirmation, gallery, settings, about, toasts) and 5 supplementary checks (filmstrip, OCR overlay, cheatsheet, crop, compare) with expected announcements and per-reader result columns.

### Testing

- **Catalog v1 schema snapshot fixture** — `tests/Images.Tests/Fixtures/catalog.v1.db` is a checked-in SQLite v1 catalog with 3 seeded assets, 5 tags, 1 root, and a schema canary. `CatalogSchemaSnapshotTests` (43 tests) verify the fixture's table/column/index shape, seed data integrity, and that `CatalogService` opens the snapshot via forward migration without data loss. Every future schema bump adds a `catalog.vN.db` and must roll all prior snapshots forward in CI.

## v0.2.11 — 2026-05-05

Self-contained document-preview runtime release.

### Changed

- **Bundled Ghostscript runtime** — portable and installer artifacts now include app-local Ghostscript 10.07.0 so PDF, EPS, PS, and AI previews work on clean machines without requiring users to find and install Ghostscript separately.
- **Ghostscript license/provenance notes** — release documentation now identifies the bundled AGPL Ghostscript runtime, the installed license path, the official Artifex source package, and the SHA-256 values used to verify the runtime installer (`8af854e2d62f9a3a674331321b347118a83928a3726631e458194121cf3bbeec`), bundled `gsdll64.dll` (`1dce67538777ab2f312890f9a2f0ffcff6a4c58ef1149dc6a44f8bd97b31030d`), and source archive (`ddace4e1721f967a55039baff564840225e0baa1d4f5432247ca1ccd1473b7c1`).

## v0.2.10 — 2026-05-05

Performance, reliability, workflow, and product-polish release.

### Changed

- **Improvement tracking** — added a repo-local improvement plan that tracks the 15 engineering, UX, reliability, and CI follow-up items from the May 2026 quality review.
- **CI verification** — added a pull-request/push CI workflow for whitespace diff checks, version metadata sync, vulnerable-package scanning, Release build, tests, and CLI smoke commands.
- **Release metadata gate** — moved version-sync validation into a reusable PowerShell script shared by CI and the release workflow.
- **Shell and clipboard integration** — About, crash recovery, settings, and viewer actions now use shared helpers for opening files/folders/URLs and copying text, reducing duplicated process and clipboard handling.
- **Update-check testability** — update checks now have seams for HTTP, clock, retry-state recording, and state-file behavior, plus non-network tests for release parsing and transient failure policy.
- **Folder preview and sorting** — folder-preview thumbnail orchestration now lives in a focused controller, and the viewer exposes app-owned folder sort modes for natural name, reverse name, modified date, created date, size, and extension grouping.
- **Premium interaction polish** — folder-sort menus now show the active sort with checked menu states, settings changes provide tone-aware saved feedback, and OCR overlay styling now uses shared theme tokens.
- **Recycle Bin confirmation preference** — the delete confirmation now offers a "don't ask again" checkbox, and Settings can restore confirmation before future Recycle Bin deletes.
- **Clipboard import testability** — paste-from-clipboard handling now uses a focused import service with deterministic seams for file lists, image data, storage, naming, and clipboard-temp pruning.
- **Viewer state coverage** — `MainViewModel` now supports isolated settings, clipboard, delete, confirmation, and navigator dependencies for regression tests covering folder-preview sort state, filmstrip persistence, paste opening, and Recycle Bin confirmation preferences.
- **Recycle Bin delete extraction** — Recycle Bin confirmation, "don't ask again" persistence, missing-file handling, and send-failure reporting now live in a focused delete service with direct regression coverage.
- **Rename safety** — extension-unlocked renames now reject unsupported target extensions before touching disk, keeping the viewer and folder navigator in sync.
- **Metadata loading extraction** — photo metadata HUD loading now lives in a focused controller with cancellation, stale-result, timeout, and dispatcher-state coverage.
- **External-edit reload extraction** — external file-watch debounce and reload feedback now live in a focused controller with coverage for coalescing, canceled reloads, failed reload feedback, and watcher creation failures.
- **OCR workflow extraction** — OCR busy/overlay state, cancellation, stale-result guards, local extraction feedback, and overlay-line conversion now live in a focused controller with direct async coverage.
- **Update-check UI extraction** — latest-release state, manual/background update feedback, command invalidation, and release-page opening now live in a focused controller with direct coverage.
- **Folder-preview cancellation coverage** — thumbnail loading now has a deterministic test seam with regression coverage for clear and superseded-refresh cancellation paths.
- **ViewModel relay coverage** — `MainViewModel` now has an internal controller-injection seam with regression coverage for metadata, OCR, and update-check relay properties.
- **UI state hardening** — refresh remains available when the current file was removed externally, and regression tests now cover rename debounce, stale-folder recovery, and command enablement states.
- **Diagnostics status pane** — About now summarizes OCR, Ghostscript, Magick.NET, logs, storage, and update-check state in a compact status section with regression-tested status composition.
- **Diagnostics actions** — the About diagnostics section now lets users copy system info, copy the codec report, open logs, and open the app data folder directly from the status pane.
- **First-run guidance** — the empty viewer state now explains local privacy defaults, broad format support, OCR readiness, document-preview requirements, and Settings/Diagnostics recovery links before a file is opened.
- **Operation status feedback** — manual reload, Save a copy, and GPS metadata stripping now share a visible busy status, with mutating image commands disabled until the operation completes.
- **Update-check transparency** — manual and background update checks now expose busy status through the main UI, suppress duplicate manual checks, and show when GitHub Releases is being contacted.
- **Decode/navigation feedback** — file-open dialog decodes and multi-page page turns now use the shared operation-status surface before slower document or page loads begin.
- **Load/navigation responsiveness** — Open Image and next/previous navigation now decode cache misses off the UI thread, await any in-flight preload instead of duplicating work, and update same-folder preview selection in place instead of rebuilding thousands of thumbnail items on every arrow press.
- **Secondary empty/error states** — unsupported clipboard data, empty recent folders, and stale recent-folder paths now show persistent, actionable side-panel feedback instead of relying only on transient toasts.
- **Secondary recovery feedback** — thumbnail-generation failures and offline update checks now retain actionable status, including failed thumbnail placeholders and no-upload reassurance for network failures.
- **Background task ownership** — thumbnail generation, metadata reads, preload decodes, clipboard-temp pruning, and thumbnail-cache eviction now run through a shared tracker with diagnostics-visible running/completed/failed/canceled counts.
- **Update/cache observability** — manual and background update checks are now included in tracked background work, and diagnostics now shows thumbnail-cache size, file count, temp-file count, cap, and last eviction sweep.
- **Storage and cache test seams** — app storage roots, default settings construction, and default thumbnail-cache construction now have deterministic seams and tests for fallback/unavailable-storage behavior.
- **Thumbnail cache controls** — About diagnostics now lets users open the thumbnail cache folder or clear disposable cached thumbnails with confirmation, progress feedback, and automatic diagnostics refresh.
- **Settings persistence hardening** — settings corruption recovery now uses collision-resistant quarantine names, with tests for corruption reset, schema migration, unavailable storage, and primitive setting defaults.
- **Large-folder stress coverage** — navigation and folder-preview tests now cover thousands of files, volatile folder changes, enumeration failure recovery, and bounded thumbnail requests.
- **Generated codec corpus** — decode/export regression tests now generate PNG, JPEG, WebP, TIFF, GIF/APNG, and SVG samples at runtime, avoiding binary fixtures while protecting codec upgrades.
- **Product differentiator scopes** — added a design scope for local semantic search, duplicate cleanup, compare/overlay, archive/book navigation, peek launch mode, viewer-side adjustments, technical pixel tools, and metadata/culling workflows.
- **Distribution trust plan** — scoped WinGet and Scoop publishing, checksum continuity, code-signing options, SmartScreen expectations, and user verification copy for the next stable release.
- **Integration policy** — documented the no-code-copied optional-runtime gate for licenses, redistribution rights, CVE tracking, binary provenance, process isolation, network behavior, and release impact.
- **Archive/book foundation** — ZIP and CBZ files now open as read-only archive books using built-in .NET ZIP support, with natural page ordering, page-count controls, a page scrubber, unsafe-entry filtering, and recursive-archive guardrails.
- **Archive read position and history** — archive books now remember the last viewed page locally, continue there on reopen, and surface a side-panel book history with page progress.
- **Archive cover handling** — archive books now promote explicit cover/front/folder image entries before natural page order and report cover provenance in decoder details.
- **Archive reader controls** — active archive books now get a side-panel book-controls card, narrow edge page-turn click zones, and reader-mode arrow/Home/End key routing for page turns.
- **Archive runtime review** — documented the RAR/7z dependency policy and approved SharpCompress 0.47.4 as the managed MIT reader for read-only RAR/CBR and 7z/CB7 archive books.
- **RAR/7z archive expansion** — RAR/CBR and 7z/CB7 books now open through SharpCompress with the same unsafe-entry filtering, nested-archive skipping, document-entry skipping, per-entry byte cap, corrupt-archive recovery copy, diagnostics provenance, and generated 7z regression coverage as the ZIP/CBZ foundation.
- **Manga page turns** — archive books now have a persisted right-to-left page-turn mode that swaps physical edge zones and Left/Right Arrow routing without changing semantic next/previous controls.
- **Clean scan preview** — archive books now offer a persisted, preview-only high-contrast grayscale filter for yellowed or low-contrast scanned pages without modifying source archives.
- **Two-page archive spreads** — archive books now have a persisted spread mode that keeps explicit covers single, pairs natural pages, respects right-to-left composition, and advances by spread.
- **Gallery workbench** — the viewer now has a keyboard-first `G` gallery overlay for the current folder with multi-column thumbnails, quick filtering, sort shortcuts, per-thumbnail context actions, current-item selection, and Enter-to-open behavior.
- **Asset smart filters** — Gallery filtering now supports current-folder smart tokens for format, folder, sidecar rating/tag data, palette, orientation, dimensions, date, and exact duplicate status, with quick chips for common filters and a clear-filter affordance.
- **Tag relationships** — `Ctrl+Shift+T` now opens a private local tag graph for namespaces, aliases/siblings, parent-tag expansion, and current-image XMP sidecar import/export.
- **Import inbox** — `Ctrl+Shift+I` now opens a local staging inbox for new files, destination duplicate checks, tag/rating sidecars, GPS stripping on imported JPEG/TIFF copies, Recycle Bin cleanup, and copy/move import.
- **Macro actions** — `Ctrl+Shift+M` now opens an inspectable JSON action runner with dry-run support, load/save JSON, and first actions for GPS stripping, export/convert/resize copies, and rename patterns.
- **Batch processor** — `Ctrl+Shift+B` now opens a preset-based batch export surface with previewed output paths/dimensions, dry-run default, load/save preset JSON, and overwrite-safe resize/convert copies.
- **Non-destructive edit stack** — `Ctrl+Shift+E` now opens edit history with XMP-backed JSON operations, virtual copies, enable/disable controls, apply-on-export Save-a-copy support, and export provenance sidecars.
- **Non-destructive crop mode** — `C` now enables an on-canvas crop selection; dragging records pixel-accurate crop bounds, Enter or Apply adds a crop operation to the XMP edit stack, and Save a copy applies it without modifying the source image.
- **Crop composition controls** — crop mode now supports free, square, 3:2, 4:3, 16:9, and custom aspect ratios plus rule-of-thirds guides while dragging.
- **Lossless JPEG transform policy** — scoped the `jpegtran.exe` runtime gate and added tested MCU-alignment planning so future crop/rotate writeback can warn before any lossless trim.
- **Resize dialog** — `Ctrl+Alt+R` opens a non-destructive resize dialog with percent, pixel, long-edge, and short-edge modes, aspect lock, Lanczos-3/Mitchell/Bicubic filters, and live output-dimension preview.
- **Adjustment workbench** — `Ctrl+Alt+A` opens a modeless non-destructive levels, curve, and HSL workbench with live preview, reset, Enter-to-apply behavior, XMP edit-stack persistence, and Save-a-copy rendering.
- **Local exposure brush** — `Ctrl+Alt+D` toggles a no-modal dodge/burn brush with soft falloff, radius/strength/tone controls, drag-to-paint strokes, Enter-to-apply behavior, XMP edit-stack persistence, and Save-a-copy rendering.
- **Red-eye correction** — `Ctrl+Alt+Y` toggles a no-modal red-eye tool with on-canvas pupil marks, soft correction overlays, radius/strength/red-threshold controls, XMP edit-stack persistence, and Save-a-copy rendering.
- **Clone/heal retouch** — `Ctrl+Alt+H` toggles a no-modal clone stamp and healing brush with Alt-click source picking, soft source-to-target stroke overlays, radius/strength controls, XMP edit-stack persistence, and Save-a-copy rendering.
- **Reference board mode** — `Ctrl+B` now opens a non-modal local reference board seeded from the current image, with supported-file drag/drop, draggable image cards, editable notes, draggable/resizable group frames, always-on-top pinning, zoom/reset controls, clear confirmation, and PNG export bounded to visible board content.
- **Inspector tools** — the side panel now includes a pixel Inspector with live coordinates, HEX/RGB/HSV/alpha readouts, copy buttons, Shift-drag pixel measurements, and nearest-neighbor preview scaling for pixel art; reference-board image cards support Ctrl-hover sampling and Ctrl-click sample copy.
- **Animation frame workbench** — animated GIF/APNG/WebP playback now has a side-panel frame timeline, scrubber, play/pause, first/previous/next/last frame stepping, playback-speed control, current-frame copy, PNG export, and drag-out frame files.
- **Pinned overlay mode** — the current image can now be pinned above other windows with side-panel opacity controls, a visible overlay status banner, context-menu exit actions, and guarded click-through mode that only enables when the `Ctrl+Alt+O` global exit hotkey is available.
- **Duplicate cleanup center** — `Ctrl+Shift+D` now opens a local cleanup surface with exact SHA-256 duplicate groups, perceptual similarity matching, a threshold slider, reference-folder keep preference, side-by-side review, session-level false-positive dismissal, and non-destructive quarantine/Recycling actions for extra candidates.
- **File health scan** — `Ctrl+Shift+H` now opens a local scan surface for bad extensions, broken supported images, zero-byte files, and suspicious temp/partial artifacts, with preview, conflict-safe extension rename, reviewed dismissal, and app-local quarantine.
- **Peek mode hardening** — `--peek` startup now records local timing milestones and first-image timing, with parser regression tests and shell-helper documentation for chromeless preview integrations.
- **OCR workflow polish** — text extraction now has a persistent in-view busy/active status, a cancel-aware toolbar state, OCR readiness in Settings/About, and OCR language-pack status in diagnostics.
- **Open-source viewer research** — added a May 2026 research scan of ImageGlass, nomacs, PicView, NeeView, QuickLook, Geeqie, gThumb, qView, JPEGView, Tacent View, Minimal Image Viewer, and LightningView, then folded the findings into the improvement plan.
- **Trust copy** — README destructive-action wording now reflects the Recycle Bin confirmation flow.

## v0.2.9 — 2026-05-04

OCR overlay usability hotfix.

### Fixed

- **OCR overlay placement** — OCR regions now place their Canvas item containers at the recognized image coordinates instead of setting Canvas offsets inside the data template, fixing boxes that were stacked along the top edge.
- **Selectable OCR text** — OCR regions now render read-only selectable text boxes so recognized text can be highlighted and copied manually with Ctrl+C or the context menu.
- **OCR line bounds** — line overlays now use the union of all recognized word boxes instead of only the first and last word, improving placement on uneven lines.

## v0.2.8 — 2026-05-04

Installer hotfix for OCR readiness.

### Fixed

- **Installer OCR provisioning** — the Inno installer now runs an elevated Windows optional-capability provisioning step for the current UI language OCR pack plus `en-US` fallback, logging details to `Images-OCR-capability.log` in the install folder.
- **Upgrade cleanup** — the installer now removes existing Images installs before copying the new build, cleans stale per-user registry shadows that could point file opens at `%LOCALAPPDATA%`, and carries existing file-association registration forward.

## v0.2.7 — 2026-05-04

Hotfix for OCR extraction reliability and diagnostics.

### Fixed

- **OCR stream lifetime** — image streams copied for Windows.Media.Ocr now keep the WinRT write adapter alive until decoding completes, fixing `ObjectDisposedException` failures when pressing the OCR button.
- **OCR failure messaging** — true OCR extraction failures now surface as extraction failures instead of being collapsed into the misleading "no language packs installed" path.

## v0.2.6 — 2026-05-04

Sixth hardening pass for release integrity and packaging gates.

### Fixed

- **Release version integrity** — the release workflow now rejects malformed version inputs and refuses to publish unless `Images.csproj`, `app.manifest`, installer defaults, and README release commands are all synced to the dispatched version.
- **Solution build consistency** — release restore/build steps now target `Images.sln` explicitly so the artifact gate exercises the same project graph as local validation.

## v0.2.5 — 2026-05-04

Fifth hardening pass for OCR overlay coordinate correctness.

### Fixed

- **OCR overlay alignment** — OCR text boxes now share the viewer's image-pixel-to-viewport transform, keeping regions aligned across fit letterboxing, zoom, pan, rotation, and flip states instead of drawing raw pixels on the viewport.
- **Transform regression coverage** — added focused matrix tests for fit-centering, zoom/pan, rotation, and flip coordinate mapping.

## v0.2.4 — 2026-05-04

Fourth hardening pass for local state files, diagnostics exports, clipboard paste storage, and single-file runtime metadata.

### Fixed

- **Update-check state safety** — `update-check.json` now rejects oversized local state, logs read/write failures, writes through a temp file, and ignores future timestamps instead of suppressing checks indefinitely.
- **Diagnostics export safety** — About-window system-info exports now use a per-file GUID in the app diagnostics folder instead of a collision-prone timestamp in the shared temp root.
- **Clipboard paste hygiene** — pasted bitmap files now use collision-resistant names, `CreateNew` writes, and background pruning for old or excessive clipboard images.
- **Single-file metadata** — app version diagnostics now fall back to the process path when assembly location is empty in bundled deployments.

## v0.2.3 — 2026-05-04

Third hardening pass for runtime utilities, recent-folder persistence, workflow gates, wallpaper safety, and diagnostics wording.

### Fixed

- **Recent folders** — MRU entries are now normalized to full paths and only persisted when the folder still exists, avoiding duplicate relative/canonical entries.
- **Wallpaper safety** — Set-as-wallpaper now copies through a temporary file and atomically swaps the stable app-data wallpaper slot.
- **Workflow coverage** — release and security vulnerability gates now scan the whole solution, including test-project dependencies, instead of only the app project.
- **Diagnostics polish** — codec capability wording no longer claims vector editing is unavailable forever, and XCF fallback guidance correctly refers to GIMP.
- **Settings cleanup** — removed the unused telemetry settings key so the local settings surface matches the documented no-telemetry product behavior.

## v0.2.2 — 2026-05-04

Second production-hardening pass for thumbnail cancellation, export writes, metadata status, crash logs, and accessibility.

### Fixed

- **Thumbnail responsiveness** — folder-preview thumbnail generation now accepts cancellation tokens so rapid navigation stops updating superseded preview state earlier.
- **Preload cleanup** — preload cancellation sources rotate under a lock and dispose after in-flight tasks have had time to observe cancellation, reducing rare reset/dispose races.
- **Export safety** — save-a-copy paths are normalized and written through same-folder temp files before atomic replace/move so partial exports do not clobber an existing destination.
- **Crash diagnostics** — crash log appends now use `FileShare.ReadWrite`, allowing About-window diagnostics or external editors to read logs while a crash record is being written.
- **Metadata UX** — metadata reads time out with a visible status instead of leaving the HUD in a perpetual loading state.
- **OCR accessibility** — OCR regions now expose screen-reader names/help text and support keyboard copy with Enter/Space.
- **Settings clarity** — update-check copy now consistently says automatic checks are off by default.

## v0.2.1 — 2026-05-04

Production hardening release for OCR, file operations, release packaging, and privacy defaults.

### Fixed

- **OCR stability** — removed the leaked reflection-based 60 FPS overlay timer, made click-to-copy selection feedback observable, ignored empty OCR word lines, and cancel stale OCR runs when the overlay is hidden or the image changes.
- **Rename safety** — invalid filenames now surface a clear toast instead of silently no-oping, and rename/undo paths retry deterministic conflict targets when another process creates a competing filename mid-operation.
- **Folder navigation resilience** — directory refresh now clamps stale indexes after external file changes, and renamed paths are normalized and validated before the navigator follows them.
- **Metadata edit safety** — GPS-stripping writes use a short GUID sibling temp file to avoid long-path temp-name failures and keep cleanup reliable.
- **Decode guards** — Magick.NET bitmap conversion now rejects oversized dimensions before stride/pixel-buffer allocation.
- **Metadata sanitation** — embedded string metadata drops control characters and GPS display rejects malformed coordinates outside valid latitude/longitude ranges.
- **Update-check safety** — release JSON downloads are bounded to 64 KB before deserialization.
- **Settings reliability** — SQLite settings open with a busy timeout and WAL mode to reduce multi-process lock failures.

### Trust and release

- **Network quiet by default** — automatic update checks now default off; users can enable startup checks in Settings, and manual checks still work from About.
- **Release workflow hardening** — optional Ghostscript bundles require a matching SHA-256, the workflow avoids ExecutionPolicy Bypass, PDBs are stripped from portable packages, and release checksums are uploaded.
- **Version sync** — manifest, installer defaults, README badge, and assembly metadata now agree on v0.2.1.

## v0.2.0 — 2026-05-04

Text extraction (OCR) using Windows.Media.Ocr API. Local processing, privacy-first.

### Features

- **Text extraction (E key)** — press `E`, click the Extract Text toolbar button, or right-click and choose "Extract text" to overlay semi-transparent blue bounding boxes on detected text regions. Windows.Media.Ocr API provides local, offline text recognition through installed Windows OCR language capabilities. Overlay toggles on/off with the same `E` key. Toast notifications confirm extraction status (number of regions found, no text found, OCR unavailable).
- **Phase 1 implementation** — uses native Windows.Media.Ocr for feature parity with Windows Photos. No additional dependencies or deployment bloat. Accuracy: ~85-90% on clean printed documents, ~75-80% on complex layouts. Speed: ~1 second per image on CPU-only processing. Phase 2 (v0.3.0+) will add optional PaddleOCRSharp "Advanced Mode" with ~92-95% accuracy and GPU acceleration for power users.

### Technical

- **WinRT interop** — updated project to `net9.0-windows10.0.22621.0` TFM for WinRT API access. Added `Services/OcrService.cs` with `ExtractTextAsync(Stream)`, `GetAvailableLanguages()`, and `IsAvailable()` methods. Handles pixel format conversion (Bgra8/Gray8 requirement), caches `OcrEngine` instance for performance.
- **UI overlay** — new `Controls/OcrOverlay.xaml` Canvas-based control with click-to-copy functionality. Semi-transparent Catppuccin Blue (#89B4FA) at 30% opacity, 1px border. Integrated into MainWindow Viewport Grid with visibility binding to `IsOcrMode` property.
- **ViewModel integration** — `MainViewModel` now includes `IsOcrMode`, `OcrModeTooltip`, `OcrOverlayLines` properties, `ExtractTextCommand`, and `OcrTextLine` helper class. `ExtractTextAsync()` method orchestrates OCR workflow with comprehensive error handling and user feedback.

### Known Limitations (Phase 1)

- **Fixed overlay coordinates** — overlay boxes don't sync with viewport zoom/pan transform. Acceptable for v0.2.0 MVP (most users view at fit-to-window). Phase 2 will bind overlay transform to `ZoomPanImage` state.
- **Single-region copy only** — no multi-select, Ctrl+click, or Select All. Click one box → copies one line. Phase 2 will add multi-region selection and "Copy all text" button.
- **No in-app language picker** — users must install language packs via Windows Settings. Phase 2 will add Settings window OCR section with language enumeration and direct link to Windows language settings.

<details>
<summary>Pre-0.2 release history (v0.1.9 through v0.1.0)</summary>

## v0.1.9 — 2026-05-04

Settings window, GPS-location strip, and automatic external-edit reload. Three ROADMAP items closed.

### Features

- **Settings window (Ctrl+,)** — dedicated Settings window (Item 2) with Viewer and Privacy sections. Viewer: filmstrip-visible-at-startup and metadata-HUD-visible-at-startup toggles. Privacy: update-check opt-in/out. Accessible via `Ctrl+,`, the gear icon in the right-panel header, and "Settings…" in the context menu. Settings apply immediately (no OK/Apply step) and are persisted to `settings.db`. After the window closes, the main viewer reflects any changes to filmstrip and HUD state without requiring a restart.
- **Strip GPS location (P-01)** — "Strip GPS location" toolbar button and context-menu item removes all GPS EXIF values from the current file using Magick.NET and writes the result atomically (temp-file swap — crash-safe). Reports the number of GPS fields removed via toast. Returns "No GPS data found" when the file is clean. Reloads the image and metadata HUD after stripping so the overlay updates in place.
- **Auto-reload on external edit (Item 61)** — when an image is opened, a `FileSystemWatcher` monitors it for `LastWrite` / `Size` changes. Rapid writes are coalesced via an 800 ms debounce timer so incremental saves from Photoshop / Paint.NET / etc. produce a single reload. Toast: "Reloaded — file changed externally". Degrades silently on network drives or locked volumes. Preload cache is cleared before reload so stale decoded frames are not reused.

## v0.1.8 — 2026-04-25

UI surface release. Promotes the foundation work from v0.1.7 into user-visible features: clipboard paste, open-with-default-app, richer decode error messages, and the recent-folders side panel. Eight ROADMAP items closed or advanced.

### Features

- **Clipboard paste (Ctrl+V)** — `Paste from clipboard` context-menu item and `Ctrl+V` shortcut. Accepts a clipboard file-drop list (file copied in Explorer — opens the first supported image directly) or raw pixel data (screenshot, web image) saved to `%LOCALAPPDATA%\Images\clipboard\clipboard-<ts>.png` and loaded immediately. Toast confirms the paste. Ctrl+V added to the keyboard cheatsheet.
- **Open with default app** — `Open with default app` context-menu item opens the current image in whatever app Windows has registered as the default for that file type (`UseShellExecute = true`). Errors surface as a toast. Gated on `HasImage`.
- **Richer decode error messages (item 86 enhancement)** — `SetLoadError` now detects `FileNotFoundException` (file not found title + navigate-away hint), `UnauthorizedAccessException` (access-denied title + check-permissions hint), and `OutOfMemoryException` (image-too-large title + free-memory hint) before falling back to the generic path. New `SupportedImageFormats.SuggestionForDecodeFailure(ext)` returns format-specific hints for supported-but-failing types: HEIC/AVIF (Microsoft Store codec), JXL (Windows 11 24H2+), camera RAW (DNG Converter), PSD/PSB (32-bit export workaround), TIFF (re-save as standard), SVG/SVGZ (browser preview), XCF (flatten + export from GIMP), EXR (convert with ImageMagick), and PDF/PS/EPS/AI (Ghostscript). Generic fallback unchanged when no hint applies.

### Trust

- **Item 34 — Vulnerable-package CI gate**. New [`Security` workflow](.github/workflows/security.yml) runs `dotnet list package --vulnerable --include-transitive` on every push/PR that touches dependencies, daily on a 09:00 UTC cron, and on demand. Fails the job if any package in the resolved graph (direct or transitive) carries a known CVE. Same scan is wired into [`Release` workflow](.github/workflows/release.yml) as a pre-publish gate so a vulnerable release literally cannot be uploaded.
- **Item 33 first slice — Diagnostics export from About**. Two new ChromeButtons in About: **Save system info** writes the same content as `Images.exe --system-info` to `%TEMP%\images-system-info-<timestamp>.txt` and reveals it in Explorer (UTF-8 BOM so Notepad opens it cleanly). **Open data folder** opens `%LOCALAPPDATA%\Images\` so users can reach Logs, `crash.log`, `settings.db`, `update-check.json`, and `thumbs/` in one click. Replaces "open a terminal and pipe stdout to a file" as the bug-report attachment workflow.
- **Item 90 — Trust docs**. Three new policies in `docs/`:
  - [`release-support-policy.md`](docs/release-support-policy.md) — what gets servicing, how long; servicing surface (NuGet + native runtimes + .NET); breaking-change policy (forward-only hop-by-hop migrations; caches always disposable); reporting and distribution channels.
  - [`codec-support-policy.md`](docs/codec-support-policy.md) — bundled-vs-optional tiers; what ships in the bundle; opt-in Ghostscript discovery contract; the five-point checklist that gates any new optional decoder (license / CVE / cadence / provenance / process isolation); decoder-removal policy.
  - [`privacy-policy.md`](docs/privacy-policy.md) — every network call (one — update check), how to turn it off, every file persisted to disk, what does **not** happen (no telemetry, cloud sync, OCR, face/object detection, ad SDKs, file-path egress), and a four-step verification recipe (toggle off + log inspect + `--system-info` + `grep HttpClient`).

### Features (cont.)

- **V20-37 `--system-info` / `--codec-report` / `--version` / `--help` CLI** — new `Services/CliReport.cs` resolves a single-token CLI flag in `App.OnStartup` BEFORE the codec runtime is configured and BEFORE any window is shown, then exits with a normal process exit code. Output is sent to the parent terminal via `AttachConsole(ATTACH_PARENT_PROCESS)` so `Images.exe --system-info` actually prints into the launching shell instead of vanishing into a detached console. `--system-info` reports application version + binary path, .NET runtime + OS + process arch + 64-bit flag + processor count + working set, decoder runtime (Magick.NET version + assembly path; Ghostscript availability/source/version/DLL path/SHA-256), open + export extension counts, and every writable storage path Images uses (app data root, Logs, thumbs, wallpaper, crash log). `--codec-report` prints the per-format capability matrix and the full extension catalog. The CLI surface and the About dialog read from the same `CodecCapabilityService.BuildProvenance()` call so they cannot disagree about what's loaded.
- **X-02 capability matrix in About** — About dialog now surfaces a per-format-family matrix (Common images, Design and production, Portable and scientific, Vector previews, Document previews, Camera RAW) with open/export counts, ternary animation/multi-page/metadata flags, the active runtime label, and a notes line describing concrete limitations (PSD layer flatten, RAW read-only, document DPI, etc.). Replaces the single "Codecs" line with an auditable surface so "broad codec support" is verifiable instead of asserted.
- **Item 86 unsupported-format guidance** — `SupportedImageFormats.SuggestionForUnsupported(ext)` keys human-readable hints off file extension. Toasts on dropped/opened video, audio, archive/comic, document, presentation, spreadsheet, native design-suite, and HEIC/PDF cases now point at the right tool ("Open video files in VLC or mpv", "Archive mode is not built yet — extract first or wait for the next milestone", etc.). The decode-error card surfaces the same suggestion as `LoadErrorHelpText` when a recognized but failing extension lands in the load path.
- **V20-21 first slice — bottom folder filmstrip** — the cached folder preview rail now lives in the bottom viewer chrome, can be toggled with the toolbar or `T`, persists the preference in settings, and falls back to the side panel when hidden so thumbnail jumping remains available.
- **V20-21 follow-up — centered active thumbnail** — the current thumbnail now auto-centers in the bottom filmstrip or side-panel fallback after folder refreshes, navigation, and filmstrip toggles. Thumbnail buttons also expose their position to assistive tech and use the shared accent state for keyboard focus.
- **V20-21 follow-up — thumbnail actions** — right-clicking a thumbnail in the bottom filmstrip or side-panel fallback now offers Open, Reveal in Explorer, and Copy path without changing the current image unless Open is chosen.
- **V20-21 complete — virtualized full-folder rail** — the filmstrip and side fallback now enumerate the full current folder through recycling WPF virtualization instead of the previous nine-item window. Thumbnails still decode lazily for visible and near-current items so large folders remain responsive.
- **V20-22 first slice — photo metadata summary** — new `Services/ImageMetadataService.cs` reads EXIF via Magick.NET `Ping()` on a background task and fills the side-panel Details area with captured date (via `MetadataDate` / `DateTimeOffset`), camera, lens, shutter/aperture/ISO, focal length, and GPS coordinates when present. Empty and loading states are explicit, the read is local-only, and GPS remains text-only so no map/network egress is introduced.
- **V20-22 follow-up — viewport metadata HUD** — press `I`, use the viewport context menu, or click the toolbar info button to toggle a persisted floating EXIF HUD. It reuses the same local metadata rows as the side panel, carries loading/empty states, and can be dismissed in place without changing the current image.

### Trust + provenance

- **Runtime provenance card in About** — new "Runtime provenance" section lists app directory, process architecture, Magick.NET version + on-disk assembly path, Ghostscript availability, source label (bundled / `IMAGES_GHOSTSCRIPT_DIR` / installed), version (when `gswin*c.exe` is present), absolute DLL path, and SHA-256 of the loaded `gsdll64.dll` / `gsdll32.dll`. The hash gives release maintainers a one-shot integrity check against the redistributable approved at release time. Same data is mirrored in the `--system-info` CLI output and the **Copy codec report** clipboard payload.
- **`CodecRuntime` provenance helpers** — `GetMagickAssemblyVersion()` and `GetMagickAssemblyPath()` read `AssemblyInformationalVersionAttribute` so the shown Magick.NET version always tracks the actually-loaded NuGet package, not a hardcoded "14.13" string. `GetGhostscriptDllPath()` + `GetGhostscriptDllSha256()` resolve the loaded `gsdll*` and stream-hash it for the provenance surface.
- **`docs/codec-bundling.md` provenance section** — documents the three surfaces (About card, clipboard report, CLI) and the SHA-256 drift check that must pass before a release ships.

- **V20-32 `--peek <path>` CLI mode** — chromeless, topmost, maximized overlay for PowerToys-Peek-style invocation. Side panel + bottom toolbar hidden; image fills the whole window. Escape closes. Lets Images drop into any external workflow that expects a single-image preview tool (File Explorer add-ons, terminal previewers, editor integrations). Path resolved through the same canonicalizer the regular open path uses so device-namespace shapes are rejected before downstream consumption. Two-token contract enforced exactly (`args.Length == 2`) — trailing junk falls through to regular argv handling.
- **V20-15-Loop animation loop-count badge** — the existing animated-image chip now surfaces `AnimationSequence.LoopCount`. Reads `{N} frames · loops` for the GIF-spec infinite case (`LoopCount <= 0`) and `{N} frames · plays Mx` for finite loops. `IsAnimated` tightened to require `Frames.Count >= 2` so the chip can never disagree with `ZoomPanImage.OnAnimationChanged`'s gate.
- **RecentUI — recent-folders menu in side panel** — V20-02 SQLite recent-folders MRU (data layer shipped v0.1.7) is now a clickable list between Recent renames and Details. Each entry is a folder-icon + basename card with full-path tooltip + `AutomationProperties.Name` for screen readers. Click loads the first supported image in that folder via `DirectoryNavigator.SupportedExtensions`. Empty / unreachable folders surface a toast; never crashes. Whole section hides on a fresh-install empty MRU.

## v0.1.7 — 2026-04-24

Factory iter-3 foundations release. Lays the persistence + preload + thumbnail-cache + UIA-peer quartet that multiple v0.2.0 items were blocked on. Seven ROADMAP items closed. All foundational — no user-visible UI surfaces change yet (those ship in v0.1.8+), but every open-file feels quicker after the first arrow-press thanks to preload, window geometry survives restarts, and the update check now has a proper opt-out toggle.

### Foundations

- **V20-02 SQLite settings service** — new `Services/SettingsService.cs` on `Microsoft.Data.Sqlite` 9.0.0 at `%LOCALAPPDATA%\Images\settings.db`. Schema v1 seeds three tables (`settings` key/value, `recent_folders` MRU, `hotkeys` action/key/mods). Hop-only migrations via `PRAGMA user_version`. Corruption recovery quarantines `settings.db` → `settings.db.corrupt-<ts>` and starts fresh — per SCH-01 the cache is disposable, never authoritative. Strongly-typed `Keys` class so call-sites get compile-time checking. `ILogger<T>` routes errors through the Serilog rolling file.
- **Window-state persistence** — `MainWindow` saves `Left/Top/Width/Height/Maximized` on `Closing`, restores on construction. Restore clamps to current `SystemParameters.WorkArea` so a window from a now-disconnected second monitor doesn't vanish offscreen. Maximized state persists but the saved geometry is always the `RestoreBounds` — unmaximize lands where you'd expect.
- **Recent-folders MRU** — `SettingsService.TouchRecentFolder` runs on every `OpenFile`; one-statement INSERT-OR-REPLACE-then-DELETE keeps the list at 10 entries. Filters out folders that no longer exist on disk when queried. The UI surface (Recent menu in the side panel) lands v0.1.8.
- **Update-check opt-out** — `UpdateCheckService.OptedIn` backed by `Keys.UpdateCheckEnabled` (default on). New "Automatically check for updates" checkbox in the About dialog. `IsDueForBackgroundCheck` short-circuits on false — zero network egress when disabled, cleanly fulfilling the charter's "zero telemetry" line for users who want it.

### Performance

- **V20-03 preload N±1 ring** — new `Services/PreloadService.cs` decodes next + previous image on a background `Task` as soon as the current one lands. Bounded at 3 slots (N-1, N, N+1) with LRU eviction. Cancellation-friendly — nav to a different image cancels the outstanding decodes. Files over 40 megapixels skip preload (memory pressure guard — a 100 MP panorama × 3 slots would burn gigabytes of managed heap to speculatively decode images the user may never look at). `MainViewModel.LoadCurrent` now prefers a cache hit, falls through to direct load on miss; `EnqueueNeighbours` runs after every load with wrap-around matching the nav semantics.
- **V20-04 thumbnail cache disk layer** — new `Services/ThumbnailCache.cs` at `%LOCALAPPDATA%\Images\thumbs\<2-char>\<sha1>.webp`. Key = `SHA1(path.lower() + mtime_ticks + size_bytes)` so path rename / file edit / file resize all invalidate the cached thumb naturally. Git-like 2-char partition directory avoids directory explosion on large libraries. Magick.NET resize to 256-px longest edge, WebP quality 80, EXIF stripped, 512 MB disk cap with LRU eviction. No UI consumer this iter — V20-21 filmstrip will be the first; disk layer ships now so that code lands without re-architecting the cache shape.

### Accessibility

- **A-01 `ImageCanvasAutomationPeer`** — new `Controls/ImageCanvasAutomationPeer.cs` subclasses `FrameworkElementAutomationPeer`. Reports `AutomationControlType.Image`, `GetName` = "Image, W by H pixels" from the live source, `GetHelpText` = arrow/wheel/drag/double-click semantics so Narrator/NVDA/JAWS announce on focus. `ZoomPanImage.OnCreateAutomationPeer` returns it. No OSS Windows image viewer publishes this UIA tree — positioning win against ImageGlass / nomacs / qView / JPEGView.

### Research artifacts

- `docs/research/iter-3-state-of-repo.md` — Phase 0 recon, scale-gate, iter-2 delta consumed.
- `docs/research/iter-3-scored.md` — condensed Phase 2+3+5 (same-session delta; only 10 new items warranted; all NOW-tier; 7-check self-audit with explicit mitigations for SQLite CVE scan + window-clamp + preload memory guard + thumb hash collision + UIA peer fallback via `AutomationProperties.Name`).

### Deps

- Added: Microsoft.Data.Sqlite 9.0.0.

## v0.1.6 — 2026-04-24

Factory iter-2 polish + observability release. Eight tasks closed — promotes the ad-hoc text crash log into structured Serilog + minidump + user-actionable crash dialog, ships Print + Save-as-copy + four zoom modes, adds a read-only GitHub-Releases update check (the first network egress — documented + throttled + opt-out), and lays the `MetadataDate` scaffold for v0.2.x metadata display.

### File ops

- **Print current image (V15-10)** — new `Services/PrintService.Print` wraps `PrintDialog` on a single `FixedDocument` page. 0.5in margins, fit-to-page with a never-upscale-past-1:1 ceiling. Ctrl+P + context-menu entry + toolbar-menu integration. Prints the undecorated decoded first-frame; rotation + flip aren't baked in (same convention as Windows Photos).
- **Save-as-copy (E6)** — Ctrl+Shift+S + menu. `SaveFileDialog` with format filter; picks a `BitmapEncoder` per extension (`JpegBitmapEncoder` @ quality 92 / `PngBitmapEncoder` / `BmpBitmapEncoder` / `TiffBitmapEncoder` / `GifBitmapEncoder` / PNG default). Writes the first frame; file becomes the selected navigation entry.

### Viewer

- **Four zoom modes (V20-20 partial)** — new `ZoomPanImage.ZoomMode` enum exposes `Fit` / `OneToOne` / `FitWidth` / `FitHeight` / `Fill`. `SetZoomMode` computes against the current source pixel size + viewport, reuses the baseline `Stretch.Uniform` as the 1.0x reference. Ctrl+F cycles with toast readout of the active mode. Auto + Lock-to-% deferred to V20-02 so the choice can persist across sessions.

### Observability

- **Structured logging (V02-06 / O-01)** — new `Services/Log.cs` bridges Serilog 4.2 into `Microsoft.Extensions.Logging` 9.0 so call-sites take an abstract `ILogger<T>`; rolling file at `%LOCALAPPDATA%\Images\Logs\images-yyyyMMdd.log`, 14-day retention, ISO-ish timestamp with offset. `App.xaml.cs` logs version + runtime + OS on startup, and every fatal-exception handler now emits both a structured log entry AND the plain-text `CrashLog.Append` record — forensic surface + user-actionable surface share the same event.
- **Minidump + crash dialog (V02-07 / O-04)** — new `CrashLog.TryWriteMiniDump` P/Invokes `dbghelp.dll!MiniDumpWriteDump` with `DataSegs | UnloadedModules | ThreadInfo` flags; dumps land at `%LOCALAPPDATA%\Images\Logs\crash-<yyyyMMdd-HHmmss>.dmp`. New `CrashDialog.xaml` replaces the raw `MessageBox.Show` on `DispatcherUnhandledException` — Copy details (to clipboard) / Open log folder / Open GitHub issue (with the details pre-filled in the URL, truncated at 5500 chars to respect GitHub's issue-new endpoint cap) / Close. AppDomain + TaskScheduler handlers also write dumps on termination paths.

### Distribution

- **Update check (P-04)** — new `Services/UpdateCheckService` does a read-only GET against `https://api.github.com/repos/SysAdminDoc/Images/releases/latest` with a 24-h throttle for the silent startup check; manual "Check for updates" button in the About dialog bypasses the throttle. Every call logged with URL + byte count + duration (beachhead for P-03 network-egress log panel). Last-checked timestamp persisted to `%LOCALAPPDATA%\Images\update-check.json`. On finding a newer tag, toast + stored latest-tag + URL so the UI can surface the "get the update" CTA.

### i18n scaffolding

- **`MetadataDate` value type (NEXT-11 / I-04 precursor)** — new `Services/MetadataDate.cs` wraps `DateTimeOffset?` with an explicit `HasOffset` flag (mirrors EXIF 3.0 convention where `DateTimeOriginal` is local-no-TZ and `OffsetTimeOriginal` carries the offset). Parses EXIF strings + formats per `CultureInfo.CurrentCulture`. Beachhead so v0.2.x metadata overlay never bakes `DateTime` into a signature that'd need a compat break.

### Docs

- **DPI audit (NEXT-12)** — new `docs/dpi-audit.md` documents that 110 literal-size attributes across 4 XAML files are all DIU (device-independent units), not raw pixels. `permonitorv2` in app.manifest + WPF layout system means all are DPI-safe. Future fragility risk lives in code-behind that bypasses WPF layout (we have none today).

### Research artifacts

- `docs/research/iter-2-state-of-repo.md` / `iter-2-sources.md` (+7 delta entries) / `iter-2-harvest.md` (12 delta items) / `iter-2-scored.md` (6 NOW + 2 NEXT) / `iter-2-audit.md` (7-check self-audit with two explicit mitigations — Serilog dep scan after this lands, update-check egress transparency via logged URL/bytes/duration).

### Deps

- Added: Serilog 4.2.0, Serilog.Sinks.File 6.0.0, Serilog.Extensions.Logging 9.0.0, Microsoft.Extensions.Logging 9.0.0.

## v0.1.5 — 2026-04-24

Factory iter-1 polish release. Nine input + discovery affordances the charter expects but v0.1.x deliberately deferred. All additive — no decoder, persistence, or theme changes. Closes ten ROADMAP items (V15-01 through V15-09 + the context-menu absorbs V15-02's original scope plus three bonus items: Rotate 180° / Flip Horizontal / Flip Vertical / Set as wallpaper / Reload).

### Input affordances

- **Mouse XButton1 / XButton2 → previous / next** (V15-01). `MainWindow.Window_PreviewMouseDown` catches the 5-button-mouse back/forward before any element captures it. TextBox-focus short-circuit prevents hijacking an in-progress rename.
- **Right-click context menu on the viewport** (V15-02). 11 items across open / reveal / reload / rotate (CW / CCW / 180°) / flip (H / V) / set as wallpaper / delete. Attached to the viewport Grid, not descendants, so the rename TextBox keeps its own edit menu. `ViewportContextMenu` + `MenuItem` + `Separator` styles in `DarkTheme.xaml` match Mocha instead of rendering system white.
- **Set as desktop wallpaper** (V15-02 bonus). New `WallpaperService.SetFromFile` copies the current image to `%LOCALAPPDATA%\Images\wallpaper\current.<ext>` before calling `SystemParametersInfo(SPI_SETDESKWALLPAPER, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE)` — a later rename or delete of the source doesn't break the desktop.
- **Flip horizontal / vertical** (V15-08). New `FlipHorizontal` / `FlipVertical` DPs on `ZoomPanImage`; the flip `ScaleTransform` sits BEFORE rotate in the transform stack so flip H flips in image frame (user intuition) rather than post-rotation frame. Pan + zoom state preserved across flip.
- **Rotate 180°** (V15-02 bonus). `Rotate180Command` — the missing neighbor of the CW / CCW pair.
- **Shift + scroll-wheel → horizontal pan** (V15-05). `ZoomPanImage.OnWheel` branches on `ModifierKeys.Shift`; translates X by ±80 px per notch. Plain wheel still zooms, drag still pans vertical.
- **Ctrl+Shift+R reload current image** (V15-04). `ReloadCommand` re-runs the decoder on the same path — useful after external edit in Photoshop / mspaint. Rotation + flip state preserved; nav index unchanged.

### Discovery + polish

- **`?` keyboard cheatsheet overlay** (V15-03). Full-width translucent card groups Navigate / View / File shortcuts including the new XButton and Shift+wheel bindings. Any key dismisses the overlay AND swallows the key so the shortcut doesn't double-fire.
- **F11 fullscreen toggle** (V15-07). `MainWindow.ToggleFullscreen` saves `WindowState` + `WindowStyle`, flips to `None` + `Maximized`, collapses the side panel via the `IsFullscreen` VM flag bound to column-1 `Border.Visibility`. Side panel `ColumnDefinition` switched to `Width="Auto"` so the column collapses with the hidden Border. `Escape` also exits fullscreen (convention).
- **About dialog** (V15-06). New `AboutWindow.xaml` + `AboutWindow.xaml.cs` + `AppInfo` service surface version + `ProductVersion` with commit SHA + .NET runtime description + OS description + decoder list + MIT copyright. GitHub + Crash-log-folder buttons. Dark native caption via existing `WindowChrome.ApplyDarkCaption` for caption consistency with the main window. Info-icon chip (`E946`) in the side-panel header opens it.

### Observability

- **Unified crash log** (V15-09). New `CrashLog` service at `%LOCALAPPDATA%\Images\crash.log` captures all three fatal-exception paths — `AppDomain.UnhandledException`, `Application.DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException` — with version + runtime + OS + full inner-exception chain per entry. Thread-safe `Append` method is reusable for non-fatal diagnostic events too. Replaces the ad-hoc inline `AppendAllText` that used to live in `App.xaml.cs`. Dispatcher dialog now points at the log path so users can attach it when reporting. Precursor to V02-07 (minidump + "Open GitHub Issue" dialog).

### Research artifacts

- `docs/research/iter-1-state-of-repo.md` — Phase 0 recon (scale gate, phase rotation, charter).
- `docs/research/iter-1-sources.md` — Phase 1 landscape scan, 60 sources across 9 classes (OSS competitors, commercial, adjacent, awesome-lists, community signal, standards, academic, dep changelogs, security advisories).
- `docs/research/iter-1-harvest.md` — Phase 2 raw candidates (115 items across 6 buckets — delta from v0.1.3/v0.1.4 ship, competitive gap, infra concretizations, cross-cutting, net-new, research spikes). Auto-extended by the Gemini probe with per-competitor feature breakdowns.
- `docs/research/iter-1-scored.md` — Phase 3 scoring on six dimensions (Fit / Impact / Effort / Risk / Dependencies / Novelty), bucketed into Now / Next / Later / Under-Consideration / Rejected.
- `docs/research/iter-1-audit.md` — Phase 5 self-audit across 7 dimensions (source traceability, tier placement, category coverage, internal consistency, adversarial review, charter alignment, file-on-disk).

## v0.1.4 — 2026-04-24

Distribution release. The portable zip stays the primary artifact; a signed-ready Inno Setup installer ships alongside it so Images can land in Settings → Apps → Installed apps like any other Windows program, with proper uninstall semantics and optional non-destructive "Open with" registration.

### Installer

- **New**: `installer/Images.iss` — Inno Setup 6 script. Installs to `%ProgramFiles%\Images` (admin, default) or `%LOCALAPPDATA%\Programs\Images` (per-user via UAC override); `PrivilegesRequiredOverridesAllowed=dialog commandline` lets the user pick at the elevation prompt. Stable `AppId` GUID so future installers auto-upgrade rather than piling up side-by-side.
- **Prerequisite check** — `InitializeSetup` probes `{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\9.*` (per-machine) + `{localappdata}\Microsoft\dotnet\shared\Microsoft.WindowsDesktop.App\9.*` (per-user) and refuses to proceed without the .NET 9 Desktop Runtime, offering to open the Microsoft download page.
- **Non-destructive file associations** — the "Add to Open with" optional task registers a `Images.File` ProgID + `Applications\Images.exe` entry + `OpenWithProgids` values for 16 extensions (jpg, jpeg, jfif, png, gif, webp, heic, heif, avif, jxl, tif, tiff, bmp, ico, psd). Also writes `Software\RegisteredApplications` + `Software\Images\Capabilities\FileAssociations` so Images surfaces in Settings → Default Apps. Never overrides the user's current default for any extension — that stays their choice. `uninsdeletevalue` cleans each added value without touching siblings on uninstall.
- **Artifacts**: `Images-v0.1.4-setup-win-x64.exe` (LZMA2/ultra64; ~11 MB on v0.1.3 dry run). Start Menu shortcut always; Desktop shortcut optional (unchecked by default); post-install "Launch Images" checkbox.
- **Verified**: local compile against v0.1.3 succeeds in ~5 s, produces a runnable installer that decompresses into a working viewer. No compile warnings.

### Release workflow

- `.github/workflows/release.yml` now builds both artifacts and uploads them to the same GitHub Release. Inno Setup 6 is pre-installed on `windows-latest`; a `choco install innosetup -y` fallback step kicks in if the runner image ever stops bundling it. `iscc /DMyAppVersion=...` passes the release version through to the script.

### Docs

- README install section split into **Installer** and **Portable** paths, with clear guidance on what each gives you and a snippet for building the installer locally.

## v0.1.3 — 2026-04-24

Format-coverage + pixel-hygiene release. Animated GIFs actually animate, toolbar / nav-arrow icons render on enterprise Win11 images that previously showed tofu boxes, and files above 256 MB decode through a memory-mapped view instead of a managed byte[].

### Viewer

- **Animated GIF playback** — `ImageLoader.Load` now probes `.gif` / `.webp` / `.apng` / `.png` via `MagickImageCollection` before falling through to the single-frame WIC path. When a file decodes as a multi-frame sequence, `collection.Coalesce()` resolves each frame's disposal method to a full-canvas BGRA `WriteableBitmap`, and the full list is returned on a new `AnimationSequence` record (frames + per-frame delays + loop count). Fixes [V20-15]. Single-frame GIFs still fast-path through WIC — the animated decoder only pays its cost when there are ≥ 2 frames.
- **Frame-delay clamp** — 0- and sub-20-ms GIF frame delays are promoted to 100 ms the way every shipping browser does, so hostile / malformed GIFs can't pin a CPU core.
- **Loop-count honored** — `AnimationSequence.LoopCount` follows the GIF convention (0 = infinite, any other value = exact iteration count) and feeds `ObjectAnimationUsingKeyFrames.RepeatBehavior` directly, so bounded-loop GIFs stop on the right frame instead of cycling forever.
- **Animated chip** — a compact green `N frames` pill lights up in the bottom toolbar's file-info row whenever `MainViewModel.IsAnimated` is true. Reads at a glance without competing with the primary metadata.
- **V20-06 memory-mapped I/O** — files ≥ 256 MB skip the byte[] round-trip in `ImageLoader.Load` and decode directly from a `MemoryMappedFile` view (`MemoryMappedFileAccess.Read`, `FileShare.ReadWrite | Delete` to preserve the existing rename/delete story). Both the WIC primary and the Magick.NET fallback now take their own `CreateViewStream` per attempt, so a 500 MB RAW or multi-GB PSD no longer lands on the LOH — the OS pages the mapping in on demand. `DecoderUsed` reports `"WIC (memory-mapped)"` / `"Magick.NET (memory-mapped)"` so you can see which path was used.

### UI fix

- **Toolbar + nav-arrow glyphs now render everywhere** — `Themes/DarkTheme.xaml` promotes a shared `IconFontFamily` resource (`"Segoe Fluent Icons, Segoe MDL2 Assets, Symbol"`) and every icon `FontFamily` setter in `DarkTheme.xaml` + `MainWindow.xaml` (10 call sites) resolves through it. On Win11 IoT Enterprise LTSC and a handful of corporate WinPE-derived images, WPF's MDL2-only lookup landed on a text fallback and rendered every icon button as an empty white tofu rectangle; declaring Fluent Icons first + MDL2 second + `Symbol` as a last-ditch fallback collapses all three worlds without touching the glyph codepoints. Same fix applied to the six MDL2 glyph `TextBlock`s (error icon, drop-accept icon, gesture-hint icon, toast icon, extension-lock padlock + unlock pencil).

### Roadmap

- `[x]` **V20-06** — Memory-mapped I/O for files > 256 MB (avoids blowing the managed heap on 500 MP RAW).
- `[x]` **V20-15** — Animated GIF / APNG / animated AVIF playback. Transport controls (play/pause/frame-step/speed) deferred; core playback is live.

## v0.1.2 — 2026-04-24

Security + accessibility + CI hardening plus a three-wave premium-polish pass that elevates the product from functional to intentional.

### UI / UX — premium polish pass (wave 3)

- **Smooth rotate** — `ZoomPanImage.RotationProperty` now animates the `RotateTransform` via an eased (`CubicEase EaseInOut`) `DoubleAnimation` instead of snapping the angle. Duration scales with angular delta (180 ms base + up to 162 ms for a 180-degree flip) so a single rotate-left still feels quick while a 270-degree round trip stays controlled.
- **Extension chip state** — locked vs unlocked now reads at a glance. Unlocked: button border + fill inherit the `YellowBrush` / `WarningPanelBrush` pair used by the warning panel below, so the two surfaces read as a coordinated state. Glyph swaps padlock (`&#xE72E;`) → pencil-edit (`&#xE70F;`) tinted yellow.
- **Window title** — `MainWindow.Title` binds to `MainViewModel.WindowTitle`, which exposes `"{filename} — Images"` when a file is open and falls back to bare `"Images"` otherwise. Standard Windows convention; makes the taskbar label + Alt-Tab card useful.

### UI / UX — premium polish pass (wave 2)

- **Windows 11 dark caption** — new `Services/WindowChrome.cs` calls `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE, 1)` in `Window.SourceInitialized` so the native title bar stops clashing with the Mocha interior. Best-effort P/Invoke — pre-20H1 no-ops cleanly with no visual regression. `DWMWA_SYSTEMBACKDROP_TYPE` (Mica, Win11 22H2+) is documented as a future hook; wiring it wants an alpha-aware window background, deferred to a later pass.
- **Status-dot success pulse** — when `RenameStatus` transitions to `Saved`, the rename-status dot scales 1.0 → 1.5 → 1.0 via a `DataTrigger.EnterActions` storyboard walking the `(UIElement.RenderTransform).(ScaleTransform.ScaleX)` property path. Commit feedback now feels confirmed, not silent.
- **First-run gesture-hint pill** — `MainViewModel` exposes a one-shot `ShowGestureHint` + `_hintTimer` (2.4 s). First image to land in the viewport surfaces an `FloatingPill` reading "Scroll to zoom · drag to pan · double-click to fit", fades in on 280 ms `Motion.Slow`, auto-dismisses. Never shown again in the session.
- **Recent-rename hover affordance** — new `InteractiveCard` style (based on `Card`) animates `BorderBrush.Color` toward `BlueColor` on `IsMouseOver`, with matching exit transition on leave. Recent-rename entries now read as tappable rows rather than static blocks.
- **Decoder badge** — "Decoder" detail row switches from raw text to a compact `Badge` style (Surface0 fill, Hairline border, 4-px radius). WIC / Magick.NET / Unavailable now reads as a label, not a value.
- **Toolbar top highlight** — 1-px inner highlight (`#12CDD6F4` ≈ 7% white-on-Mantle) sits just above the toolbar divider line for a gently lit upper edge — the layered-chrome cue you see in polished macOS / Win11 apps.

### UI / UX — premium polish pass

- **Design token system** — `Themes/DarkTheme.xaml` now exposes explicit radius (`Sm` 6 · `Md` 10 · `Lg` 14 · `Xl` 18), elevation (`Low` / `Medium` / `High` / `Focus` as `DropShadowEffect` resources), and motion tokens (`Motion.Fast` 120 ms · `Motion.Base` 180 ms · `Motion.Slow` 280 ms + shared `CubicEase` easing). Styles now compose from tokens instead of ad-hoc per-element values.
- **Reusable surface styles** — `Card`, `ElevatedCard`, `FloatingPill`, `Toast`, `Divider` styles retire the copy-pasted Border-with-radius blocks scattered through `MainWindow.xaml`. Empty state, decode-error state, drop-confidence panel, rename status card, and recent-rename entries now all inherit a single visual language.
- **Typography scale** — new `Text.Display` / `Text.Title` / `Text.Subtitle` / `Text.Body` / `Text.Caption` / `Text.Hint` styles built on Windows 11's `Segoe UI Variable` (graceful fallback to `Segoe UI` on Win10). `SectionLabel` switches to OpenType small-caps (`Typography.Capitals="AllSmallCaps"`) for a refined tracked look without per-letter hackery.
- **Motion** — `ChromeButton` and `PrimaryButton` hover now cross-fades background color via a 120 ms eased `ColorAnimation` instead of a binary setter flip. `NavArrowButton` hover adds a 1.06× scale cue + border-tint transition + elevation shadow. Toast fades in via Opacity animation from its Style trigger. Nav-arrow viewport fade now uses `CubicEase EaseOut` instead of linear.
- **Floating chrome elevation** — position chip, toast, nav arrows, and the empty / error / drop-overlay cards gain layered `DropShadowEffect` so they read as lifted above the viewport instead of floating flat on the near-black background.
- **Hairline unification** — `HairlineBrush` tuned to `#4045475A` (lower opacity); all 1-px dividers now inherit the new `Divider` style so separators no longer compete with content. Toolbar top border switches from `Surface0Brush` to `HairlineBrush`.
- **Toolbar polish** — outer padding tightens rhythm (`14,9` → `20,12`), button cluster gaps go `6` → `4` px (denser), divider bar gets more vertical breathing room. `ToolbarButton` ships a transparent resting state so icons sit on the bar rather than on a box.
- **Empty-state invitational card** — larger logo (74 → 84 px), tighter copy, new inline hint line ("Tip — arrow keys browse the folder, Enter commits a rename."). Copy rewritten for warmer, shorter cadence.
- **Decode-error semantic surface** — low-opacity `DangerPanelBrush` background replaces full red fill so the panel reads as informative not alarming; icon sizes up to 38 px.
- **Drop-overlay hierarchy** — inner card now uses `ElevatedCard` with 2-px themed border, keeping the accept / reject color signal while cleaning up the doubled-border construction that previously sat inside another border.
- **Toolbar + right panel microcopy** — warning/hint copy tightened ("Changing the extension renames the file — it won't convert the image."), rename helper collapses three sentences into "Renames save on pause. Enter commits now · Esc reverts.", empty-undo copy trims to "Your undo list will appear here."
- **Escape discipline extended** — `Window_KeyDown` Escape now also dismisses an active toast via `MainViewModel.DismissToast()` (A-03 extension).
- **Right panel spacing** — column width 340 → 360, panel padding 18 → 22, header margin 0,0,0,18 → 0,0,0,22 — tighter rhythm without feeling airy.
- **Form polish** — `TextBox` focused state switches from 2-px ring color-on-color ambiguity to crisp 2-px `AccentBrush` border + hover hint on `Surface2Brush`; selection opacity drops to 35% so highlighted text stays readable.
- **ScrollBar retemplate** — compact pill thumb on transparent track replaces the default Aero chrome.
- **Accessibility extras** — position chip gets `AutomationProperties.LiveSetting="Polite"` so folder-position changes are announced; folder label inherits `ToolTip` so ellipsized paths are fully recoverable.

### Security

- **S-02** — Argv-open hardening. `App.xaml.cs` normalizes `argv[0]` through `Path.GetFullPath` + `File.Exists` and rejects device-namespace (`\\?\`, `\\.\`) shapes outright. `MainViewModel.RevealInExplorer` switches from `UseShellExecute=true` + embedded-quote `Arguments` string to `UseShellExecute=false` + `ArgumentList.Add("/select," + Path.GetFullPath(CurrentPath))`, so filenames with commas, quotes, or trailing spaces cannot compose an injection against `CommandLineToArgvW` quoting rules.

### Accessibility

- **A-03** — Keyboard focus + Escape discipline. New shared `FocusVisual` style in `Themes/DarkTheme.xaml` (2 px inset dashed `FocusRingBrush` rect, ~7:1 contrast on the Catppuccin base — WCAG-AA pass) is wired via `FocusVisualStyle` setters on `ChromeButton` / `PrimaryButton` / `NavArrowButton` / `ToolbarButton` / the ambient `TextBox` style. The `RecentRenames` ItemsControl gains `KeyboardNavigation.DirectionalNavigation="Cycle"` + `TabNavigation="Continue"` + `AutomationProperties.Name`. `Window_KeyDown` now handles `Escape` to dismiss an active drop overlay and return focus to the shell.

### CI

- Bumped release workflow actions off the deprecated Node 20 runner: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. GitHub removes Node 20 runners on 2026-06-02 — this clears that deprecation well ahead of the deadline.
- **S-03** — `.github/dependabot.yml` added for the two ecosystems in use (`nuget`, `github-actions`). Weekly sweep on Monday, grouped by package family (`Magick.NET*`, `Microsoft.*`, `actions/*`), commit prefixes `chore(deps)` / `chore(ci)`. Security-advisory PRs bypass the 5-PR throttle per Dependabot defaults.

### Dependencies

- `Magick.NET-Q16-AnyCPU` 14.12.0 → 14.13.0 and `Magick.NET.Core` 14.12.0 → 14.13.0 (minor bump; keeps the bundled native decoder stack current for ImageGlass-advisory CVE hygiene per ROADMAP S-03). Build clean, 0 warnings.

### Branding

- Added the project logo. `src/Images/Resources/logo.png` ships as a WPF `<Resource>` for in-app use; `src/Images/Resources/icon.ico` is a 7-frame multi-resolution Windows app icon (16/24/32/48/64/128/256, Catmull-Rom downscale from a square-padded 431×431 source) wired via `<ApplicationIcon>` in `Images.csproj` — the built `Images.exe` now shows the logo in Explorer, the taskbar, and Alt-Tab. `icon.svg` is a PNG-embedded SVG wrapper for web/README contexts.
- Added the project banner at `assets/banner.png` and embedded it at the top of the README.

### CI

- Bumped release workflow actions off the deprecated Node 20 runner: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. GitHub removes Node 20 runners on 2026-06-02 — this clears that deprecation well ahead of the deadline.

### Docs

- `ROADMAP.md` refreshed to v2 (2026-04-24). Seeded v1 covered viewer/editor/organizer/converter/AI/plugins; v2 adds nine cross-cutting tracks — security (S-01–S-10 incl. WIC CVE-2025-50165, libwebp CVE-2023-4863, SharpCompress zip-slip, ExifTool safe invocation, MSIX AppContainer, Wasmtime-hosted decoder spike), privacy (P-01–P-07 incl. strip-location, default-off telemetry, network-egress log panel, C2PA read/verify via `c2patool`), accessibility (A-01–A-06 incl. custom `ImageAutomationPeer`, high-contrast `SystemColors` theme, Magnifier-aware UIA events, published UIA tree), i18n/l10n (I-01–I-05 incl. Crowdin for OSS, RTL audit, `DateTimeOffset` switch), observability (O-01–O-05 incl. Serilog, opt-in Sentry, ETW decode counters, local minidump), testing (T-01–T-05 incl. `Images.Domain` pure lib + FlaUI smoke + golden-image diff), distribution (D-01–D-07 incl. winget via `WinGet Releaser`, Scoop `extras`, Microsoft Store MSIX, Azure Trusted Signing), catalog-schema strategy (SCH-01–SCH-05 — XMP sidecars authoritative, forward-only hop-don't-jump EF Core migrations), and migration-from-competing-tools (M-01–M-06 — Picasa `.picasa.ini`, Lightroom `.lrcat`, digiKam, XnView, Apple Photos, IrfanView). Refreshes AI track with Windows ML dual-path (saves ~150 MB installer on Win11 24H2+ by skipping private ORT), cjpegli export (F-03, ~35% smaller JPEG), LaMa generative erase (U-03), Copilot+ Restyle (U-04); drops HEVC-bundling (Nokia enforcement). Adds Appendix A with 220+ deduplicated source URLs so every item is traceable. Companion research filed under `docs/gap-research-report-1.md` + `docs/gap-research-report-2.md`.
- README architecture tree now shows `Resources/icon.ico`, `icon.svg`, `logo.png` instead of the "not yet added" placeholder.

## v0.1.1 — 2026-04-24

### Changed

- Folder watcher (`FileSystemWatcher`) now actually runs — external add/delete/rename from Explorer or another app refreshes the list without pressing F5. The position chip updates live, and if the currently displayed file vanishes, the viewer advances to the next slot the navigator lands on.
- `BoolToVis` converter is now declared in `Themes/DarkTheme.xaml` (single source of truth, available to any view) instead of being redeclared per-window.
- `ImageLoader.Load` narrows its WIC catch to decode/format exceptions — `OutOfMemoryException` and thread aborts now propagate instead of silently falling through to Magick.NET. The WIC-path `MemoryStream` is disposed deterministically.
- `DirectoryNavigator.Open` short-circuits when called with a path inside the already-watched folder — no more full rescans on repeat drops from the same directory.
- `DirectoryNavigator.Rescan` catches transient IO / ACL / disconnection errors and keeps the prior list instead of throwing to the UI thread.

### Docs

- README zoom row clarifies wheel-zoom anchors on the cursor; removed the stray `Ctrl+wheel` alias claim that the code never honored.
- README architecture tree no longer claims a shipped `Resources/icon.ico` (icon is a v0.1.2 follow-up).

## v0.1.0 — 2026-04-24

Initial release.

- WPF / .NET 9 image viewer with WIC + Magick.NET decode pipeline (~100 formats incl. BMP/JPG/PNG/GIF/TIFF/WEBP/HEIC/ICO/JXL/AVIF/PSD/TGA/RAW).
- Windows 7 Photo Viewer–inspired chrome in Catppuccin Mocha dark theme.
- Hover-reveal left/right navigation arrows; Left/Right/Home/End/Space/Backspace keyboard navigation with wrap-around.
- Natural-sort directory scan on open; auto-refresh when files are added/removed.
- Split stem/extension rename editor with 600 ms debounced auto-save, live conflict preview, commit-on-navigation.
- Recent Renames panel with one-click undo for the last 10 renames.
- Zoom (wheel / Ctrl+wheel), pan (drag), fit-to-window, 1:1, rotate, delete-to-recycle-bin.
- Command-line: `Images.exe "C:\path\to\image.jpg"` opens file and populates directory.

</details>
