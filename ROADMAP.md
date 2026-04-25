# Images — ROADMAP

Tracks planned work. `[ ]` pending, `[x]` shipped. Priorities `P0` must / `P1` should / `P2` nice.
Effort tags: **S** ≤ 2 days · **M** ≤ 1 week · **L** > 1 week · **XL** multi-week project.

> **Document version**: v4 · 2026-04-25. Supersedes v3 (2026-04-25). Adds a supplemental project-mining pass across PicView, NeeView, YACReader, PhotoDemon, Czkawka/Krokiet, PureRef, Eagle, Hydrus, OpenSeadragon, OpenSlide, Bio-Formats, napari, QuPath, libvips, OpenImageIO, and OpenColorIO. The new findings translate into archive/book mode, a gallery workbench, clipboard/URL/Base64 ingress, reference-board mode, duplicate cleanup, bad-extension scanning, deep-zoom viewing, scientific/whole-slide format evaluation, multidimensional channel/time navigation, a streaming batch backend spike, OCIO/ACES color-management work, and macro/action editing. Retains the v3 research additions: V20-32 `--peek` CLI mode, V20-15-Loop animation-loop badge, V-OCR text-on-image spike, D-05a SignPath.io evaluation, S-11 libde265 floor, updated S-09 codec security floors, P-05 C2PA verify promoted to P0, and the Sources Appendix.

> **Vision**: One Windows app that replaces Photos, IrfanView, XnConvert, Upscayl, and a light Lightroom — by cannibalising the best ideas from a dozen OSS/freeware projects. Local-first, fast, dark-mode, no cloud, no subscription. The killer features are **CLIP semantic search** on a local library, **live inline rename** (already shipped), **Squoosh-style visual-diff converter**, and — differentiator nobody else ships — **network-egress transparency**: the viewer never touches the network silently, and you can see every call it makes.

## Current state (v0.1.7 — shipped 2026-04-25)

