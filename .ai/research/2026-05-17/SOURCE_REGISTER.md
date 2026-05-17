# Source Register

Date: 2026-05-17

This register lists local and external sources used for the research and planning pass. Local files are evidence for current state; URLs are evidence for ecosystem, dependency, runtime, security, and model opportunities.

## Local Repository Sources

| ID | Source | Used for |
| --- | --- | --- |
| L-AGENTS | `AGENTS.md` | Repo-level agent instruction pointer and new canonical context pointer. |
| L-CLAUDE | `CLAUDE.md` | Repo stack, architecture, build/test/release commands, current version history, and gotchas. |
| L-README | `README.md` | User-facing current capability, installer/runtime claims, command docs, policies, and architecture map. |
| L-CHANGELOG | `CHANGELOG.md` | Shipped and Unreleased feature evidence, version history, and date inconsistency. |
| L-ROADMAP-OLD | `ROADMAP.md` before v7 insertion | Prior research archive, stale current-state claim, source appendix, and backlog history. |
| L-IMPROVEMENT | `docs/improvement-plan.md` | Closed roadmap IDs, shipped workflow evidence, verification standard. |
| L-DIFFERENTIATORS | `docs/design-product-differentiators.md` | Product philosophy, semantic search, duplicate cleanup, compare, archive, metadata, and model boundaries. |
| L-INPAINT | `docs/inpaint-runtime-decision.md` | LaMa ONNX runtime decision, Windows ML/DirectML plan, no bundled model policy. |
| L-ARCHIVE | `docs/archive-runtime-review.md` | SharpCompress archive runtime review and security refresh. |
| L-INTEGRATION | `docs/integration-policy.md` | Optional dependency review template and current integration statuses. |
| L-CSPROJ | `src/Images/Images.csproj` | Target framework and NuGet package versions. |
| L-EXPORT | `src/Images/Services/ImageExportService.cs`, `src/Images/Services/ExportPreviewService.cs`, `src/Images/Services/BatchProcessorService.cs`, `src/Images/Services/MacroActionService.cs`, `src/Images/Services/ExportCapabilityWarningService.cs` | Export/write path, preview estimator, batch dry-run behavior, and target-format capability warning implementation. |
| L-COLOR | `src/Images/Services/ImageColorAnalysisService.cs`, `src/Images/Services/ImageMetadataService.cs`, `src/Images/ViewModels/ColorAnalysisController.cs`, `src/Images/MainWindow.xaml` | V7-15 read-only ICC/profile, histogram/channel, alpha-stat, side-panel warning implementation, and delete-sharing hardening for background Magick reads. |
| L-RECOVERY | `src/Images/Services/RecoveryCenterService.cs`, `src/Images/RecoveryCenterWindow.xaml`, `src/Images/DuplicateCleanupWindow.xaml.cs`, `src/Images/FileHealthScanWindow.xaml.cs`, `src/Images/ViewModels/MainViewModel.cs`, `tests/Images.Tests/RecoveryCenterServiceTests.cs` | V7-16 destructive-action ledger, reveal/restore UI, destructive-path recording, and recovery service coverage. |
| L-CI | `.github/workflows/ci.yml` | CI gates for build/test/version/security smoke. |
| L-RELEASE | `.github/workflows/release.yml` | Release packaging and artifact policy. |
| L-INSTALLER | `installer/Images.iss` | Installer version and release packaging details. |
| L-TESTS | `tests/Images.Tests` | Existing regression scope and generated fixture strategy. |
| L-GITLOG | `git log -10` | Recent shipped feature sequence. |
| L-GH-RELEASE | `gh release view v0.2.11` | Release asset names, dates, and checksums. |
| L-DOTNET-VULN | `dotnet list Images.sln package --vulnerable --include-transitive` | Vulnerability status before and after SharpCompress upgrade. |
| L-DOTNET-OUTDATED | `dotnet list Images.sln package --outdated` | Upgrade opportunities. |

## Shared Memory And Instruction Sources

