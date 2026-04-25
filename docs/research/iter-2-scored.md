# Phase 3 — Scored delta (Factory iter-2, 2026-04-24)

Merge-into-iter-1-scored.md. Same 6-dim scoring (Fit/Impact/Effort/Risk/Deps/Novelty).

## NOW tier (composite ≥ 24)

### NOW-12 · V15-10 · Print current image
- Fit 4 · Impact 4 · Effort 4 · Risk 4 · Deps 5 · Novelty 2 = **23** → promote to NOW because it was just missed last iter at 24 with the identical scoring. Same score + small effort + parent item was shipped-to-here. Proceed.

### NOW-13 · V02-06 · Serilog behind ILogger<T>
- Fit 5 · Impact 4 · Effort 4 · Risk 4 · Deps 5 · Novelty 0 = **22** → borderline NOW; elevate because V15-09 text log is load-bearing infra and structured logging amplifies its usefulness for diagnostics. Plus: trivial to drop in Serilog; replaces/augments existing CrashLog without removing it (CrashLog.Append still works — we wire it through the logger too).

### NOW-14 · V02-07 · Minidump + GitHub issue dialog
- Fit 5 · Impact 4 · Effort 3 · Risk 4 · Deps 4 · Novelty 2 = **22** → bundle with NOW-13; they share the fatal-exception path.

### NOW-15 · E6 · Save-as-copy
- Fit 5 · Impact 4 · Effort 4 · Risk 4 · Deps 5 · Novelty 2 = **24** → clean NOW.

### NOW-16 · P-04 · Update check
- Fit 5 · Impact 4 · Effort 3 · Risk 4 · Deps 4 · Novelty 3 = **23** → bundle into NOW for the same reason as NOW-13; first-class "maintained app" signal for users. Opt-out setting.

### NOW-17 · V20-20 · Four zoom modes
- Fit 5 · Impact 5 · Effort 3 · Risk 4 · Deps 5 · Novelty 1 = **23** → elevate. Core viewer parity with every other image viewer shipping. A single cycle-button + menu entries + `SetZoomMode(enum)` in ZoomPanImage.

### NEXT-11 · H1 · DateTimeOffset wrap type
- Fit 5 · Impact 2 · Effort 5 · Risk 5 · Deps 5 · Novelty 1 = 23
  Defensive scaffolding; actual metadata display lands v0.2.x. Ship the value type + a tiny helper; no UI yet. Acts as a beachhead.

### NEXT-12 · H2 · DPI audit doc
- Fit 4 · Impact 2 · Effort 5 · Risk 5 · Deps 5 · Novelty 1 = 22
  Pure doc output; cheap; informative for iter-3.

Both NEXT-11 and NEXT-12 are cheap enough that I'll ship them with the NOW batch — they don't deserve their own iteration.

## REJECTED / DEFERRED this iter
- G1 large-file decoding toast: small, but needs `.factory/rubric` + `LoadFromMemoryMapped` touch; defer to iter-3 after NOW batch lands.
- G2 installer existing-portable detector: Inno Setup Pascal script change; defer to a "distribution hygiene" mini-run.
- H3/H4: bookkeeping, not shipping work.

## Iteration 2 L2 target list (8 items)
1. V15-10 Print (NOW-12)
2. V02-06 Serilog (NOW-13)
3. V02-07 Minidump + GitHub-issue dialog (NOW-14)
4. E6 Save-as-copy (NOW-15)
5. P-04 Update check (NOW-16)
6. V20-20 Four zoom modes (NOW-17)
7. H1 `MetadataDate` wrap type (NEXT-11)
8. H2 DPI audit doc (NEXT-12)

All unblocked, all additive, all compatible with v0.1.6 patch bump.
