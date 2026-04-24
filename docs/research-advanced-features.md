# Images — Research: Advanced Features, AI Tooling, Editor Capabilities

Research pass dated 2026-04-24. Target stack: C# / .NET 9 / WPF on Windows. Goal: extend the basic viewer (WIC + Magick.NET, zoom/pan/rotate, inline rename) into a powerful viewer + editor + organizer + converter. OSS / freeware bias.

Legend: effort **S** (<1 wk) · **M** (1-3 wk) · **L** (1-2 mo) · **XL** (quarter+). Friendly = MIT/Apache/BSD and drop-in from NuGet. Hostile = GPL/AGPL or CLI shell-out.

---

## 1. AI Upscaling

Real-ESRGAN remains the OSS gold standard. Upscayl v2.15.0 (AGPL-3.0, Dec 2024) ships six models — General Photo (Real-ESRGAN), UltraSharp, Remacri, Ultramix Balanced, High Fidelity (HFA2k), plus Real-ESRGAN Anime 6B. Upscayl requires a Vulkan-capable GPU (GTX 1060 / Vega / Intel Arc class, 6 GB VRAM recommended); CPU and most iGPUs are unsupported. Backend is `upscayl-ncnn`, a fork of Real-ESRGAN-ncnn-vulkan using NCNN + Vulkan — no Python, no CUDA.

For .NET we do not need Vulkan/NCNN — we can run the same Real-ESRGAN weights as ONNX via **ONNX Runtime + DirectML EP** (all DX12 GPUs: NVIDIA/AMD/Intel/Qualcomm) or **CUDA EP** on NVIDIA. DirectML is in maintenance mode but still shipping; Microsoft's forward path is **Windows ML** (same ONNX Runtime APIs, auto-selects EP at runtime). The anime-6B variant uses `nf=64, nb=6` and is ~4x lighter on VRAM than the default 23-block model. `RealESRGAN_x4plus` ONNX needs opset 18 for the anti-aliased Resize op. Tile the input (e.g. 512×512 with 16 px overlap) to fit 4 GB VRAM.

**chaiNNer** (GPL-3.0) is the node-based "blender" for upscaling workflows — supports PyTorch, NCNN, ONNX, and TensorRT via the Spandrel architecture detector (ESRGAN, SPAN, OmniSR, RealCUGAN, etc.). Useful as a model-conversion helper, not as a runtime. **OpenModelDB** hosts 1000+ community-trained models (photo, anime, scans, text, JPEG artifacts).

**Integration**: `Microsoft.ML.OnnxRuntime.DirectML` NuGet + tile-wise inference. Models download lazily to `%LOCALAPPDATA%\Images\models`. Known gotcha: OnnxRuntime-DirectML in C# has had x86/x64 build quirks — test both.

**Effort**: **M** for single-model 4x integration; **L** for model picker + tiling + progress + chaining (4x → 16x).

---

## 2. Background Removal

Three generations of models, all ONNX-friendly, all already proven in `rembg` (Apache-2.0, danielgatis/rembg):

| Model | Size | Input | IoU (DIS5K+Humans avg) | Inference (small GPU) |
|---|---|---|---|---|
| **U²-Net** | 176 MB | 320² | 0.39–0.89 (wildly varies) | 307 ms |
| **IS-Net general-use** | 179 MB | 1024² | 0.82 | 351 ms |
| **BiRefNet general / HR** | 973 MB | 1024² / 2048² | **0.87** (SOTA 2025) | 821 ms / 17 FPS on RTX 4090 FP16 |
| silueta | 43 MB | 320² | — | fastest |
| u2net_human_seg / cloth_seg | 176 MB | 320² | specialized | — |
| bria-rmbg | proprietary weights | — | SOTA commercial | commercial license |

BiRefNet is the default pick for quality (hair, fur, transparent glass, cluttered bg). U²-Net/IS-Net stay on the menu as fast fallbacks. Ship *all four* — let user pick by workload. `BiRefNet_HR` trained on 2048² handles poster-size images; `BiRefNet_dynamic` (Mar 2025) accepts any resolution from 256 to 2304.

No mature C#/.NET port of rembg exists. Easiest path is to re-implement the pre/post in C#: ImageNet mean/std normalization, sigmoid the mask, alpha-composite. **rembg-web** (TypeScript) is the cleanest reference for session classes. Critical: use 320² for u2net-family, **1024² for IS-Net/BiRefNet** or ORT throws `InvalidArgument`. Post-refine edges with guided filter or `BiRefNet`'s `refine_foreground` (8x faster with the Jun 2025 GPU rewrite).