| ID | Source | Used for |
| --- | --- | --- |
| M-GLOBAL-CLAUDE | `C:\Users\--\.claude\CLAUDE.md` | Global work conventions, auto-continue and commit expectations, GUI constraints. |
| M-USER-CLAUDE | `C:\Users\--\CLAUDE.md` | Session-start ritual, repo handling, C# stack rules, release conventions. |
| M-SHARED-INDEX | `C:\Users\--\.claude\projects\c--Users----repos\memory\MEMORY.md` | Historical project memory index. |
| M-IMAGES | `C:\Users\--\.claude\projects\c--Users----repos\memory\projects\images-viewer.md` | Older Images memory, treated as historical after live verification. |
| M-STACK-CSHARP | `C:\Users\--\.claude\projects\c--Users----repos\memory\stack-csharp.md` | .NET/WPF/MVVM stack conventions. |
| M-CODEX | `C:\Users\--\.codex\memories\MEMORY.md` | Prior Codex Images export/crop work and user preference for autonomous continuation. |

## Direct Viewer And Workflow Competitors

| ID | URL | Used for |
| --- | --- | --- |
| S-IMAGEGLASS-FORMATS | https://imageglass.org/docs/supported-formats | Format capability matrix, ImageMagick/Ghostscript dependency pattern. |
| S-IMAGEGLASS-GITHUB | https://github.com/d2phap/ImageGlass | Windows viewer positioning, docs/features/source activity. |
| S-PICVIEW | https://picview.org/download/ | Fast customizable viewer, PDF export, zoom-preview control, Windows/macOS direction. |
| S-NOMACS-GITHUB | https://github.com/nomacs/nomacs | Cross-platform viewer with RAW/PSD, AVIF/JXL topics, active release signal. |
| S-NOMACS-SYNC | https://nomacs.org/blog/synchronization/ | Linked pan/zoom, opacity overlay, and multi-instance comparison reference. |
| S-NOMACS-FEATURES | https://nomacs.org/docs/documentation/features/ | Format provider/writeability matrix reference. |
| S-QUICKLOOK | https://github.com/QL-Win/QuickLook | Windows spacebar preview and Explorer-adjacent lightweight workflow. |
| S-QVIEW | https://interversehq.com/qview/ | Minimal image-first viewer positioning and common-format support. |
| S-QVIEW-GITHUB | https://github.com/jurplel/qView | Minimal viewer source reference. |
| S-JPEGVIEW | https://github.com/sylikc/jpegview | Lightweight viewer/edit reference and JPEG workflow comparison. |
| S-QIMGV | https://github.com/easymodo/qimgv | Hybrid image/video viewer reference. |
| S-OCULANTE | https://github.com/woelper/oculante | Rust viewer and input/IPC comparison reference. |
| S-NEEVIEW | https://neelabo.github.io/NeeView/en-us/userguide.html | Book/comic navigation and library affordance reference. |
| S-YACREADER | https://www.yacreader.com/ | Comic-reader library/navigation benchmark. |
| S-GEEQIE | https://www.geeqie.org/ | Metadata/compare/catalog-style viewer reference. |

## Commercial And Closed Competitors

| ID | URL | Used for |
| --- | --- | --- |
| S-XNVIEW | https://www.xnview.com/en/xnviewmp/ | Broad-format viewer/organizer, batch, metadata, duplicate, and catalog benchmark. |
| S-FASTSTONE | https://www.faststone.org/FSViewerDetail.htm | Viewer/editor/batch converter, histogram, quality/file-size compare, crop/draw board reference. |
| S-ACDSEE-FEATURES | https://www.acdsee.com/en/products/photo-studio-ultimate/features/ | DAM, AI face recognition, people mode, AI keywords, edit workflow benchmark. |
| S-EAGLE | https://eagle.cool/ | Design asset management, tags, collections, and library tradeoffs. |
| S-PUREREF-FEATURES | https://www.pureref.com/handbook/2.0/features/ | Always-on-top, canvas, GIF playback, drawing, and reference-board workflows. |
| S-PUREREF-IMAGES | https://new.pureref.com/handbook/2.0/images/ | Embedded-vs-linked scene/image reference decisions. |

## Adjacent OSS And Domain Projects

