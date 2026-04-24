# Images — Research: Organizers, DAMs & Batch Converters

Research pass dated 2026-04-24. License-aware survey for the C# / .NET 9 / WPF stack.

## Organizers / DAMs

### digiKam 8.6 (GPL-2+, Qt/C++)
Gold-standard FOSS photo DAM. **Four specialized SQLite/MySQL/MariaDB databases** (core, thumbnails in PGF-wavelet compression, `recognition.db`, `similarity.db`). Duplicate detection uses **Haar wavelet signatures** (Fast Multi-Resolution Image Querying paper); 7.3.0 lock-free multithreaded. 8.6 rewrote face recognition: **YuNet detector + SFace recognizer** RGB pipeline 25-50% faster, **cross-validating KNN + SVM ensemble**, **FIQA** (Face Image Quality Assessment) filtering blurry training samples via FFT + Gaussian. **Sketch-based fuzzy search**, GPU via OpenCL/OpenCV DNN, **Light Table** compare, versioned non-destructive edits, 40+ editor plugins, G'MIC-Qt, exports to Dropbox/Flickr/etc. **License blocker**: GPL — ideas only, no vendoring.

### Shotwell 0.32 (LGPL-2.1, Vala)
Simple GNOME organizer. Key idea: **Events** — date-based auto-clusters with "key photo" thumbnail; default view is reverse-chronological. Face tagging uses OpenCV cascade by default, optional `res10_300x300_ssd_iter_140000_fp16` DNN side-loaded due to licensing. Weak at 10K+ libraries — cautionary scaling tale.

### XnView MP (freeware private, Qt)
Dual-pane browser + **Categories** (hierarchical keywords) + **Category Sets** (saveable context layouts — switch "Wedding+Portraits" → "Sports+Action+Portraits" in one click). Shortcut-per-category tagging ("P"=Portrait). Single `xnview.db` catalog; recommends exporting categories to normalized IPTC/XMP for portability. Dockable IPTC-IIM/XMP/EXIF editors.

### Daminion Standalone (freeware, Windows)
**Enterprise bar** — scales to 500K-1M assets per catalog, 150+ formats. Unlimited-depth hierarchical tags, **faceted search**, near-duplicate detection, AI auto-tagging + facial recognition (paid server), OCR-in-image + speech-to-text. Catalog-only, no editor; integrates via context menu. **Uniquely portable**: exports hierarchical keywords to plain text, always round-trips tags into IPTC/XMP/EXIF on the file itself.

### Apple Photos (closed, on-device)
Reference for **privacy-preserving ML**. **Apple Neural Scene Analyzer (ANSA)** — multi-task backbone for scene classification, object detection, People & Pets, dedup, wallpaper suggestions — all client-side. **On-device knowledge graph** of people/places/trips/events drives Memories. **Differential privacy** crowd-learns iconic scenes across 4.5M location-category pairs without identifiable uploads. iOS 18.1 added text-prompt Memory Movies.

### Google Photos (closed, cloud)
Face clusters ~80-85% accuracy, object search, **OCR indexed across entire library** (search "license plate"), auto-generated Memories, **Photo Stacks**, auto-sorted categories (screenshots/receipts/notes). April 2026 Nano Banana 2 added conversational edits with **C2PA Content Credentials**.

### Picasa (legacy reference)
2002-2016 consumer DAM. Four-pane People/Places/Tags/Properties model. Face tags via `Tools → Experimental → Write faces to XMP` — migration escape hatch still used today via Jeffrey Friedl's LR plugin. Worth studying its "store name tags in photo" opinionated default.

