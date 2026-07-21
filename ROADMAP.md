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

- [ ] **V120-01** P2 — Reuse one ONNX `InferenceSession` across the single-shot face/object/orientation batch paths.
  Why: `--face-cluster` over N images builds ~2N sessions (detect + recognize per image); scene/aesthetic/safety already cache one session per batch — face/object/orientation should match, cutting model-load overhead on multi-file runs.
  Evidence: `FaceDetectionService.cs:138`, `FaceRecognitionService.cs:63,115`, `ObjectDetectionService.cs:119`, `OrientationSuggestionService.cs:96` each call `OnnxRuntimeService.CreateSession` per image; `AestheticScoringService.cs:74-93` / `SceneClassificationService.cs:91-110` show the target `using(session){foreach…}` pattern.
  Touches: `Services/FaceDetectionService.cs`, `Services/FaceRecognitionService.cs`, `Services/FaceClusterService.cs`, `Services/ObjectDetectionService.cs`, `Services/OrientationSuggestionService.cs`, `Services/FaceCli.cs`.
  Acceptance: a multi-image `--face-cluster`/`--object-detect`/`--orientation-suggest` run creates one detection (and one recognition) session total, not one per image; existing per-image results unchanged; tests cover the batched overload.
  Complexity: M

- [ ] **V120-02** P2 — Thread `CancellationToken` through the ML batch APIs and CLI drivers.
  Why: Long `--scene-classify`/`--aesthetic-score`/`--safety-classify`/`--face-cluster` runs over hundreds of files are currently uninterruptible; catalog/semantic rebuilds already thread cancellation correctly, so this closes the last gap.
  Evidence: `AestheticScoringService.ScoreMany` (`:41`), `SceneClassificationService.ClassifyMany` (`:58`), `SafetyClassificationService.ClassifyMany` (`:44`), `FaceCli.ExecuteCluster` (`:97`) take no token; `CatalogService.cs:153,195` / `SemanticSearchService.cs:152,180` show the pattern.
  Touches: `Services/AestheticScoringService.cs`, `Services/SceneClassificationService.cs`, `Services/SafetyClassificationService.cs`, `Services/FaceCli.cs`, `Services/SceneCli.cs`, `Services/AestheticCli.cs`, `Services/SafetyCli.cs`, `Services/ObjectCli.cs`.
  Acceptance: each batch loop honors a `CancellationToken` (Ctrl+C in CLI cancels promptly); a cancellation test asserts partial results + `OperationCanceledException` handling; no behavior change when no token is passed.
  Complexity: M

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

- [ ] **V120-04** P3 — Distinguish "model load failed" from "N images failed" in the ML CLI exit codes.
  Why: A model-load exception in `ScoreMany`/`ClassifyMany` fans a generic `Failed` result across every path, so the CLI cannot report whether the model is broken or a single image was bad.
  Evidence: `AestheticScoringService.cs:65`, `SceneClassificationService.cs:82`, `SafetyClassificationService.cs:68` (identical fan-out); `SceneCli`/`AestheticCli`/`SafetyCli`/`ObjectCli` collapse both to one exit code.
  Touches: `Services/AestheticScoringService.cs`, `Services/SceneClassificationService.cs`, `Services/SafetyClassificationService.cs`, `Services/ObjectDetectionService.cs`, the four `*Cli.cs` drivers.
  Acceptance: model-unavailable/load-failure returns a distinct non-zero exit code from per-image decode failures; the difference is documented in `--help`/README CLI table; tests assert both codes.
  Complexity: S

- [ ] **V120-08** P3 — Cap survivors before the O(k²) face NMS.
  Why: `ApplyNonMaximumSuppression` runs a linear `selected.All(...)` scan inside the loop over up to `topK=5000` candidates — bounded but worst-case quadratic on dense/pathological detections (YOLOX NMS is already class-partitioned and capped at 300).
  Evidence: `FaceDetectionService.cs:165,259-263`.
  Touches: `Services/FaceDetectionService.cs`.
  Acceptance: face NMS caps candidate survivors to a sane bound before suppression; detection results on normal images are unchanged; a stress test with many overlapping boxes stays within a bounded time budget.
  Complexity: S
