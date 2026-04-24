# Images — ROADMAP

Tracks planned work. `[ ]` pending, `[x]` shipped. Priorities `P0` must / `P1` should / `P2` nice.

> **Vision**: One Windows app that replaces Photos, IrfanView, XnConvert, Upscayl, and a light Lightroom — by cannibalising the best ideas from a dozen OSS/freeware projects. Local-first, fast, dark-mode, no cloud, no subscription. The killer feature is **CLIP semantic search** on a local library, with **live inline rename** (already shipped) and a **Squoosh-style visual-diff converter** as the other two differentiators.

## Current state (v0.1.1 — shipped 2026-04-24)

Core viewer. Natural-sort folder nav. Zoom/pan/rotate. Live inline rename with 600 ms debounce, conflict resolution, 10-deep undo stack. Drag-drop. FSW. Catppuccin Mocha dark theme. ~100 formats via WIC + Magick.NET 14.12.0. Framework-dependent win-x64. No persistence, no editor, no organizer, no batch.

Companion research (supports this roadmap):
- [docs/research-viewers-editors.md](docs/research-viewers-editors.md) — IrfanView, XnView MP, ImageGlass, nomacs, qView, JPEGView, FastStone, Honeyview, Windows Photos, QuickLook/Seer/Peek, Pictus
- [docs/research-organizers-converters.md](docs/research-organizers-converters.md) — digiKam, Shotwell, XnView organizer, Daminion, Apple/Google Photos, Picasa, Lightroom, Bridge, PhotoPrism, Immich, XnConvert, ImageMagick, Converseen, Squoosh, FileOptimizer
- [docs/research-advanced-features.md](docs/research-advanced-features.md) — AI (Upscayl/Real-ESRGAN, rembg/BiRefNet, CLIP, YOLO, OCR, faces), editors (GIMP/Krita/Paint.NET/darktable), panorama (Hugin), HDR (enfuse), lossless transforms, plugin hosts, canvas-engine decision

---

## v0.1.2 — polish + branding pass

- [ ] **V02-01** *P0* — Bump GitHub Actions: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. Closes Node 20 deprecation (runner removes Node 20 on 2026-06-02).
- [x] **V02-02** *P1* — 5-prompt logo brief at `assets/logo-prompt.md`; user generated `logo.png` + `banner.png`.
- [x] **V02-03** *P1* — `<ApplicationIcon>` wired. 7-frame multi-res `icon.ico` + PNG-embedded `icon.svg` + bundled `logo.png` resource.
- [ ] **V02-04** *P2* — Recapture main screenshot (DPI-aware). Requires Windows GUI session — deferred to manual.
- [ ] **V02-05** *P2* — Human runtime smoke: prev/next/wrap, rename+undo, drag-drop, Del-to-recycle, external add/delete 250 ms roundtrip.

---

## v0.2.0 — Foundations + Viewer polish (M, 2-3 weeks)

**Theme**: replace the canvas engine, add persistence, match IrfanView/ImageGlass/JPEGView viewer baseline.

### Engine / infra
- [ ] **V20-01** *P0* — **SkiaSharp canvas** replacing `WriteableBitmap` in `ZoomPanImage`. `SKCodec` decodes to target size (1000×800 buffer for 800×600 view of 4000×3600 source). ~2× load, ~4× thumbnail gen vs ImageSharp. MIT, no strings. Unlocks HDR path and every AI overlay later. [stack: `SkiaSharp`]
- [ ] **V20-02** *P0* — **Persistent settings** via SQLite at `%LOCALAPPDATA%\Images\settings.db`. Schema: `settings(key TEXT PK, value TEXT)`, `recent_folders(path, last_opened)`, `key_bindings(action, key)`. Seed theme, last folder, zoom mode, arrow visibility. [stack: `Microsoft.Data.Sqlite`]
- [ ] **V20-03** *P0* — **Preload next + previous** image in a background thread. Cancellation token on navigate. Target: N-1 and N+1 decoded before user asks.
- [ ] **V20-04** *P1* — **Persistent thumbnail cache** at `%LOCALAPPDATA%\Images\thumbs\<hash>.webp` keyed by `(path, mtime, size)`.
- [ ] **V20-05** *P1* — SIMD-accelerated decode path via SkiaSharp (AVX2/SSE2 automatic).
- [ ] **V20-06** *P1* — Memory-mapped I/O for files >256 MB (avoids blowing the managed heap on 500 MP RAW). `MemoryMappedFile.CreateFromFile`.