### Lightroom Classic 15.3 (commercial reference)
**Catalog semantics**: `.lrcat` SQLite is authoritative; XMP sidecar is partial export (stacks/collections/smart-collections/virtual-copies **don't** round-trip). **Collection Sets** nest hierarchically; Collections and Smart Collections cannot nest in each other. **Virtual Copies** fork develop settings without duplicating pixels.

### Adobe Bridge 15.x (free with Adobe ID)
**Multi-token batch rename** (filename + seq# padded + date/time + folder + metadata + custom, live preview, "Preserve Current Filename in XMP"). **Metadata templates** applied across hundreds of files. **Output workspace** generates PDF contact sheets with header/footer/watermark.

### PhotoPrism (AGPL-3, Go)
Self-hosted AI library. TensorFlow for labels, optional Ollama/OpenAI for captions. Automatic **stacking** by filename/time/location/unique ID. Interactive world map, reverse geocoding privacy-preserving. Supports 900 MP images. **License blocker**: AGPL.

### Immich (AGPL-3, TS/Python)
Self-hosted Google Photos. **OpenAI CLIP** (ViT-B/32 default ~300 MB) for natural-language semantic search, backed by Postgres pgvector. **Face recognition with InsightFace `buffalo_l`** or lighter `buffalo_s`. **DBSCAN clustering** with tunable distance (0.3-0.7) and core-point min. Duplicate distance slider 0.001-0.1. **License blocker**: AGPL.

## Batch Converters

### XnConvert 1.106 (freeware, multiplatform)
**Reference implementation** — 80+ chainable operations, 500+ input / ~70 output formats. Categories: Metadata, Transforms (resize/crop/rotate/canvas/flip), Adjustments (bright/contrast/gamma/saturation/HSL/auto-levels/depth/DPI), Filters (blur/sharpen/emboss/relief/noise), Effects (vignette/mask/text+image watermarks/sepia/oil paint), Output (prefix/suffix/number/date tokens, EXIF/IPTC/XMP preservation). **Watch-folder** auto-apply. Actions → **presets**. Tab workflow (input → actions → output → settings). CPU-core throttle. CLI twin: **NConvert**.

### ImageMagick 7.1.1 / `magick mogrify` (Apache-style)
Scripting reference; native engine under Magick.NET + Converseen. 2026 delegates: heic, jp2, jpeg, jxl, lcms, png, raw, tiff, webp, zstd. Canonical batch: `magick mogrify -format avif -depth 10 -define heic:speed=2 -path out/ *.jpg`. Without `-format` or `-path`, **overwrites originals** — design around this footgun. Mogrify can't use multi-image operators (`-fx`, `-composite`, `-append`, `-layers`); those need per-file `magick` calls.

### Converseen 0.15.2 (GPL-3, Qt + Magick++)
Simpler GUI over ImageMagick. 100+ formats (DPX/EXR/HEIC/SVG). Percent/pixel resize with per-image aspect lock, transparent-background color fill, bulk prefix/suffix/number rename, PDF page→image. **License blocker**: GPL-3 — patterns only.

### FFmpeg 8.0 (LGPL/GPL)
HEIC/video thumbnails, GIF creation, animated WebP/AVIF. Required in FileOptimizer chain.

### Modern Codec CLIs
- **cwebp** (libwebp 1.6.0): `-q 80 -m 6`, `-lossless`, `-preset photo|icon`, `-z 9`. Sweet-spot q=80-85.
- **cjxl** (libjxl): `-q 70 -e 7`; `--brotli_effort 11` for lossless. Sweet-spot q=63-73.
- **avifenc** (libavif + aom): `--speed 0-10`, `-a cq-level=0-63`, `tune=ssim|butteraugli`, `-y 444/422/420`, `-d 10`. Sweet-spot cq=32 for CDN.
- **cjpeg/MozJPEG**: `-quality 80 -quant-table 2 -tune-ms-ssim -arithmetic -progressive`. Sweet-spot q=65-76.
- **cjpegli** (Google, in libjxl): `-q 80 --chroma_subsampling=420` — strictly better than MozJPEG at the same quality score.

### Squoosh (Apache-2, GoogleChromeLabs, WASM)
**Gold-standard interactive compression.** WASM MozJPEG + OxiPNG + libwebp + avifenc + cjxl + QOI with **real-time draggable split-pane preview** and live byte-delta readout. Quality/effort sliders, palette reduction, resize. Limitation: single-image — no batch. Apache-2 — **reusable**.

### FileOptimizer 17.x (freeware)
Lossless **chain orchestrator**. Runs each file through every relevant optimizer, keeps smallest. 2026 chain: **ECT 0.9.5** + **OxiPNG 9.1.5** + pngquant + pingo + Leanify for PNG; pingo + Guetzli + **cjpegli** + ImageMagick for JPEG; **gifsicle**; FLAC/OptiVorbis/mkclean/ffmpeg for A/V; Ghostscript/cpdf/mutool for PDF. Sends originals to Recycle Bin for rollback.

### Magick.NET 14.12 (Apache-2 — we already use)
Full format coverage via Magick. **SixLabors.ImageSharp** (Apache-2 OSS / commercial license) is managed-only, thinner format list. **SkiaSharp** (MIT) fastest but narrow. **GraphicsMagick** has best AVIF/HEIF/JXL + OpenMP.

---

## Features Worth Stealing (grouped — **bold = priority**)

### 1. Cataloging
- **SQLite catalog** with separate DBs per concern (digiKam's four-DB split prevents `VACUUM` on the thumbs blob locking metadata panel).
- **Watched folders** + incremental scan + `FileSystemWatcher` (XnConvert-style) for auto-apply presets.
- **Sidecar `.xmp`** alongside originals (darktable/digiKam naming) so metadata survives catalog deletion; always round-trip to embedded IPTC/XMP/EXIF (Daminion portability guarantee).
- Multi-root library: local + removable + network; prompt when a root goes offline, don't delete records.
- **File-move-safe identity** via hash-based asset ID (Lightroom `id_global` UUID, not path-based).
- Catalog portability: export hierarchical keywords to plain text.

### 2. Tagging & Metadata
- **Hierarchical keywords** unlimited nesting (Daminion, Bridge, Lightroom).
- **Color labels + 1-5 stars + pick/reject flags** (digiKam's three-axis).
- **Metadata templates** — IPTC copyright/creator/contact blocks applied in one click (Bridge).
- Keyword autocomplete with incremental search.
- **Keyboard-shortcut-per-tag** for triage speed ("P"=Portrait).
- **Category Sets** — switch tag-panel layout per job type.
- **Batch metadata write** — select N → apply template → atomic write + one undo.

### 3. Faces / People
- **Face detection via YuNet** + **recognition via SFace/ArcFace** (digiKam 8.6 pipeline — in OpenCV DNN, ONNX, or **FaceONNX** / **FaceAiSharp** — both MIT, vendorable).
- **FIQA gating** — reject blurry/small/noisy faces via FFT + Gaussian (digiKam 8.6 accuracy win).
- **DBSCAN clustering** configurable distance 0.3-0.7 + min core-points (Immich pattern).
- **People pages** with hero thumbnail + "confirm these suggestions" workflow (Picasa's orange-dot still best).
- **Write name tags to XMP** (`MWG-rs:Regions`) for portability.
- Pets as parallel cluster type (Apple Photos 2023+).

### 4. Similarity & Dedup
- **Perceptual hashing** — pHash (DCT) or Haar wavelet (digiKam). **`CoenM.ImageHash` MIT** .NET.
- **Fuzzy-slider duplicate threshold** with live "N pairs detected" counter (Immich 0.001-0.1).
- **Near-duplicate stacks** — auto-stack by time+location+hash (PhotoPrism, Google Photos).
- **Reference strategy**: "Older or Larger", "Prefer selected folder", "Prefer newer" (digiKam 8.1+).
- **Sketch-based fuzzy search** — draw rough shape/color, find matches (digiKam's Sketch tab — delightful).
- Lock-free multithreaded dedup, iterator-range work-stealing (digiKam 7.3 pattern).

### 5. Geotagging
- **EXIF GPS read/write** (ExifTool or MetadataExtractor — both MIT).
- **Interactive map pane** w/ clustering at zoom (digiKam + PhotoPrism). Leaflet via WebView2 is cheapest for WPF.
- **Reverse geocoding** via local offline DB (privacy — digiKam bundles OpenStreetMap/GeoNames CC-BY).
- **GPX track-log sync** — match photo timestamps to GPS track, back-fill EXIF.

### 6. Smart Albums / Saved Searches
- **Criteria builder**: date, rating, label, keyword, camera, lens, ISO, geo bbox, face count (Lightroom Smart Collections).
- **Dynamic vs static collections** — static manual, smart re-evaluates. Only Collection Sets nest.
- **Auto-albums by pattern**: screenshots, receipts, notes, docs (Google Photos OCR+shape auto-sort).
- **Trip detection**: contiguous days + distance > threshold from home (Apple Photos Memories engine).
- Time-lapse grouping — bursts / 1-sec intervals.

### 7. AI Features
- **CLIP semantic search** — OpenCLIP ViT-B/32 ONNX local, ~300 MB, embed on ingest. **sqlite-vec** for persistence (or in-memory cosine for <10k).
- **Object detection** — YOLOv8-nano ONNX for objects/pets/food (trivial in ONNX Runtime).
- **OCR-in-image indexing** — `Windows.Media.Ocr` free built-in, Tesseract 5 cross-platform, index to SQLite FTS5.
- Scene classification — Places365 or ANSA-style multi-task.
- NSFW safety classifier (toggle) — open_nsfw2 ONNX, off by default.
- Aesthetic/quality score — digiKam Pick via NIMA.
- Auto-rotate — detect upside-down via scene classifier.

### 8. Batch Conversion
- **Output formats**: JPEG (MozJPEG + cjpegli), PNG (OxiPNG), WebP, AVIF, JXL, HEIC, TIFF, BMP, GIF, PSD read, RAW read (LibRaw), PDF split/merge. Magick.NET core + libheif/libjxl bindings for finer quality control.
- **Resize policies**: percent, px, long-edge, short-edge, canvas-fit, canvas-fill, DPI-only.
- **Operation chain**: drag-orderable list with per-op enable/disable (XnConvert tab 2).
- **Drag-to-target**: drop folder → convert w/ last-used preset.
- **Watch folders** + auto-apply.
- **Presets** saveable/nameable/import/export.
- **Dry-run preview** — single-image preview of full chain before batch (XnConvert real-time + Squoosh split-slider).
- **Strip metadata** toggle per output with granularity (strip all / keep GPS / keep copyright).
- **Rename tokens**: `{name}_{date:yyyy-MM-dd}_{seq:000}_{exif.iso}` (Bridge token engine).
- Watermark: text + image, opacity/position/rotation/tile.
- **Overwrite vs new-folder guardrails** — refuse to overwrite originals without confirm.
- CPU-core throttle slider.

### 9. Compression Pipelines
- **Lossless re-pack chain** per format:
  - PNG: OxiPNG → ECT → pngquant (opt-in lossy) — keep smallest.
  - JPEG: jpegtran-optimize → jpegoptim → MozJPEG re-encode (opt-in).
  - GIF: gifsicle `-O3`.
  - WebP/AVIF/JXL: re-encode max effort (`-m 6`, `--speed 0`, `-e 9`).
- **Lossy sliders with visual diff** — Squoosh draggable split + byte-delta readout. Single most impactful polish item.
- **"Best-of" mode** — N encoders in parallel, pick smallest under target SSIMULACRA2 (FileOptimizer philosophy).
- **Send originals to Recycle Bin on replace**.
- **SSIMULACRA2 or Butteraugli** quality metric alongside raw slider (2026 codec-comparison standard).

### 10. Exports
- **Export presets**: Web 1920, Instagram 1080, Email 2MB, Print 300 DPI (Lightroom Publish Services).
- **Social-size presets** with platform-accurate aspect + quality.
- **Contact sheets** → PDF: grid, header/footer, metadata captions (Bridge Output).
- **Web gallery** — static HTML + thumbs + lightbox (digiKam HTMLGallery).
- **Print layout** — multi-image/page with margins + alignment.
- **Direct publish** to Flickr/Imgur/Pinterest/Dropbox/OneDrive/SMB (digiKam KIPI — OAuth and done).
- **C2PA Content Credentials** on export — stamp "edited with Images v0.x" + operations for provenance. Becoming table stakes in 2026.

---

## License Cheat Sheet for C#/.NET 9 Vendoring

### Directly vendorable (MIT/Apache/BSD)
- `Magick.NET` (Apache-2) — already in; v14.12
- `SixLabors.ImageSharp` 3.x (Apache-2 / commercial dual) — pure-managed fallback
- `SkiaSharp` (MIT) — fast viewer rendering
- `MetadataExtractor` (Apache-2) — EXIF/IPTC/XMP read
- `ExifTool` binary (Artistic/GPL dual — use Artistic) — write metadata via child process
- `FaceAiSharp` (Apache-2) / `FaceONNX` (MIT) / `Clearly.ML.Faces` (MIT) — face detect+embed
- `Microsoft.ML.OnnxRuntime` (MIT) — CLIP/YOLO/Places365
- `CoenM.ImageHash` (MIT) — pHash/aHash/dHash
- `sqlite-vec` (Apache-2/MIT) — vector similarity in SQLite
- Squoosh codec WASM (Apache-2) — re-wrap, or native binaries (MozJPEG/cjxl/avifenc/cwebp — all BSD/Apache)
- `Leaflet` (BSD-2) in WebView2

### Ideas only (cannot vendor)
- digiKam (GPL-2+), Converseen (GPL-3), Shotwell (LGPL-2.1), PhotoPrism (AGPL-3), Immich (AGPL-3).

### Binary-chain callable (license-isolated via `CreateProcess`)
- FileOptimizer chain: ECT, OxiPNG, pingo, gifsicle (GPL), ffmpeg (LGPL/GPL), Leanify (MIT).

## Sources
- [digiKam](https://www.digikam.org/about/features/), [8.6 release](https://www.digikam.org/news/2025-03-15-8.6.0_release_announcement/)
- [Lock-free dedup in digiKam 7.3](https://brunoabinader.github.io/2022/08/07/lock-free-multithreaded-find-duplicates-in-digikam-7.3.0/)
- [XnConvert](https://www.xnview.com/en/xnconvert/), [XnView cataloging wiki](https://www.xnview.com/wiki/index.php/Cataloging_Features_in_XnView_MP)
- [PhotoPrism](https://www.photoprism.app/features), [Immich](https://docs.immich.app/administration/system-settings/)
- [Daminion](https://daminion.net/features/)
- [Apple ANSA](https://machinelearning.apple.com/research/on-device-scene-analysis)
- [Lightroom Collections](https://helpx.adobe.com/lightroom-classic/help/photo-collections.html)
- [Adobe Bridge Batch Rename](https://helpx.adobe.com/bridge/desktop/organize-and-find-files/tag-and-find-files/batch-rename-files.html)
- [ImageMagick Mogrify](https://imagemagick.org/script/mogrify.php)
- [Converseen](https://github.com/Faster3ck/Converseen)
- [FileOptimizer](https://nikkhokkho.sourceforge.io/?page=FileOptimizer)
- [cwebp](https://developers.google.com/speed/webp/docs/cwebp), [MozJPEG](https://github.com/mozilla/mozjpeg)
- [Squoosh](https://github.com/GoogleChromeLabs/squoosh)
- [Image codec comparison 2026 (Gianni Rosato)](https://giannirosato.com/blog/post/image-comparison/)
