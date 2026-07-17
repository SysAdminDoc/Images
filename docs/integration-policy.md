# Optional Runtime And Integration Policy

Status: closes roadmap `X-03`  
Date: 2026-05-05  
Scope: any optional runtime, model, decoder, extractor, plugin host, CLI tool, or native library used by Images.

## Policy

Images can study external projects and reuse permissively licensed libraries, but it must not copy code from incompatible projects or quietly bundle risky runtimes. Every optional integration needs a written review before code or binaries are added.

This policy applies to:

- Ghostscript bundles.
- Archive tooling such as 7-Zip, UnRAR, libarchive, or SharpCompress.
- OpenSlide, Bio-Formats, OpenImageIO, libvips, OCIO, and other pro/scientific backends.
- OCR engines beyond Windows.Media.Ocr.
- AI models and ONNX runtimes.
- ExifTool, C2PA tooling, and metadata writers.
- Plugin hosts and scripting runtimes.

## Required Review Fields

| Field | Required answer |
| --- | --- |
| Name and version | Exact package, binary, model, or tool version. |
| Source URL | Canonical upstream URL and release URL. |
| License | SPDX identifier where possible, plus model/data license if separate from code. |
| Redistribution permission | Whether the exact artifact may be bundled in Images releases. |
| Source-use boundary | Whether code can be linked, referenced, shell-called, or only used as design inspiration. |
| Update cadence | How releases are monitored and who owns upgrades. |
| CVE/advisory tracking | Advisory feeds, GitHub advisories, upstream security page, or vendor notices. |
| Binary provenance | Download source, checksum, signing status, build reproducibility if known. |
| Process boundary | In-process, child process, sandboxed child process, or unsupported. |
| File access boundary | Which user files, temp files, cache folders, or network resources the runtime can access. |
| Network behavior | Whether the runtime can contact the network and how Images prevents silent egress. |
| Failure mode | User-facing error copy and diagnostics when the runtime is missing or fails. |
| Test corpus | Generated or checked-in fixtures required to validate the integration. |
| Release impact | Added size, startup cost, install complexity, and package-manager consequences. |

## Default Decisions

- Prefer in-box Windows APIs when they meet the user need.
- Prefer permissive NuGet libraries over native binaries.
- Prefer child-process boundaries for GPL/AGPL, fragile, high-CVE, or untrusted-file parsers.
- Prefer generated test fixtures over checked-in binary corpora unless a format requires exact bytes.
- Prefer app-local storage under `%LOCALAPPDATA%\Images` for disposable caches and extracted temp files.
- Reject integrations that require silent network access.
- Reject original-file writes until the workflow has confirmation, rollback, and regression coverage.

## Integration Review: SkiaSharp WPF