Core viewer. Natural-sort folder nav. Zoom/pan/rotate/flip. Four zoom modes (Fit / 1:1 / FitWidth / FitHeight / Fill) [V20-20]. Live inline rename with 600 ms debounce, conflict resolution, 10-deep undo stack. Drag-drop. FSW. Catppuccin Mocha dark theme. ~100 formats via WIC + Magick.NET 14.13.0 [[S-MAGICK-RELEASES]](https://github.com/dlemstra/Magick.NET/releases). **Animated GIFs play inline** (V20-15 core shipped). **>256 MB files decode via MemoryMappedFile view** (V20-06). **SQLite persistence** (V20-02): window state restored across sessions, recent folders MRU, update-check opt-out. **Preload N±1 ring** (V20-03). **Thumbnail cache disk layer** (V20-04, UI consumer pending). **UIA peer** (A-01). **Structured logging** via Serilog (V02-06) + **minidump on fatal** with crash dialog that can open a GitHub issue w/ context pre-filled (V02-07). **Pull-only update check** against GitHub Releases, 24-h throttle, opt-out toggle in About (P-04). **Print** (V15-10) + **Save-as-copy** (E6). Framework-dependent win-x64 portable zip **and Inno Setup installer** ship on every release (D-01b). No editor, no organizer, no batch, no filmstrip UI.

Companion research:
- [docs/research-viewers-editors.md](docs/research-viewers-editors.md) — IrfanView, XnView MP, ImageGlass, nomacs, qView, JPEGView, FastStone, Honeyview, Windows Photos, QuickLook/Seer/Peek, Pictus.
- [docs/research-organizers-converters.md](docs/research-organizers-converters.md) — digiKam, Shotwell, XnView organizer, Daminion, Apple/Google Photos, Picasa, Lightroom, Bridge, PhotoPrism, Immich, XnConvert, ImageMagick, Converseen, Squoosh, FileOptimizer.
- [docs/research-advanced-features.md](docs/research-advanced-features.md) — AI (Upscayl/Real-ESRGAN, rembg/BiRefNet, CLIP, YOLO, OCR, faces), editors (GIMP/Krita/Paint.NET/darktable), panorama (Hugin), HDR (enfuse), lossless transforms, plugin hosts, canvas-engine decision.
- [docs/gap-research-report-1.md](docs/gap-research-report-1.md) — accessibility, i18n, observability, distribution, testing, migration importers, catalog schema strategy.
- [docs/gap-research-report-2.md](docs/gap-research-report-2.md) — security/privacy/CVEs, Azure Trusted Signing vs EV, winget/Scoop/MSIX status, Windows ML vs DirectML, OSS viewer release tracker, 2026 codec status, LaMa/chaiNNer/OpenModelDB.

---

## Supplemental project-mining pass — 2026-04-25

Goal: mine adjacent viewers, editors, asset managers, scientific viewers, and image-processing stacks for features that would make Images a stronger local-first Windows image platform rather than another viewer clone. No source code should be copied. All external projects below are references for product behavior, architecture, format coverage, or UX patterns only.

### Viewer and navigation upgrades

- [ ] **V20-33** *P0* — **Archive/book mode**. Treat folders and archives as the same navigable unit: ZIP/CBZ first, then RAR/CBR/7z/CB7 if redistributable extraction can be bundled safely. Add a page scrubber, page count, cover detection, natural spread handling, "open archive as book", and recursive-archive guardrails. Inspired by PicView archive loading, NeeView's "book/page" mental model, and YACReader's comic-library workflow. Effort: M. [[S-PICVIEW-ORG]](https://picview.org/) [[S-NEEVIEW-GUIDE]](https://neelabo.github.io/NeeView/en-us/userguide.html) [[S-YACREADER]](https://www.yacreader.com/)
- [ ] **V20-34** *P1* — **Gallery workbench**. Upgrade the folder preview strip into a keyboard-first gallery mode (`G`) with multi-column thumbnails, per-thumbnail context menus, filter/sort controls, selection, and Enter-to-load. PicView proves the value of a gallery that remains inside the viewer instead of becoming a separate DAM screen. Effort: M. [[S-PICVIEW-ORG]](https://picview.org/)
- [ ] **V20-35** *P1* — **Universal open/paste ingress**. Accept files, folders, archives, image clipboard data, file paths, `data:image/...;base64`, and opt-in URL opens from one command. URL opens must flow through the network-egress log and never auto-fetch silently. PicView's paste-from-clipboard/URL/Base64 support is the reference, with Images' privacy model as the differentiator. Effort: M. [[S-PICVIEW-ORG]](https://picview.org/)
- [ ] **V20-36** *P2* — **Reader comfort controls**. Add book-facing controls that normal photo viewers skip: left/right page-turn click zones, optional dual-page spread, manga/right-to-left ordering, old-scan filter preset, persistent read position, and "continue reading" for archives. Keep this hidden until an archive/book is active so the normal photo workflow stays minimal. Effort: M. [[S-NEEVIEW-GUIDE]](https://neelabo.github.io/NeeView/en-us/userguide.html) [[S-YACREADER]](https://www.yacreader.com/)
- [ ] **V20-37** *P2* — **Codec/system-info CLI**. Add `Images.exe --system-info` and `Images.exe --codec-report` to print runtime, OS, Magick.NET, Ghostscript, WIC, writable export targets, cache paths, and app data paths. YACReader's system-info command is the model; it makes bug reports and support threads materially cleaner. Effort: S. [[S-YACREADER]](https://www.yacreader.com/)

### Creative reference and inspection workflows

- [ ] **V81-21** *P1* — **Reference board mode**. A separate local workspace where users can drop images, arrange them on an infinite canvas, group them, add notes, pin the window, and export the board. PureRef shows that "view many references while working" is a distinct workflow from browsing a folder; Images can make it local, lightweight, and codec-rich. Effort: L. [[S-PUREREF]](https://www.pureref.com/handbook/2.0/features/)
- [ ] **V81-22** *P1* — **Inspector tools**. Add eyedropper, copied HEX/RGB/HSV values, pixel coordinates, selection dimensions, nearest-neighbor toggle for pixel art, and per-image sampling mode. PureRef's color/coordinate inspection is the interaction target; Images should expose it in the main viewer and reference-board mode. Effort: M. [[S-PUREREF]](https://www.pureref.com/handbook/2.0/features/)
- [ ] **V81-23** *P2* — **Animation frame workbench**. Extend the shipped GIF animation support with a frame timeline, scrubber, frame-step commands, playback speed, copy-current-frame, and drag/export selected frame. PureRef's GIF timeline is the product reference. Effort: M. [[S-PUREREF]](https://www.pureref.com/handbook/2.0/features/)
- [ ] **V81-24** *P2* — **Pinned overlay mode**. Add an optional always-on-top/current-app-only overlay mode and click-through mode for tracing, visual comparison, and design reference. Must be explicit, visibly indicated, and easy to exit from taskbar/tray/menu to avoid trapping users. Effort: M. [[S-PUREREF]](https://www.pureref.com/handbook/2.0/features/)

### Library intelligence, cleanup, and asset management

- [ ] **V40-60** *P0* — **Duplicate and similar-image cleanup center**. Build a local-only cleanup surface with exact hash duplicates, perceptual image similarity, similarity threshold slider, side-by-side compare, reference folders, "mark not duplicate", and non-destructive quarantine/recycle actions. Czkawka/Krokiet supplies the power-user model; digiKam/Eagle prove this belongs in photo libraries. Effort: L. [[S-CZKAWKA]](https://github.com/qarmin/czkawka) [[S-DIGIKAM-FEATURES]](https://www.digikam.org/about/features/) [[S-EAGLE]](https://www.eagle.cool/)
- [ ] **V40-61** *P1* — **Bad-extension and broken-file scan**. Scan selected folders for files whose extension does not match detected content, corrupt images that fail decode, zero-byte files, and suspicious temporary files. Offer rename/repair/quarantine actions with a preview. Czkawka's "Bad Extensions" and "Broken Files" tools are the reference. Effort: M. [[S-CZKAWKA]](https://github.com/qarmin/czkawka)
- [ ] **V40-62** *P1* — **Asset-manager smart filters**. Add Eagle-style fast filters for tag, folder, rating, format, color palette, orientation, dimensions, date, and duplicate status. These should work against the SQLite catalog and return instantly on tens of thousands of local files. Effort: L. [[S-EAGLE]](https://www.eagle.cool/)
- [ ] **V40-63** *P2* — **Tag namespaces and relationship graph**. Introduce optional Hydrus-inspired local tag namespaces (`person:`, `place:`, `project:`), tag aliases/siblings, tag parents, and sidecar import/export. Keep it private and offline; do not replicate Hydrus' public tag network. Effort: XL. [[S-HYDRUS]](https://hydrusnetwork.github.io/hydrus/)
- [ ] **V40-64** *P2* — **Import inbox workflow**. A staging area for newly opened/imported images where users can tag, rate, delete duplicates, strip location, and choose a destination before files join the permanent library. Borrow Hydrus' "large collections need an intake process" lesson but simplify heavily for normal Windows photo users. Effort: L. [[S-HYDRUS]](https://hydrusnetwork.github.io/hydrus/)

### Professional editing, batch, and automation

- [ ] **E10** *P1* — **Macro recorder and action runner**. Record common operations (resize, convert, strip GPS, rename pattern, export format, quality, watermark later) into reusable local actions. PhotoDemon proves macro recorder + batch processor is a power multiplier; Images should keep actions inspectable as JSON before running. Effort: L. [[S-PHOTODEMON]](https://photodemon.org/)
- [ ] **E11** *P1* — **Batch processor with live preview and presets**. Batch resize/convert/optimize/export selected files with before/after estimate, overwrite protection, output-folder defaults, saved presets, and resumable progress. This bridges PicView's batch resize/convert UI with PhotoDemon's batch processor discipline. Effort: L. [[S-PICVIEW-ORG]](https://picview.org/) [[S-PHOTODEMON]](https://photodemon.org/)
- [ ] **E12** *P2* — **Selections, crop, and content-aware repair spike**. Add on-canvas crop first, then selection tools, then inpaint/content-aware fill through an explicit model/runtime choice. PhotoDemon's portable editor breadth is the product benchmark, but Images should only ship this after non-destructive history and export provenance exist. Effort: XL. [[S-PHOTODEMON]](https://photodemon.org/)
- [ ] **E13** *P2* — **Realtime adjustment preview contracts**. Every adjustment dialog should support live preview, saved presets, keyboard navigation, and undo/redo integration before broad editor features land. PhotoDemon's "all tools preview/presets/keyboard" standard is the quality bar. Effort: M. [[S-PHOTODEMON]](https://photodemon.org/)

### Extreme formats, huge images, and scientific workflows

- [ ] **V80-20** *P0* — **Deep-zoom image engine**. Add a tile-pyramid cache for huge TIFF/PSD/EXR/JXL/WSI files so pan/zoom never requires full decode into memory. OpenSeadragon's DZI/IIIF/IIP/Zoomify model and OpenSlide's whole-slide tiling show the architecture: viewport requests tiles, background workers fill cache, UI remains responsive. Effort: XL. [[S-OPENSEADRAGON]](https://openseadragon.github.io/) [[S-OPENSLIDE]](https://openslide.org/)
- [ ] **V80-21** *P1* — **OpenSlide Lab Pack evaluation**. Evaluate optional bundled support for Aperio SVS, Hamamatsu NDPI, Leica SCN, MIRAX, Philips TIFF, Sakura SVSLIDE, Ventana BIF, Zeiss CZI, DICOM WSI, and generic tiled TIFF. Treat as an optional "Lab Pack" because the UX, file sizes, licensing, and test corpus are different from consumer photos. Effort: L. [[S-OPENSLIDE]](https://openslide.org/) [[S-QUPATH]](https://qupath.github.io/)
- [ ] **V80-22** *P2* — **Bio-Formats bridge spike**. Research a Java sidecar or service boundary for Bio-Formats to preview proprietary microscopy formats and normalize metadata to OME concepts. Must be opt-in and process-isolated: JVM startup cost, GPL/commercial licensing paths, and untrusted-file attack surface make in-process loading inappropriate for the main viewer. Effort: L. [[S-BIOFORMATS]](https://www.openmicroscopy.org/bio-formats/)
- [ ] **V80-23** *P2* — **Multidimensional image navigator**. Add channel toggles, Z-stack slider, time slider, intensity range, and overlay/annotation layers for formats that expose multiple dimensions. napari is the interaction reference; Images should implement the smallest Windows-native subset that makes scientific stacks understandable. Effort: XL. [[S-NAPARI]](https://napari.org/stable/) [[S-BIOFORMATS]](https://www.openmicroscopy.org/bio-formats/)
- [ ] **V80-24** *P1* — **Streaming batch backend spike**. Evaluate NetVips/libvips for resize, thumbnail, pyramid generation, and batch export where demand-driven/horizontally threaded processing can beat full-frame Magick.NET pipelines. Do not replace Magick.NET by default; test on huge TIFF/PSD/EXR and 10k-file batch workloads first. Effort: M. [[S-LIBVIPS]](https://www.libvips.org/)
- [ ] **V80-25** *P2* — **OpenImageIO toolchain evaluation**. Evaluate OIIO as an optional pro/VFX backend for EXR, DPX, Cineon, PSD, OpenVDB/Ptex metadata, `idiff`-style image comparison, `iinfo` metadata, tiled MIP generation, and format-plugin architecture. This is likely a sidecar/tool boundary, not a direct WPF dependency. Effort: L. [[S-OIIO]](https://openimageio.readthedocs.io/en/latest/)
- [ ] **V80-26** *P1* — **OCIO/ACES color-management roadmap**. Add an explicit color pipeline plan for ICC display transform, OpenColorIO config selection, ACES 2.0 display/view transforms, proofing warnings, and "unmanaged vs managed" status badges. This should land before serious RAW/VFX workflows. Effort: L. [[S-OCIO]](https://opencolorio.org/)

### Platform and extensibility lessons

- [ ] **X-01** *P1* — **Plugin boundary design doc**. Study napari, OpenImageIO, Eagle, and Hydrus before implementing plugins. The first version should define stable extension points (reader, metadata extractor, export action, batch action, library panel) and a trust model (signed/local-only/disabled-by-default), not a marketplace. Effort: M. [[S-NAPARI]](https://napari.org/stable/) [[S-OIIO]](https://openimageio.readthedocs.io/en/latest/) [[S-EAGLE]](https://www.eagle.cool/) [[S-HYDRUS]](https://hydrusnetwork.github.io/hydrus/)
- [ ] **X-02** *P0* — **Capability matrix UI**. Turn the About codec report into a full capability matrix: open, animate, multi-page, metadata, export, color-managed, sandboxed, bundled runtime, optional runtime, and known limitations per format family. This makes broad codec support trustworthy instead of merely long. Effort: M. [[S-PICVIEW-ORG]](https://picview.org/) [[S-BIOFORMATS]](https://www.openmicroscopy.org/bio-formats/) [[S-OIIO]](https://openimageio.readthedocs.io/en/latest/)
- [ ] **X-03** *P1* — **No-code-copied integration policy**. Before adding any optional runtime, document license, redistribution permission, update cadence, CVE tracking, binary provenance, and process-isolation choice. Required for Ghostscript, 7-Zip/UnRAR, OpenSlide, Bio-Formats, OCR engines, AI models, and any plugin host. Effort: S. [[S-CZKAWKA]](https://github.com/qarmin/czkawka) [[S-OPENSLIDE]](https://openslide.org/) [[S-BIOFORMATS]](https://www.openmicroscopy.org/bio-formats/)

### Research conclusions from this pass

- **Highest leverage near-term**: archive/book mode, gallery workbench, universal paste/open, duplicate cleanup, bad-extension scanning, and a real batch processor. These expand user value without making Images feel like a bloated editor.
- **Big moat**: local-first library intelligence plus transparent network behavior. Eagle, PhotoPrism, and Hydrus prove users value search/tagging/import workflows; Images can own the privacy-preserving Windows-native version.
- **Most technically differentiating**: deep-zoom tiles and optional scientific/whole-slide support. Very few consumer Windows viewers handle gigantic tiled images, microscopy formats, and normal photo folders in one coherent UX.
- **Most dangerous scope creep**: full editor parity. PhotoDemon is the right benchmark, but Images should build macro/batch, non-destructive history, and preview contracts before layers/content-aware tools.
- **Hard requirement before bundling more runtimes**: every optional decoder/tool needs provenance, license review, CVE monitoring, process isolation or sandboxing decision, and a visible capability matrix.

---

## v0.1.2 — polish + branding pass

- [x] **V02-01** *P0* — Bump GitHub Actions: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. Closes Node 20 deprecation (2026-06-02).
- [x] **V02-02** *P1* — 5-prompt logo brief at `assets/logo-prompt.md`; user generated `logo.png` + `banner.png`.
- [x] **V02-03** *P1* — `<ApplicationIcon>` wired. 7-frame multi-res `icon.ico` + PNG-embedded `icon.svg` + bundled `logo.png` resource.
- [ ] **V02-04** *P2* — Recapture main screenshot (DPI-aware, `PrintWindow(hwnd, hdc, 2)` per `screenshots.md`). Requires Windows GUI session.
- [ ] **V02-05** *P2* — Human runtime smoke: prev/next/wrap, rename+undo, drag-drop, Del-to-recycle, external add/delete 250 ms roundtrip.
- [x] **V02-06** *P2* — Serilog + `%LOCALAPPDATA%\Images\Logs\` rolling file behind `ILogger<T>`. Replaces the ad-hoc `crash.log` file. [O-01] *(shipped v0.1.6 — `Services/Log.cs` bridges Serilog 4.2 → Microsoft.Extensions.Logging 9.0 so call-sites use the abstract `ILogger<T>`; rolling file `images-yyyyMMdd.log`, 14-day retention.)*
- [x] **V02-07** *P2* — "Copy crash details + open GitHub Issue" dialog on unhandled exception. No network call. [O-04] *(shipped v0.1.6 — `CrashLog.TryWriteMiniDump` P/Invokes `dbghelp!MiniDumpWriteDump`; new `CrashDialog.xaml` replaces the MessageBox with Copy-details / Open-log-folder / Open-GitHub-issue (pre-filled URL) / Close buttons.)*

Promote `Unreleased` → `v0.1.2 — <date>` once V02-04 and V02-05 are complete.

---

## v0.1.5 — Input + discovery polish (factory iter-1 harvest, S, 1 day)

Replenishes ROADMAP from Phase 3 scoring at `docs/research/iter-1-scored.md`. All additive, none touch the decoder or persistence layers. Composite scores in brackets.

- [x] **V15-01** *P0* — **Mouse XButton1 / XButton2 bind to Prev / Next**. *(shipped v0.1.5 — `Window_PreviewMouseDown` in MainWindow.xaml.cs dispatches XButton1→Prev / XButton2→Next; TextBox focus short-circuits so in-progress renames aren't disturbed.)*
- [x] **V15-02** *P0* — **Right-click context menu on viewport**. *(shipped v0.1.5 — 11-item ContextMenu bound on the viewport Grid; new `SetAsWallpaperCommand` → `WallpaperService` copies to `%LOCALAPPDATA%\Images\wallpaper\current.<ext>` before `SystemParametersInfo(SPI_SETDESKWALLPAPER)` so renames don't break the desktop. Themed MenuItem / Separator / ContextMenu styles added to DarkTheme.xaml.)*
- [x] **V15-03** *P0* — **Keyboard cheatsheet overlay** (`?` key). *(shipped v0.1.5 — `ShowCheatsheet` VM flag, full-width translucent overlay in MainWindow.xaml groups Navigate / View / File shortcuts; any key dismisses and swallows the key so the shortcut doesn't double-fire.)*
- [x] **V15-04** *P0* — **Ctrl+Shift+R reload current image**. *(shipped v0.1.5 — `ReloadCommand` re-runs `LoadCurrent` on the same path; rotation + flip state preserved across reload.)*
- [x] **V15-05** *P0* — **Shift + scroll-wheel pans horizontally**. *(shipped v0.1.5 — `ZoomPanImage.OnWheel` branches on `ModifierKeys.Shift`, translating X by ±80 px per notch.)*
- [x] **V15-06** *P1* — **About dialog**. *(shipped v0.1.5 — new `AboutWindow.xaml` + `AboutWindow.xaml.cs` + `AppInfo` service; binds version / ProductVersion-with-SHA / .NET runtime / OS description / decoder list; GitHub + Crash-log-folder buttons; dark native caption applied.)*
- [x] **V15-07** *P1* — **F11 toggles fullscreen**. *(shipped v0.1.5 — `MainWindow.ToggleFullscreen` saves WindowState/Style, flips to None + Maximized, collapses the side panel via `IsFullscreen` VM flag; Escape also exits fullscreen.)*
- [x] **V15-08** *P1* — **Flip horizontal / vertical**. *(shipped v0.1.5 — `FlipHorizontal` / `FlipVertical` DPs on ZoomPanImage; flip ScaleTransform sits before rotate in the TransformGroup so flip H flips in image frame, not post-rotation frame.)*
- [x] **V15-09** *P1* — **Unhandled-exception → text crash log** at `%LOCALAPPDATA%\Images\crash.log`. *(shipped v0.1.5 — new `CrashLog` service captures AppDomain + Dispatcher + TaskScheduler exceptions with version + runtime + OS + full inner-exception chain; thread-safe Append method reusable for non-fatal diagnostics.)*
- [x] **V15-10** *P2* — **Print current image**. *(shipped v0.1.6 — `Services/PrintService.Print` on a `FixedDocument` single page; 0.5in margins, fit-to-page with 1:1 ceiling. Ctrl+P + menu entry.)*

Deferred from Phase 3 NOW tier to iter-2: **NOW-05 window-state + recent-folders JSON persistence** — pushed into V20-02 settings work so the persistence layer lands in one place.

---

## Cross-cutting tracks

These span multiple versions. Items are referenced by tag (`A-01`, `S-03`, etc.) from the relevant phase below.

### Security & hardening

The current codebase inherits the full WIC + Magick.NET + (eventually) libheif/libavif/libwebp attack surface. Several 2024-2026 CVEs are load-bearing — including BLASTPASS-class exploitation of libwebp in the wild — so this track runs in parallel with every phase, not as a phase of its own.

- [ ] **S-01** *P0* — **SharpCompress post-extract canonicalization**. Every entry path goes through `Path.GetFullPath` + `StartsWith(destRoot, StringComparison.Ordinal)` before write; reject symlinks/hardlinks; enforce per-entry uncompressed-size cap. Lands with V20-17 (archive browsing). Effort: S. [JFrog zip-slip catalogue; ConnectWise ScreenConnect CVE-2024-1708]
- [x] **S-02** *P0* — **Argv-open hardening**. `Path.GetFullPath` + reject `..` pre-resolve + allowlist-root check; `Process.Start` for Reveal-in-Explorer uses `UseShellExecute=false` + `ArgumentList`. Lands in v0.1.2. Effort: S. [OWASP .NET Cheat Sheet] *(shipped 2026-04-24 — commit c812551)*
- [x] **S-03** *P0* — **Pin Magick.NET ≥ 14.9.1** (baseline — we ship 14.13.0) and wire Dependabot/Renovate so a lagging pin surfaces next release. ImageGlass 9.4's note flags CVE-2025-53015 / 55004 / 55154 / 55298 / 62594 upstream. Effort: S. [ImageGlass 9.4 announce] *(shipped 2026-04-24 — commit b1c96db; Magick.NET bumped 14.12.0 → 14.13.0)*
- [ ] **S-04** *P0* — **CVE-delta CI gate** on release: query GHSA / NVD for every shipped native dep (WIC gated on OS version, Magick.NET, SharpCompress, later libheif/libavif/libwebp/libjxl) and fail the release if an unacknowledged advisory is open. Effort: M.
- [ ] **S-05** *P0* — **ExifTool safe-invocation wrapper**. `ProcessStartInfo.UseShellExecute=false` + `ArgumentList` + `-@ argfile.txt` UTF-8 argfile for paths/metadata; fuzz the filename channel with `\r\n`, `<`, `>`, `|`. Lands with v0.4 sidecar writes (V40-04). Effort: S. [exiftool docs]
- [ ] **S-06** *P1* — **WIC JPEG re-encode gate**. On pre-patch `windowscodecs.dll` (< 10.0.26100.4946), thumbnails skip the 12-bit / 16-bit re-encode path that triggers CVE-2025-50165. Toast "Windows update recommended" once. Effort: M. [ESET CVE-2025-50165]
- [ ] **S-07** *P1* — **MSIX + Win32 App Isolation** side-artifact. AppContainer, declare `picturesLibrary` + `broadFileSystemAccess` brokered. Unpackaged zip stays the primary artifact for file-association UX. Effort: L. [MS Learn AppContainer; Win32 App Isolation report]
- [ ] **S-08** *P2* — **Wasmtime-hosted libheif / libavif / libwebp spike** — opt-in "Paranoid Mode" routes untrusted-source decode through a capability-only Wasm sandbox (~1.3× CPU cost). Research spike; prototype only until a proven libheif-in-wasmtime crate exists. Effort: L. [Wasmtime security model; Hyperlight Wasm; Gobi USENIX]
- [ ] **S-09** *P1* — **On bundled decoders**: if we ever vendor libheif / libavif / libwebp / libjxl / libde265 directly (today they come via WIC + Store extensions), minima are:
  - libheif ≥ **1.21.0** [[S-LIBHEIF]](https://github.com/advisories/GHSA-j87x-4gmq-cqfq) — CVE-2025-68431 heap-buffer-overread in `HeifPixelImage::overlay()` `iovl` path, fixed 1.21.0
  - libavif ≥ **1.3.0** [[S-LIBAVIF]](https://github.com/advisories/GHSA-f6x7-5x3c-j3rg) — CVE-2025-48174 integer overflow in `makeRoom` in stream.c, fixed 1.3.0
  - libwebp ≥ **1.3.2** [[S-LIBWEBP]](https://github.com/advisories/GHSA-j7hp-h8jx-5ppr) — CVE-2023-4863 BLASTPASS OOB write in `BuildHuffmanTable`, fixed 1.3.2
  - libde265 ≥ **1.0.17** [[S-LIBDE265-64]](https://github.com/advisories/GHSA-wqrf-6rf5-v78r) [[S-LIBDE265-65]](https://github.com/advisories/GHSA-653q-9f73-8hvg) — CVE-2026-33164 + CVE-2026-33165 HEVC-SPS-triggered OOB heap writes, fixed 1.0.17 (HEVC decoder shipped under libheif)
  - libjxl current
  Effort: conditional.
- [ ] **S-10** *P2* — **libwebp-in-WIC isolation** — prefer Microsoft's shipped WebP path (OS-patched) over bundling `libwebp.dll`; if forced to bundle for non-Windows or MSIX sandbox, keep version current and consider the same wasm-sandbox route in S-08. Effort: conditional. [[S-LIBWEBP-ORCA]](https://orca.security/resources/blog/understanding-libwebp-vulnerability/)
- [ ] **S-11** *P1* — **Transitive-dep pinning**: when a S-09 floor implies a deeper library (libde265 under libheif, aom under libheif-AV1, LittleCMS under most), verify the pin transitively. The release-workflow CVE gate [S-04] must walk the transitive tree. Effort: S. [[S-LIBDE265-64]](https://github.com/advisories/GHSA-wqrf-6rf5-v78r)

### Privacy

No competitor in the OSS viewer space makes network egress auditable. That's the specific moat.

- [ ] **P-01** *P0* — **One-click "Strip location"** in the toolbar + right-click menu. Strips `GPSInfo`, `XMP-exif:GPS*`, IPTC location; preserves camera/date/copyright. Diff toast ("removed: GPS, IPTC-LocationCreated"). Effort: S. [ExifRemover pattern]
- [ ] **P-02** *P0* — **Default-off opt-in telemetry**. First-run banner, toggle in Settings, local JSON preview of what would be sent before enabling. No IP, no MAC, no hostname. VS Code is the reference pattern. Effort: S. [VS Code telemetry docs; TelemetryDeck privacy FAQ]
- [ ] **P-03** *P1* — **Network-egress log panel**. Every call (update check, C2PA fetch, extension-install deep-link, crash-report upload if enabled) logs `{url, purpose, bytes, ms}` to a visible pane. No competitor ships this. Effort: M.
- [x] **P-04** *P0* — **Update check is pull-only** to GitHub Releases API, no PII, opt-out switch. Store `last_checked` locally, no server-side record. Effort: S. *(shipped v0.1.6 — `UpdateCheckService` does a read-only GET against `/releases/latest`, 24-h throttle for silent startup check, manual "Check for updates" button in About dialog bypasses throttle. Every call logged with URL + bytes + duration. Last-checked persisted to `%LOCALAPPDATA%\Images\update-check.json`.)*
- [ ] **P-05** *P0* — **C2PA read/verify** via `c2patool` CLI shellout (no .NET SDK exists as of April 2026). Toolbar badge: green (signed + verified + Trust List), amber (signed but cert unlisted), red (invalid/tampered). Effort: M. *Promoted P1 → P0 on the 2026-04-25 research pass: **C2PA v2.3** shipped Feb 2026; **EU AI Act Article 50** makes machine-readable AI-content marking mandatory **2026-08-02** [[S-C2PA]](https://contentcredentials.org/) [[S-C2PA-2026]](https://aiphotocheck.com/blog/c2pa-specification-latest-version-2026). Real-world signed files now ubiquitous via Pixel 10 (Titan M2 hw signing by default), Leica M11-P, Sony α9 III / α1 II (cloud opt-in), Samsung S25 (AI-edited only). Nikon Z6 III suspended C2PA after Sept 2025 cert-revocation incident — illustrates trust-list consequence.*
- [ ] **P-06** *P2* — **C2PA P/Invoke spike** — bind directly to `c2pa-rs` C API for in-process verify instead of shelling out to `c2patool`. Eliminates ~30 ms per-file process spawn. Effort: L. [c2pa-rs README]
- [ ] **P-07** *P2* — **C2PA write-on-export** — stamp "edited with Images v0.x" + operation list on every export from v0.3/v0.5. Requires signing identity (Azure Trusted Signing works). Defers until P-05 is stable. Effort: M.

### Accessibility (UIA / high-contrast / keyboard / Magnifier)

No OSS Windows viewer publishes a documented UIA tree; that's a free differentiator.

- [x] **A-01** *P0* — **Custom `ImageAutomationPeer`** on the main canvas. *(shipped v0.1.7 — `ImageCanvasAutomationPeer` overrides `GetNameCore` → "Image, W by H pixels", `GetHelpTextCore` explains arrow/wheel/drag/double-click semantics, `AutomationControlType.Image`. `ZoomPanImage.OnCreateAutomationPeer` returns it. No OSS Windows viewer publishes this UIA tree.)*
- [ ] **A-02** *P0* — **High-contrast theme dictionary** keyed to `SystemColors.*BrushKey` / `SystemColors.ControlTextBrushKey` + `SystemEvents.UserPreferenceChanged` listener that swaps Catppuccin → HighContrast at runtime. Catppuccin hex fails WCAG 1.4.3 on white system backgrounds — don't guess, degrade properly. Lands with v0.2 settings UI. Effort: M. [MS Learn high-contrast-themes]
- [x] **A-03** *P0* — **Keyboard focus + escape discipline**. Restore `FocusVisualStyle` on every templated control (our styles currently suppress the default ring — common regression), `KeyboardNavigation.DirectionalNavigation="Cycle"` on filmstrip, `Escape` bound to close every modal. Effort: S. [MS Learn keyboard-accessibility] *(shipped 2026-04-24 — commit 3fdae11; shared `FocusVisual` style in DarkTheme, DirectionalNavigation=Cycle on RecentRenames, Escape clears drop overlay)*
- [ ] **A-04** *P1* — **Magnifier integration** via UIA `TextSelectionChanged` on the rename caret so the OS Magnifier follows the edit point. No hosting of `magnification.dll` — just raise the right UIA event. Effort: S. [Win32 magapi docs]
- [ ] **A-05** *P1* — **Publish the UIA tree** in the README (`docs/accessibility.md`: "what Narrator will say on image load, rename, rating change"). No competitor does this. Effort: S.
- [ ] **A-06** *P2* — **Narrator + NVDA + JAWS manual test matrix** before each release: image load, rename, rating change, Del-to-recycle. Documented test script, not automation. Effort: S.

### i18n / l10n

XnView MP ships ~45 languages via plain `.lng` community text. ImageGlass crowd-sources `.iglang` XML on GitHub without a platform. That's the bar; Crowdin/Weblate beats it for contributor ergonomics.

- [ ] **I-01** *P0* — **Extract all user-visible strings** to `Strings.resx` (en default). CI check fails if any non-en locale is missing a key. Bind XAML via `{x:Static strings:Strings.MenuOpen}` or a `LocExtension`. Effort: M. [MS Learn WPF localization-overview]
- [ ] **I-02** *P1* — **Crowdin for OSS** (free tier under 60k words) over GitHub. Ship en + de + fr + es + ja + pt-BR + zh-Hans as v1 locale set. Effort: M. [Crowdin OSS programme]
- [ ] **I-03** *P2* — **RTL audit pass**. `FlowDirection="RightToLeft"` at window root mirrors layout, but `Canvas`, `Image`, custom `DrawingVisual`, negative-`X` `ScaleTransform`, and `DataGrid` column order need manual mirroring. Arabic + Hebrew test fixtures. Effort: L. [MS Learn localization-overview]
- [ ] **I-04** *P0* — **`DateTime` → `DateTimeOffset` everywhere** metadata is displayed. EXIF `DateTimeOriginal` is local-time-string-no-TZ; EXIF `OffsetTimeOriginal` (2016+) carries the offset. Never assume UTC. MetadataExtractor.NET reads both. Effort: S. [ExifTool XMP tag names; drewnoakes/metadata-extractor-dotnet]
- [ ] **I-05** *P2* — **Locale switcher** at runtime (no app restart). Swap `ResourceDictionary` on `LanguageChanged`. Effort: S.

### Observability (logging / crash reports / counters)

- [ ] **O-01** *P0* — **Serilog behind `ILogger<T>`** with rolling file at `%LOCALAPPDATA%\Images\Logs\`. Lands in v0.1.2 as V02-06. [Serilog.net]
- [ ] **O-02** *P2* — **Opt-in Sentry** (free tier 5k events/month) wired via `Sentry.Serilog` sink, gated on the default-off privacy toggle (P-02). Effort: S. [Sentry WPF guide]
- [ ] **O-03** *P1* — **Custom `EventSource`** around `BitmapDecoder.Create`, Magick.NET boundary, and thumbnail writes so `dotnet-counters` sees the decode pipeline live. Ship a `docs/perf.md` with the recipe. Effort: M. [MS Learn dotnet-counters]
- [ ] **O-04** *P1* — **Local minidump + "Open GitHub Issue" button** on fatal — `MiniDumpWriteDump`, copy to clipboard, do not upload. Paint.NET's pattern. Lands in v0.1.2 as V02-07. [Paint.NET CrashLogs doc]
- [ ] **O-05** — **OpenTelemetry parked post-v1**. No OSS desktop viewer runs OTel in anger as of April 2026; revisit when there's demand. [OTel .NET docs]

### Testing strategy

User preference is "no tests unless explicitly requested" — but domain-logic tests for rename conflict, sort order, and catalog migration are load-bearing and cheap. Carry this track lightly.

- [ ] **T-01** *P1* — **`Images.Domain` class library** — extract sort/filter, rename-conflict resolution, EXIF/XMP date parsing, thumbnail-cache eviction into a pure library with xUnit coverage. Effort: M.
- [ ] **T-02** *P2* — **FlaUI smoke suite** — launch, open fixture folder, assert filmstrip count + title bar text. Runs as a gated CI job on windows-latest. Effort: M. [FlaUI repo + docs]
- [ ] **T-03** *P2* — **Golden-image render tests** under `tests/render/`, DPI-pinned, per-pixel RGBA compare with tolerance via ImageSharp. Catches canvas-engine regressions when SkiaSharp lands in V20-01. Effort: M. [ImageSharp repo]
- [ ] **T-04** *P1* — **Ship `images.v1.db` snapshot** in `tests/fixtures/` now, so every future catalog-schema bump gets a forward-migration regression test. Pattern borrowed from darktable / digiKam. Effort: S. [digiKam docs]
- [ ] **T-05** — **Avoid WinAppDriver** (Microsoft's repo effectively frozen since 2022). Use FlaUI or `appium-windows-driver`. [microsoft/WinAppDriver; appium/appium-windows-driver]

### Distribution channels

Primary = GitHub Releases (source of truth). Secondary = winget + Scoop extras. Tertiary = Microsoft Store via MSIX. Skip Chocolatey unless demand materializes.

- [~] **D-01** *P0* — **Framework-dependent single-file win-x64** (~5 MB zip) as the primary artifact; self-contained non-trimmed (~70 MB zip) as the "no .NET runtime" fallback. Avoid trimming WPF until upstream warnings are resolved (tracked in dotnet/wpf#3070). Effort: S. [MS Learn single-file; dotnet/wpf#3070] *(partially shipped v0.1.4: portable framework-dependent `Images-vX.Y.Z-win-x64.zip` + a new Inno Setup installer `Images-vX.Y.Z-setup-win-x64.exe` ride alongside it on every release. Self-contained non-trimmed fallback still deferred.)*
- [x] **D-01b** *P0* — **Inno Setup installer** at `installer/Images.iss`. Admin-default per-machine install with per-user override via UAC, stable AppId for clean upgrades, .NET 9 Desktop Runtime prerequisite check, optional non-destructive "Open with" registration for 16 image extensions (ProgID + `Applications\Images.exe` + `OpenWithProgids` + `RegisteredApplications\Capabilities\FileAssociations` so Images surfaces in Settings → Default Apps without hijacking any existing default). `.github/workflows/release.yml` builds it with the pre-installed Inno Setup on `windows-latest` and uploads it next to the portable zip. *(shipped v0.1.4)*
- [ ] **D-02** *P0* — **`winget` publishing** via `WinGet Releaser` GitHub Action (`vedantmgoyal9/winget-releaser`). First submission manual via `wingetcreate new`; subsequent releases auto-fire on `release: [published]`. Requires classic PAT + forked `microsoft/winget-pkgs`. Effort: S. [WinGet Releaser action; Grafana k6 PR #5203]
- [ ] **D-03** *P1* — **Scoop `extras` bucket manifest** with `autoupdate` section pointed at the GitHub release URL template. Effort: S. [ScoopInstaller/Extras]
- [ ] **D-04** *P1* — **Microsoft Store via MSIX** for discovery, paired with S-07 AppContainer work. GitHub Releases stays primary. Effort: M. [MS Learn MSIX overview]
- [ ] **D-05** *P0* — **Azure Artifact Signing** (rebrand of Azure Trusted Signing, now GA April 2026) via `azure/artifact-signing-action` in the release workflow. SmartScreen reputation warm-up still applies (since 2023 even EV is throttled for new publishers) — so no reason to pay for EV. Self-employed individuals now eligible (no 3-yr history requirement); restricted to US/CA/EU/UK businesses/individuals. Effort: M. [[S-ARTIFACT-SIGNING]](https://azure.microsoft.com/en-us/products/artifact-signing) [[S-SMARTSCREEN-REGRESSION]](https://learn.microsoft.com/en-us/answers/questions/5855708/trusted-signing-regression-in-smartscreen-reputati) *Risk flagged 2026-03/04: Microsoft silently rotated issuing CAs (EOC CA 02 → AOC CA 03 → EOC CA 04) which broke SmartScreen reputation for existing customers. Expect the first ~500 installs to trip "Unrecognized app" even with a valid cert. Hanselman has the working GitHub-Actions setup [[S-HANSELMAN-SIGN]](https://www.hanselman.com/blog/automatically-signing-a-windows-exe-with-azure-trusted-signing-dotnet-sign-and-github-actions).*
- [ ] **D-05a** *P1* — **SignPath.io OSS code-signing evaluation** (new, 2026-04-25 research). Free certificate via SignPath Foundation for OSS projects (used by PicView). Pre-requisite: GitHub Actions integration + SignPath-approved project status. Evaluate in parallel with D-05 — whichever lands first wins; both are fine to keep running simultaneously (dual-signing is supported by Authenticode). Effort: S (application) + M (pipeline). [[S-PV]](https://github.com/Ruben2776/PicView)
- [ ] **D-06** — **Chocolatey parked** until v1.x. Community-feed moderation runs days-to-weeks; low ROI for an OSS viewer with winget + Scoop already covered.
- [ ] **D-07** *P2* — **Trim-warning audit spike** — enable `<PublishTrimmed>true</PublishTrimmed>` once, capture every IL2xxx warning, decide whether WPF is trimmable enough in .NET 9 to justify the ~50-70 MB saving. If net-negative, park. Effort: M. [MS Learn trim self-contained]

### Catalog schema & migration strategy

Set the philosophy before writing the first migration. Getting this wrong means the v5 user can't open their v1 library.

- [ ] **SCH-01** *P0* — **XMP sidecars are authoritative**; SQLite catalog is a cache. "Delete `catalog.db`, we rebuild from `.xmp` + file scan" must always be a valid recovery step. darktable's philosophy; digiKam explicitly says the same via their "Write metadata to files" export. Effort: L (architecture decision, enforced across v0.4+). [darktable sidecar manual]
- [ ] **SCH-02** *P0* — **EF Core migrations with guardrails**. Pre-bump: `PRAGMA integrity_check`, `PRAGMA wal_checkpoint(TRUNCATE)`, backup DB to `catalog.db.bak.v<old>-<new>`, close all connections. Post-bump: canary-row assertion. On failure: restore backup, surface actionable error. Effort: M. [MS Learn EF Core migrations]
- [ ] **SCH-03** *P0* — **Forward-only migrations**. No downgrade path (no OSS DAM supports one). Document the "delete catalog, rebuild from sidecars" recovery in README. Effort: S. [darktable philosophy]
- [ ] **SCH-04** *P1* — **Hop, don't jump**. A v1→v5 upgrade runs v1→v2→v3→v4→v5 with integrity check after each hop, not a direct-to-target diff. Effort: S.
- [ ] **SCH-05** *P1* — **Snapshot fixtures under version control**. `tests/fixtures/catalog.v1.db`, `catalog.v2.db` etc. — every bump must roll every prior snapshot forward in CI. Effort: S (per version).

### Migration from competing tools

Import once, never re-type tags. This is the friction every DAM user complains about.

- [ ] **M-01** *P1* — **Picasa importer**. `.picasa.ini` per folder + `contacts.xml` global → MWG `mwg-rs:Regions` in `.xmp`. ~200 lines of `.ini` parsing; XMP write via ExifTool. Jeffrey Friedl's Lightroom plugin is the canonical reference for the face-rectangle coordinate mapping. Effort: M. [Jeffrey Friedl Picasa plugin; mvz/picasa-contacts as reference port]
- [ ] **M-02** *P2* — **Lightroom `.lrcat` importer**. SQLite read: `Adobe_images` (ratings, flags), `AgLibraryKeyword` + `AgLibraryKeywordImage` (tags), `AgLibraryCollection` + `AgLibraryCollectionImage` (collections). Drop `Adobe_imageDevelopSettings` — proprietary XML, dead end. Effort: L. [Adobe LrClassic FAQ; StackOverflow schema thread]
- [ ] **M-03** *P1* — **digiKam importer** reads XMP sidecars produced by digiKam's "Write metadata to files" action. Do not read the DB. Effort: S. [digiKam docs]
- [ ] **M-04** *P1* — **XnView MP importer** — same pattern: tell user to run XnView's built-in "Export to XMP" first; we read XMP. `xnview.db` schema is undocumented and changed between 0.9x and 1.x. Effort: S. [XnView newsgroup pattern]
- [ ] **M-05** *P2* — **Apple Photos doc-only**. Direct read of `.photoslibrary/database/Photos.sqlite` is a moving target (Core Data schema per macOS release). Document: "run `osxphotos export --sidecar xmp` on macOS, ingest the XMP here." Effort: S (doc only). [osxphotos project convention]
- [ ] **M-06** — **IrfanView `.thumbs.db` skip**. Thumbnail cache, not a tag store. No migration value.

---

## v0.2.0 — Foundations + Viewer polish (M, 2-3 weeks)

**Theme**: replace the canvas engine, add persistence, match IrfanView / ImageGlass / JPEGView viewer baseline. Cross-cutting: O-01, A-01/02/03, I-01, I-04, T-04 land here.

### Engine / infra
- [ ] **V20-01** *P0* — **SkiaSharp canvas** replacing `WriteableBitmap` in `ZoomPanImage`. `SKCodec` decodes to target size (1000×800 buffer for 800×600 view of 4000×3600 source). ~2× load, ~4× thumbnail gen vs ImageSharp. MIT, no strings. Unlocks HDR path and every AI overlay later. [stack: `SkiaSharp`]
- [x] **V20-02** *P0* — **Persistent settings** via SQLite at `%LOCALAPPDATA%\Images\settings.db`. *(shipped v0.1.7 — `SettingsService` on `Microsoft.Data.Sqlite` 9.0. Schema v1 seeds `settings` (key/value) + `recent_folders` (path, last_opened) + `hotkeys` tables. Hop-only migrations via `PRAGMA user_version`. Auto-quarantine + reset on corruption per SCH-01. Consumers shipped this iter: window geometry restore + maximized state, update-check opt-out, recent-folders MRU. Full settings UI [V20-07] still open for next iter. **2026-04-25 update**: side-panel **Recent folders** menu shipped — clickable folder cards between Recent renames and Details, click loads the first supported image via `DirectoryNavigator.SupportedExtensions`. Empty / unreachable folders surface a toast. Section hides on empty MRU. Commit `1bda3ea`.)*
- [x] **V20-03** *P0* — **Preload next + previous** image. *(shipped v0.1.7 — `PreloadService` with 3-slot ring, cancellation-friendly, LRU eviction, skips files > 40 megapixels to avoid managed-heap blow-up on RAW panoramas. `MainViewModel.LoadCurrent` prefers cache hit; `EnqueueNeighbours` runs after every load with wrap-around matching nav semantics.)*
- [x] **V20-04** *P1* — **Persistent thumbnail cache** at `%LOCALAPPDATA%\Images\thumbs\<hash>.webp`. *(disk layer shipped v0.1.7 — `ThumbnailCache` keys by SHA1(path.lower()+mtime_ticks+size_bytes), git-like 2-char partition dirs, 256-px longest-edge WebP (quality 80, EXIF stripped), 512 MB cap with LRU eviction. UI consumer — V20-21 filmstrip — lands later. Per SCH-01 the cache is disposable: `rm -rf %LOCALAPPDATA%\Images\thumbs` is always a safe recovery.)*
- [ ] **V20-05** *P1* — SIMD-accelerated decode path via SkiaSharp (AVX2/SSE2 automatic).
- [x] **V20-06** *P1* — Memory-mapped I/O for files >256 MB (avoids blowing the managed heap on 500 MP RAW). `MemoryMappedFile.CreateFromFile`. *(shipped v0.1.3 — `ImageLoader.LoadFromMemoryMapped` opens a read-only mapping and feeds separate `CreateViewStream` instances to the WIC primary + Magick.NET fallback; `DecoderUsed` reports "WIC (memory-mapped)" / "Magick.NET (memory-mapped)")*
- [ ] **V20-07** *P2* — Settings UI surface — expose the V20-02 keys: theme (dark/light/high-contrast [A-02]), locale [I-01], telemetry [P-02], update check [P-04], hotkeys.

### Format expansion
- [ ] **V20-10** *P0* — HEIC / HEIF via WIC (Windows "HEIF Image Extensions" Store package — do not bundle HEVC, Nokia enforces licensing [F-02]) with libheif fallback bundled for offline boxes (S-09 sets version floor).
- [ ] **V20-11** *P0* — AVIF via `AV1 Video Extension` + libavif fallback (S-09).
- [ ] **V20-12** *P1* — **JPEG XL via Microsoft's WIC JPEG XL Image Extension** (Store deep-link pattern [F-05]). Don't bundle libjxl directly until Microsoft ships it OS-default — adoption is still flag-gated in Chrome 145 as of Feb 2026 and Nightly-only in Firefox.
- [ ] **V20-13** *P1* — WebP + animated WebP. WIC path preferred; libwebp floor in S-09.
- [ ] **V20-14** *P1* — RAW decode via `Sdcb.LibRaw` (MIT wrapper / LGPL native) — Canon CR2/CR3, Nikon NEF, Sony ARW, Fuji RAF, DNG. [[S-SDCB-LIBRAW]](https://github.com/sdcb/Sdcb.LibRaw) [[S-LIBRAW-022]](https://www.libraw.org/news/libraw-0-22-0-release) *Upstream LibRaw 0.22.0 shipped 2026-01 with DNG 1.7 + JPEG-XL compression support, CR3 ≥ 4 GB fix, +Canon R5 Mark II / R6 Mark II / R8 / R50 / R100 / Ra / EOS R1 / R5 C, Fujifilm X-T50 / GFX100-II / X-H2 / X-H2S, Sony A9-III / A7 R V / A7CR / A7C II / FX30, plus TALOS-2026-2359/2363/2364 security fixes. Sdcb.LibRaw NuGet still tracks 0.21.x as of 2026-04 — V20-14 ships with whatever Sdcb has; v0.22 arrives when the wrapper bumps (or we shell out to native `dcraw_emu`).*
- [~] **V20-15** *P2* — Animated GIF / APNG / animated AVIF with transport controls (play/pause/frame-step/speed). *(core playback shipped v0.1.3 — `MagickImageCollection.Coalesce` + `AnimationSequence` + WPF `ObjectAnimationUsingKeyFrames` on `Image.SourceProperty` in `ZoomPanImage`; per-frame delays and GIF loop count honored; green "N frames" chip in the bottom toolbar. Transport controls deferred to a follow-up.)*
- [x] **V20-15-Loop** *P2* — **Animation loop-count badge on the "N frames" chip** (new, 2026-04-25 research). When the decoded `AnimationSequence.LoopCount > 0` render the chip as `{N} frames · plays {LoopCount}×`; when zero (GIF convention = infinite) render as `{N} frames · loops`. We already HONOR the loop count — we just don't surface it. Effort: S. [own shipment review] **CLOSED** 2026-04-25 (commits `765d127` + `d9466af` audit counter-pass tightened `IsAnimated` to require `Frames.Count >= 2`).
- [ ] **V20-16** *P2* — Multi-frame TIF / ICO / multi-page PDF / DICOM — per-frame navigation UI (ImageGlass pattern).
- [ ] **V20-17** *P2* — **Images inside archives** — ZIP/7Z/RAR/CBR/CBZ browsing without extraction (Honeyview's moat). `SharpCompress` MIT covers all formats; S-01 canonicalization is load-bearing here.
- [ ] **V20-18** *P2* — **Store-extension detect + prompt** (F-01): on unknown-format open, probe `Windows.ApplicationModel.Store.CurrentApp`-free registry for HEIF / AV1 / WebP / JPEG XL / Raw extensions; if missing, toast with one-click `ms-windows-store://pdp/?productid=...` deep-link. Effort: S.

### Viewer UX
- [~] **V20-20** *P0* — **Six zoom modes** (ImageGlass): Auto, Lock-to-%, Fit-to-Width, Fit-to-Height, Fit (uniform), Fill. *(four of six shipped v0.1.6 — `ZoomPanImage.ZoomMode` enum exposes Fit / OneToOne / FitWidth / FitHeight / Fill; Ctrl+F cycles with toast. Auto + Lock-to-% deferred until V20-02 settings lands so the mode persists across sessions.)*
- [ ] **V20-21** *P0* — **Filmstrip** at bottom (togglable), virtualised, synced to current index. Honor A-03 (focus ring).
- [ ] **V20-22** *P1* — **EXIF overlay** (togglable HUD) — camera/lens/ISO/shutter/aperture/date/GPS. Tap-to-expand for full panel. Date via I-04 (`DateTimeOffset`).
- [ ] **V20-23** *P1* — **GPS coordinates overlay** with click-to-open-in-map (Honeyview). P-01 "Strip location" one click away.
- [ ] **V20-24** *P1* — **Histogram overlay** per-channel + luminance (`0.299R + 0.587G + 0.114B`, log-scale toggle).
- [ ] **V20-25** *P1* — **Color picker** eyedropper — hex + RGB + HSL + LAB readout (PixiEditor.ColorPicker MIT).
- [ ] **V20-26** *P1* — **Hidden edge-triggered fullscreen toolbar** (FastStone pattern) — chromeless by default, reveal on edge approach.
- [ ] **V20-27** *P1* — Dual/multi-monitor — remember per-monitor placement, "send to monitor N" shortcut.
- [ ] **V20-28** *P2* — **Individual color-channel isolation** (ImageGlass R/G/B/A only views).
- [ ] **V20-29** *P2* — **Command palette** (Ctrl+Shift+P) — greenfield; no viewer does this well.
- [ ] **V20-30** *P2* — **File Explorer sort-order sync** (ImageGlass v9.3+) — read Explorer's current sort, match it.
- [ ] **V20-31** *P2* — **Network-listen mode** (`Images.exe -l <port>`) accepting paths on a local socket (borrow from Oculante). Unlocks pipelined workflows — "ImageMagick outputs to this pipe, viewer refreshes live." Egress log panel (P-03) surfaces it. Effort: M. [[S-OCULANTE]](https://github.com/woelper/oculante)
- [x] **V20-32** *P1* — **`--peek <path>` CLI mode** (new, 2026-04-25 research). Same invocation PowerToys Peek uses (`PowerToys.Peek.UI.exe <file>`) — lets Images drop into any workflow that expects an external preview tool (File Explorer context menus, terminal previewers, editor integrations). Accept a single path, open it as a standalone chromeless window, exit on Escape. ~20 LOC on top of the existing argv path. Effort: S. [[S-PEEK]](https://learn.microsoft.com/en-us/windows/powertoys/peek) **CLOSED** 2026-04-25 (commits `a5cbd67` + `d9466af` audit counter-pass enforces exact two-token argv contract).

---

## v0.3.0 — Light editor + lossless (M, 3-4 weeks)

**Theme**: edits-inside-viewer, no modal dialogs. JPEGView's real-time-inline pattern. FastStone's clone/heal/red-eye. Windows-Photos-class AI generative erase. Cross-cutting: S-05 (ExifTool wrapper), P-07 (C2PA write-on-export once P-05 stable).

### Edits
- [ ] **V30-01** *P0* — Crop (draggable rect, aspect-ratio presets, free/square/3:2/4:3/16:9 + custom, rule-of-thirds overlay).
- [ ] **V30-02** *P0* — **Lossless JPEG transforms** — rotate 90/180/270 + crop MCU-aligned via bundled `jpegtran.exe` (libjpeg-turbo BSD). Confirm dialog when MCU forces trim. [stack: shell-out] [libjpeg-turbo #233]
- [ ] **V30-03** *P0* — Resize dialog — Lanczos-3 / Mitchell / Bicubic; percent / px / long-edge / short-edge; aspect lock; preview.
- [ ] **V30-04** *P0* — Levels + curves + hue/saturation/lightness — real-time slider, Enter to apply (no modal, JPEGView pattern).
- [ ] **V30-05** *P1* — **Local exposure compensation** — dodge/burn with soft brush, no modal (JPEGView's unique UX).
- [ ] **V30-06** *P1* — Red-eye removal (FastStone).
- [ ] **V30-07** *P1* — Clone stamp + healing brush (FastStone).
- [ ] **V30-08** *P1* — **Annotations overlay** — arrows (Bezier), text, boxes, circles, **numbered step-callouts** (auto-increment), freehand, **blur/pixelate redact**. ~800 LOC SkiaSharp. Avoids ShareX/Greenshot GPL taint.
- [ ] **V30-09** *P1* — Sharpen, noise reduction, vignette (Magick.NET presets).
- [ ] **V30-10** *P2* — Perspective correction (4-corner handles + keystone).
- [ ] **V30-11** *P2* — **Auto Enhance** 1-click (Windows Photos parity) — curves + WB + sharpen.

### File ops
- [ ] **V30-20** *P0* — Copy to folder / Move to folder with recent-folder jump list (IrfanView pattern).
- [ ] **V30-21** *P0* — Set as wallpaper (span/fill/fit/tile).
- [ ] **V30-22** *P0* — Send to email / default print / copy to clipboard (image AND path).
- [ ] **V30-23** *P1* — **Send-to-app** integration — ImageGlass-style "open in Photoshop / GIMP / Paint.NET" menu, configurable.
- [ ] **V30-24** *P2* — Scan via TWAIN/WIA to image (IrfanView pattern — `Saraff.Twain.NET` NuGet). Breaks under MSIX AppContainer (S-07) — unpackaged build only.

### Comparison + slideshow
- [ ] **V30-30** *P1* — **Image compare** 2-up / 4-up with synchronized pan/zoom (XnView MP, FastStone).
- [ ] **V30-31** *P1* — **Opacity-overlay compare** (nomacs) — slider blend two images for AB review.
- [ ] **V30-32** *P2* — **Multi-instance LAN sync** lite — optional "Compare mode" syncs pan/zoom across two open windows (local machine; network sync is a v1.0 item). [nomacs 3.22 pattern]
- [ ] **V30-33** *P1* — Slideshow — configurable interval, transitions (fade/slide/wipe), background music (MP3/FLAC), loop/shuffle, pause on hover.
- [ ] **V30-34** *P2* — **Standalone .exe slideshow export** (IrfanView — unique) — packs N images + runtime into a self-extracting viewer.

---

## v0.4.0 — Organizer / DAM (L, 6-8 weeks)

**Theme**: catalog, tags, dedup, map, triage. digiKam minus the GPL. Cross-cutting: SCH-01/02/03/04/05, M-01…M-06, S-05 (ExifTool-safe write), P-01 (strip location) surfaces in context menus here.

### Catalog
- [ ] **V40-01** *P0* — **SQLite catalog** at `%LOCALAPPDATA%\Images\catalog.db`. Four-DB split (digiKam pattern): `core.db` (assets/metadata, cache of XMP), `thumbs.db` (blobs), `search.db` (FTS5 + vectors), `similarity.db` (pHash/Haar). XMP sidecars are authoritative (SCH-01); DB is a rebuildable cache.
- [ ] **V40-02** *P0* — **Watched folders** — add/remove library roots, scan-on-start, FSW for deltas. Multi-root w/ offline-prompt behavior (don't delete records on drive eject).
- [ ] **V40-03** *P0* — **Hash-based asset identity** (SHA-256 or xxHash64) — survives move/rename. Path is denormalised cache, not authoritative (Lightroom `id_global` pattern).
- [ ] **V40-04** *P1* — **Sidecar XMP writing** — `<basename>.<ext>.xmp` alongside originals (darktable/digiKam naming), namespace `xmlns:imv="http://maven.imaging/1.0/"`. Round-trip to embedded IPTC/XMP. Via ExifTool (S-05 safe wrapper) + MetadataExtractor for reads.

### Tagging + metadata
- [ ] **V40-10** *P0* — **1-5 star rating + color labels + pick/reject flags** (digiKam's three-axis).
- [ ] **V40-11** *P0* — **Hierarchical keywords** unlimited nesting. Incremental-search autocomplete. Keyboard-shortcut-per-tag ("P"=Portrait) — XnView triage pattern.
- [ ] **V40-12** *P1* — **Metadata templates** — save/apply IPTC copyright/creator blocks across N files atomically (Bridge pattern).
- [ ] **V40-13** *P1* — **Category Sets** — saveable tag-panel layouts, swap per job type (XnView).
- [ ] **V40-14** *P1* — Full IPTC / XMP / EXIF editor pane (dockable, XnView MP style).
- [ ] **V40-15** *P1* — **Multi-token batch rename** — `{name}_{date:yyyy-MM-dd}_{seq:000}_{exif.iso}_{folder}`, live preview, "Preserve Current Filename in XMP" (Bridge).

### Dedup + similarity
- [ ] **V40-20** *P0* — **Perceptual-hash duplicate finder** — pHash DCT + dHash + Haar wavelet (CoenM.ImageHash MIT). Reference-image strategy: "Older or Larger" / "Prefer selected folder" / "Prefer newer" (digiKam 8.1+).
- [ ] **V40-21** *P1* — **Fuzzy-slider distance threshold** with live "N pairs detected" counter (Immich 0.001-0.1).
- [ ] **V40-22** *P1* — **Near-duplicate stacking** — auto-stack by time+location+hash proximity (PhotoPrism, Google Photos Photo Stacks).
- [ ] **V40-23** *P2* — **Sketch-based fuzzy search** — draw rough color blobs, match (digiKam Sketch tab — delightful differentiator).

### Geo
- [ ] **V40-30** *P1* — **EXIF GPS read/write** (ExifTool shell-out S-05 for writes; MetadataExtractor for reads).
- [ ] **V40-31** *P1* — **Interactive map pane** with clustering at zoom (Leaflet via WebView2, OpenStreetMap tiles). Egress logged by P-03.
- [ ] **V40-32** *P2* — **Reverse geocoding** — local offline DB (GeoNames CC-BY) — privacy-first, no API calls.
- [ ] **V40-33** *P2* — **GPX track-log sync** — match photo timestamps to GPS track, backfill EXIF.

### Smart albums
- [ ] **V40-40** *P1* — **Smart Collections** criteria builder — date, rating, label, keyword, camera, lens, ISO, geo bbox, face count (Lightroom).
- [ ] **V40-41** *P2* — **Auto-albums by pattern** — screenshots, receipts, notes, documents (Google Photos OCR+shape).
- [ ] **V40-42** *P2* — **Trip detection** — contiguous days + distance >threshold from home (Apple Memories engine).
- [ ] **V40-43** *P2* — **Events view** — date-based clusters with key-photo thumbnail (Shotwell).

---

## v0.5.0 — Converter / Batch (M, 3-4 weeks)

**Theme**: XnConvert operation-chain UX + Squoosh visual-diff slider + FileOptimizer lossless chain. The batch tab most people will open daily. Cross-cutting: F-03 (cjpegli export), P-07 (C2PA write), S-05 (metadata-write safety).

- [ ] **V50-01** *P0* — **Operation-chain builder** — drag-orderable list, per-op enable/disable, live preview on first selected image (XnConvert tab 2 pattern).
- [ ] **V50-02** *P0* — **Output formats** with per-format quality controls: JPEG (**MozJPEG + cjpegli** [F-03]), PNG (OxiPNG), WebP (cwebp), AVIF (avifenc), JXL (cjxl), HEIC (libheif, no-HEVC-bundle caveat [F-02]), TIFF, BMP, GIF. cjpegli ships with libjxl and delivers ~35% smaller JPEG at equal quality vs MozJPEG. [stack: bundled CLIs + Magick.NET core] [Google OSS blog Jpegli]
- [ ] **V50-03** *P0* — **Resize policies** — %, px, long-edge, short-edge, canvas-fit, canvas-fill, DPI-only.
- [ ] **V50-04** *P0* — **Presets** saveable/nameable/import-export (JSON). Default presets: "Web 1920 / Instagram 1080 / Email 2MB / Print 300 DPI".
- [ ] **V50-05** *P0* — **Overwrite-vs-new-folder guardrails** — refuse overwrite originals without confirm (ImageMagick `mogrify` footgun lesson).
- [ ] **V50-06** *P0* — **Drag-to-target** — drop folder on app, convert with last preset.
- [ ] **V50-07** *P1* — **Watch-folder** auto-apply (XnConvert Watch).
- [ ] **V50-08** *P1* — **Rename tokens** — `{name}_{date:yyyy-MM-dd}_{seq:000}_{exif.iso}` (Bridge engine).
- [ ] **V50-09** *P1* — **Strip metadata** granular — all / keep GPS / keep copyright / keep XMP. P-01 "strip location only" is the single-file version of this.
- [ ] **V50-10** *P1* — **Watermark** — text + image, opacity/position/rotation/tile (XnConvert).
- [ ] **V50-11** *P1* — **CPU-core throttle** slider (XnConvert).

### Compression pipelines (the differentiator)
- [ ] **V50-20** *P0* — **Squoosh-style visual-diff slider** — draggable split-pane preview + live byte-delta + SSIM/Butteraugli readout. Nothing native on Windows does this.
- [ ] **V50-21** *P1* — **Lossless re-pack chain** per format (bundled CLIs):
  - PNG: OxiPNG → ECT → pngquant (opt-in lossy) — keep smallest
  - JPEG: jpegtran-optimize → jpegoptim → **cjpegli** → MozJPEG re-encode (opt-in)
  - GIF: gifsicle `-O3`
  - WebP/AVIF/JXL: max-effort re-encode (`-m 6`, `--speed 0`, `-e 9`)
- [ ] **V50-22** *P1* — **"Best-of" mode** — run N encoders in parallel, pick smallest under target SSIMULACRA2 score (FileOptimizer philosophy).
- [ ] **V50-23** *P2* — **SSIMULACRA2 + Butteraugli** quality metric alongside raw slider (2026 codec-comp community standard).
- [ ] **V50-24** *P2* — Send originals to Recycle Bin on replace (FileOptimizer rollback).
- [ ] **V50-25** *P2* — **C2PA write-on-export** (P-07). Per-op, opt-in; embeds operation manifest + signing identity. Requires D-05 (Trusted Signing) for the cert.

### Exports
- [ ] **V50-30** *P1* — **Contact sheets → PDF**: grid, header/footer, metadata captions (Bridge Output). [stack: PdfSharpCore MIT]
- [ ] **V50-31** *P1* — **Print layout** — multi-image/page with margins + alignment (Lightroom Print module).
- [ ] **V50-32** *P2* — **Web gallery** — static HTML + thumbs + lightbox (digiKam HTMLGallery).
- [ ] **V50-33** *P2* — **Direct publish** — Flickr/Imgur/Pinterest/Dropbox/OneDrive/SMB/FTP (OAuth + known APIs). Every egress call is logged by P-03.
- [ ] **V50-34** *P2* — **Configurable C2PA signing identity** — default to Azure Trusted Signing cert (D-05); allow user-supplied identity.

---

## v0.6.0 — AI features (L, 6-8 weeks — THE DIFFERENTIATOR)

**Theme**: on-device inference, no cloud, no telemetry. Adopt Windows ML on Win11 24H2+ so we don't ship our own ORT (saves ~150 MB); fall back to our own ORT + DirectML on older Windows. Models downloaded lazily to `%LOCALAPPDATA%\Images\models`; user can disable/delete. Every download logged by P-03.

- [ ] **V60-01** *P0* — **Inference runtime — dual-path**. On **Win11 24H2+**: use **Windows ML** (`Microsoft.Windows.AI.MachineLearning.dll`), automatic EP selection (DirectML + CPU + OS-delivered QNN/OpenVINO/VitisAI/TensorRT-for-RTX). On older Windows: ship `Microsoft.ML.OnnxRuntime` + DirectML provider. Auto-detect at startup. UI label: "Running on NPU / GPU / CPU" (W-02). Effort: M. [MS Learn Windows ML EPs; Copilot+ dev guide]
- [ ] **V60-02** *P0* — **CLIP semantic search** (KILLER FEATURE). `ElBruno.LocalEmbeddings.ImageEmbeddings` (Feb 2026, MIT). OpenCLIP ViT-B/32 ONNX ~300 MB. Embed all library images on ingest; store 512-d vectors in **sqlite-vec** table. Text query → encoder → cosine → ranked results. Windows Photos has NO text-to-image search — single biggest moat.
- [ ] **V60-03** *P0* — **Face detection + recognition + clustering**. Pipeline: **YuNet** (detector, MIT) → **ArcFace/SFace** (recognizer, MIT upstream via Clearly.ML.Faces) → **FIQA gating** (digiKam 8.6 — FFT + Gaussian filters blurry training samples) → 512-d L2 embeddings → **HDBSCAN** (HdbscanSharp 3.0.1) → "confirm these suggestions" UX (Picasa orange-dot). Write `MWG-rs:Regions` XMP so tags survive reinstall.
- [ ] **V60-04** *P0* — **Object detection auto-tagging**. YoloDotNet 4.2.0 MIT wrapper. YOLO-World or MIT-weighted older-gen to dodge Ultralytics AGPL weights trap. COCO 80 classes → tag sidebar. Cache per image hash.
- [ ] **V60-05** *P0* — **OCR-in-image indexing**. Default: `Windows.Media.Ocr` (zero deps, 25 langs). Optional: Tesseract 5 (100+ langs incl. Asian) + Sdcb.PaddleOCR (94.5% on OmniDocBench v1.5). Index into SQLite FTS5.
- [ ] **V60-06** *P0* — **Background removal**. Four models, user picks by workload: **BiRefNet** (SOTA 2025, 1024²/2048²) for quality; **IS-Net** general-use middle; **U²-Net** fast; **silueta** 43 MB fallback. ONNX Runtime + ImageSharp pre/post. Edge refinement via guided filter.
- [ ] **V60-07** *P1* — **AI upscaling**. Default **RealESRGAN 4x** (BSD, legal); downloadable options **HAT-L** (photo quality), **SPAN-S** (fast), **RealESRGAN Anime 6B** (anime). Model index from **OpenModelDB** JSON with SHA-256 verify before use; never bundle models in installer. Tile-wise inference (512² + 16 px overlap). [U-01/U-02; OpenModelDB]
- [ ] **V60-08** *P1* — **Generative Erase (LaMa)**. LaMa fp16 ONNX via WinML (auto-EP picks NPU / DirectML / CPU). 512×512 tile + dilated mask. OpenCV 5.0+ ships a native sample (Feb 2025) — simplest reference. Alternative: Carve/LaMa-ONNX. [U-03; Carve/LaMa-ONNX; OpenCV PR #26736]
- [ ] **V60-09** *P2* — **Restyle Image** (Copilot+ PCs only). Use Windows App SDK `ImageGenerator` API with `ImageFromImageGenerationStyle.Restyle` preset styles + custom prompt. Requires NPU; fall back to "not available" banner elsewhere. [U-04; MS Learn ImageGenerator]
- [ ] **V60-10** *P2* — **Auto-rotate** — scene classifier detects upside-down orientation.
- [ ] **V60-11** *P2* — **NIMA aesthetic quality score** — digiKam's Pick-label source. Surface "best of trip" auto-suggestions.
- [ ] **V60-12** *P2* — **Scene classification** — Places365 or ANSA-style multi-task. Feed into smart-album auto-creation.
- [ ] **V60-13** *P2* — **NSFW safety classifier** (opt-in) — open_nsfw2 ONNX, off by default.

---

## v0.7.0 — Plugin + extensibility (M, 3-4 weeks)

**Theme**: power-user extensibility without GPL contamination.

- [ ] **V70-01** *P0* — **Roslyn C# scripting plugin API**. `Microsoft.CodeAnalysis.CSharp.Scripting` + Westwind.Scripting (MIT) wrapper. User writes snippets against `IImageContext` host API. Sandbox: whitelist namespaces, restrict reflection, target framework.
- [ ] **V70-02** *P1* — **G'MIC shell-out** — bundle `gmic.exe` (CeCILL/LGPL, ships as exe = license-isolated). 640+ filters in plugin build, 4000+ CLI commands in 3.6. Stock set covers artistic effects, denoise (BM3D, DCT), sharpen, local contrast. Plugin pane lists filters, user picks + tweaks + applies.
- [ ] **V70-03** *P2* — **Adobe 8BF filter host** — PICA suites + FilterRecord struct implementation. Unlocks Nik Collection, Topaz legacy, every Photoshop filter ever shipped. Tricky (Paint.NET's PSFilterShim shows pattern).
- [ ] **V70-04** *P2* — **Explorer shell extension** — PSD/RAW/JXL/AVIF thumbnails in Explorer (Pictus pattern). Separate DLL, registers as IThumbnailProvider. Breaks under MSIX AppContainer (S-07) — unpackaged build only.

---

## v1.0.0 — Lightroom-class (XL, quarter+)

**Theme**: RAW development, panorama, HDR, color-managed wide gamut, LAN sync. The "real app" bar.

- [ ] **V100-01** *P0* — **Non-destructive edit stack**. JSON-serialised `EditOperation[]` in XMP sidecar. Full version history reconstructible. Apply-on-export pipeline. Virtual copies (fork develop without duplicating pixels — Lightroom pattern). Reinforces SCH-01.
- [ ] **V100-02** *P0* — **RAW development pipeline** beyond LibRaw's "basic conversion". Demosaic (AHD/DCB/DHT/AMaZE) + WB + exposure + shadows/highlights + S-curve + clarity + lens correction (lensfun) + noise reduction (BM3D via G'MIC). Target RawTherapee parity.
- [ ] **V100-03** *P1* — **Panorama stitching** via bundled Hugin CLI chain (`align_image_stack` → `autooptimiser` → `hugin_executor` → `enblend`). All GPL, all shell-out, all license-isolated. UI: select N → preview → stitch.
- [ ] **V100-04** *P1* — **HDR merge** via bundled `enfuse` (Mertens-Kautz-Van Reeth exposure fusion, halo-free, no intermediate HDR file). RAW bracket set → LibRaw → linear float → enfuse → tone-mapped 16-bit output.
- [ ] **V100-05** *P1* — **Color management** — lcms2 (MIT) P/Invoke or Magick.NET profile conversion. Embed source ICC in exports. Wide-gamut display support (Windows 11 ICC compat helper opt-in).
- [ ] **V100-06** *P2* — **HDR display** (PQ/HLG) via SkiaSharp native HDR path or Direct2D interop swap chain. WPF itself doesn't render to HDR.
- [ ] **V100-07** *P2* — **Multi-instance LAN sync** (nomacs moat, full version) — pan/zoom/image-send mirror between instances on same network. Per-client permissions. Builds on V30-32 local lite version. Egress logged by P-03.

---

## Under consideration (no commitment)

- **SUPIR / diffusion upscalers as viewer-sized ONNX**. As of April 2026 no viewer-sized SUPIR-derivative ONNX beats Real-ESRGAN/HAT-L on photos. Revisit when a sub-500 MB weight lands that wins a photo bench. [OpenModelDB FAQ]
- **C2PA durable credentials (watermark + fingerprint + manifest)** for exports — separate from P-05/P-07 manifest signing; lets social-media-stripped C2PA survive re-upload. Standard is still moving; wait for stable spec. [C2PA whitepaper Oct 2025]
- **Hardware NPU routing per-model** — decide per-model whether CPU / DirectML / NPU wins at runtime, not compile-time. WinML auto-EP gets us most of this for free already.
- **Screenreader voice-over demo video** in the README — after A-01 through A-05 ship, a 30s clip of Narrator reading the viewer is a marketing differentiator.
- **V-OCR — OCR text-on-image** via Tesseract or IronOCR (new, 2026-04-25 research). "Select text on the image" is a Phone-Photos / ACDSee / Google Lens feature users increasingly expect. Tesseract .NET SDK is MIT + requires ~40 MB tessdata; IronOCR has better preprocessing but commercial license ($749+). Defer to v0.4+ because: (a) the rectangle-select UI + copy-text surface is non-trivial, (b) tessdata inflates portable-zip by 40%, (c) Windows 11 already ships an OCR via the Snipping Tool for the same workflow. **CHARTER-REVIEW** — a viewer doing OCR drifts toward DAM territory. [[S-TESSERACT]](https://github.com/tesseract-ocr/tesseract)

## Dropped / won't-do (with reasons)

- **Paint.NET file format reuse** — Paint.NET is *source-available* not open-source; `PdnImage` cannot be redistributed. Writing our own `.pdn` reader is possible via pypdn reference, but low ROI. PSD via Aspose.PSD (commercial) or libpsd (MIT, limited) is the pragmatic path.
- **Ultralytics YOLO pretrained weights** — AGPL-3.0 for commercial use; would taint the binary. YOLO-World or older-gen weights + YoloDotNet (MIT) is the safe path.
- **InsightFace buffalo_l pack** — research-only. Use YuNet+ArcFace-original via Clearly.ML.Faces (MIT) or pay for commercial license.
- **Stable Diffusion inpainting** — possible via `stable-diffusion-onnx-ui` but 5+ GB weights, 30s+ per image. LaMa ONNX (V60-08) is the realistic path.
- **iccMAX HDR pipeline** — too bleeding-edge in 2026, tooling thin. Ship sRGB-correct first, iccMAX later.
- **Bundling HEVC decoder** — Nokia enforces via MPEG LA / Access Advance / Velos Media (Acer/Asus halted German sales 2024 over this). Rely on Microsoft's Store-delivered HEIF Image Extension for HEIC decode; user handles licensing. [F-02; Nokia HEIF license; Tom's Hardware Acer/Asus]
- **Apple Photos `.photoslibrary` direct read** — Core Data-backed schema changes per macOS release; osxphotos (Python) keeps up only because it's maintained full-time. Ship the osxphotos-export-on-Mac doc instead. [M-05]
- **WinAppDriver for UI tests** — effectively frozen (Microsoft hasn't shipped since 2022). Use FlaUI or appium-windows-driver. [T-05]
- **Chocolatey community package** (near-term) — days-to-weeks moderation queue, low marginal value over winget + Scoop. [D-06]
- **OpenTelemetry desktop export** (near-term) — no OSS desktop viewer runs OTel in anger as of April 2026; no proof it helps desktop users today. [O-05]
- **Ship our own HEVC / libjxl** on by default — the Windows Store Extension path is license-clean and auto-updates via OS. Detect-and-deep-link (V20-18) is the ergonomic play. [F-01; F-05]

---

## Library manifest (MIT/Apache/BSD-friendly unless noted)

**Canvas + codecs**
- `SkiaSharp` (MIT) — v0.2 canvas engine
- `Magick.NET-Q16-AnyCPU` (Apache-2) — current format coverage, keep. Floor **14.9.1** for CVE-clean.
- `Sdcb.LibRaw` (MIT wrapper / LGPL native) — RAW
- libheif (≥ 1.21.2) / libavif (≥ 1.3.0) / libwebp (≥ 1.3.2) / libjxl — floors per S-09

**Metadata**
- `MetadataExtractor` (Apache-2) — EXIF/IPTC/XMP read
- `ExifTool.exe` (Artistic dual — use Artistic) — write path via shell-out (S-05 wrapper)
- `XmpCore` (BSD) — direct XMP ops

**AI / ML**
- `Microsoft.Windows.AI.MachineLearning` (Windows ML, Win11 24H2+) — primary inference runtime
- `Microsoft.ML.OnnxRuntime` + DirectML provider (MIT) — older-Windows fallback
- `ElBruno.LocalEmbeddings.ImageEmbeddings` (MIT) — CLIP
- `FaceAiSharp` (Apache-2) / `Clearly.ML.Faces` (MIT) / `FaceONNX` (MIT) — face pipeline
- `YoloDotNet` (MIT; careful with weights) — object detection
- `Sdcb.PaddleOCR` / `Tesseract` / `Windows.Media.Ocr` — OCR
- `HdbscanSharp` 3.0.1 (MIT) — face clustering
- `CoenM.ImageHash` (MIT) — dedup pHash/aHash/dHash
- `OpenCvSharp4` (BSD-3) — optional, for LaMa inpainting sample path (V60-08)

**Storage**
- `Microsoft.Data.Sqlite` (MIT) — catalog + settings
- `Microsoft.EntityFrameworkCore.Sqlite` (MIT) — migrations (SCH-02)
- `sqlite-vec` (Apache-2/MIT) — vector search in SQLite

**Logging + crash**
- `Serilog` + `Serilog.Sinks.File` (Apache-2) — rolling file logs (O-01)
- `Sentry` + `Sentry.Serilog` (MIT) — opt-in crash reports (O-02)

**UI controls**
- `PixiEditor.ColorPicker` (MIT) — color picker
- `PdfSharpCore` (MIT) — contact sheets / PDF export
- `WebView2` + Leaflet (BSD-2) — map pane
- `SharpCompress` (MIT, latest) — archive browsing (S-01 guardrails)

**Scripting / plugins**
- `Microsoft.CodeAnalysis.CSharp.Scripting` (MIT) — Roslyn
- `Westwind.Scripting` (MIT) — Roslyn wrapper

**Testing**
- `xUnit` (Apache-2) — domain tests
- `FlaUI` (MIT) — UIA smoke (T-02)
- `ImageSharp` (Apache-2 OSS / commercial for non-OSS) — golden-image diff (T-03)

**Bundled binaries (license-isolated via `CreateProcess`)**
- `jpegtran.exe` (libjpeg-turbo BSD) — lossless JPEG transforms
- `cjxl.exe` / `cjpegli.exe` (libjxl BSD) — JXL + better JPEG (F-03)
- `cwebp.exe` / `dwebp.exe` (libwebp BSD) — WebP
- `avifenc.exe` (libavif BSD) — AVIF
- `MozJPEG cjpeg.exe` (BSD) — JPEG re-encode
- `OxiPNG.exe` (MIT) — lossless PNG
- `ECT.exe` (Apache-2) — PNG/JPEG/GIF/ZIP lossless
- `gifsicle.exe` (GPL — isolated via shell-out) — GIF optimize
- `gmic.exe` (CeCILL/LGPL — isolated via shell-out) — filter bus
- `hugin_executor.exe`, `enblend.exe`, `enfuse.exe`, `align_image_stack.exe` (GPL — isolated) — panorama + HDR
- `c2patool.exe` (Apache-2 / MIT dual) — C2PA read/verify (P-05) and optional write (P-07)
- `exiftool.exe` (Artistic) — metadata write via S-05 safe wrapper

**Cannot vendor (GPL/AGPL) — ideas only**
- digiKam (GPL-2+), Converseen (GPL-3), Shotwell (LGPL-2.1), PhotoPrism (AGPL-3), Immich (AGPL-3), Upscayl (AGPL-3), chaiNNer (GPL-3), Hugin core GUI (GPL-2), nomacs (GPLv3), JPEGView (GPLv3), ImageGlass (GPLv3).

---

## Unique-differentiator checklist (what we build that nobody else has)

- [x] **Live inline rename** while viewing — ours, already shipped.
- [ ] **CLIP semantic text-to-image search** on local library — Windows Photos can't do this. (V60-02)
- [ ] **Squoosh-style visual-diff converter** on a Windows batch tool — nothing native offers this. (V50-20)
- [ ] **Multi-instance LAN pan/zoom sync** — nomacs only, and nomacs is GPL Qt. (V100-07)
- [ ] **Local exposure compensation with no modal** — JPEGView only. (V30-05)
- [ ] **Sketch-based fuzzy search** — digiKam only, and digiKam is GPL. (V40-23)
- [ ] **File Explorer sort-order sync** — ImageGlass only. (V20-30)
- [ ] **Images-in-archive browsing** (CBR/CBZ/ZIP/RAR/7Z) — Honeyview only, and discontinued. (V20-17)
- [ ] **C2PA Content Credentials read badge + write-on-export** — nobody in the OSS viewer space does this yet. (P-05/P-07)
- [ ] **Live byte-delta + SSIMULACRA2 readout during conversion** — Squoosh only, and Squoosh is web-only single-image. (V50-20/V50-23)
- [ ] **Network-egress log panel** — no OSS viewer surfaces this; precedent is Little Snitch / GlassWire. (P-03)
- [ ] **Documented UIA tree + Narrator/NVDA/JAWS test matrix** — no competitor publishes one. (A-01 + A-05 + A-06)
- [ ] **Store-extension detect + one-click deep-link** — ImageGlass nags you to install them by filename; nobody offers the deep-link. (V20-18)
- [ ] **Network-listen mode for piped workflows** — Oculante is the only viewer with this; it's Rust/MIT, not Windows-native. (V20-31)
- [ ] **NPU-aware UI label ("Running on NPU")** — no OSS Windows viewer exposes the EP the user paid for. (V60-01 + W-02)

---

## Notes on scoping / ordering

The phased order is deliberate:

1. **v0.1.2** (polish + branding) ships the last UX foot-guns plus observability groundwork (V02-06/V02-07). Small but compounding.
2. **v0.2 (Foundations)** must land first — SkiaSharp + persistence + preload + format expansion + settings UI. Accessibility (A-01/02/03), i18n (I-01/04), O-03 decode counters, SCH-02 migration guardrails all piggyback on settings. Everything downstream depends on it.
3. **v0.3 (Editor)** and **v0.5 (Converter)** both ride on v0.2; can be developed in parallel by different passes once v0.2 is in.
4. **v0.4 (Organizer)** needs v0.2's SQLite foundation and SCH-01/04 decision (XMP-authoritative). The AI-powered parts of the organizer (faces, CLIP) wait for v0.6. M-01…M-06 importers slot here.
5. **v0.6 (AI)** is the strategic differentiator. CLIP semantic search alone is worth more than all of v0.3+v0.5 combined from a marketing perspective. Doing it after v0.4 means we have an organizer to search *into*. V60-01 Windows ML dual-path is load-bearing for the install-size story.
6. **v0.7 (Plugins)** is low-urgency polish — ship when core is stable.
7. **v1.0** is a north star, not a commitment — RAW development, panorama, HDR, LAN sync each a big-L item on their own.

Cross-cutting tracks (Security, Privacy, A11y, i18n, Observability, Testing, Distribution, Catalog-schema, Migration) run in parallel with every phase. No phase ships without touching at least one item in each track.

Adjacent cleanup that falls out naturally:
- Keyboard-shortcut surface documented in-app (fold into V20-07 settings UI).
- Optional Catppuccin Latte light theme (V20-07 settings UI lands it for free).
- DPI-aware screenshot recapture (V02-04) — fold into each phase's release smoke.
- CHANGELOG + README badge synced on every release (non-negotiable per project rules).

---

## Appendix A — Sources

Merged, deduplicated list from the three research docs (`docs/research-viewers-editors.md`, `docs/research-organizers-converters.md`, `docs/research-advanced-features.md`), the two gap-research passes (`GAP_RESEARCH.md`, `docs/gap-research-report-2.md`), the three factory-loop iterations (`docs/research/iter-{1,2,3}-*.md`), and the 2026-04-25 roadmap research pass (`docs/research/roadmap-2026-04-25-harvest.md`). Every item in this roadmap is traceable to at least one of these URLs.

### Keyed anchors (cited inline as `[S-XXX]`)

These are the short keys used in the inline citations elsewhere in this document. Kept separate at the top so an inline `[[S-C2PA]](...)` link resolves without the reader scrolling a 200-entry list.

- **[S-IG10]** — ImageGlass 10 Beta 1 release notes: https://imageglass.org/news/imageglass-10-beta-1-is-here-99 · ImageGlass 2026 roadmap update: https://imageglass.org/news/imageglass-roadmap-update-2026-98
- **[S-PV]** — PicView (Avalonia rewrite, SignPath-signed, Native AOT): https://github.com/Ruben2776/PicView · https://picview.org/download/
- **[S-PEEK]** — PowerToys Peek docs + CLI: https://learn.microsoft.com/en-us/windows/powertoys/peek
- **[S-NOMACS]** — nomacs 3.22 release notes: https://github.com/nomacs/nomacs/releases · https://nomacs.org/
- **[S-JPEGVIEW]** — JPEGView sylikc fork releases: https://github.com/sylikc/jpegview/releases · v1.3.46: https://github.com/sylikc/jpegview/releases/tag/v1.3.46
- **[S-OCULANTE]** — Oculante (Rust viewer with network-listen mode): https://github.com/woelper/oculante · https://crates.io/crates/oculante
- **[S-QVIEW]** — qView changelog: https://github.com/jurplel/qView/releases · https://interversehq.com/qview/changelog/
- **[S-XNVIEW]** — XnView MP 1.10.5 changelog: https://www.xnview.com/xnviewmp_update.txt
- **[S-MAGICK-RELEASES]** — Magick.NET release index: https://github.com/dlemstra/Magick.NET/releases
- **[S-MAGICK-14-12-0]** — Magick.NET 14.12.0 notes: https://github.com/dlemstra/Magick.NET/releases/tag/14.12.0
- **[S-MAGICK-14-11-1]** — Magick.NET 14.11.1 security fix (GHSA-8793-7xv6-82cf InterpretImageFilename overflow): https://github.com/dlemstra/Magick.NET/releases
- **[S-MAGICK-14-10-2]** — Magick.NET 14.10.2 batch CVE fixes (BilateralBlur, OpenCL XML, MSL NULL, MSL stack overflow, XBM parser): https://github.com/dlemstra/Magick.NET/releases/tag/14.10.2
- **[S-LIBHEIF]** — CVE-2025-68431 libheif heap-buffer-overread in `HeifPixelImage::overlay()` (fixed 1.21.0): https://github.com/advisories/GHSA-j87x-4gmq-cqfq
- **[S-LIBAVIF]** — CVE-2025-48174 libavif integer overflow in `makeRoom` (fixed 1.3.0): https://github.com/advisories/GHSA-f6x7-5x3c-j3rg
- **[S-LIBWEBP]** — CVE-2023-4863 libwebp BLASTPASS OOB write in `BuildHuffmanTable` (fixed 1.3.2): https://github.com/advisories/GHSA-j7hp-h8jx-5ppr
- **[S-LIBWEBP-ORCA]** — Orca Security writeup of CVE-2023-4863: https://orca.security/resources/blog/understanding-libwebp-vulnerability/
- **[S-LIBDE265-64]** — CVE-2026-33164 libde265 OOB heap write via crafted HEVC (fixed 1.0.17): https://github.com/advisories/GHSA-wqrf-6rf5-v78r
- **[S-LIBDE265-65]** — CVE-2026-33165 libde265 companion advisory (fixed 1.0.17): https://github.com/advisories/GHSA-653q-9f73-8hvg
- **[S-JXL-CANIUSE]** — Can I use JPEG XL: https://caniuse.com/jpegxl
- **[S-JXL-DEVCLASS]** — Chromium JXL reversal (Nov 2025, lands Chrome 145 flag-gated): https://devclass.com/2025/11/24/googles-chromium-team-decides-it-will-add-jpeg-xl-support-reverses-obsolete-declaration/
- **[S-C2PA]** — Content Credentials: https://contentcredentials.org/ · C2PA 2.2 spec: https://spec.c2pa.org/specifications/specifications/2.2/specs/C2PA_Specification.html
- **[S-C2PA-2026]** — C2PA v2.3 + c2patool v0.26.27 (Feb 2026) + EU AI Act Article 50 deadline Aug 2 2026: https://aiphotocheck.com/blog/c2pa-specification-latest-version-2026
- **[S-SKIASHARP]** — SkiaSharp 3.119.2 on NuGet: https://www.nuget.org/packages/SkiaSharp/ · https://github.com/mono/SkiaSharp/releases
- **[S-SERILOG]** — Serilog 4.3.1 release: https://github.com/serilog/serilog/releases
- **[S-SERILOG-FILE]** — Serilog.Sinks.File 7.0.0 release: https://github.com/serilog/serilog-sinks-file/releases · https://www.nuget.org/packages/Serilog.Sinks.File/
- **[S-LIBRAW-022]** — LibRaw 0.22.0 release notes (DNG 1.7 + JPEG-XL compression + CR3 fix + +Canon R5 II/R6 II/R8/R50/R100, Fujifilm X-T50, Sony A9-III): https://www.libraw.org/news/libraw-0-22-0-release
- **[S-SDCB-LIBRAW]** — Sdcb.LibRaw .NET wrapper (currently tracks 0.21.x): https://github.com/sdcb/Sdcb.LibRaw · https://www.nuget.org/packages/Sdcb.LibRaw
- **[S-SHARPCOMPRESS]** — SharpCompress 0.44.0 (multi-TFM .NET 8/10/Framework 4.8, async I/O): https://github.com/adamhathcock/sharpcompress · https://www.nuget.org/packages/SharpCompress
- **[S-ARTIFACT-SIGNING]** — Azure Artifact Signing (rebrand of Trusted Signing, GA April 2026): https://azure.microsoft.com/en-us/products/artifact-signing · https://learn.microsoft.com/en-us/azure/artifact-signing/concept-trust-models
- **[S-SMARTSCREEN-REGRESSION]** — SmartScreen reputation regression after CA rotation (March-April 2026): https://learn.microsoft.com/en-us/answers/questions/5855708/trusted-signing-regression-in-smartscreen-reputati · https://github.com/Azure/artifact-signing-action/issues/128
- **[S-HANSELMAN-SIGN]** — Hanselman's Azure Trusted Signing + GitHub Actions setup: https://www.hanselman.com/blog/automatically-signing-a-windows-exe-with-azure-trusted-signing-dotnet-sign-and-github-actions
- **[S-WINGET-RELEASER]** — WinGet Releaser GitHub Action (289 stars, 2026-03 update): https://github.com/marketplace/actions/winget-releaser · https://github.com/vedantmgoyal9/winget-releaser
- **[S-WINML]** — Windows ML GA docs: https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview · introduction blog: https://blogs.windows.com/windowsdeveloper/2025/05/19/introducing-windows-ml-the-future-of-machine-learning-development-on-windows/ · NVIDIA TensorRT-for-RTX EP: https://developer.nvidia.com/blog/deploy-ai-models-faster-with-windows-ml-on-rtx-pcs/ · AMD NPU EP: https://www.amd.com/en/developer/resources/technical-articles/2026/ai-model-deployment-using-windows-ml-on-amd-npu.html
- **[S-WIN32-ISOLATION]** — Win32 App Isolation (AppContainer + MSIX capability manifest, preview): https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation · https://learn.microsoft.com/en-us/windows/security/book/application-security-application-isolation · https://www.bleepingcomputer.com/news/microsoft/windows-11-win32-app-isolation-security-feature-now-in-preview/
- **[S-HEIF-CODEC]** — HEIF WIC codec docs: https://learn.microsoft.com/en-us/windows/win32/wic/heif-codec · Store HEIF Image Extension: https://apps.microsoft.com/detail/9pmmsr1cgpwg
- **[S-TESSERACT]** — Tesseract OCR: https://github.com/tesseract-ocr/tesseract · IronOCR C# guide: https://ironsoftware.com/csharp/ocr/blog/ocr-tools/install-tesseract/
- **[S-UPSCAYL]** — Upscayl: https://github.com/upscayl/upscayl · https://upscayl.org/ · Real-ESRGAN: https://github.com/xinntao/Real-ESRGAN · OpenModelDB: https://openmodeldb.info/
- **[S-EXIF]** — EXIF 3.0 Wikipedia entry (UTF-8 support, May 2023): https://en.wikipedia.org/wiki/Exif · ExifTool DateTimeOriginal forum: https://exiftool.org/forum/index.php?topic=13474.0
- **[S-ALTERNATIVES]** — AlternativeTo IrfanView alternatives: https://alternativeto.net/software/irfanview/ · Slant image viewers for Windows: https://www.slant.co/topics/4022/~image-viewers-for-windows
- **[S-SQLITE]** — Microsoft.Data.Sqlite (what we ship): https://www.nuget.org/packages/Microsoft.Data.Sqlite
- **[S-CBZ]** — CBZ file format primer: https://www.online-convert.com/file-format/cbz
- **[S-PICVIEW-ORG]** — PicView feature map: archive loading, gallery, batch resizing, clipboard/URL/Base64 ingress, metadata/rating, effects: https://picview.org/
- **[S-NEEVIEW-GUIDE]** — NeeView user guide: folders/archives as books, image formats via WIC, archive/PDF support, configurable settings: https://neelabo.github.io/NeeView/en-us/userguide.html
- **[S-YACREADER]** — YACReader feature/news stream: comic reader/library, read-position/library workflows, image filters, custom covers, `--system-info`: https://www.yacreader.com/
- **[S-PHOTODEMON]** — PhotoDemon overview: portable open-source Windows editor, 200+ tools, layers/selections, content-aware fill, macro recorder, batch processor, live previews/presets: https://photodemon.org/
- **[S-CZKAWKA]** — Czkawka/Krokiet README: Rust cleanup core, similar images/videos, duplicate/bad-extension/broken-file/Exif-remover tools, cache, CLI/core-library architecture: https://github.com/qarmin/czkawka
- **[S-PUREREF]** — PureRef 2.0 handbook: reference boards, always-on-top/app-specific pinning, click-through, GIF frame timeline, drawing, notes, groups, hierarchy, color/coordinate tools: https://www.pureref.com/handbook/2.0/features/
- **[S-EAGLE]** — Eagle asset-manager feature map: semantic/visual search, browser capture, smart folders, tags, annotation, duplicate merge, hover preview, plugin system: https://www.eagle.cool/
- **[S-HYDRUS]** — Hydrus Network docs: local tag-first media management, tag sharing optionality, duplicates, sidecars, import/download flows, no silent phoning home: https://hydrusnetwork.github.io/hydrus/
- **[S-OPENSEADRAGON]** — OpenSeadragon docs: open-source high-resolution zoomable image viewer, DZI/IIIF/IIP/Zoomify/custom tile sources, sequence/reference-strip/collection modes: https://openseadragon.github.io/
- **[S-OPENSLIDE]** — OpenSlide docs: whole-slide image library, DICOM WSI, SVS, NDPI, SCN, MRXS, SVSLIDE, BIF, CZI, tiled TIFF, Deep Zoom generator: https://openslide.org/
- **[S-BIOFORMATS]** — OME Bio-Formats: proprietary microscopy image data/metadata reader, standardized interface, 160+ formats and OME data model mapping: https://www.openmicroscopy.org/bio-formats/
- **[S-NAPARI]** — napari docs: fast interactive 2D/3D/higher-dimensional image viewer, overlays, annotation/editing of derived datasets, plugin ecosystem: https://napari.org/stable/
- **[S-QUPATH]** — QuPath: open-source bioimage analysis and whole-slide visualization/annotation platform: https://qupath.github.io/
- **[S-LIBVIPS]** — libvips project: demand-driven, horizontally threaded image-processing stack, .NET binding availability, streaming/large-image processing reference: https://www.libvips.org/
- **[S-OIIO]** — OpenImageIO docs: VFX/animation image I/O, format-agnostic API, bundled plugins, ImageCache/TextureSystem, `iinfo`, `iconvert`, `igrep`, `idiff`, `maketx`: https://openimageio.readthedocs.io/en/latest/
- **[S-OCIO]** — OpenColorIO: production color-management solution, OCIO v2.5, config merging, Vulkan GPU support, ACES 2.0 built-ins: https://opencolorio.org/
- **[S-DIGIKAM-FEATURES]** — digiKam feature overview: face recognition, similarity database, duplicate discovery, full photo workflow reference: https://www.digikam.org/about/features/

### Viewers, editors, organizers, converters
- https://www.irfanview.com/
- http://irfanview.helpmax.net/en/file-menu/batch-conversionrename/
- https://www.xnview.com/en/xnviewmp/
- https://www.xnview.com/wiki/index.php/Cataloging_Features_in_XnView_MP
- https://newsgroup.xnview.com/
- https://imageglass.org/docs/features
- https://imageglass.org/news/announcing-imageglass-9-4-97
- https://imageglass.org/news/imageglass-roadmap-update-2026-98
- https://github.com/d2phap/ImageGlass
- https://github.com/d2phap/ImageGlass/releases
- https://github.com/d2phap/ImageGlass/wiki/Multilingual
- https://github.com/nomacs/nomacs
- https://github.com/nomacs/nomacs/releases
- https://nomacs.org/blog/synchronization/
- https://www.neowin.net/software/nomacs-3220-final/
- https://interversehq.com/qview/
- https://github.com/sylikc/jpegview
- https://www.faststone.org/FSViewerDetail.htm
- https://www.bandisoft.com/honeyview/
- https://github.com/QL-Win/QuickLook
- https://1218.io/seer/
- https://github.com/poppeman/Pictus
- https://www.portablefreeware.com/index.php?id=2666
- https://github.com/woelper/oculante
- https://github.com/topics/image-viewer?l=rust

### DAMs + catalog + sidecars
- https://www.digikam.org/about/features/
- https://www.digikam.org/news/2025-03-15-8.6.0_release_announcement/
- https://www.digikam.org/documentation/
- https://brunoabinader.github.io/2022/08/07/lock-free-multithreaded-find-duplicates-in-digikam-7.3.0/
- https://github.com/darktable-org/darktable
- https://docs.darktable.org/usermanual/development/en/overview/sidecar-files/sidecar/
- https://marcrphoto.wordpress.com/2025/07/28/darktable-and-digikam-more-xmp-questions/
- https://www.photoprism.app/features
- https://docs.immich.app/administration/system-settings/
- https://daminion.net/features/
- https://machinelearning.apple.com/research/on-device-scene-analysis
- https://helpx.adobe.com/lightroom-classic/help/photo-collections.html
- https://helpx.adobe.com/lightroom-classic/kb/lightroom-catalog-faq.html
- https://stackoverflow.com/questions/10148079/where-is-the-lightroom-catalog-schema-documented
- https://helpx.adobe.com/bridge/desktop/organize-and-find-files/tag-and-find-files/batch-rename-files.html
- https://regex.info/blog/lightroom-goodies/picasa
- https://github.com/mvz/picasa-contacts

### Converters + compression
- https://www.xnview.com/en/xnconvert/
- https://imagemagick.org/script/mogrify.php
- https://github.com/Faster3ck/Converseen
- https://nikkhokkho.sourceforge.io/?page=FileOptimizer
- https://developers.google.com/speed/webp/docs/cwebp
- https://github.com/mozilla/mozjpeg
- https://github.com/GoogleChromeLabs/squoosh
- https://giannirosato.com/blog/post/image-comparison/
- https://opensource.googleblog.com/2024/04/introducing-jpegli-new-jpeg-coding-library.html
- https://www.squeezejpg.com/blog/jpeg-compression-in-2025-best-practices-and-new-formats

### AI / ML
- https://github.com/upscayl/upscayl
- https://github.com/upscayl/upscayl-ncnn
- https://www.aiarty.com/ai-image-enhancer/upscayl-review.htm
- https://github.com/xinntao/Real-ESRGAN
- https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan
- https://github.com/chaiNNer-org/chaiNNer
- https://openmodeldb.info/
- https://openmodeldb.info/docs/faq
- https://openmodeldb.info/models/4x-realesrgan-x4plus
- https://openmodeldb.info/models/4x-realesrgan-x4plus-anime-6b
- https://github.com/danielgatis/rembg
- https://github.com/ZhengPeng7/BiRefNet
- https://dev.to/om_prakash_3311f8a4576605/birefnet-vs-rembg-vs-u2net-which-background-removal-model-actually-works-in-production-2j70
- https://blog.cloudflare.com/background-removal/
- https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html
- https://github.com/microsoft/DirectML
- https://gpuopen.com/learn/onnx-directlml-execution-provider-guide-part1/
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/supported-execution-providers
- https://learn.microsoft.com/en-us/windows/ai/npu-devices/
- https://developer.nvidia.com/blog/deploy-ai-models-faster-with-windows-ml-on-rtx-pcs/
- https://www.amd.com/en/developer/resources/technical-articles/2026/ai-model-deployment-using-windows-ml-on-amd-npu.html
- https://github.com/FaceONNX/FaceONNX
- https://www.nuget.org/packages/FaceAiSharp.Models.ArcFace.LResNet100E-IR
- https://github.com/deepinsight/insightface
- https://www.nuget.org/packages/Clearly.ML.Faces
- https://elbruno.com/2026/02/16/%F0%9F%96%BC%EF%B8%8F-local-image-embeddings-in-net-clip-onnx/
- https://bartbroere.eu/2023/07/29/openai-clip-csharp-onnx/
- https://github.com/openai/CLIP
- https://github.com/NickSwardh/YoloDotNet
- https://www.nuget.org/packages/YoloDotNet
- https://github.com/dme-compunet/YoloSharp
- https://ironsoftware.com/csharp/ocr/blog/compare-to-other-components/paddle-ocr-vs-tesseract/
- https://www.koncile.ai/en/ressources/paddleocr-analyse-avantages-alternatives-open-source
- https://hackernoon.com/c-ocr-libraries-the-definitive-net-comparison-for-2026
- https://huggingface.co/Carve/LaMa-ONNX
- https://huggingface.co/opencv/inpainting_lama
- https://github.com/opencv/opencv/pull/26736
- https://github.com/advimman/lama
- https://www.nuget.org/packages/DBSCAN/
- https://github.com/doxakis/HdbscanSharp
- https://act-labs.github.io/posts/facenet-clustering/
- https://www.neowin.net/news/microsoft-now-lets-you-restyle-images-in-paint/
- https://learn.microsoft.com/en-us/windows/ai/apis/image-generation

### Formats, codecs, C2PA
- https://www.corewebvitals.io/pagespeed/jpeg-xl-core-web-vitals-support
- https://en.wikipedia.org/wiki/JPEG_XL
- https://jpegxl.info/resources/supported-software.html
- https://www.phoronix.com/news/JPEG-XL-Possible-Chrome-Back
- https://openaviffile.com/how-to-open-avif-files-on-windows/
- https://forums.getpaint.net/topic/116233-avif-filetype-03-29-2026/
- https://github.com/nokiatech/heif/blob/master/LICENSE.TXT
- https://www.tomshardware.com/laptops/acer-and-asus-halt-pc-and-laptop-sales-in-germany-amid-h-264-codec-patent-dispute-nokia-wins-patent-ruling-forcing-tech-giants-to-license-hevc-codec
- https://github.com/contentauth/
- https://github.com/contentauth/c2pa-rs
- https://c2pa.org/wp-content/uploads/sites/33/2025/10/content_credentials_wp_0925.pdf
- https://attesttrail.com/blog/c2pa-cameras-support
- https://c2pa.camera/
- https://github.com/cookmscott/c2pa-compatibility-list

### Security + privacy
- https://www.welivesecurity.com/en/eset-research/revisiting-cve-2025-50165-critical-flaw-windows-imaging-component/
- https://www.zscaler.com/blogs/security-research/cve-2025-50165-critical-flaw-windows-graphics-component
- https://windowsforum.com/threads/understanding-and-mitigating-windows-imaging-component-cve-2025-47980-vulnerability.372759/
- https://blog.isosceles.com/the-webp-0day/
- https://blog.cloudflare.com/uncovering-the-hidden-webp-vulnerability-cve-2023-4863/
- https://ubuntu.com/security/cves?package=libheif
- https://tracker.debian.org/pkg/libheif
- https://github.com/advisories/GHSA-f6x7-5x3c-j3rg
- https://security.snyk.io/vuln/SNYK-DEBIAN12-LIBAVIF-10180086
- https://research.jfrog.com/vulnerabilities/archiver-zip-slip/
- https://www.huntress.com/threat-library/vulnerabilities/cve-2024-1708
- https://docs.telerik.com/devtools/wpf/knowledge-base/kb-security-unsafe-deserialization-cve-2024-10012
- https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html
- https://exiftool.org/exiftool_pod.html
- https://www.junian.dev/SharpExifTool/
- https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation
- https://www.directionsonmicrosoft.com/reports/win32-app-isolation-another-sandbox/
- https://docs.wasmtime.dev/security.html
- https://opensource.microsoft.com/blog/2025/03/26/hyperlight-wasm-fast-secure-and-os-free/
- https://www.microsoft.com/en-zw/p/exif-metadata-editor-pro-photo-gps-viewer/9ph7f9zh9z8w
- https://exifremover.com/
- https://code.visualstudio.com/docs/configure/telemetry
- https://telemetrydeck.com/docs/guides/privacy-faq/

### Accessibility + i18n
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.imageautomationpeer
- https://learn.microsoft.com/en-us/windows/win32/winauto/microsoft-ui-automation-overview
- https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-overview
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility
- https://learn.microsoft.com/en-us/windows/win32/api/_magapi/
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/localization-overview
- https://weblate.org/en/hosting/
- https://crowdin.com/pricing
- https://exiftool.org/TagNames/XMP.html
- https://github.com/drewnoakes/metadata-extractor-dotnet

### Observability + testing
- https://serilog.net/
- https://github.com/open-telemetry/opentelemetry-dotnet
- https://opentelemetry.io/docs/languages/net/
- https://docs.sentry.io/platforms/dotnet/guides/wpf/
- https://www.getpaint.net/doc/latest/CrashLogs.html
- https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters
- https://github.com/FlaUI/FlaUI
- https://docs.flaui.org/
- https://github.com/microsoft/WinAppDriver
- https://github.com/appium/appium-windows-driver
- https://github.com/SixLabors/ImageSharp
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/

### Distribution + signing
- https://learn.microsoft.com/en-us/windows/msix/overview
- https://learn.microsoft.com/en-us/windows/msix/msix-container
- https://www.advancedinstaller.com/user-guide/faq-msix.html
- https://www.turbo.net/blog/posts/2025-06-16-understanding-msix-limitations-enterprise-application-compatibility
- https://learn.microsoft.com/en-us/windows/package-manager/winget/
- https://github.com/marketplace/actions/winget-releaser
- https://github.com/microsoft/winget-create
- https://github.com/grafana/k6/pull/5203
- https://scoop.sh/
- https://github.com/ScoopInstaller/Extras
- https://chocolatey.org/docs/create-packages
- https://community.chocolatey.org/packages?q=dotnet
- https://learn.microsoft.com/en-us/azure/trusted-signing/overview
- https://learn.microsoft.com/en-us/azure/artifact-signing/faq
- https://azure.microsoft.com/en-us/products/trusted-signing
- https://textslashplain.com/2025/03/12/authenticode-in-2025-azure-trusted-signing/
- https://signmycode.com/digicert-ev-code-signing
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained
- https://github.com/dotnet/wpf/issues/3070

### Lossless transforms + plugins + color + annotation
- https://linux.die.net/man/1/jpegtran
- https://github.com/libjpeg-turbo/libjpeg-turbo/issues/233
- https://www.graphicsmill.com/docs/gm5/ApplyingLosslessJPEGTransforms.htm
- https://gmic.eu/
- https://gmic.eu/gmic36/
- https://github.com/c-koi/gmic-qt
- https://en.wikipedia.org/wiki/G'MIC
- https://hugin.sourceforge.io/docs/manual/Hugin.html
- https://hugin.sourceforge.io/docs/manual/HDR_workflow_with_hugin.html
- https://hugin.sourceforge.io/docs/manual/Enfuse.html
- https://www.littlecms.com/color-engine/
- https://learn.microsoft.com/en-us/windows/win32/wcs/advanced-color-icc-profiles
- https://github.com/dantmnf/MHC2
- https://www.w3.org/Graphics/Color/Workshop/slides/Derhak.pdf
- https://docs.krita.org/en/reference_manual/filters.html
- https://krita.org/en/features/
- https://alternativeto.net/software/shottr/
- https://www.screensnap.pro/blog/best-markup-tools
- https://www.captio.work/blog/greenshot-alternatives
- https://paintdotnet.github.io/apidocs/
- https://github.com/paintdotnet/PdnV5EffectSamples
- https://github.com/addisonElliott/pypdn
- http://justsolve.archiveteam.org/wiki/Paint.NET_image
- https://www.nuget.org/packages/ColorThief.ImageSharp
- https://docs.sixlabors.com/articles/imagesharp/gettingstarted.html
- https://github.com/PixiEditor/ColorPicker
- https://www.nuget.org/packages/Aspose.PSD
- https://www.photopea.com/learn/opening-saving
- https://github.com/lancedb/lancedb
- https://github.com/qdrant/qdrant
- https://docs.lm-kit.com/lm-kit-net/guides/glossary/vector-database.html
- https://zilliz.com/comparison/qdrant-vs-lancedb
- https://dev.to/saint_vandora/the-ultimate-guide-choosing-between-sixlaborsimagesharp-and-skiasharp-for-net-image-processing-17hi
- https://github.com/mono/SkiaSharp/issues/319
- https://anthonysimmon.com/benchmarking-dotnet-libraries-for-image-resizing/
- https://github.com/muhammad-ahmed-ghani/RealESRGAN_ONNX
- https://github.com/the-database/mpv-upscale-2x_animejanai
- https://onnxruntime.ai/docs/tutorials/mobile/superres.html
- https://github.com/sdcb/Sdcb.LibRaw
- https://github.com/LibRaw/LibRaw
- https://github.com/laheller/SharpLibraw
- https://github.com/JbPasquier/stable-diffusion-onnx-ui
- https://github.com/ssube/onnx-web
- https://github.com/lkwq007/stablediffusion-infinity
- https://learn.microsoft.com/en-us/archive/blogs/csharpfaq/introduction-to-the-roslyn-scripting-api
- https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md
- https://weblog.west-wind.com/posts/2022/Jun/07/Runtime-C-Code-Compilation-Revisited-for-Roslyn
