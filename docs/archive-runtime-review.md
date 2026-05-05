# Archive Runtime Review

Date: 2026-05-05

Scope: decide whether Images should expand archive/book mode beyond ZIP/CBZ to RAR/CBR and 7z/CB7, and define the gates before any runtime is bundled or referenced.

## Current Decision

Approved and implemented for a managed in-process adapter:

- ZIP/CBZ remain on `System.IO.Compression`.
- RAR/CBR and 7z/CB7 use `SharpCompress` 0.47.4 from NuGet.
- The adapter is read-only. It lists candidate page entries, rejects unsafe paths, skips nested archives and document-preview entries, caps every buffered page at 256 MiB, and never extracts entry names or archive contents to disk.

SharpCompress was accepted because the package is MIT licensed, NuGet-distributed, targets `net9.0`, has no additional dependencies for this target, and keeps Images away from native 7-Zip or UnRAR sidecars for this first expansion.

## Accepted Integration: SharpCompress

Review result:

- Version: `SharpCompress` 0.47.4.
- Source: NuGet package and upstream GitHub repository.
- License: MIT.
- Redistribution permission: allowed as a NuGet dependency in the app output.
- Source-use boundary: linked managed library only; no copied source.
- Process boundary: in-process, managed parser.
- File access boundary: reads only the user-selected archive stream and page entry streams; does not write extracted files.
- Network behavior: none.
- Failure mode: corrupt, encrypted, unsupported, or empty archives surface decode-recovery copy through the existing load-error surface.
- Test corpus: generated ZIP/CBZ and 7z/CB7 archives cover valid pages, natural sort, nested archive skipping, document-entry skipping, unsupported-entry skipping, empty archives, and corrupt managed archive recovery. RAR/CBR write fixtures are not generated because the managed package does not create RAR archives and proprietary samples are intentionally not checked in.
- Release impact: one managed NuGet dependency; runtime provenance is reported in About and `Images.exe --system-info`.

Validation gates completed:

- Package added as `SharpCompress` 0.47.4.
- MIT/license and package target reviewed before implementation.
- `dotnet list package --vulnerable --include-transitive` required after the package add.
- `ArchiveBookService` kept read-only with unsafe path rejection, nested archive skipping, document-preview entry skipping, and per-entry byte caps.
- Generated 7z/CB7 regression coverage added without binary fixtures.

## Rejected Or Deferred Candidates

### 7-Zip Runtime

7-Zip remains a fallback only if the managed adapter proves incomplete. The official FAQ allows commercial use of the EXE/DLL path, but requires documentation that 7-Zip is used, that it is GNU LGPL, and that users can find source at `www.7-zip.org`. Direct source or DLL integration also carries LGPL obligations; a sidecar process boundary is safer than loading native DLL code into the viewer.

Gates before any future use:

- Prefer an app-local `Codecs\7zip` sidecar over PATH probing.
- Hash and report the sidecar in diagnostics, matching the Ghostscript provenance model.
- Keep it offline and process-isolated.
- Use temp-free stdout/file-stream extraction where possible; if unavoidable, extract only to a private app temp folder with bounded cleanup.
- Document LGPL notices and source links in release artifacts.

### UnRAR / WinRAR

Do not bundle WinRAR, RAR, or UnRAR binaries as part of Images without explicit legal review. RARLAB's license allows some UnRAR component distribution separately, but the same EULA also forbids using RAR binary code, WinRAR binary code, UnRAR source, or UnRAR binary code to recreate the proprietary RAR compression algorithm. That restriction makes casual bundling a poor fit for a small MIT viewer unless the exact binary, notice, and redistribution path is approved.

Gates before use:

- No in-process UnRAR DLL.
- No WinRAR trial installer bundling.
- No RAR archive creation.
- If support is later approved, use read-only extraction through an isolated adapter and record the exact license text, source/binary origin, hash, and update cadence.

## UX Policy

RAR/CBR and 7z/CB7 are now supported archive-book extensions. They should flow through the same viewer state as ZIP/CBZ:

- Valid books open as read-only pages.
- Empty archives explain that no supported image pages were found.
- Corrupt, encrypted, or damaged archives explain that extraction or repair may be needed.
- Images must not prompt to download archive runtimes automatically.

## Implementation Boundary

Non-ZIP archive work should not change the viewer contract. The archive adapter boundary exposes only normalized page candidates and bounded page bytes. Everything above that boundary stays format-agnostic: natural sorting, cover promotion, page controls, history, read-position persistence, and UI state do not care whether the container is ZIP, 7z, or RAR.

## Release Gates

Before enabling any new archive runtime in a release:

- License notice added to release docs.
- Binary/source provenance documented.
- CVE/update cadence documented.
- Diagnostics reports active runtime and package assembly path.
- Vulnerability scan passes.
- Generated corpus covers valid archive, empty archive, unsafe path, nested archive, oversized page, corrupt archive, and missing-file cases where the format can be generated legally.
- Archive runtime failures produce actionable user-facing copy without crashing the viewer.