- Version: `SkiaSharp.Views.WPF` 4.150.1, with NuGet-resolved SkiaSharp/native and OpenTK dependencies pinned in `packages.lock.json`.
- Source: [mono/SkiaSharp](https://github.com/mono/SkiaSharp); [official NuGet package](https://www.nuget.org/packages/SkiaSharp.Views.WPF/4.150.1).
- Release artifact: the NuGet package graph from nuget.org, including `SkiaSharp.NativeAssets.Win32`; no separately downloaded binary.
- License: MIT for SkiaSharp; transitive licenses remain subject to the release inventory/audit gate.
- Redistribution permission: MIT permits bundling; published builds redistribute the lockfile-resolved Windows native assets inside the app package.
- Source-use boundary: in-process package reference; no copied upstream source.
- Update cadence: reviewed with dependency servicing and before release; upgrades are explicit package/lockfile changes.
- CVE/advisory tracking: `dotnet list package --vulnerable --include-transitive` plus upstream GitHub/NuGet advisory review.
- Binary provenance: nuget.org package hashes are pinned by the three solution lockfiles and validated by locked restore.
- Process boundary: in-process software `SKElement`; the initial static-image slice does not activate the OpenGL WPF surface.
- File access boundary: none; it receives a premultiplied pixel copy from the existing bounded decoder and does not open user files.
- Network behavior: none at runtime.
- Failure mode: allocation/conversion failure falls back to the existing WPF `Image` presenter without changing source or export data.
- Test corpus: generated BGRA/PBGRA control fixtures, animation/tile fallback tests, then golden-render fixtures and the non-activating background smoke lane as migration slices land.
- Release impact: adds SkiaSharp Windows native assets and WPF view dependencies; no installer, service, account, or startup-network requirement.

## Integration Review: Windows ML

- Version: `Microsoft.Windows.AI.MachineLearning` 2.1.74, including its pinned ONNX Runtime and DirectML payload.
- Source: [official NuGet package](https://www.nuget.org/packages/Microsoft.Windows.AI.MachineLearning/2.1.74); [Windows ML documentation](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview).
- Release artifact: nuget.org package graph restored into the app's self-contained Windows publish output.
- License: Microsoft Windows ML Runtime terms plus the package's third-party notices; the terms explicitly permit redistribution of NuGet-binplaced files with an application, subject to their distribution requirements.
- Redistribution permission: accepted for unchanged app-local runtime files; license and third-party notices remain in the package/release inventory.
- Source-use boundary: in-process binary package reference; no copied Microsoft or provider source.
- Update cadence: explicit dependency review per servicing release; package and solution lockfiles move together.
- CVE/advisory tracking: NuGet vulnerability scan plus Windows ML, ONNX Runtime, DirectML, and ready provider advisory review.
- Binary provenance: NuGet content hashes are pinned in all solution lockfiles; locked restore is a release gate.
- Process boundary: in-process managed/WinRT runtime. Only providers already `Certified` and `Ready` are registered; DirectML and CPU stay available as bundled fallbacks.
- File access boundary: imported, SHA-256-approved ONNX models and in-memory image tensors; the runtime router does not discover arbitrary models or folders.
- Network behavior: Images never invokes `EnsureReadyAsync` or another provider-acquisition API, and disables ONNX Runtime telemetry. Windows servicing of already-installed system/provider packages remains governed by Windows settings.
- Failure mode: session creation retries the ordered NPU, GPU, DirectML, and CPU candidates per model; failure leaves the source image untouched and surfaces the model/runtime error.
- Test corpus: ONNX upstream `test_add` model, 129 bytes, SHA-256 `93CF0438706CDDABF683ADC8B13C8A17C4B8B12D8BCCB1B041268E1F4DFF0A2D`, sourced from `onnx/backend/test/data/node/test_add/model.onnx`; tests require 60 exact Add outputs through every detected path.
- Release impact: about 41-48 MB of self-contained Windows ML/ONNX/DirectML runtime files, replacing the previous standalone ONNX Runtime DirectML package rather than adding a second ORT copy.

## Process Isolation Rules

| Risk level | Examples | Required boundary |
| --- | --- | --- |
| Low | Small permissive managed library, no native parser, no network. | In-process allowed after review. |
| Medium | Native codec, metadata parser, archive reader, large model runtime. | In-process only with CVE tracking and corpus tests; otherwise child process. |
| High | GPL/AGPL tool, JVM sidecar, untrusted document parser, experimental decoder. | Child process or sandboxed process; no in-process loading. |
| Rejected | Unknown license, unverifiable binary, silent network, no redistribution rights. | Do not integrate. |

## Review Template

Copy this block into a new design or decision document before adding the integration:

```markdown
## Integration Review: <name>

- Version:
- Source:
- Release artifact:
- License:
- Redistribution permission:
- Source-use boundary:
- Update cadence:
- CVE/advisory tracking:
- Binary provenance:
- Process boundary:
- File access boundary:
- Network behavior:
- Failure mode:
- Test corpus:
- Release impact:
- Decision:
- Owner:
```

## Current Reviewed Integrations

| Integration | Review status | Notes |
| --- | --- | --- |
| Magick.NET 14.15.0 | Accepted | Existing NuGet dependency; release workflow vulnerable-package gate covers managed package advisories. Upgraded from 14.14.0 for ImageMagick 7.1.2-27 / libheif >=1.22.0, addressing CVE-2026-32740 and 2026 ImageMagick heap/OOB-write CVEs on the untrusted-file decode path. |
| Ghostscript 10.07.0 | Conditionally accepted | Optional app-local/system runtime; bundling requires exact approved artifact and SHA-256 continuity. See `docs/codec-bundling.md`. |
| SharpCompress 0.50.0 | Accepted | Managed MIT NuGet dependency for read-only RAR/CBR and 7z/CB7 archive books. Upgraded from 0.49.1 for the LZMA/RAR decode allocation reductions and Zip64 non-seekable-stream / entry-metadata-corruption fixes; the 0.50.0 Tar auto-decompress and Detection API breaking changes do not affect the `ArchiveFactory.OpenArchive` + `IArchive.Entries` path Images uses. See `docs/archive-runtime-review.md`. |
| Microsoft.Data.Sqlite 10.0.10 + SQLitePCLRaw.bundle_e_sqlite3 3.0.3 | Accepted | App-local catalog, settings, and semantic index storage use managed ADO.NET over the bundled SQLitePCLRaw e_sqlite3 runtime; release readiness keeps package and vulnerability gates local. |
| Microsoft.Windows.AI.MachineLearning 2.1.74 | Accepted for self-contained Windows-only inference | NuGet-binplaced Windows ML, ONNX Runtime, and DirectML files are redistributable under the included Microsoft Windows ML Runtime terms. Images disables ORT telemetry, never calls provider acquisition, registers only already-ready certified catalog providers, and falls back to bundled DirectML/CPU. The pinned ONNX Add fixture validates every detected path. |
| Windows.Media.Ocr | Accepted | In-box Windows API; no bundled runtime. |
| 7-Zip/UnRAR native archive readers | Not reviewed | Native sidecars remain unapproved; ZIP/CBZ use .NET built-in APIs and RAR/7z use the reviewed managed SharpCompress path. |
| jpegtran.exe | Accepted for release staging through reviewed libjpeg-turbo 3.1.4.1 artifact | Lossless JPEG crop/rotation planning, runtime diagnostics, exact MCU-aligned writeback, right-angle rotation writeback, confirmed-trim UI, exact artifact URL, license files, `jpegtran.exe` plus `jpeg62.dll` SHA-256 provenance, staging script, and release diagnostics smoke are documented in `docs/lossless-jpeg-transform-policy.md` and `src/Images/Codecs/JpegTran/PROVENANCE.md`. Runtime binaries remain ignored by git and are staged only for build/publish output. |
| LaMa inpainting ONNX | Accepted, opt-in model not bundled | Content-aware repair uses user-imported, hash-pinned LaMa ONNX through the shared Windows ML / DirectML / CPU runtime. See `docs/inpaint-runtime-decision.md`. |
| OpenCV YuNet and SFace ONNX | Accepted, opt-in models not bundled | Official OpenCV Zoo Apache-2.0 artifacts are pinned by repository revision, byte length, and SHA-256. Images only accepts manual imports and routes inference through the shared Windows ML / DirectML / CPU runtime. Face regions and embeddings remain local derived metadata until a user reviews an export. |
| ExifTool | Not reviewed | Required before sidecar or metadata write workflows. |
| Other ONNX models | Not reviewed | Required before additional AI tagging or classification workflows. |
| OpenSlide/Bio-Formats | Not reviewed | Required before lab/scientific image packs. |
| Plugin host | Not reviewed | Requires a separate trust model and disabled-by-default policy. |

## Release Gate

A release that includes a new optional runtime must include:

- The completed integration review.
- A changelog entry naming the runtime and version.
- A diagnostics surface showing runtime availability and provenance.
- At least one automated test or smoke command that exercises the runtime when present.
- A fallback path when the runtime is absent.
- Updated user documentation explaining install, privacy, and recovery behavior.