**Integration**: ONNX Runtime + ImageSharp for pre/post. Weights from HuggingFace `ZhengPeng7/BiRefNet` or `tomjackson2023/rembg`.

**Effort**: **M** for one model; **L** for all four + edge refinement + alpha matting fallback.

---

## 3. Face Detection + Recognition

Three production-ready .NET libraries — all ONNX-based:

- **FaceAiSharp** (MIT, NuGet `FaceAiSharp` + `FaceAiSharp.Models.ArcFace.LResNet100E-IR` or `-int8` quantized at ~99 MB). Ships InsightFace's **SCRFD-2.5G-KPS** detector + 5-point landmarks + ArcFace 512-d embeddings + eye-state detector. .NET Standard 2.0.
- **FaceONNX** (GitHub `FaceONNX/FaceONNX`) — fuller analytics suite (age, gender, emotion, race, beauty) plus detection and recognition. MIT license on the wrapper.
- **Clearly.ML.Faces** (MIT) — YuNet detector + ArcFace, both **MIT upstream** — the only option that's cleanly commercial-safe. Depends on `Microsoft.ML.OnnxRuntime` 1.17.1+ and SFML.NET.

**Licensing trap**: InsightFace's *code* is MIT but its *pretrained models* (buffalo_l pack) are research-only. For commercial products use YuNet+ArcFace-original via Clearly.ML.Faces or contact `recognition-oss-pack@insightface.ai`.

ArcFace outputs 512-d L2-normalized vectors — cosine distance is the native metric. Raw input 112×112 aligned via landmark affine warp; that alignment step is the biggest accuracy lever.

**Clustering (face-groups-by-person)**: `HdbscanSharp` 3.0.1 (.NET 8+, uses `TensorPrimitives` for AVX acceleration) with `GenericCosineSimilarity` is the best fit — auto-selects cluster count, robust to outliers. `Dbscan` (viceroypenguin) 3.0.0 with R-Tree spatial index is the O(n log n) alternative when photo count passes ~100k. Reduce 512-d → 128-d via PCA first; HDBSCAN degrades in very high dimensions. Sources note **Chinese Whispers** actually beats both on face-clustering benchmarks — worth a prototype.

**Effort**: **M** detect+recognize+person-gallery; **L** full DBSCAN clustering + "This is Mom" UX.

---

## 4. Semantic Search (CLIP)

CLIP (OpenAI, MIT) gives you text→image and image→image search in a shared 512-d embedding space. **`ElBruno.LocalEmbeddings.ImageEmbeddings`** (Feb 2026) is the current best .NET wrapper — two ONNX models (image encoder + text encoder), fully local via ONNX Runtime. Cosine similarity = done. Bart Broere's 2023 bare-metal C# example shows the raw `Microsoft.ML.OnnxRuntime` + `SixLabors.ImageSharp` plumbing if you want no wrapper.

**OpenCLIP** goes bigger — ViT-L/14, ViT-G/14, DFN5B — up to 4x accuracy lift over the OpenAI ViT-B/32 baseline. Export to ONNX, drop into the same .NET pipeline.

**Vector store**: for desktop, **LanceDB** is the architecturally-correct pick ("SQLite of vector DBs", embedded, zero-config, scales to millions on disk), but **has no first-party C# SDK** — Rust interop only. Practical picks for a .NET-native desktop app:
- **sqlite-vec** extension → attach to existing SQLite DB. Simplest.
- **LM-Kit.NET** `IVectorStore` with built-in file-based engine (commercial EULA but free for small use).
- **Qdrant** local server — best perf (HNSW, 20-30 ms queries at 95% R@1) but adds a process.
- **In-memory cosine** — fine up to ~10k images.

**Effort**: **M** for CLIP + in-memory cosine search; **L** for sqlite-vec persistence + batch indexing + UI.

---

## 5. OCR (Text-in-Image)

Three viable engines:

