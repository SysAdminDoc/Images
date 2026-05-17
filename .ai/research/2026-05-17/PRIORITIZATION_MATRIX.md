# Prioritization Matrix

Date: 2026-05-17

Scoring:

- Fit: 1 to 5, alignment with Images philosophy and current architecture.
- Impact: 1 to 5, user/release value.
- Evidence: 1 to 5, strength of local/external evidence.
- Effort: 1 to 5, lower is easier.
- Risk: 1 to 5, lower is safer.
- Score: `Fit + Impact + Evidence - Effort - Risk`.

## Now

| Rank | Candidate | Fit | Impact | Evidence | Effort | Risk | Score | Rationale |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 1 | Roadmap/status reconciliation and canonical context | 5 | 5 | 5 | 2 | 1 | 12 | Current roadmap drift is actively misleading; docs are the operating map for future work. |
| 2 | SharpCompress vulnerability gate fix | 5 | 5 | 5 | 1 | 1 | 13 | Local vulnerable scan flagged a package; 0.48.1 clears the gate. Done in this run. |
| 3 | Settings/accessibility IA completion | 5 | 5 | 4 | 3 | 2 | 9 | Shipped feature surface is too broad for current settings tabs. Reduces user friction and support cost. |
| 4 | Runtime/dependency provenance dashboard | 5 | 5 | 5 | 3 | 2 | 10 | Fits codec-report culture and de-risks Ghostscript/jpegtran/model expansion. |
| 5 | Approved jpegtran artifact staging | 5 | 4 | 5 | 3 | 2 | 9 | Unreleased lossless JPEG path needs bundled-runtime proof before it is complete. |
| 6 | Repair verified changelog date inconsistency | 4 | 3 | 5 | 1 | 1 | 10 | Low-cost release-history trust fix after tag/date verification. |

## Next

| Rank | Candidate | Fit | Impact | Evidence | Effort | Risk | Score | Rationale |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 7 | Compare/overlay mode | 5 | 5 | 5 | 4 | 2 | 9 | Shipped 2026-05-17 under V7-10 with current+next, chosen-file, and duplicate-pair entry points. |
| 8 | Visual diff export workbench | 5 | 5 | 4 | 4 | 2 | 8 | Shipped 2026-05-17 under V7-11 with original/encoded preview, size delta, resize-aware save, warning copy, and batch dry-run estimates. |
| 9 | Catalog schema v1 | 5 | 5 | 5 | 5 | 3 | 7 | Shipped 2026-05-17 under V7-12 as rebuildable `catalog.db` schema with source paths, fingerprints, dimensions, dates, codec metadata, and sidecar rating/tag state. |
| 10 | Culling/review mode | 5 | 4 | 4 | 4 | 2 | 7 | Shipped 2026-05-17 under V7-13 with XMP-backed star ratings, pick/reject labels, side-panel controls, keyboard flow, gallery refresh, and undo. |
| 11 | Target-format capability warnings | 5 | 4 | 4 | 3 | 2 | 8 | Shipped 2026-05-17 under V7-14 with shared export preview, batch preview, and macro dry-run warnings for alpha, animation, pages/layers, metadata, ICC profile, and lossy settings. |
| 12 | Release/post-install smoke script | 5 | 4 | 4 | 3 | 2 | 8 | Shipped 2026-05-17 under V7-07; catches Ghostscript/OCR/portable/installer regressions early. |
| 13 | ICC/profile status and histogram basics | 4 | 4 | 4 | 4 | 3 | 5 | Shipped 2026-05-17 under V7-15 with read-only embedded ICC/profile status, sampled histogram/channel stats, alpha stats, and unmanaged-color warnings. |
| 14 | Destructive-action recovery center | 5 | 5 | 4 | 4 | 3 | 7 | Shipped 2026-05-17 under V7-16 with app-local operation records, reveal actions, restore for moves/renames/quarantines, sidecar recovery, and writeback/Recycling guidance. |
| 15 | Local model manager | 5 | 5 | 5 | 5 | 4 | 6 | Shipped 2026-05-17 under V7-30 with approved ONNX definitions, manual import/delete/reveal controls, app-local grouped storage, pinned SHA-256 validation, runtime status copy, and diagnostics provenance rows. |

## Later

| Candidate | Fit | Impact | Evidence | Effort | Risk | Score | Rationale |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Semantic search | 5 | 5 | 5 | 5 | 4 | 6 | V7-31 foundation started with local index/search UI, provider seam, deterministic metadata embeddings, exact cosine search, and delete controls; next blockers are approved ONNX image/text provider, runtime package selection, and model execution validation. |
| LaMa content-aware repair | 4 | 4 | 4 | 4 | 4 | 4 | Existing decision exists, but should wait for runtime foundation. |
| Background removal | 4 | 4 | 4 | 4 | 4 | 4 | Useful but model/runtime and UX boundaries must be solved first. |
| Super-resolution | 4 | 4 | 4 | 4 | 4 | 4 | Good fit if opt-in and local; not before model manager. |
| Deep-zoom/tile engine | 4 | 4 | 4 | 5 | 4 | 3 | Strategic for huge images, but separate architecture. |
| WinGet/Scoop publishing | 4 | 3 | 3 | 3 | 2 | 5 | Valuable after release smoke/signing decisions are stronger. |
| C2PA provenance inspection | 3 | 3 | 4 | 4 | 3 | 3 | Interesting trust feature, but depends on metadata/catalog foundations. |

## Watch Or Reject

| Candidate | Decision | Reason |
| --- | --- | --- |
| Full video player | Reject for now | Not central to image workflow; qimgv-style hybrid scope would add many unrelated edge cases. |
| Cloud sync/account features | Reject | Conflicts with local-first and no-subscription positioning. |
| Automatic model downloads | Reject unless opt-in | Existing model policy requires visible user action, hash verification, and delete controls. |
| Full RAW development suite | Later/watch | Too large; current app should stay viewer/workflow first. |
| Scientific/whole-slide domain support | Watch | Needs a target user and tile/metadata architecture first. |

## Dependency Order

1. Roadmap/context hygiene.
2. Security and runtime provenance.
3. Settings/accessibility surface.
4. jpegtran artifact staging and release smoke.
5. Destructive-action recovery center.
6. Embedding provider seam and derived-data controls.
7. Semantic search and AI-assisted tools.
8. Deep-zoom and specialized imaging.