| ID | URL | Used for |
| --- | --- | --- |
| S-DIGIKAM-FEATURES | https://www.digikam.org/about/features/ | Photo workflow, search, face recognition, similarity database, duplicate/reference features. |
| S-DIGIKAM-DB | https://docs.digikam.org/en/getting_started/database_intro.html | Multi-database catalog architecture including similarity database. |
| S-DIGIKAM-SIMILARITY | https://docs.digikam.org/fr/left_sidebar/similarity_view.html | Haar/wavelet similarity search and duplicate/sketch search reference. |
| S-IMMICH-SEARCH | https://docs.immich.app/features/searching/ | CLIP contextual search, OCR/location/person filters, model-change cautions. |
| S-IMMICH-FACES | https://docs.immich.app/features/facial-recognition | Face detection, embeddings, DBSCAN-like clustering, queueing, and model pipeline. |
| S-PHOTOPRISM-FEATURES | https://www.photoprism.app/features | Private photo app search/indexing and WebDAV workflow reference. |
| S-PHOTOPRISM-DOCS | https://docs.photoprism.app/ | Search filters and AI-powered local/private workflow reference. |
| S-PHOTOPRISM-FACES | https://docs.photoprism.app/user-guide/ai/face-recognition/ | Face detection/embedding/clustering pipeline reference. |
| S-CZKAWKA | https://czkawka.net/ | Exact duplicate and perceptual similar-image cleaning comparison. |
| S-CZKAWKA-GITHUB | https://github.com/qarmin/czkawka | Duplicate/junk cleaner source reference. |
| S-PHOTODEMON | https://photodemon.org/ | Lightweight local photo editor comparison. |
| S-SQUOOSH | https://squoosh.app/ | Visual-diff converter UX reference. |
| S-SQUOOSH-GITHUB | https://github.com/GoogleChromeLabs/squoosh | Converter architecture/reference source. |
| S-UPSCALE | https://github.com/upscayl/upscayl | Local upscaling app and Real-ESRGAN style workflow reference. |
| S-HYDRUS | https://hydrusnetwork.github.io/hydrus/ | Tag-heavy local collection management reference. |

## Specialized Imaging, Color, And Huge-Image Sources

| ID | URL | Used for |
| --- | --- | --- |
| S-OPENSEADRAGON | https://openseadragon.github.io/ | Deep-zoom tile viewer architecture reference. |
| S-OPENSEADRAGON-DZI | https://openseadragon.github.io/examples/tilesource-dzi/ | DZI tile source pattern. |
| S-OPENSLIDE | https://openslide.org/ | Whole-slide imaging and pyramidal image reference. |
| S-BIOFORMATS | https://www.openmicroscopy.org/bio-formats/ | Scientific image format breadth and metadata model reference. |
| S-NAPARI | https://napari.org/ | Multidimensional viewer workflow reference. |
| S-QUPATH | https://qupath.github.io/ | Large image/annotation/analysis workflow reference. |
| S-LIBVIPS | https://www.libvips.org/ | Streaming image pipeline and large-image processing reference. |
| S-OIIO | https://openimageio.readthedocs.io/en/latest/ | Film/VFX image IO and color pipeline reference. |
| S-OCIO | https://opencolorio.readthedocs.io/en/latest/releases/ | Color management roadmap reference. |
| S-C2PA | https://spec.c2pa.org/specifications/specifications/2.4/specs/C2PA_Specification.html | Content provenance and trust-display opportunity. |

## Runtime, Dependency, And Security Sources