| | License | Langs | Complex layout | .NET bridge |
|---|---|---|---|---|
| **Windows.Media.Ocr** | Windows OS | ~25 | Moderate | Native WinRT, zero deps |
| **Tesseract 5 (LSTM)** | Apache-2.0 | 100+ | Weak | `Tesseract` NuGet (C++ interop) |
| **PaddleOCR / PP-OCRv5 / VL-1.5** | Apache-2.0 | 109 | **Strong** (tables, curves, rotation) | `Sdcb.PaddleInference` + `Sdcb.PaddleOCR` |

`Windows.Media.Ocr` is the right default — zero dependency, ships in Windows 10/11, good-enough for screenshot text. Tesseract bolts on 100+ languages including Asian scripts — ship traineddata next to app. PaddleOCR is the accuracy leader (PP-OCRv5 +13% over prior gen; PaddleOCR-VL-1.5 Jan 2026 hits 94.5% on OmniDocBench v1.5) and handles rotated/curved/skewed text; drawback is the Paddle runtime is ~300 MB.

**Pattern**: ship Windows.Media.Ocr for "copy text from screenshot"; make Tesseract/PaddleOCR optional plug-ins.

**Effort**: **S** for Windows.Media.Ocr; **M** to add Tesseract; **M** for Sdcb.PaddleOCR.

---

## 6. Object Detection Tagging

**YoloDotNet 4.2.0** (MIT) is the current leader — supports YOLOv5u through YOLOv26, YOLO-World, YOLO-E, RT-DETR. Pluggable EPs via separate NuGets: `.Cpu / .Cuda / .OpenVino / .CoreML / .DirectML`. Built on ONNX Runtime + SkiaSharp, no OpenCV. Does Classification, Detection, OBB, Segmentation, Pose. **YoloSharp / YoloV8 5.3.0** (dme-compunet) is the simpler alternative — `new YoloPredictor("model.onnx").Detect("image.jpg")` one-liner.

**License trap**: YoloDotNet is MIT, but **Ultralytics' YOLO weights are AGPL-3.0** for commercial use. For open-weight detection, stick to YOLO-World or older pre-v5 MIT weights, or pay for Ultralytics commercial license.

Target use: auto-tag photos with COCO 80 classes (cat/dog/car/person/…) → power a faceted sidebar. Run at import time, cache tags per image hash.

**Effort**: **M** — standard YOLO pipeline, COCO classes are baked in.

---

## 7. Non-Destructive Edits with XMP Sidecars

**darktable** is the reference OSS implementation — every edit stored in `image.ext.xmp` alongside the RAW, synchronized automatically, full edit history reconstructible. RawTherapee uses `.pp3` (not XMP) and cross-vendor XMP interop is notoriously unreliable — even Adobe↔darktable loses most edits beyond crop.

For our own implementation: write `<basename>.<ext>.xmp` (matches darktable/digiKam naming), namespace edits under a custom RDF namespace (`xmlns:imv="http://maven.imaging/1.0/"`), store a JSON payload of the edit stack in a single `<imv:edits>` element, and optionally read darktable's tags for import round-trips.

**Libraries**: `MetadataExtractor` (MIT) for reading, `XmpCore` or shell-out to ExifTool for writing. ExifTool handles namespace priority, rationals, `xml:lang` alternatives correctly — write operations are safer via shell-out.

**Effort**: **M** — edit-stack schema + apply-on-export pipeline is the real work.

---

## 8. RAW Development

**Sdcb.LibRaw 0.21.1.7** (MIT wrapper; LGPL-2.1 / CDDL-1.0 native) is the actively-maintained .NET wrapper. High-level `RawContext` API, pre-compiled vcpkg binaries for Windows/Linux. `DcrawProcess` with camera white balance / gamma / brightness / interpolation (AHD / DCB / DHT / X-Trans demosaic). `SharpLibraw` is the lower-level P/Invoke alternative.

Caveat from LibRaw upstream: *"LibRaw offers some basic RAW conversion … not production-quality rendering."* For a Lightroom-class pipeline you'd still want to layer on your own WB, exposure, highlights/shadows, curves, lens correction, noise reduction. **LibRaw-demosaic-pack-GPL3** adds AMaZE and LMMSE demosaicers but taints the binary with GPL.

Realistic MVP: demosaic + WB + exposure + shadow/highlight + S-curve + clarity. Ship RawTherapee-quality later.

**Effort**: **L** — demosaic is free; the development pipeline is the work.

---

## 9. Color Management (ICC / HDR)

