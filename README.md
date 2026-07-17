<div align="center">

<img src="assets/banner.png" alt="Images — a premium, local-first Windows image viewer" width="100%" />

# Images

[![Version](https://img.shields.io/badge/version-0.2.30-89b4fa?style=flat-square)](https://github.com/SysAdminDoc/Images/releases)
[![License](https://img.shields.io/badge/license-MIT-a6e3a1?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-cba6f7?style=flat-square)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-f38ba8?style=flat-square)](#)

A premium, local-first Windows image viewer with an image-first workspace and inline rename-while-viewing.

</div>

---

## Why another image viewer?

Because sometimes you don't know what to call a photo until you actually *see* it — and the existing dark-mode viewers on Windows make you close the image, rename the file, and reopen. **Images** fixes that: the filename lives in a side panel right next to the photo. Type; the file is renamed 600 ms after you stop typing. Change your mind? Hit **Undo** in the Recent Renames list.

## Features

- **Broad format coverage** via WPF's built-in WIC plus [Magick.NET](https://github.com/dlemstra/Magick.NET): JPG, PNG/APNG, GIF, TIFF, WEBP, HEIC, AVIF, JXL, PSD/PSB, TGA, DDS, QOI, EXR, HDR, DPX, JPEG 2000, DICOM, FITS, XCF/ORA, SVG, WMF/EMF, WPG, RAW/DNG/NEF/CR2/CR3/ARW/RW2/RAF/ORF/PEF, legacy production formats, and more.
- **HDR-to-SDR tonemapping** — the HDRI decoder preserves extended EXR/Radiance/RAW/high-bit-depth samples until a selectable Reinhard, Hable, or ACES curve maps them into the WPF SDR surface. Decoder status names the applied operator instead of silently hard-clipping highlights; on an active HDR desktop, a dedicated badge makes the SDR presentation explicit and diagnostics report DXGI color space plus peak/full-frame luminance.
- **RAW/photo culling overlays** — press `F` for focus peaking on high-contrast edges and `H` to mark clipped highlights/shadows. Analysis is bounded and cancellable, follows zoom/pan/rotate/flip, and does no pixel work while both overlays are off.
- **Native codec allowlist** — ImageMagick starts deny-all, then enables only the coders Images advertises plus internal XC/MSVG/MVG support required for safe rendering. MNG, TIM, MSL, and direct MVG inputs are not exposed; filters and `@` paths are disabled; external delegates are denied except the four Ghostscript raster-preview delegates when the approved document runtime is present.
- **Document/vector previews** for PDF, EPS, PS, and AI when Ghostscript is bundled app-local or installed on the machine. Images auto-detects `Codecs\Ghostscript`, `IMAGES_GHOSTSCRIPT_DIR`, and standard Ghostscript installs.
- **Multi-page navigation** for documents and layered/page-based image formats. PDF, TIFF, PSD/PSB, ICO, DICOM, FITS, DCX, and related formats surface page/frame controls only when the current file has more than one page.
- **Archive book previews** for ZIP/CBZ, RAR/CBR, and 7z/CB7. Images opens supported image entries as read-only pages, promotes explicit cover/front images, ignores unsafe or nested archive entries, keeps archive navigation inside page controls plus book-specific side-panel and edge-turn controls, supports persisted right-to-left page turns, two-page spreads, and a continuous vertical webtoon strip that lazily loads and recycles visible pages, adds a preview-only clean-scan filter, remembers the last read page locally, and lists recent books with progress in the side panel. The managed SharpCompress reader streams pages without extracting entries to disk, and Images independently verifies advertised ZIP/RAR/7z page CRCs before returning bytes.
- **Animated GIFs play inline** — multi-frame GIFs (and animated WebP / APNG when the Magick build supports them) decode via `MagickImageCollection.Coalesce()` and cycle through `ZoomPanImage` with the original per-frame delays + loop count intact. A green "N frames" chip in the bottom toolbar marks animated files.
- **Image-first viewer layout** — a quiet file-context bar and large stage keep the active image dominant, while a compact navigation rail, 316 px Details inspector, image-only filmstrip, and flat transport groups keep controls readable without surrounding every action or fact with a box. The shared desktop visual system uses 15 px body type with a 13 px minimum for literal labels, sentence-case section headings, underline tabs, restrained dividers, and outlines only where focus, selection, input, or structure needs them.
- **Multi-path launch** — `Images.exe a.jpg b.png c.webp` opens an ad hoc session containing exactly those files in argument order, supporting next/previous/Home/End navigation. Single-path launch falls back to folder navigation as before.
- **Peek mode** — `Images.exe --peek "C:\path\to\image.jpg"` opens a chromeless, topmost preview window that closes with Escape and leaves normal window settings alone.
- **Listen mode** — `Images.exe --listen 9876` (or `-l 9876`) opens the viewer with a local TCP listener on port 9876 (loopback 127.0.0.1 only). Connect with `ncat 127.0.0.1 9876`, send the session token on the first line, then send UTF-8 file paths one per line and the viewer opens or refreshes the sent image live. The green toolbar chip shows the active port and exposes the token in its tooltip; the app log also records it for automation setup. All received paths are logged in the network activity panel.
- **Live inline rename** — split stem + extension editor on the right. Extension is locked by default (no more accidentally renaming `photo.jpg` → `photo.jp`). Debounced auto-save; no Save button.
- **Conflict-safe** — if a target name already exists in the folder, the rename preview shows exactly what it will become (`name (2).jpg`) before it commits.
- **Recent Renames panel** — the last 10 renames are stacked on the side with **Undo** buttons.
- **Full directory navigation** — open one photo, scroll through the whole folder with ← / → keys or the hover arrows. Wraps at the ends. Natural-sorted so `IMG_2.jpg` comes before `IMG_10.jpg`.
- **Explorer-like folder order fallback** — the folder sort menu includes an explicit Explorer-like name order. Windows does not expose the live sort/grouping state of the Explorer window that launched a file, so Images uses a deterministic Shell-style fallback: natural base-name order, extension tie-break, then full name.
- **Text extraction (OCR)** — press `E` to overlay selectable text boxes directly over detected text regions. Highlight any recognized text manually and copy it with Ctrl+C or the context menu. Uses Windows.Media.Ocr for local, offline processing with installed Windows OCR language packs. No cloud, no network, no bloat.
- **Togglable folder filmstrip** — a compact, virtualized, cached rail of larger image-only previews spans the current folder, keeps the current item centered, preserves filenames and positions for tooltips and assistive technology, supports right-click Open/Reveal/Copy actions, and falls back to the side panel when hidden.
- **Gallery workbench** — press `G` to open a virtualized multi-column thumbnail grid for the current folder with quick filtering, smart filter tokens for format/folder/rating/tag/palette/orientation/dimensions/date/duplicate status, sort shortcuts, context actions, selection, and Enter-to-load.
- **Private tag relationships** — press `Ctrl+Shift+T` to manage local-only tag namespaces such as `person:`, `place:`, and `project:`, resolve aliases/siblings, expand parent tags, and import/export the current image's XMP sidecar tags.
- **Import inbox** — press `Ctrl+Shift+I` to stage new files before they join a library folder. The inbox detects exact duplicates in the staging set and destination, lets you tag/rate into XMP sidecars, imports Picasa `.picasa.ini` ratings/albums/faces into sidecars without modifying originals, strips GPS from imported JPEG/TIFF copies, recycles unwanted staged files, and copies or moves originals with collision-safe naming.
- **Macro actions** — press `Ctrl+Shift+M` to build and run local JSON actions. Plans stay inspectable before execution, support dry runs, load/save as JSON, and currently cover GPS stripping, export/convert/resize copies with quality settings, and rename patterns with tokens.
- **Batch processor** — press `Ctrl+Shift+B` to build ordered copy pipelines for resize, rotate, flip, metadata stripping, output naming, and export settings. Presets remain JSON-based, preview shows output paths/dimensions/size deltas, dry-run is default, and long runs can be canceled.
- **Export workbench** — press `Ctrl+Alt+W` to compare the displayed image against an in-memory encoded preview before writing a copy. JPEG, PNG, WebP, AVIF, and JXL presets show output dimensions, estimated size, byte delta, C2PA handoff status, and warnings for alpha flattening, animation/page loss, metadata, ICC profile risk, and lossy quality settings; linked pan/zoom and a toggleable difference view support close inspection before saving.
- **Non-destructive edit history** — press `Ctrl+Shift+E` to inspect XMP-backed edit operations, fork virtual copies without duplicating source pixels, enable or disable individual operations, and export edited copies with provenance sidecars.
- **Destructive crop apply for flat rasters** — JPEG/PNG/WebP/TIFF/GIF/BMP and similar bitmap images open directly in freehand crop mode, so you can drag a crop rectangle immediately. Press `C` to pause/resume crop mode, choose free/square/3:2/4:3/16:9/custom aspect ratios, then press Enter, the side-panel Apply button, or the Apply button attached to the crop box to overwrite the file with the cropped image and notify Explorer so thumbnails refresh. Crop stays disabled for layered, vector, document, archive, and RAW formats such as PSD/PSB, SVG/EPS, PDF/AI, CBZ, and DNG.
- **Lossless JPEG writeback runtime** — release packaging stages the approved libjpeg-turbo 3.1.4.1 `jpegtran.exe` sidecar with its adjacent `jpeg62.dll` dependency so exact MCU-aligned JPEG crop and right-angle rotation overwrite can avoid raster re-encoding. Diagnostics show the runtime path, version, and SHA-256; unaligned or oriented cases still fall back to the safer raster path unless the user confirms a lossless trim.
- **Non-destructive resize** — press `Ctrl+Alt+R` to add a resize operation with percent, pixel, long-edge, or short-edge sizing, aspect lock, Lanczos-3/Mitchell/Bicubic filters, and a live output-dimensions preview. Save a copy applies the resize without modifying the source file.
- **Non-destructive adjustments** — press `Ctrl+Alt+A` for a modeless levels, curve, and HSL workbench with live preview, reset, and Enter-to-apply behavior. Save a copy applies the adjustment stack without modifying the source file.
- **Local exposure brush** — press `Ctrl+Alt+D` to paint non-destructive dodge or burn strokes directly on the image. The side panel exposes tone, radius, and strength controls; Enter adds the soft-brush stroke stack to edit history, and Save a copy renders it without changing the source file.
- **Red-eye correction** — press `Ctrl+Alt+Y` to mark red pupils directly on the image. The side panel exposes radius, strength, and red-threshold controls; Enter adds the correction marks to edit history, and Save a copy renders them without changing the source file.
- **Clone/heal retouch** — press `Ctrl+Alt+H`, Alt-click or first-click a clean source, then paint clone-stamp or healing-brush strokes over the target area. The side panel exposes mode, radius, and strength controls; Save a copy renders the retouch stack without changing the source file.
- **Reference board mode** — press `Ctrl+B` to open a separate local board seeded from the current image. Drop supported files, arrange image cards, add notes and group frames, pin the board above other windows, zoom the canvas, and export the composed board as PNG.
- **Duplicate cleanup center** — press `Ctrl+Shift+D` or use the side-panel Cleanup card to scan local folders for exact SHA-256 duplicates and perceptually similar images, prefer keep candidates from reference folders, inspect pairs side by side, mark false positives, and move extras to app-local quarantine or the Recycle Bin.
- **Compare mode** — press `Ctrl+Alt+C` to compare the current image with the next folder item, choose another local image from the side panel or context menu, or send the selected duplicate-cleanup pair into the viewer. 2-up and opacity-overlay layouts share pan, zoom, rotate, flip, A/B swap, and keyboard-accessible opacity controls.
- **Rebuildable watched-folder catalog** — Images maintains an app-local SQLite catalog cache for library-scale workflows. Registered roots persist across launches, scan on normal startup, and refresh through debounced recursive file-system events; offline or ejected roots keep their cached rows until they reconnect or are explicitly removed. The catalog records source path, exact SHA-256 and perceptual hashes, dimensions, file dates, codec metadata, XMP sidecar rating/tags, GPS, capture time, and camera/lens facts; sidecars and source files remain authoritative, so deleting `catalog.db` is a valid recovery step. `--catalog-search "<terms>"`, `--catalog-near <lat> <lon> <radiusKm>`, `--catalog-stacks [<maxHashDistance> <maxSeconds> <maxMeters>]`, `--catalog-trips <homeLat> <homeLon> [<minDistanceKm> <maxGapDays>]`, `--catalog-events [<maxGapHours>]`, `--catalog-root-add <folder>`, `--catalog-root-remove <folder>`, `--catalog-root-list`, and `--catalog-rescan` provide window-free automation. Catalog migrations run forward only with integrity checks, WAL checkpointing, versioned backups, and a schema canary before the cache is reused.
- **Local semantic search** — open Semantic search from the context menu or Automation card to add or remove persistent library roots, explicitly rebuild app-local `semantic-index.db`, filter by folder, reveal/open results, cancel indexing, and delete derived search data. Offline roots are labeled while their cached catalog rows remain available. It uses approved, user-imported CLIP ViT-B/32 ONNX models when their pinned hashes validate and otherwise falls back visibly to deterministic offline metadata embeddings.
- **Tag graph and category sets** — open Tag relationships from the context menu or Automation card to manage local namespaces, aliases, and parent tags, preview expansion, and deliberately import or export XMP sidecar keywords. Category Sets save the current tag panel by job type and swap it back into the editor without writing metadata until Export is explicitly chosen.
- **Local face review workflow** — after manually importing the pinned OpenCV YuNet and SFace models, `Images.exe --face-detect <imagePath>` emits reviewable bounds and landmarks, `--face-xmp <imagePath>` emits an unassigned MWG-rs draft, and `--face-cluster <imagePath> <imagePath> [...]` reports local cosine clusters without exposing private vectors. The Face-region review workbench adds the deliberate write path: inspect color-coded bounds and face crops grouped by local cluster, accept or reject every region, name every accepted region, and only then merge Images-owned MWG-rs entries atomically into XMP sidecars while preserving foreign regions and unrelated metadata. Each crop also shows transparent, fallible face-local blur and possible-closed-eye signals derived from normalized sharpness and eye-region texture; they are review hints only and never change Pick/Reject state.
- **Local object workflow CLI** — after manually importing the pinned Apache-2.0 OpenCV YOLOX-S model, `Images.exe --object-detect <imagePath>` emits class-aware-NMS COCO boxes and deduplicated `object:` keyword suggestions, while `--object-xmp <imagePath>` prints a reviewable `dc:subject` draft that the catalog can index after explicit placement. Neither command opens a window or modifies the image, sidecar, or catalog.
- **Local orientation suggestion CLI** — after manually importing the revision/size/SHA-256-pinned MIT ConvNeXtV2 orientation model, `Images.exe --orientation-suggest <imagePath>` reports a clockwise correction hint for receipts, invoices, documents, and screenshots. It applies EXIF orientation first, withholds ambiguous results unless confidence is at least 0.80 and the runner-up margin is at least 0.20, and never rotates or writes a file.
- **Local aesthetic culling CLI** — the repository stages a reproducibly converted, Apache-2.0 idealo NIMA MobileNet model with its exact source checkpoint, license, conversion recipe, and hash provenance. After manual Model Manager import, `Images.exe --aesthetic-score <imagePath> [imagePath ...]` reuses one local inference session to rank the batch and report each 1–10 opinion distribution, mean, and uncertainty. Scores are relative review hints: the command never writes Pick/Reject labels, ratings, metadata, or files.
- **Local scene suggestion CLI** — the repository stages the official Places365 ResNet-18 checkpoint as a reproducible ONNX graph with all 365 aligned category and indoor/outdoor labels, its MIT code notice, and the model's required CC-BY attribution. After manual Model Manager import, `Images.exe --scene-classify <imagePath> [imagePath ...]` reports five scene probabilities, a probability-weighted indoor/outdoor assessment, and conservative `scene:`/`environment:` smart-album keyword suggestions. It never writes keywords, metadata, files, or smart albums.
- **Optional local safety-score CLI** — the repository stages a reproducibly converted, Apache-2.0 Marqo ViT-Tiny model with exact checkpoint/config/license/hash provenance. After manual Model Manager import, the default-off `Images.exe --safety-classify <imagePath> [imagePath ...]` command explicitly exports NSFW/SFW probabilities to stdout for human review. Images imposes no moderation threshold, never runs the classifier automatically, and never stores its scores in files, source metadata, catalog labels, or logs.
- **File health scan** — press `Ctrl+Shift+H` to find files with mismatched image extensions, corrupt supported images, zero-byte files, and temporary/partial-download artifacts, then rename detected extensions, mark reviewed, or move files to app-local quarantine.
- **Recovery center** — open it from the context menu or Cleanup card to inspect recent move, rename, quarantine, writeback, and Recycle Bin actions. Moves, renames, and quarantines can be restored with collision-safe targets and matching XMP sidecars when the recovery source still exists; writebacks and Recycle Bin sends show explicit restore guidance.
- **Pinned overlay mode** — pin the current image above other windows for tracing or design comparison, tune opacity in the side panel, and optionally enable click-through only when the `Ctrl+Alt+O` global exit hotkey is registered.
- **Pixel inspector** — enable Inspector in the side panel to sample coordinates and HEX/RGB/HSV/alpha values, copy color values, Shift-drag pixel measurements, and switch to nearest-neighbor preview scaling for pixel art.
- **Animation frame workbench** — animated GIF/APNG/WebP files get a side-panel timeline with a scrubber, frame stepping, playback-speed control, copy-current-frame, PNG frame export, and drag-out selected frames.
- **Photo metadata and monitor-aware color** — the Details panel and optional `I` metadata HUD surface embedded EXIF date, camera, lens, exposure, focal length, GPS coordinates, and safely version/shape-gated Exif 3.1 learning-use and processing provenance when present. The side panel reports ICC/profile status, decoded color space, luma/RGB histogram basics, and alpha statistics. The opt-in color-managed display path converts tagged images to the active monitor ICC profile in legacy SDR mode, falls back to sRGB when Advanced Color is active or uncertain, and re-decodes when the window crosses monitors.
- **Deep-zoom huge-image viewing** — images over 256 MB or 50 megapixels are rendered through a local DZI-style WebP tile pyramid, so pan and zoom request cached visible tiles instead of treating the full source as one WPF bitmap.
- **Zoom + pan** — mouse wheel to zoom in/out about the cursor, drag to pan, double-click to toggle fit/1:1.
- **Export a copy** to JPEG, PNG, WebP, AVIF, JXL, TIFF, BMP, GIF, PSD/PSB, TGA, DDS, QOI, EXR, HDR, JPEG 2000, X11/Magick, production/scientific, and portable bitmap formats. APNG remains supported for opening and frame inspection, while copy/export writes normalize APNG targets to PNG. PDF/EPS/SVG/MVG/MSL/URL-style write targets fall back to PNG by policy; document/vector formats remain preview-only through the optional runtime path.
- **Rotate**, **delete-to-Recycle-Bin**, **Reveal in Explorer**, **Copy path**.
- **Calm confirmations and recovery** — destructive file actions confirm before moving anything to Recycle Bin, routine actions complete with toast feedback, and recent destructive operations are recorded in the app-local Recovery Center.
- **Organized, portable settings** — Settings groups startup, appearance, accessibility, archive defaults, hotkeys, diagnostics, text extraction, and privacy controls. Export/import carries an explicit allowlist of preferences and custom shortcuts between installations while excluding private paths, window coordinates, history, and update-check consent. The authoritative local-data inventory reports every cache, index, diagnostic record, user-owned backup, model, and draft; a confirmed privacy reset clears rebuildable/diagnostic stores while preserving imported models and user-owned recovery files.
- **C2PA content credential inspection** — when an optional `c2patool` runtime is placed under `Codecs\C2paTool` or configured with `IMAGES_C2PATOOL_EXE`, the side panel shows a read-only Content Credentials section for supported image formats displaying trust status, claim generator, signature date, assertions, and ingredient provenance. Export preview and Save a copy report whether Content Credentials will be preserved, written through an approved writer, or omitted; current re-encoded exports omit credentials unless an approved writer is configured. Images explicitly communicates that content credentials show provenance (who created or edited a file), not authenticity (whether the content is truthful).
- **Runtime provenance dashboard** — About, `--system-info`, and `--codec-report` list key NuGet packages, optional runtimes, OS OCR, c2patool, and local model/runtime status with source, version, path, SHA-256 where available, advisory status, and setup/release action copy. AI sessions report the hardware actually selected as `NPU`, `GPU`, or `CPU`, with the execution provider/vendor detail; Windows ML registers only already-ready certified providers and retains bundled DirectML/CPU fallbacks. The loaded Magick.NET, native ImageMagick, and native SQLite versions are queried directly; startup warns below the reviewed ImageMagick 7.1.2-2 and SQLite 3.53.2 floors.
- **Local model manager** — open Model manager from the context menu or Automation card to inspect approved local model definitions, reveal app-local model storage, import model files you downloaded yourself, verify SHA-256 against pinned OpenCV/Carve LaMa, Qdrant CLIP ViT-B/32, OpenCV YuNet/SFace/YOLOX-S, Fachuan ConvNeXtV2 orientation, idealo NIMA, CSAIL Places365, and Marqo safety candidates, and delete mismatched or stale files. Images does not download models automatically.
- **Redacted support bundle export** — About offers a one-click "Export support bundle" that writes a ZIP with system info, codec report, network activity, diagnostics status, recent logs, crash log, recovery records, redacted settings, and cache health for bug reports. No image bytes are included, and every recognized absolute drive, UNC/device, file-URI, or POSIX path in textual entries is replaced with `%PATH%`.
- **Network-quiet by default** — automatic update checks are disabled until enabled in Settings; manual About checks remain available.

## Install

Both artifacts ship alongside every release. They're the same build — pick whichever fits your workflow.

### Installer (recommended for most users)

1. Grab `Images-vX.Y.Z-setup-win-x64.exe` from [Releases](https://github.com/SysAdminDoc/Images/releases).
2. Run it. Installs to `%ProgramFiles%\Images`, removes any older per-user or machine-wide Images install first, bundles Ghostscript for PDF/EPS/PS/AI previews, and provisions the Windows OCR language capability for the current UI language plus `en-US` fallback. No separate .NET runtime or Ghostscript install is required.
3. Optional boxes on the wizard: **Desktop icon**, **Add to "Open with" menu** (non-destructive — adds *Images* to the Windows "Open with" list without overriding whatever you currently have set as default for those extensions). If an older install already had Images associations enabled, the installer carries them forward automatically.
4. Uninstalls cleanly from Settings → Apps → Installed apps.

The installer is self-contained: the .NET Desktop runtime, Magick.NET, SharpCompress, bundled Ghostscript runtime, and approved libjpeg-turbo `jpegtran.exe` plus `jpeg62.dll` sidecar ship inside the app folder.

### Portable (zero install)

1. Grab `Images-vX.Y.Z-win-x64.zip` from [Releases](https://github.com/SysAdminDoc/Images/releases).
2. Extract anywhere.
3. Run `Images.exe`. Leaves no registry writes. The portable folder includes the same bundled Ghostscript and jpegtran runtimes as the installer.

To associate file types from a portable install: right-click any image → **Open with** → **Choose another app** → browse to `Images.exe` → tick **Always use this app**.

### From source

```bash
git clone https://github.com/SysAdminDoc/Images.git
cd Images
dotnet restore --locked-mode
dotnet build -c Release
dotnet run --project src/Images
```

The repository pins the .NET 10 SDK feature band in `global.json` and commits NuGet lock files for repeatable restores. Release/SBOM work also starts with `dotnet tool restore`; the local manifest pins the CycloneDX generator used by `scripts/New-Sbom.ps1`.

### Bundled Ghostscript

PDF, EPS, PS, and AI previews require Ghostscript. Official release artifacts bundle Ghostscript 10.07.0 app-local under `Codecs\Ghostscript`, so users do not need to install Ghostscript separately. The bundled runtime is the AGPL Ghostscript distribution from Artifex; its license is installed at `Codecs\Ghostscript\doc\COPYING`, and the matching source archive is attached to the GitHub release.

Development builds can still detect `IMAGES_GHOSTSCRIPT_DIR` and normal system installs under `%ProgramFiles%\gs`. Keep third-party binaries out of source control unless redistribution rights for the exact package are already approved.

Release builders can use `scripts/Prepare-GhostscriptBundle.ps1`; see `docs/codec-bundling.md`.

To build the installer locally, install [Inno Setup 6](https://jrsoftware.org/isdl.php), stage Ghostscript with `scripts\Prepare-GhostscriptBundle.ps1`, run `dotnet publish src/Images -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish`, then `iscc /DMyAppVersion=0.2.30 installer\Images.iss`. Output lands at `installer\output\Images-vX.Y.Z-setup-win-x64.exe`.

OCR depends on Microsoft Windows OCR optional capabilities. The installer installs the current Windows UI language OCR capability plus `en-US` fallback when needed; Images cannot legally bundle those Microsoft language packs inside the app folder.

## Keyboard

| Key | Action |
| --- | --- |
| **← / →** | Previous / next image in folder, or physical page turn in book mode |
| **Home / End** | First / last image |
| **Space / Backspace** | Next / previous image |
| **Delete** | Send current image to Recycle Bin |
| **C** | Toggle crop mode |
| **Ctrl+Alt+R** | Open resize dialog |
| **Ctrl+Alt+A** | Open adjustments workbench |
| **Ctrl+Alt+D** | Toggle local exposure brush |
| **Ctrl+Alt+Y** | Toggle red-eye correction |
| **Ctrl+Alt+H** | Toggle clone/heal retouch |
| **E** | Extract text (OCR) — toggle overlay |
| **F** | Toggle focus peaking |
| **H** | Toggle highlight/shadow clipping |
| **F5** | Rescan current directory |
| **G** | Toggle gallery workbench |
| **Ctrl+B** | Open reference board |
| **Ctrl+Shift+D** | Open duplicate cleanup |
| **Ctrl+Shift+H** | Open file health scan |
| **Ctrl+Shift+T** | Open tag relationships |
| **Ctrl+Shift+I** | Open import inbox |
| **Ctrl+Shift+M** | Open macro actions |
| **Ctrl+Shift+B** | Open batch processor |
| **Ctrl+Shift+E** | Open edit history |
| **Ctrl+Alt+W** | Open export workbench |
| **Ctrl+Alt+C** | Compare current image with next folder item |
| **Ctrl+Alt+V** | Compare current image with a chosen local file |
| **O / X** | In compare mode, toggle overlay layout / swap A-B |
| **[ / ]** | In compare mode, adjust B opacity |
| **Ctrl+Alt+O** | Exit pinned overlay mode |
| **Ctrl+Left / Ctrl+Right** | Step animated image frames |
| **Ctrl+Space** | Play/pause animated image |
| **I** | Toggle metadata HUD |
| **Enter** | Apply the active crop selection, or commit rename when the rename box is active |
| **Esc** (in rename box) | Cancel edit, revert textbox to disk name |
| **+ / -** | Zoom in / out |
| **0** | Fit to window |
| **1** | 100% zoom |

*(Navigation keys are swallowed by the rename textbox while it has focus, so you can freely use arrow keys inside the filename editor.)*

## Architecture

```
src/Images/
├── App.xaml                    # Entry point, theme merge
├── MainWindow.xaml             # Layout: image canvas + side rename panel + bottom toolbar
├── ReferenceBoardWindow.xaml   # Local reference-board canvas for images, notes, groups, and PNG export
├── DuplicateCleanupWindow.xaml # Local duplicate/similar-image cleanup surface
├── FileHealthScanWindow.xaml   # Local bad-extension, broken-file, zero-byte, and temp-file inspection surface
├── RecoveryCenterWindow.xaml   # Inspect, reveal, and restore recent destructive actions where possible
├── ModelManagerWindow.xaml     # Approved local ONNX model import, hash verification, and runtime status
├── SemanticSearchWindow.xaml   # Local semantic-index build/search/reveal surface
├── ViewModels/
│   ├── ObservableObject.cs     # INotifyPropertyChanged base
│   ├── RelayCommand.cs         # ICommand impl
│   ├── AnimationFrameItem.cs    # Timeline item state for animated-image frames
│   └── MainViewModel.cs        # All view state + commands
├── Services/
│   ├── ImageLoader.cs          # WIC-first, Magick.NET fallback, cached decoding
│   ├── ArchiveBookService.cs   # Read-only ZIP/CBZ, RAR/CBR, and 7z/CB7 page discovery
│   ├── AnimationWorkbenchService.cs # Frame timing, labels, PNG export, and drag-file creation
│   ├── ReferenceBoardLayoutService.cs # Board placement, clamping, and export bounds
│   ├── DuplicateCleanupService.cs # Exact hash grouping, perceptual similarity, and quarantine moves
│   ├── FileHealthScanService.cs # Content-signature checks, decode health scans, rename/quarantine actions
│   ├── RecoveryCenterService.cs # Durable destructive-action ledger and collision-safe restore support
│   ├── ModelManagerService.cs  # Approved model registry, app-local storage, SHA-256 checks, runtime status
│   ├── OverlayWindowService.cs # Native always-on-top/click-through overlay window helpers
│   ├── PixelInspectorService.cs # Pixel coordinate mapping, sampling, color formatting, and measurement math
│   ├── ImageExportService.cs   # Codec-aware Save a copy / conversion output
│   ├── ExportPreviewService.cs # In-memory export previews, size estimates, and format warnings
│   ├── ExportCapabilityWarningService.cs # Shared target-format loss warnings for export and batch
│   ├── CatalogService.cs       # Rebuildable app-local catalog cache for metadata/search foundations
│   ├── SemanticSearchService.cs # Local semantic index, embedding provider seam, and cosine search
│   ├── NonDestructiveEditService.cs # XMP edit-stack persistence and export application
│   ├── ImageAdjustmentService.cs # Levels, curve, and HSL adjustment planning/rendering
│   ├── LocalExposureBrushService.cs # Soft dodge/burn brush strokes for non-destructive local exposure
│   ├── RedEyeCorrectionService.cs # Red-dominant pupil correction marks for non-destructive red-eye removal
│   ├── RetouchBrushService.cs # Clone stamp and healing brush retouch operations
│   ├── ImageMetadataService.cs # Read-only EXIF summary for the Details panel and HUD
│   ├── ImageColorAnalysisService.cs # Read-only ICC/profile and histogram/channel summaries
│   ├── CodecCapabilityService.cs # About-window codec summary and copyable diagnostics
│   ├── SupportBundleService.cs # Redacted ZIP bundle for bug reports
│   ├── SupportedImageFormats.cs # Central extension catalog for discovery/dialogs
│   ├── CodecRuntime.cs         # Optional app-local Ghostscript runtime discovery
│   ├── AppStorage.cs           # LocalAppData/Temp storage fallback for caches and logs
│   ├── DirectoryNavigator.cs   # Natural-sort folder scan, prev/next/wrap, FileSystemWatcher
│   ├── ThumbnailCache.cs       # Disposable WebP thumbnail cache for the folder preview strip
│   └── RenameService.cs        # Debounced File.Move, conflict resolution, undo history
├── Controls/
│   ├── ZoomPanImage.cs         # Wheel-zoom + drag-pan image host and renderer routing
│   └── SkiaBitmapPresenter.cs  # Software Skia surface for static BitmapSource presentation
├── Themes/
│   ├── DarkTheme.xaml          # Catppuccin Mocha tokens + control styles
│   └── HighContrastTheme.xaml  # SystemColors-backed high-contrast overrides
└── Resources/                  # icon.ico (app icon), icon.svg (vector wrapper), logo.png
tests/Images.Tests/             # focused regression tests for file-operation services
```

## Diagnostics

Images carries its own diagnostics surface — no terminal required for the common cases:

- **About → Save system info** writes the same content as `Images.exe --system-info` to a file in `%TEMP%` and reveals it in Explorer. Attach the file to a bug report.
- **About → Open data folder** opens `%LOCALAPPDATA%\Images\` so logs (`Logs\images-<date>.log`), crash records (`crash.log`, `crash-*.dmp`), settings (`settings.db`), and caches (`thumbs/`, `update-check.json`) are reachable in one click.
- **About → Codec report** copies the per-format capability matrix, supported-extension list, and runtime/dependency provenance rows to the clipboard.
- `Images.exe --system-info` and `Images.exe --codec-report` print the same provenance rows to stdout for support tickets and CI smoke tests.
- `Images.exe --perf-report` measures cold-start time, directory scan, cache health, settings access, and memory against budgeted thresholds for regression tracking.
- `scripts/Test-WpfBackgroundSmoke.ps1` builds Release and drives six offscreen, non-activating FlaUI checks against an isolated data root; it never uses global mouse/keyboard input or the signed-in user's Images settings.

## Policies

- [Release support policy](docs/release-support-policy.md) — what versions get servicing and for how long.
- [Release checklist](docs/release-checklist.md) — current-state, roadmap-closure, version/date, and runtime checks before publishing.
- [Codec support policy](docs/codec-support-policy.md) — bundled-vs-optional tiers and the gate every new optional decoder must pass.
- [Privacy policy](docs/privacy-policy.md) — exactly one network call (the opt-out update check), every file persisted to disk, and a four-step verification recipe.
- [Distribution trust plan](docs/distribution-trust.md) — unsigned WinGet/Scoop scope, checksum continuity, and verification copy.
- [Optional runtime and integration policy](docs/integration-policy.md) — license, provenance, CVE, process-boundary, and release gates for external runtimes.
- [Archive runtime review](docs/archive-runtime-review.md) — approved SharpCompress RAR/7z reader path plus native 7-Zip/UnRAR fallback gates.
- [Peek mode](docs/peek-mode.md) — shell-helper invocation, local startup timing diagnostics, and manual smoke steps.

## Credits / inspiration

Architectural inspiration taken from existing OSS viewers (no code copied, both are GPL-3):

- [**ImageGlass**](https://github.com/d2phap/ImageGlass) — Windows-native decoding model, format breadth, toolbar ergonomics.
- [**nomacs**](https://github.com/nomacs/nomacs) — side-panel information architecture, filename-edit UX.

## License

[MIT](LICENSE) © SysAdminDoc
