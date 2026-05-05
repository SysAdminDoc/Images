# Archive Runtime Review

Date: 2026-05-05

Scope: decide whether Images should expand archive/book mode beyond ZIP/CBZ to RAR/CBR and 7z/CB7, and define the gates before any runtime is bundled or referenced.

## Current Decision

Keep shipped archive/book support limited to ZIP and CBZ for now. They are handled by `System.IO.Compression`, require no new native runtime, and fit the current read-only in-memory page model.

RAR/CBR and 7z/CB7 remain planned, but not approved for implementation until the project has a small, isolated archive adapter with explicit license, provenance, vulnerability, and failure-mode gates.

## Candidates

### SharpCompress

SharpCompress is the preferred first spike for 7z and RAR read-only archive listing because it is managed, NuGet-distributed, and the current package advertises `net9.0` support with no additional dependencies for that target. NuGet currently lists SharpCompress 0.47.4 and shows no `net9.0` dependencies.

Gates before use:

- Add package only in a branch dedicated to archive expansion.
- Verify MIT/license notices from the package repository and transitive assets.
- Run `dotnet list package --vulnerable --include-transitive` after adding it.
- Keep the `ArchiveBookService` contract read-only: list entries, reject unsafe paths, skip nested archives, cap per-entry bytes, and never extract entry names to disk.
- Add generated tests for `.7z`, `.cb7`, `.rar`, and `.cbr` fixtures only if they can be generated or legally checked in without proprietary samples.

### 7-Zip Runtime

7-Zip is a viable fallback for 7z if the managed adapter is incomplete. The official FAQ allows commercial use of the EXE/DLL path, but requires documentation that 7-Zip is used, that it is GNU LGPL, and that users can find source at `www.7-zip.org`. Direct source or DLL integration also carries LGPL obligations; a sidecar process boundary is safer than loading native DLL code into the viewer.

Gates before use:

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

Unsupported archive extensions should stay calm and actionable:

- `.rar`, `.cbr`, `.7z`, and `.cb7` should explain that archive/book mode currently supports ZIP/CBZ first.
- Users should be told to extract the archive or convert it to CBZ until runtime support is approved.
- The app must not prompt to download archive runtimes automatically.

## Implementation Boundary

Future non-ZIP archive work should not change the viewer contract. The adapter boundary should expose only:

- `bool CanOpen(string path)`
- `IReadOnlyList<ArchiveEntryInfo> ListPages(string path)`
- `byte[] ReadPage(string path, ArchiveEntryInfo entry, long maxBytes)`

Everything above that boundary stays format-agnostic: natural sorting, cover promotion, page controls, history, read-position persistence, and UI state should not care whether the container is ZIP, 7z, or RAR.

## Release Gates

Before enabling any new archive runtime in a release:

- License notice added to release docs.
- Binary/source provenance documented.
- CVE/update cadence documented.
- Diagnostics reports active runtime and hash.
- Vulnerability scan passes.
- Generated corpus covers valid archive, empty archive, unsafe path, nested archive, oversized page, corrupt archive, and missing-file cases.
- Archive runtime failures produce actionable user-facing copy without crashing the viewer.