**Little CMS 2 (lcms2)** (MIT) is the universal ICC engine — ICC v4.4, all rendering intents, gamut mapping. No first-class .NET wrapper found; P/Invoke `lcms2.dll` or use Magick.NET's built-in `ColorProfile` (we already ship Magick.NET 14.12.0).

Critical Windows 11 gotcha: **by default, ICC-profile-based apps are clipped to sRGB even on wide-gamut displays** (since system-level Advanced Color now handles color management). To get the full display gamut, opt in to the Windows 11 **ICC compatibility helper** per-app — this still leaves you SDR-bound; HDR requires going HDR-native (DirectX 12 / Direct2D swap chain).

WPF itself does not render to HDR. For HDR display mapping you'd do one of: Direct2D interop swap chain, D3D11 image host, or SkiaSharp (uses native Skia which can render to scRGB/HDR10). **iccMAX** (ICC v5) is the emerging standard — floating-point PCS, native HDR semantics — but tooling support is thin.

**MVP plan**: `Magick.NET` profile conversion to sRGB for display; embed source ICC in exports; HDR pipeline as **XL** follow-up via SkiaSharp or Direct2D interop.

**Effort**: **S** basic ICC-to-sRGB on display; **L** full wide-gamut; **XL** HDR PQ/HLG.

---

## 10. Annotation Tools

Reference set: **ShareX** (GPL-3.0) — arrows, text, boxes, numbered step-callouts, blur/pixelate redact, obfuscation, freehand. **Greenshot** (GPL-1.0) — minimalistic version of same. **Flameshot** — cross-platform. All are GPL — no code reuse.

The annotations themselves are ~800 lines of SkiaSharp/WPF primitives: draw Bezier arrows, textboxes, shapes on an overlay layer, plus a box-pixelate filter for redaction. The differentiator vs built-in Snipping Tool is *numbered step callouts* (auto-increment) and *drop-shadow-on-arrow* — both straightforward.

**Effort**: **M** — own implementation is fastest, avoids GPL taint.

---

## 11. Lossless JPEG Transforms

`libjpeg-turbo`'s `jpegtran` does rotate / flip / crop without re-encoding DCT coefficients. Hard constraints — upper-left crop must fall on an iMCU boundary (typically 8 or 16 px) else it's silently snapped; dimensions non-divisible by MCU size can't losslessly rotate and will be trimmed.

.NET options: **Aurigma Graphics Mill** `LosslessJpeg.WriteRotated(..., RotateFlipType.Rotate90FlipNone)` and `WriteCropped(..., Rectangle)` — the only managed option but commercial. Alternative: bundle `jpegtran.exe` (~200 KB) and `Process.Start` it — zero interop, BSD-like libjpeg-turbo license. Third option: P/Invoke `turbojpeg.dll` — most work, best perf.

**Effort**: **S** — shell-out jpegtran.exe wins. Show a confirmation dialog when MCU-alignment forces a crop.

---

## 12. Panorama Stitching

**Hugin 2025.0.1** (GPL-2.0, Dec 2025) orchestrates a pipeline of CLI tools, all shippable independently:
- `align_image_stack` — automated stack alignment
- `autooptimiser` — pairwise optimizer (no manual seed placement)
- `nona` — remapper with photometric corrections
- `enblend` 4.0 — multi-resolution spline seam blending
- `enfuse` 4.0 — Mertens-Kautz-Van Reeth exposure fusion (no HDR intermediate)
- `hugin_executor` — headless `.pto` stitcher
- `tca_correct` / `fulla` — chromatic aberration + barrel distortion

All GPL — **bundle as CLI, don't link**. Workflow: user selects N images → `align_image_stack` → `autooptimiser` → `hugin_executor` → `enblend` output. For disk pressure, redirect `TMP`/`TEMP` to a fast SSD — enblend swaps to disk aggressively.

**Effort**: **L** — orchestration is the work; UI (preview, control points, reprojection) is most of it.

---

## 13. HDR Merge

Two paths, both OSS:
- **`enfuse` exposure fusion** — no intermediate HDR file, halo-free, trivially scriptable. Three criteria: exposure (luminance centered), saturation (favor highly saturated), contrast (favor high local stddev). This is the pragmatic pick.
- **True HDR tone-map pipeline** — merge to 32-bit float linear TIFF via `hugin_executor` + enblend; tone-map via Mantiuk/Reinhard/Drago operators. libHDR / libpfs under GPL.

Ship enfuse first. For RAW-based HDR, LibRaw → linear float → enfuse across bracket set.

