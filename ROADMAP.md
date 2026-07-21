# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, paid-account setup, legal decisions, or required external human action stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

Images ships unsigned on every platform by permanent owner policy; signing is not a release gate and is not backlog. The reachable `1.0` boundary is:

1. GitHub Releases remain the primary unsigned distribution channel (already shipping).
2. Every release generates checksum-pinned WinGet and Scoop manifests through `New-PackageManifests.ps1` and passes the package-manifest hash gate. Account-based submission is optional distribution follow-up, not a version gate.
3. The offscreen, non-activating WPF smoke lane stays green alongside the full unit/integration suite.

Promote to `1.0.0` when a release candidate satisfies those three unsigned checks; do not add certificate, SmartScreen-reputation, Store-account, or package-manager-account requirements to this milestone.

## Actionable Work


The remaining research item is decision-gated (Ghostscript-source AGPL mechanism) and lives in `Roadmap_Blocked.md`.

## Research-Driven Additions

Unblocked, net-new items only. HDR display (V100-06), full color management (V100-05), GPS map overlay (V20-23), and the Explorer thumbnail handler (V70-04) remain correctly parked in `Roadmap_Blocked.md` and are not duplicated here.

### 2026-07-16 research pass (V110 block)

The actionable V110 items shipped (see CHANGELOG and git history). The remaining V110 items (V110-09 MainViewModel extraction, V110-12 archive-to-archive rolling navigation, V110-13 hold-to-scrub fly-through, V110-16 gain-map grayscale overlay) require foreground GUI verification of interactive behavior and are parked in `Roadmap_Blocked.md` until a GUI-verifiable session is available.

### 2026-07-20 research pass (V120 block)

Net-new, code-ready items surfacing from the local-ML wave (v0.2.27 → v0.2.30) and the SharpCompress 0.50.0 bump. The prior catalog shared-cache lock, archive-gate dispose, and invert-colors toggle all shipped and are not re-listed. Inline video playback (largest community-validated gap) and gain-map *display* are decision-/renderer-gated and stay out of this actionable set — see `RESEARCH.md` "Under Consideration". SigLIP-2 semantic upgrade, WinGet/Store submission, and gain-map display remain in `Roadmap_Blocked.md`.

- [ ] **V120-03** P2 — Add a SharpCompress-0.50.0 archive-detection/CRC regression gate.
  Why: 0.50.0 changed the Detection API and CRC defaults (and stopped `TarArchive` auto-decompress); no fixtures pin CBZ/CBR/CB7 detection + page-CRC behavior after the bump, so a future SharpCompress update could silently alter archive reading.
  Evidence: SharpCompress 0.50.0 release notes (breaking Detection API, CRC default-on, Tar no auto-decompress); `Services/ArchiveBookService.cs` (advertised-CRC verification path).
  Touches: `tests/Images.Tests/` (new archive fixtures + detection/CRC assertions), small CBZ/CBR/CB7 test fixtures.
  Acceptance: tests open a known-good CBZ/CBR/CB7, assert correct format detection, page count, and per-page CRC verification, and fail if SharpCompress detection/CRC semantics regress.
  Complexity: S

- [ ] **V120-05** P3 — Cap the semantic-search in-RAM cosine scan with a configurable candidate ceiling.
  Why: `SemanticSearchService.Search` scores every cached vector before `Take(limit)`; a dependency-free candidate ceiling / early-out bounds per-query cost as libraries grow (distinct from the blocked SigLIP-2 model swap).
  Evidence: `SemanticSearchService.cs:216-239` (full scan, `Take` applied after scoring); `EnsureVectorCache` already removes per-query deserialization.
  Touches: `Services/SemanticSearchService.cs`, settings surface for the ceiling.
  Acceptance: a configurable maximum candidate count bounds the scan; top-k results are unchanged for libraries under the ceiling; a test asserts the ceiling is honored on a large synthetic vector set.
  Complexity: M

- [ ] **V120-06** P3 — Add dedicated unit tests for `PerceptualHashService` and `Exif31MetadataReader`.
  Why: pHash feeds near-duplicate stacking (a wrong hash silently mis-stacks) and Exif 3.1 reading is provenance/security-relevant; both currently lack a dedicated test file.
  Evidence: audit found no `PerceptualHashService*Tests` / `Exif31MetadataReader*Tests`; near-dup stacking depends on pHash output.
  Touches: `tests/Images.Tests/PerceptualHashServiceTests.cs`, `tests/Images.Tests/Exif31MetadataReaderTests.cs`.
  Acceptance: deterministic-fixture tests assert pHash stability/Hamming distance on known image pairs and Exif 3.1 tag decode (incl. UTF-8 and version/shape gating); both run in the existing suite.
  Complexity: S

