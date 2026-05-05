# Codec support policy

What "broad codec support" means for Images, what's bundled vs optional, and how we decide to add or drop a decoder.

## Tiers

| Tier | What it means | Examples |
|---|---|---|
| **Bundled, in-process** | Always available, ships inside the app's directory. | WIC (via Windows), Magick.NET (Apache 2.0, NuGet), SharpCompress (MIT, NuGet), .NET ZIP APIs |
| **Optional, app-local or system-installed** | Surfaces extra format families when present, never required. Provenance shown in About + `--system-info`. | Ghostscript (PDF/EPS/PS/AI previews) |
| **Not supported** | Requested, not in scope today. The viewer's unsupported-format hint points at the right tool. | video, audio, native design-suite docs |

## What ships in the bundle

- **WIC**: handles common Windows codecs. No version pinning needed; tracks the OS.
- **Magick.NET-Q16-AnyCPU + Magick.NET.Core**: pinned in [`Images.csproj`](../src/Images/Images.csproj). Native codec pack updates with the NuGet release. CVEs are tracked through the `Magick.NET*` Dependabot group and the daily Security workflow.
- **Archive books**: ZIP/CBZ use `System.IO.Compression`; RAR/CBR and 7z/CB7 use `SharpCompress` pinned in [`Images.csproj`](../src/Images/Images.csproj). Archive support is read-only, never extracts entries to disk, skips nested archives and document-preview entries, rejects unsafe paths, and caps each buffered page.

The current open extension count and writable export count are reported live by About → Capability matrix and `Images.exe --codec-report`.

## What can be opt-in

**Ghostscript** — optional runtime for PDF / EPS / PS / AI document previews. Three discovery paths:

1. App-local: `Codecs/Ghostscript/` (or `Codecs/Ghostscript/bin/`) inside the app directory.
2. Environment override: `IMAGES_GHOSTSCRIPT_DIR` pointing at a directory holding `gsdll64.dll`.
3. System install: `%ProgramFiles%\gs\gs*` is auto-discovered.

The active source is shown in About → Runtime provenance (path, version, SHA-256 of `gsdll64.dll`). See [`codec-bundling.md`](codec-bundling.md) for the bundling layout and the SHA-256 drift check.

Ghostscript is **never bundled into a public release** unless redistribution rights for the exact package are explicitly approved. Out of the box, Images relies on the user's installed copy.

## Adding a new optional decoder

Required before any new optional decoder/runtime lands (per ROADMAP X-03):

1. **License review** — redistribution rights documented; license type recorded.
2. **CVE tracking** — public advisory feed identified (e.g. Ghostscript CVE list).
3. **Update cadence** — minimum supported version pinned, with a process for raising the floor on advisories.
4. **Binary provenance** — SHA-256 (or signed digest) of the shipped binaries recorded in the release notes; same hash must be reachable through About / `--system-info`.
5. **Process isolation decision** — in-process is acceptable only for libraries with a strong security record. Larger surfaces (Ghostscript, archive readers, Bio-Formats) get a sidecar/process-isolation plan written before bundling.

This applies to: Ghostscript, native 7-Zip / UnRAR sidecars, OpenSlide, Bio-Formats, OCR engines, AI models, plugin hosts.

Lossless JPEG writeback is covered by the same gate. The `V30-02` planning scaffold documents the required `jpegtran.exe` review in [`lossless-jpeg-transform-policy.md`](lossless-jpeg-transform-policy.md), but no JPEG transform sidecar ships until the exact binary is approved.

## Dropping a decoder

A bundled decoder is dropped when:

- License changes to a form Images cannot redistribute, **or**
- A live CVE has no scheduled fix from upstream, **or**
- The format is no longer represented in the supported corpus.

A removal lands in CHANGELOG with the version, the affected formats, and the recommended migration (export to a still-supported format before upgrading).

## Where to look

- About → Codec capability + Capability matrix + Runtime provenance
- `Images.exe --codec-report` — full per-family matrix and extension list
- `Images.exe --system-info` — runtime/OS/decoder snapshot for support tickets
- [`codec-bundling.md`](codec-bundling.md) — bundling layout for release builders