**Effort**: **M** enfuse-only bracket merging.

---

## 14. Print Layout / Contact Sheets

Basic grid-of-thumbnails PDF/print. No external dependency — WPF `FixedDocument` or `PdfSharpCore` (MIT) handles paper sizes, DPI, margins, captions. Provide presets: 2×3, 3×4, 4×5, 5×7, contact-sheet (6×8). Include EXIF caption, filename, date.

**Effort**: **S** — no novelty, just UX.

---

## 15. Histogram / Palette / Color Picker

Histogram: per-channel 256-bin arrays via `ProcessPixelRows`, render to `WriteableBitmap` or SkiaSharp canvas overlay. Classic luminance weights `0.299R + 0.587G + 0.114B`. Log-scale Y axis for dark images.

Palette extraction: **`ColorThief.ImageSharp`** NuGet (drop-in) — modified median-cut on a color histogram. Or roll your own with ImageSharp's built-in `WuQuantizer` / `OctreeQuantizer` and read the resulting palette off `IndexedImageFrame<TPixel>`. Downsample to 200 px first for a 100x speedup.

Color picker UI: **`PixiEditor.ColorPicker`** (MIT) is the modern WPF + Avalonia control. Extended WPF Toolkit has an older one.

**Effort**: **S** for histogram + picker; **S** for palette via ColorThief.

---

## 16. Plugin / Scripting API

Two viable paths:

**Roslyn scripting** (`Microsoft.CodeAnalysis.CSharp.Scripting`) — user writes C# snippets against an `IImageContext` host API; compiled in-process; full IDE-like experience via CodeLab-style pane. **Westwind.Scripting** (MIT) wraps the ugly bits — proven in Markdown Monster's Commander Addin. Pattern: `ScriptOptions.Default.WithImports("Images.Api").AddReferences(typeof(ImageContext).Assembly)`. Sandboxing: restrict namespaces, illegal types, and target framework — commercial Roslyn plugin kits demonstrate the pattern.

**G'MIC** (`gmic.exe`, CeCILL + LGPL) — 640+ filters in the plugin build, 4000+ CLI commands in 3.6. Ship gmic.exe, shell out with a script string. User discovers filters via the 500+ pre-built stock set, including artistic effects (painting, engraving, halftone), denoise (BM3D, DCT), sharpen, local contrast enhancement. Best bang-for-the-buck plugin bus.

**8bf host** — G'MIC-Qt, Nik Collection, Topaz legacy plugins all ship as Photoshop 8bf. Writing an 8bf host is tricky (PICA suites, FilterRecord struct) but unlocks the entire commercial filter ecosystem. Paint.NET's PSFilterShim shows the pattern.

**Effort**: **M** Roslyn + host API; **M** G'MIC shell-out; **L** 8bf host.

---

## Cross-Cutting: Canvas & Engine Choice

For pan/zoom on large images (20+ MP, RAW at 60 MP) **SkiaSharp wins decisively** over WPF `WriteableBitmap` or ImageSharp:
- `SKCodec` can decode to a *target size* — loads a 1000×800 buffer when you want to show 800×600 of a 4000×3600 source. Huge win for zoom-out.
- Hardware-accelerated rendering (OpenGL / Vulkan backends available).
- Real-world benchmarks: ~2x faster image load, ~4x faster thumbnail generation vs ImageSharp.
- Already the render layer under MAUI / Blazor.

ImageSharp is CPU-only — fine for batch processing, wrong for interactive viewer. **License note**: ImageSharp is dual-licensed; commercial apps need a paid license (Apache-2.0 for open-source only). **SkiaSharp is MIT, no strings**.

Recommendation: **SkiaSharp as the viewer/canvas; Magick.NET for codec breadth; ImageSharp removed from the critical path** (or kept under its Apache-2.0 OSS terms only).

---

## Priority Build Order (opinion)

1. **SkiaSharp canvas** (replace WriteableBitmap for pan/zoom) — unlocks everything else.
2. **Windows.Media.Ocr** + **YoloDotNet COCO tags** — cheap wins, build the search/filter UX.
3. **CLIP semantic search** + sqlite-vec — this is the killer feature vs Windows Photos.
4. **BiRefNet + Real-ESRGAN** — the two AI tools normies actually want.
5. **Face detection + HDBSCAN clustering** — "show me photos of X" completes the semantic triad.
6. **Lossless JPEG transforms + annotation overlay** — polish pass.
7. **XMP-sidecar non-destructive edits** — foundation for everything in editor mode.
8. **Roslyn plugin API + G'MIC shell-out** — let power users extend.
9. RAW development, panorama, HDR — Lightroom-class follow-ups.

