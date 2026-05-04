# Phase 5 — Adversarial self-audit (Factory iter-1, 2026-04-24)

**Mode note**: Per recipe §Execution-Modes, Phase 5 should run on a **different model family** than Phases 2-4. The orchestrator's Gemini probe did deliver partial content (picked up by our harvest/sources files post-hoc), but the Codex and Claude-Sonnet probes came back empty. In the spirit of honest fallback, this audit is done by the same model family that wrote the harvest + scoring — declared degradation.

## 7-dimension check

### 1. Source traceability
- **Pass.** Every item in `iter-1-harvest.md` has `Provenance: <source>` inline. Sources file enumerates 60 entries across all 9 classes.
- **Gap**: Sources were not freshly fetched from the internet during this run (orchestrator Gemini recovered enough signal but we can't claim "April 2026 current state" from a single cached sweep). Any item tagged "as of April 2026" relies on knowledge through the assistant's cutoff. Next iter: if `rtk curl`-based fetch from a few key release-notes pages would materially improve signal, re-run Phase 1 with that instead of orchestrate.sh.

### 2. Tier placement reasoning
- **Pass for NOW tier.** All 11 NOW items have a written composite score and a reasoning line. Top 6 picked for L2 this iteration are all scored ≥ 26.
- **Gap for LATER / UNDER-CONSIDERATION**: condensed listing; scores noted but dimension-by-dimension breakdown not written for every item. Acceptable because LATER items aren't being actioned this run — they'd warrant dimension breakdowns if promoted.

### 3. Category coverage
Claimed 13-track coverage: security, a11y, i18n, observability, testing, docs, distribution, plugin, mobile, offline, multi-user, migration, upgrade.
- Security: **covered** (S-04, S-06, S-08 still open; V15-09 crash log adjacent)
- A11y: **covered** (A-01, A-02 still open; V15-03 cheatsheet is entry-level a11y affordance)
- I18n: **covered** (I-01 Strings.resx scoped down in D4)
- Observability: **covered** (V15-09 → V02-06 → V02-07 ladder)
- Testing: **partial** (T-01 Images.Domain class lib scored into LATER; no tests land this iter, consistent with charter)
- Docs: **covered** (iter artifacts write docs/research/*)
- Distribution: **covered** (A12 SHA256SUMS, A13 Authenticode, D14/D15 winget/Scoop)
- Plugin: **deferred** (F8 chaiNNer investigation — research spike only, no ROADMAP commitment yet)
- Mobile: **N/A** (Windows-only charter — intentionally not a current target)
- Offline: **inherent** (zero-telemetry charter)
- Multi-user: **N/A** (single-user viewer, no accounts)
- Migration: **covered** (M-01 through M-06 already in v2)
- Upgrade: **covered** (SCH-01 through SCH-05 in v2)

### 4. Internal consistency
- **Pass.** v0.1.5 section sits between v0.1.2 and Cross-cutting; follows naming convention (V15-NN). Composite scores cited. Provenance IDs reference the scored.md + harvest.md IDs.
- Consistency check: V15-09 crash-log says "precursor to V02-07 minidump" — verified V02-07 still exists and targets minidump + GitHub-issue-open, not covered by V15-09.

### 5. Adversarial review
Weakest items:
- **V15-06 About dialog**: charter says "no confirmation dialogs" — is an About dialog a "dialog" that violates that rule? **Answer**: no — the charter rule specifically says "no confirmation dialogs" (dialogs that gate action behind a user-confirm step). An About surface is an informational affordance, not a confirmation gate. Proceed.
- **V15-07 F11 fullscreen**: charter says "keyboard-navigation required" but fullscreen removes the side panel which holds the rename affordance. **Answer**: fullscreen is explicitly an *opt-in* mode (press F11 to enter, F11 again to exit) — rename isn't available in fullscreen but the user just exits. No charter violation.
- **V15-10 Print**: not a core viewer concern. Rejected during scoring? **Answer**: scored 24 which barely qualifies for NOW. Print is the lowest-impact in the NOW tier; can be deferred to iter-2 without shame.

### 6. Charter alignment
Charter bullets (from `ROADMAP.md` §Vision + repo `CLAUDE.md`):
- Dark, keyboard-first, Windows-only — **all V15 items align**.
- Local-first, zero telemetry — **V15-01..10 don't phone home**.
- No cloud, no subscription — **N/A** (no payment surface proposed).
- Live inline rename — **not disrupted by V15 items**.
- Differentiators (CLIP, visual-diff, network-egress transparency) — **not advanced by V15**, but V15 is polish not core-differentiator work. Core differentiators live in v0.2.0+.

### 7. File-on-disk verification
- `docs/research/iter-1-state-of-repo.md` — **written**.
- `docs/research/iter-1-sources.md` — **written** (60 entries); auto-extended by Gemini probe.
- `docs/research/iter-1-harvest.md` — **written** (115 items); auto-extended by Gemini probe.
- `docs/research/iter-1-scored.md` — **written** (11 NOW + 10 NEXT + LATER/UNDER/REJECTED tail).
- `docs/research/iter-1-audit.md` — **this file**.
- `ROADMAP.md` — **updated** with v0.1.5 section (V15-01..10).

## Verdict
All 7 checks pass with acknowledged single-family-audit degradation. Phase 4 reconcile stands. Proceed to L2 implementation of V15-01 through V15-06 (top 6 composite scores, ~5-10 minutes each, all additive).