| ID | URL | Used for |
| --- | --- | --- |
| S-DOTNET-POLICY | https://dotnet.microsoft.com/en-us/platform/support/policy | .NET 9 STS support and .NET 10 LTS migration planning. |
| S-DOTNET-RELEASES | https://learn.microsoft.com/en-us/dotnet/core/releases-and-support | .NET support terminology and lifecycle confirmation. |
| S-MAGICK-NUGET | https://www.nuget.org/packages/Magick.NET-Q16-AnyCPU | Magick.NET version, target frameworks, release dates, vulnerability floor evidence. |
| S-MAGICK-RELEASES | https://github.com/dlemstra/Magick.NET/releases | Magick.NET changelog/release tracking. |
| S-SHARPCOMPRESS-NUGET | https://www.nuget.org/packages/SharpCompress | SharpCompress package version evidence. |
| S-SHARPCOMPRESS-GHSA | https://github.com/advisories/GHSA-6c8g-7p36-r338 | SharpCompress path traversal advisory evidence. |
| S-SHARPCOMPRESS-OSV | https://osv.dev/vulnerability/GHSA-6c8g-7p36-r338 | CVE-2026-44788 details and affected API. |
| S-GHOSTSCRIPT-RELEASES | https://www.ghostscript.com/releases/ | Ghostscript 10.07.0 latest release and date. |
| S-GHOSTSCRIPT-CVE | https://ghostscript.com/releases/cve/index.html | Ghostscript CVE tracking. |
| S-GHOSTSCRIPT-NEWS | https://ghostscript.readthedocs.io/en/gs10.07.0/News.html | 10.07.0 release notes and behavior/security notes. |
| S-LIBJPEG-TURBO-RELEASE | https://github.com/libjpeg-turbo/libjpeg-turbo/releases/tag/3.1.4.1 | Approved jpegtran release artifact, source archive, and GitHub-provided SHA-256 digests. |
| S-LIBJPEG-TURBO-BINARIES | https://libjpeg-turbo.org/Documentation/OfficialBinaries | Official binary packaging reference for libjpeg-turbo. |
| S-LIBJPEG-TURBO-LICENSE | https://github.com/libjpeg-turbo/libjpeg-turbo/blob/main/LICENSE.md | BSD-style license text and redistribution notice source for tracked `Codecs\JpegTran` license files. |
| S-SERILOG | https://github.com/serilog/serilog/releases | Logging package update review. |
| S-SERILOG-NUGET | https://www.nuget.org/packages/Serilog | Current Serilog release evidence. |
| S-INNO | https://jrsoftware.org/isinfo.php | Installer tooling reference. |
| S-WINDOWS-OCR | https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr | OCR API reference. |
| S-WINDOWS-ML | https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview | Preferred local model runtime reference. |
| S-ONNX-DIRECTML | https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html | Fallback GPU inference runtime reference. |

## Model, Dataset, And Integration Sources

| ID | URL | Used for |
| --- | --- | --- |
| S-OPENMODELDB | https://openmodeldb.info/ | Upscale model registry and model-card style reference. |
| S-REALESRGAN | https://github.com/xinntao/Real-ESRGAN | Super-resolution model family reference. |
| S-BIREFNET | https://github.com/ZhengPeng7/BiRefNet | Background segmentation candidate. |
| S-REMBG | https://github.com/danielgatis/rembg | Background removal packaging/workflow reference. |
| S-U2NET | https://github.com/xuebinqin/U-2-Net | Segmentation model reference. |
| S-OPENCLIP | https://github.com/mlfoundations/open_clip | Local semantic search embedding model reference. |
| S-SIGLIP | https://huggingface.co/docs/transformers/model_doc/siglip | Embedding/search model family reference. |
| S-SQLITEVEC | https://github.com/asg017/sqlite-vec | Local vector search extension candidate. |
| S-LAMA-OPENCV | https://huggingface.co/opencv/inpainting_lama | Primary inpaint model candidate from existing repo decision. |
| S-LAMA-OPENCV-ONNX | https://huggingface.co/opencv/inpainting_lama/blob/main/inpainting_lama_2025jan.onnx | Approved OpenCV LaMa ONNX artifact identity and SHA-256 pin used by the model manager. |
| S-LAMA-CARVE | https://huggingface.co/Carve/LaMa-ONNX | Fallback LaMa ONNX candidate from existing repo decision. |
| S-LAMA-CARVE-ONNX | https://huggingface.co/Carve/LaMa-ONNX/blob/main/lama_fp32.onnx | Approved Carve LaMa ONNX fallback artifact identity and SHA-256 pin used by the model manager. |
| S-LAMA-ORIGINAL | https://github.com/advimman/lama | Original LaMa source/paper reference. |