### Format expansion
- [ ] **V20-10** *P0* — HEIC / HEIF via WIC (Windows "HEIF Image Extensions" store package) with libheif fallback bundled for offline boxes.
- [ ] **V20-11** *P0* — AVIF via `AV1 Video Extension` + libavif fallback.
- [ ] **V20-12** *P1* — JPEG XL via libjxl bindings (Apple iPhone 16 default output format — Windows support is still thin).
- [ ] **V20-13** *P1* — WebP + animated WebP.
- [ ] **V20-14** *P1* — RAW decode via `Sdcb.LibRaw` 0.21 (MIT wrapper / LGPL native) — Canon CR2/CR3, Nikon NEF, Sony ARW, Fuji RAF, DNG.
- [ ] **V20-15** *P2* — Animated GIF / APNG / animated AVIF with transport controls (play/pause/frame-step/speed).
- [ ] **V20-16** *P2* — Multi-frame TIF / ICO / multi-page PDF / DICOM — per-frame navigation UI (ImageGlass pattern).
- [ ] **V20-17** *P2* — **Images inside archives** — ZIP/7Z/RAR/CBR/CBZ browsing without extraction (Honeyview's moat). `SharpCompress` MIT covers all formats.

### Viewer UX
- [ ] **V20-20** *P0* — **Six zoom modes** (ImageGlass): Auto, Lock-to-%, Fit-to-Width, Fit-to-Height, Fit (uniform), Fill.
- [ ] **V20-21** *P0* — **Filmstrip** at bottom (togglable), virtualised, synced to current index.
- [ ] **V20-22** *P1* — **EXIF overlay** (togglable HUD) — camera/lens/ISO/shutter/aperture/date/GPS. Tap-to-expand for full panel.
- [ ] **V20-23** *P1* — **GPS coordinates overlay** with click-to-open-in-map (Honeyview).
- [ ] **V20-24** *P1* — **Histogram overlay** per-channel + luminance (`0.299R + 0.587G + 0.114B`, log-scale toggle).
- [ ] **V20-25** *P1* — **Color picker** eyedropper — hex + RGB + HSL + LAB readout (PixiEditor.ColorPicker MIT).
- [ ] **V20-26** *P1* — **Hidden edge-triggered fullscreen toolbar** (FastStone pattern) — chromeless by default, reveal on edge approach.
- [ ] **V20-27** *P1* — Dual/multi-monitor — remember per-monitor placement, "send to monitor N" shortcut.
- [ ] **V20-28** *P2* — **Individual color-channel isolation** (ImageGlass R/G/B/A only views).
- [ ] **V20-29** *P2* — **Command palette** (Ctrl+Shift+P) — greenfield; no viewer does this well.
- [ ] **V20-30** *P2* — **File Explorer sort-order sync** (ImageGlass v9.3+) — read Explorer's current sort, match it.

---

## v0.3.0 — Light editor + lossless (M, 3-4 weeks)

**Theme**: edits-inside-viewer, no modal dialogs. JPEGView's real-time-inline pattern. FastStone's clone/heal/red-eye. Windows-Photos-class AI generative erase.

### Edits
- [ ] **V30-01** *P0* — Crop (draggable rect, aspect-ratio presets, free/square/3:2/4:3/16:9 + custom, rule-of-thirds overlay).
- [ ] **V30-02** *P0* — **Lossless JPEG transforms** — rotate 90/180/270 + crop MCU-aligned via bundled `jpegtran.exe` (libjpeg-turbo BSD). Confirm dialog when MCU forces trim. [stack: shell-out]
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
- [ ] **V30-24** *P2* — Scan via TWAIN/WIA to image (IrfanView pattern — `Saraff.Twain.NET` NuGet).

### Comparison + slideshow
- [ ] **V30-30** *P1* — **Image compare** 2-up / 4-up with synchronized pan/zoom (XnView MP, FastStone).
- [ ] **V30-31** *P1* — **Opacity-overlay compare** (nomacs) — slider blend two images for AB review.
- [ ] **V30-32** *P1* — Slideshow — configurable interval, transitions (fade/slide/wipe), background music (MP3/FLAC), loop/shuffle, pause on hover.
- [ ] **V30-33** *P2* — **Standalone .exe slideshow export** (IrfanView — unique) — packs N images + runtime into a self-extracting viewer.

---

## v0.4.0 — Organizer / DAM (L, 6-8 weeks)

**Theme**: catalog, tags, dedup, map, triage. digiKam minus the GPL.

### Catalog
- [ ] **V40-01** *P0* — **SQLite catalog** at `%LOCALAPPDATA%\Images\catalog.db`. Four-DB split (digiKam pattern): `core.db` (assets/metadata), `thumbs.db` (blobs), `search.db` (FTS5 + vectors), `similarity.db` (pHash/Haar). Avoids `VACUUM` locks stalling the UI.
- [ ] **V40-02** *P0* — **Watched folders** — add/remove library roots, scan-on-start, FSW for deltas. Multi-root w/ offline-prompt behavior (don't delete records on drive eject).
- [ ] **V40-03** *P0* — **Hash-based asset identity** (SHA-256 or xxHash64) — survives move/rename. Path is denormalised cache, not authoritative (Lightroom `id_global` pattern).
- [ ] **V40-04** *P1* — **Sidecar XMP** writing — `<basename>.<ext>.xmp` alongside originals (darktable/digiKam naming), namespace `xmlns:imv="http://maven.imaging/1.0/"`. Also round-trip to embedded IPTC/XMP (Daminion portability guarantee). [stack: ExifTool child process + MetadataExtractor read]

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
- [ ] **V40-30** *P1* — **EXIF GPS read/write** (ExifTool shell-out for writes; MetadataExtractor for reads).
- [ ] **V40-31** *P1* — **Interactive map pane** with clustering at zoom (Leaflet via WebView2, OpenStreetMap tiles).
- [ ] **V40-32** *P2* — **Reverse geocoding** — local offline DB (GeoNames CC-BY) — privacy-first, no API calls.
- [ ] **V40-33** *P2* — **GPX track-log sync** — match photo timestamps to GPS track, backfill EXIF.

### Smart albums
- [ ] **V40-40** *P1* — **Smart Collections** criteria builder — date, rating, label, keyword, camera, lens, ISO, geo bbox, face count (Lightroom).
- [ ] **V40-41** *P2* — **Auto-albums by pattern** — screenshots, receipts, notes, documents (Google Photos OCR+shape).
- [ ] **V40-42** *P2* — **Trip detection** — contiguous days + distance >threshold from home (Apple Memories engine).
- [ ] **V40-43** *P2* — **Events view** — date-based clusters with key-photo thumbnail (Shotwell).

---

## v0.5.0 — Converter / Batch (M, 3-4 weeks)

**Theme**: XnConvert operation-chain UX + Squoosh visual-diff slider + FileOptimizer lossless chain. The batch tab most people will open daily.

- [ ] **V50-01** *P0* — **Operation-chain builder** — drag-orderable list, per-op enable/disable, live preview on first selected image (XnConvert tab 2 pattern).
- [ ] **V50-02** *P0* — **Output formats** with per-format quality controls: JPEG (MozJPEG + cjpegli), PNG (OxiPNG), WebP (cwebp), AVIF (avifenc), JXL (cjxl), HEIC (libheif), TIFF, BMP, GIF. [stack: bundled CLIs + Magick.NET core]
- [ ] **V50-03** *P0* — **Resize policies** — %, px, long-edge, short-edge, canvas-fit, canvas-fill, DPI-only.
- [ ] **V50-04** *P0* — **Presets** saveable/nameable/import-export (JSON). Default presets: "Web 1920 / Instagram 1080 / Email 2MB / Print 300 DPI".
- [ ] **V50-05** *P0* — **Overwrite-vs-new-folder guardrails** — refuse overwrite originals without confirm (ImageMagick `mogrify` footgun lesson).
- [ ] **V50-06** *P0* — **Drag-to-target** — drop folder on app, convert with last preset.
- [ ] **V50-07** *P1* — **Watch-folder** auto-apply (XnConvert Watch).
- [ ] **V50-08** *P1* — **Rename tokens** — `{name}_{date:yyyy-MM-dd}_{seq:000}_{exif.iso}` (Bridge engine).
- [ ] **V50-09** *P1* — **Strip metadata** granular — all / keep GPS / keep copyright / keep XMP.
- [ ] **V50-10** *P1* — **Watermark** — text + image, opacity/position/rotation/tile (XnConvert).
- [ ] **V50-11** *P1* — **CPU-core throttle** slider (XnConvert).

### Compression pipelines (the differentiator)
- [ ] **V50-20** *P0* — **Squoosh-style visual-diff slider** — draggable split-pane preview + live byte-delta + SSIM/Butteraugli readout. Nothing native on Windows does this.
- [ ] **V50-21** *P1* — **Lossless re-pack chain** per format (bundled CLIs):
  - PNG: OxiPNG → ECT → pngquant (opt-in lossy) — keep smallest
  - JPEG: jpegtran-optimize → jpegoptim → MozJPEG re-encode (opt-in)
  - GIF: gifsicle `-O3`
  - WebP/AVIF/JXL: max-effort re-encode (`-m 6`, `--speed 0`, `-e 9`)
- [ ] **V50-22** *P1* — **"Best-of" mode** — run N encoders in parallel, pick smallest under target SSIMULACRA2 score (FileOptimizer philosophy).
- [ ] **V50-23** *P2* — **SSIMULACRA2 + Butteraugli** quality metric alongside raw slider (2026 codec-comp community standard).
- [ ] **V50-24** *P2* — Send originals to Recycle Bin on replace (FileOptimizer rollback).

### Exports
- [ ] **V50-30** *P1* — **Contact sheets → PDF**: grid, header/footer, metadata captions (Bridge Output). [stack: PdfSharpCore MIT]
- [ ] **V50-31** *P1* — **Print layout** — multi-image/page with margins + alignment (Lightroom Print module).
- [ ] **V50-32** *P2* — **Web gallery** — static HTML + thumbs + lightbox (digiKam HTMLGallery).
- [ ] **V50-33** *P2* — **Direct publish** — Flickr/Imgur/Pinterest/Dropbox/OneDrive/SMB/FTP (OAuth + known APIs).
- [ ] **V50-34** *P2* — **C2PA Content Credentials** on export — stamp "edited with Images v0.x" + operations for provenance (table stakes 2026).

---

## v0.6.0 — AI features (L, 6-8 weeks — THE DIFFERENTIATOR)

**Theme**: ONNX Runtime + DirectML, everything local, no cloud. The features Windows Photos 2026 *can* do we do better; the ones it can't (CLIP semantic search) become our moat.

All models downloaded lazily to `%LOCALAPPDATA%\Images\models` on first use. User can disable/delete.

- [ ] **V60-01** *P0* — **ONNX Runtime + DirectML foundation** — `Microsoft.ML.OnnxRuntime.DirectML`. Auto-detect DX12 GPU, fallback CPU. Model cache + download UI with progress. Forward path: Windows ML (same ORT APIs, auto-EP selection).
- [ ] **V60-02** *P0* — **CLIP semantic search** (KILLER FEATURE). `ElBruno.LocalEmbeddings.ImageEmbeddings` (Feb 2026, MIT). OpenCLIP ViT-B/32 ONNX ~300 MB. Embed all library images on ingest; store 512-d vectors in **sqlite-vec** table. Text query → encoder → cosine → ranked results. Windows Photos has NO text-to-image search — this is the single biggest moat.
- [ ] **V60-03** *P0* — **Face detection + recognition + clustering**. Pipeline: **YuNet** (detector, MIT) → **ArcFace/SFace** (recognizer, MIT upstream via Clearly.ML.Faces) → **FIQA gating** (digiKam 8.6 — FFT + Gaussian filters blurry training samples → +big accuracy). 512-d L2 embeddings → **HDBSCAN** (HdbscanSharp 3.0.1 w/ TensorPrimitives AVX) → "confirm these suggestions" UX (Picasa orange-dot). Write `MWG-rs:Regions` XMP so tags survive reinstall.
- [ ] **V60-04** *P0* — **Object detection auto-tagging**. YoloDotNet 4.2.0 MIT wrapper. YOLO-World or MIT-weighted older-gen to dodge Ultralytics AGPL weights trap. COCO 80 classes → tag sidebar. Cache per image hash.
- [ ] **V60-05** *P0* — **OCR-in-image indexing**. Default: `Windows.Media.Ocr` (zero deps, 25 langs, good-enough for screenshots). Optional: Tesseract 5 (100+ langs incl. Asian) + Sdcb.PaddleOCR (accuracy leader 94.5% on OmniDocBench). Index into SQLite FTS5 — "find my passport" search.
- [ ] **V60-06** *P0* — **Background removal**. Ship four models, let user pick by workload: **BiRefNet** (SOTA 2025, 1024²/2048²) for quality; **IS-Net** general-use (1024², 0.82 IoU) middle; **U²-Net** (320², 307 ms) fast; **silueta** 43 MB fastest fallback. ONNX Runtime + ImageSharp pre/post (rembg-web is the clean reference). Edge refinement via guided filter.
- [ ] **V60-07** *P1* — **AI upscaling**. Real-ESRGAN + OpenModelDB community models (General Photo, UltraSharp, Remacri, Ultramix Balanced, HFA2k, Real-ESRGAN Anime 6B). ONNX Runtime + DirectML, tile-wise inference (512² + 16 px overlap) to fit 4 GB VRAM. 2x and 4x; chain 4x→16x for posters.
- [ ] **V60-08** *P1* — **Auto-rotate** — scene classifier detects upside-down orientation.
- [ ] **V60-09** *P2* — **NIMA aesthetic quality score** — digiKam's Pick-label source. Surface "best of trip" auto-suggestions.
- [ ] **V60-10** *P2* — **Scene classification** — Places365 or ANSA-style multi-task. Feed into smart-album auto-creation.
- [ ] **V60-11** *P2* — **NSFW safety classifier** (opt-in) — open_nsfw2 ONNX, off by default.
- [ ] **V60-12** *P2* — **Generative Erase** (Windows Photos parity). Inpainting ONNX (LaMa or simpler). Paint mask → erase. Ship after 0.6 core lands.

---

## v0.7.0 — Plugin + extensibility (M, 3-4 weeks)

**Theme**: power-user extensibility without GPL contamination.

- [ ] **V70-01** *P0* — **Roslyn C# scripting plugin API**. `Microsoft.CodeAnalysis.CSharp.Scripting` + Westwind.Scripting (MIT) wrapper. User writes snippets against `IImageContext` host API. Sandbox: whitelist namespaces, restrict reflection, target framework.
- [ ] **V70-02** *P1* — **G'MIC shell-out** — bundle `gmic.exe` (CeCILL/LGPL, ships as exe = license-isolated). 640+ filters in plugin build, 4000+ CLI commands in 3.6. Stock set covers artistic effects, denoise (BM3D, DCT), sharpen, local contrast. Plugin pane lists filters, user picks + tweaks + applies.
- [ ] **V70-03** *P2* — **Adobe 8BF filter host** — PICA suites + FilterRecord struct implementation. Unlocks Nik Collection, Topaz legacy, every Photoshop filter ever shipped. Tricky (Paint.NET's PSFilterShim shows pattern).
- [ ] **V70-04** *P2* — **Explorer shell extension** — PSD/RAW/JXL/AVIF thumbnails in Explorer (Pictus pattern). Separate DLL, registers as IThumbnailProvider.

---

## v1.0.0 — Lightroom-class (XL, quarter+)

**Theme**: RAW development, panorama, HDR, color-managed wide gamut, LAN sync. The "real app" bar.

- [ ] **V100-01** *P0* — **Non-destructive edit stack**. JSON-serialised `EditOperation[]` in XMP sidecar. Full version history reconstructible. Apply-on-export pipeline. Virtual copies (fork develop without duplicating pixels — Lightroom pattern).
- [ ] **V100-02** *P0* — **RAW development pipeline** beyond LibRaw's "basic conversion". Demosaic (AHD/DCB/DHT/AMaZE) + WB + exposure + shadows/highlights + S-curve + clarity + lens correction (lensfun) + noise reduction (BM3D via G'MIC). Target RawTherapee parity.
- [ ] **V100-03** *P1* — **Panorama stitching** via bundled Hugin CLI chain (`align_image_stack` → `autooptimiser` → `hugin_executor` → `enblend`). All GPL, all shell-out, all license-isolated. UI: select N → preview → stitch.
- [ ] **V100-04** *P1* — **HDR merge** via bundled `enfuse` (Mertens-Kautz-Van Reeth exposure fusion, halo-free, no intermediate HDR file). RAW bracket set → LibRaw → linear float → enfuse → tone-mapped 16-bit output.
- [ ] **V100-05** *P1* — **Color management** — lcms2 (MIT) P/Invoke or Magick.NET profile conversion. Embed source ICC in exports. Wide-gamut display support (Windows 11 ICC compat helper opt-in).
- [ ] **V100-06** *P2* — **HDR display** (PQ/HLG) via SkiaSharp native HDR path or Direct2D interop swap chain. WPF itself doesn't render to HDR.
- [ ] **V100-07** *P2* — **Multi-instance LAN sync** (nomacs moat) — pan/zoom/image-send mirror between instances on same network. Per-client permissions. Single biggest unique feature in FOSS space.

---

## Dropped / won't-do (with reasons)

- **Paint.NET file format reuse** — Paint.NET is *source-available* not open-source; `PdnImage` cannot be redistributed. Writing our own `.pdn` reader is possible via pypdn reference, but low ROI. PSD via Aspose.PSD (commercial) or libpsd (MIT, limited) is the pragmatic path.
- **Ultralytics YOLO pretrained weights** — AGPL-3.0 for commercial use; would taint the binary. YOLO-World or older-gen weights + YoloDotNet (MIT) is the safe path.
- **InsightFace buffalo_l pack** — research-only. Use YuNet+ArcFace-original via Clearly.ML.Faces (MIT) or pay for commercial license.
- **Stable Diffusion inpainting** — possible via `stable-diffusion-onnx-ui` but 5+ GB weights, 30s+ per image, huge UX burden. Lightweight LaMa ONNX is the realistic "Generative Erase" path.
- **iccMAX HDR pipeline** — too bleeding-edge in 2026, tooling thin. Ship sRGB-correct first, iccMAX later.

---

## Library manifest (MIT/Apache-friendly unless noted)

**Canvas + codecs**
- `SkiaSharp` (MIT) — v0.2 canvas engine
- `Magick.NET-Q16-AnyCPU` (Apache-2) — current format coverage, keep
- `Sdcb.LibRaw` (MIT wrapper / LGPL native) — RAW
- libheif / libavif / libjxl bindings or native exes (MIT/BSD) — modern codecs

**Metadata**
- `MetadataExtractor` (Apache-2) — EXIF/IPTC/XMP read
- `ExifTool.exe` (Artistic — dual-licensed, use Artistic) — write path via shell-out
- `XmpCore` (BSD) — direct XMP ops

**AI / ML**
- `Microsoft.ML.OnnxRuntime.DirectML` (MIT) — inference runtime
- `ElBruno.LocalEmbeddings.ImageEmbeddings` (MIT) — CLIP
- `FaceAiSharp` (Apache-2) / `Clearly.ML.Faces` (MIT) / `FaceONNX` (MIT) — face pipeline
- `YoloDotNet` (MIT; careful with weights) — object detection
- `Sdcb.PaddleOCR` / `Tesseract` / `Windows.Media.Ocr` — OCR
- `HdbscanSharp` 3.0.1 (MIT) — face clustering
- `CoenM.ImageHash` (MIT) — dedup pHash/aHash/dHash

**Storage**
- `Microsoft.Data.Sqlite` (MIT) — catalog
- `sqlite-vec` (Apache-2/MIT) — vector search in SQLite

**UI controls**
- `PixiEditor.ColorPicker` (MIT) — color picker
- `PdfSharpCore` (MIT) — contact sheets / PDF export
- `WebView2` + Leaflet (BSD-2) — map pane
- `SharpCompress` (MIT) — archive browsing

**Scripting / plugins**
- `Microsoft.CodeAnalysis.CSharp.Scripting` (MIT) — Roslyn
- `Westwind.Scripting` (MIT) — Roslyn wrapper

**Bundled binaries (license-isolated via `CreateProcess`)**
- `jpegtran.exe` (libjpeg-turbo BSD) — lossless JPEG transforms
- `cjxl.exe` / `cjpegli.exe` (libjxl BSD) — JXL + better JPEG
- `cwebp.exe` / `dwebp.exe` (libwebp BSD) — WebP
- `avifenc.exe` (libavif BSD) — AVIF
- `MozJPEG cjpeg.exe` (BSD) — JPEG re-encode
- `OxiPNG.exe` (MIT) — lossless PNG
- `ECT.exe` (Apache-2) — PNG/JPEG/GIF/ZIP lossless
- `gifsicle.exe` (GPL — isolated via shell-out) — GIF optimize
- `gmic.exe` (CeCILL/LGPL — isolated via shell-out) — filter bus
- `hugin_executor.exe`, `enblend.exe`, `enfuse.exe`, `align_image_stack.exe` (GPL — isolated) — panorama + HDR

**Cannot vendor (GPL/AGPL) — ideas only**
- digiKam (GPL-2+), Converseen (GPL-3), Shotwell (LGPL-2.1), PhotoPrism (AGPL-3), Immich (AGPL-3), Upscayl (AGPL-3), chaiNNer (GPL-3), Hugin core GUI (GPL-2).

---

## Unique-differentiator checklist (what we build that nobody else has)

- [x] **Live inline rename** while viewing — ours, already shipped.
- [ ] **CLIP semantic text-to-image search** on local library — Windows Photos can't do this.
- [ ] **Squoosh-style visual-diff converter** on a Windows batch tool — nothing native offers this.
- [ ] **Multi-instance LAN pan/zoom sync** — nomacs only, and nomacs is GPL Qt.
- [ ] **Local exposure compensation with no modal** — JPEGView only.
- [ ] **Sketch-based fuzzy search** — digiKam only, and digiKam is GPL.
- [ ] **File Explorer sort-order sync** — ImageGlass only.
- [ ] **Images-in-archive browsing** (CBR/CBZ/ZIP/RAR/7Z) — Honeyview only, and discontinued.
- [ ] **C2PA Content Credentials on export** — nobody in the OSS viewer space does this yet.
- [ ] **Live byte-delta + SSIMULACRA2 readout during conversion** — Squoosh only, and Squoosh is web-only single-image.

---

## Notes on scoping / ordering

The phased order is deliberate:

1. **v0.2 (Foundations)** must land first — SkiaSharp + persistence + preload + format expansion. Everything downstream depends on it.
2. **v0.3 (Editor)** and **v0.5 (Converter)** both ride on v0.2; can be developed in parallel by different passes once v0.2 is in.
3. **v0.4 (Organizer)** needs v0.2's SQLite foundation; the AI-powered parts of the organizer (faces, CLIP) wait for v0.6.
4. **v0.6 (AI)** is the strategic differentiator. CLIP semantic search alone is worth more than all of v0.3+v0.5 combined from a marketing perspective. Doing it after v0.4 means we have an organizer to search *into*.
5. **v0.7 (Plugins)** is low-urgency polish — ship when core is stable.
6. **v1.0** is a north star, not a commitment — RAW development, panorama, HDR, LAN sync each a big-L item on their own.

Adjacent cleanup that falls out naturally:
- Keyboard-shortcut surface documented in-app (v0.1.2 backlog item — resurface in v0.2 settings UI).
- Optional Catppuccin Latte light theme (v0.2 settings UI can land it for free).
- DPI-aware screenshot recapture (v0.1.2 V02-04) — fold into each phase's release smoke.