---

## Sources

- [Upscayl GitHub](https://github.com/upscayl/upscayl), [upscayl-ncnn](https://github.com/upscayl/upscayl-ncnn), [Upscayl Review 2026](https://www.aiarty.com/ai-image-enhancer/upscayl-review.htm)
- [Real-ESRGAN GitHub](https://github.com/xinntao/Real-ESRGAN), [Real-ESRGAN-ncnn-vulkan](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan), [chaiNNer](https://github.com/chaiNNer-org/chaiNNer), [OpenModelDB FAQ](https://openmodeldb.info/docs/faq)
- [rembg GitHub](https://github.com/danielgatis/rembg), [BiRefNet vs rembg vs U²Net (DEV)](https://dev.to/om_prakash_3311f8a4576605/birefnet-vs-rembg-vs-u2net-which-background-removal-model-actually-works-in-production-2j70), [BiRefNet GitHub](https://github.com/ZhengPeng7/BiRefNet), [Cloudflare background-removal eval](https://blog.cloudflare.com/background-removal/)
- [ONNX Runtime DirectML EP](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html), [DirectML GitHub](https://github.com/microsoft/DirectML), [AMD GPUOpen ONNX+DirectML guide](https://gpuopen.com/learn/onnx-directlml-execution-provider-guide-part1/)
- [FaceONNX](https://github.com/FaceONNX/FaceONNX), [FaceAiSharp on NuGet](https://www.nuget.org/packages/FaceAiSharp.Models.ArcFace.LResNet100E-IR), [InsightFace GitHub](https://github.com/deepinsight/insightface), [Clearly.ML.Faces](https://www.nuget.org/packages/Clearly.ML.Faces)
- [ElBruno Local Image Embeddings](https://elbruno.com/2026/02/16/%F0%9F%96%BC%EF%B8%8F-local-image-embeddings-in-net-clip-onnx/), [CLIP in C# (Bart Broere)](https://bartbroere.eu/2023/07/29/openai-clip-csharp-onnx/), [CLIP repo](https://github.com/openai/CLIP)
- [YoloDotNet](https://github.com/NickSwardh/YoloDotNet), [YoloDotNet 4.2.0 on NuGet](https://www.nuget.org/packages/YoloDotNet), [YoloSharp](https://github.com/dme-compunet/YoloSharp)
- [Tesseract vs PaddleOCR (IronOCR)](https://ironsoftware.com/csharp/ocr/blog/compare-to-other-components/paddle-ocr-vs-tesseract/), [PaddleOCR analysis](https://www.koncile.ai/en/ressources/paddleocr-analyse-avantages-alternatives-open-source), [C# OCR Libraries 2026](https://hackernoon.com/c-ocr-libraries-the-definitive-net-comparison-for-2026)
- [Paint.NET Plugin API](https://paintdotnet.github.io/apidocs/), [PdnV5EffectSamples](https://github.com/paintdotnet/PdnV5EffectSamples), [pypdn Python PDN reader](https://github.com/addisonElliott/pypdn), [Paint.NET image format notes](http://justsolve.archiveteam.org/wiki/Paint.NET_image)
- [G'MIC](https://gmic.eu/), [G'MIC 3.6 release](https://gmic.eu/gmic36/), [G'MIC-Qt](https://github.com/c-koi/gmic-qt), [G'MIC Wikipedia](https://en.wikipedia.org/wiki/G'MIC)
- [darktable sidecar manual](https://docs.darktable.org/usermanual/development/en/overview/sidecar-files/sidecar/), [Darktable + digiKam XMP](https://marcrphoto.wordpress.com/2025/07/28/darktable-and-digikam-more-xmp-questions/)
- [Sdcb.LibRaw](https://github.com/sdcb/Sdcb.LibRaw), [LibRaw main repo](https://github.com/LibRaw/LibRaw), [SharpLibraw](https://github.com/laheller/SharpLibraw)
- [Hugin manual](https://hugin.sourceforge.io/docs/manual/Hugin.html), [Hugin HDR workflow](https://hugin.sourceforge.io/docs/manual/HDR_workflow_with_hugin.html), [Enfuse manual](https://hugin.sourceforge.io/docs/manual/Enfuse.html)
- [jpegtran(1) man page](https://linux.die.net/man/1/jpegtran), [libjpeg-turbo #233 perfect transforms](https://github.com/libjpeg-turbo/libjpeg-turbo/issues/233), [Graphics Mill lossless transforms](https://www.graphicsmill.com/docs/gm5/ApplyingLosslessJPEGTransforms.htm)
- [Little CMS](https://www.littlecms.com/color-engine/), [Win32 Advanced Color ICC behavior](https://learn.microsoft.com/en-us/windows/win32/wcs/advanced-color-icc-profiles), [MHC2 hardware color pipeline](https://github.com/dantmnf/MHC2), [iccMAX for HDR (W3C)](https://www.w3.org/Graphics/Color/Workshop/slides/Derhak.pdf)
- [Krita Filters manual](https://docs.krita.org/en/reference_manual/filters.html), [Krita Filter Brush Engine](https://docs.krita.org/en/reference_manual/brushes/brush_engines/filter_brush_engine.html), [Krita features](https://krita.org/en/features/)
- [ShareX Alternatives (AlternativeTo)](https://alternativeto.net/software/shottr/), [Best Markup Tools 2026](https://www.screensnap.pro/blog/best-markup-tools), [Greenshot / Flameshot roundup](https://www.captio.work/blog/greenshot-alternatives)
- [Dbscan NuGet](https://www.nuget.org/packages/DBSCAN/), [HdbscanSharp](https://github.com/doxakis/HdbscanSharp), [Face clustering with HDBSCAN (ACT blog)](https://act-labs.github.io/posts/facenet-clustering/)
- [Roslyn Scripting API docs (MS)](https://learn.microsoft.com/en-us/archive/blogs/csharpfaq/introduction-to-the-roslyn-scripting-api), [Roslyn Scripting samples](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md), [Westwind.Scripting (Rick Strahl)](https://weblog.west-wind.com/posts/2022/Jun/07/Runtime-C-Code-Compilation-Revisited-for-Roslyn)
- [stable-diffusion-onnx-ui](https://github.com/JbPasquier/stable-diffusion-onnx-ui), [onnx-web](https://github.com/ssube/onnx-web), [stablediffusion-infinity](https://github.com/lkwq007/stablediffusion-infinity)
- [ColorThief.ImageSharp](https://www.nuget.org/packages/ColorThief.ImageSharp), [ImageSharp Quantizers](https://docs.sixlabors.com/articles/imagesharp/gettingstarted.html), [PixiEditor ColorPicker](https://github.com/PixiEditor/ColorPicker)
- [Aspose.PSD](https://www.nuget.org/packages/Aspose.PSD), [Photopea Open/Save](https://www.photopea.com/learn/opening-saving)
- [LanceDB GitHub](https://github.com/lancedb/lancedb), [Qdrant GitHub](https://github.com/qdrant/qdrant), [LM-Kit.NET vector DB guide](https://docs.lm-kit.com/lm-kit-net/guides/glossary/vector-database.html), [Qdrant vs LanceDB (Zilliz)](https://zilliz.com/comparison/qdrant-vs-lancedb)
- [SkiaSharp vs ImageSharp (DEV)](https://dev.to/saint_vandora/the-ultimate-guide-choosing-between-sixlaborsimagesharp-and-skiasharp-for-net-image-processing-17hi), [SkiaSharp scaling (issue #319)](https://github.com/mono/SkiaSharp/issues/319), [.NET image resize benchmark (Anthony Simmon)](https://anthonysimmon.com/benchmarking-dotnet-libraries-for-image-resizing/)
- [RealESRGAN_ONNX repo](https://github.com/muhammad-ahmed-ghani/RealESRGAN_ONNX), [mpv-upscale-2x_animejanai](https://github.com/the-database/mpv-upscale-2x_animejanai), [OpenModelDB RealESRGAN_x4plus](https://openmodeldb.info/models/4x-realesrgan-x4plus), [OpenModelDB RealESRGAN_x4plus Anime 6B](https://openmodeldb.info/models/4x-realesrgan-x4plus-anime-6b), [ONNX Runtime mobile superres tutorial](https://onnxruntime.ai/docs/tutorials/mobile/superres.html)
