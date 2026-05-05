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
| Magick.NET | Accepted | Existing NuGet dependency; release workflow vulnerable-package gate covers managed package advisories. |
| Ghostscript | Conditionally accepted | Optional app-local/system runtime; bundling requires exact approved artifact and SHA-256 continuity. See `docs/codec-bundling.md`. |
| SharpCompress 0.47.4 | Accepted | Managed MIT NuGet dependency for read-only RAR/CBR and 7z/CB7 archive books. See `docs/archive-runtime-review.md`. |
| Windows.Media.Ocr | Accepted | In-box Windows API; no bundled runtime. |
| 7-Zip/UnRAR native archive readers | Not reviewed | Native sidecars remain unapproved; ZIP/CBZ use .NET built-in APIs and RAR/7z use the reviewed managed SharpCompress path. |
| jpegtran.exe | Policy scoped, runtime not approved | Lossless JPEG crop/rotation planning is documented in `docs/lossless-jpeg-transform-policy.md`; the exact libjpeg-turbo Windows artifact still needs license, provenance, and CVE review before bundling. |
| ExifTool | Not reviewed | Required before sidecar or metadata write workflows. |
| ONNX models | Not reviewed | Required before semantic search, AI tagging, background removal, upscaling, or face recognition. |
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
