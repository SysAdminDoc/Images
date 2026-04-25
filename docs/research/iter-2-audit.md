# Phase 5 — Adversarial self-audit (Factory iter-2, 2026-04-24)

Same mode degradation as iter-1: single-family self-audit (orchestrator's Codex/Claude-Sonnet probes unreliable on Windows). Declared upfront.

## 7-dimension check

### 1. Source traceability
Pass — `iter-2-sources.md` adds 7 entries (67 total including iter-1). Each has URL + takeaway. Delta mode correctly limits to net-new.

### 2. Tier placement reasoning
Pass for NOW tier — all 6 NOW items have explicit composite scores + "elevate because…" reasoning for borderline cases (NOW-13, NOW-16). NEXT-11 and NEXT-12 collapsed into the same shipment because effort is cheap and they're adjacent.

### 3. Category coverage (13 tracks)
- Security: **partial** (no security task this iter; iter-1 shipped A-03 + S-02/S-03). Next iter: D-05 Authenticode / S-04 CVE gate.
- A11y: covered via iter-1; no new a11y regressions.
- I18n: **partial** (H1 `MetadataDate` is i18n-adjacent defensive scaffolding).
- Observability: **covered** (NOW-13 Serilog + NOW-14 minidump promote V15-09 into structured infra).
- Testing: still deferred per charter (no tests unless requested).
- Docs: covered (NEXT-12 DPI audit).
- Distribution: **covered** (NOW-16 update check; installer hygiene G2 deferred).
- Plugin, mobile, offline, multi-user, migration: N/A or unchanged.
- Upgrade: **covered** (NOW-16 update check closes part of it).

### 4. Internal consistency
- V15-10 section already exists in ROADMAP; mark as closing with a commit ref.
- V02-06 / V02-07 numbering sits in v0.1.2 block — still valid tags; shipping them now advances the intent.
- P-04 is a cross-cutting track item; will mark `[x]` with commit ref in reconcile.
- NEXT-11 / NEXT-12 = internal-only workings; won't appear in ROADMAP per the charter (keep ROADMAP = user-facing features).

### 5. Adversarial review
Weakest items:
- **NOW-13 Serilog**: adds a dep. Dep scan (D1/D2) MUST run after this lands to catch any upstream CVE on the chain. Added to iter-2 D-phase schedule.
- **NOW-16 update check**: this IS a network egress — charter line is "network-egress transparency, never silent". Mitigation: log every call via `CrashLog.Append` (diagnostic severity) with `{url, bytes, ms}` same scheme P-03 describes; toast on first-check per session. Opt-out setting.
- **NOW-17 zoom modes**: touches `ZoomPanImage` — the one piece of code the entire viewer depends on. Risk 4, not 5. Mitigation: the existing ResetView() + OneToOne() are treated as Fit + 1:1 equivalents; new modes are ADDITIVE (SetZoomMode enum with Fit/OneToOne/FitWidth/FitHeight/Fill), existing methods unchanged. Rollback is a single-line revert of the SetZoomMode switch.

### 6. Charter alignment
- "Dark, local-first, zero-telemetry" — NOW-16 update check is the first network egress. Mitigated by opt-out + transparent logging per above.
- "No cloud, no subscription" — NOW-16 hits GitHub API read-only; no account. Safe.
- Charter-review risk: none.

### 7. File-on-disk
- `docs/research/iter-2-state-of-repo.md` — written.
- `docs/research/iter-2-sources.md` — written (7 additions).
- `docs/research/iter-2-harvest.md` — written (12 delta items).
- `docs/research/iter-2-scored.md` — written (6 NOW + 2 NEXT-collapsed).
- `docs/research/iter-2-audit.md` — this file.

## Verdict
All 7 checks pass with acknowledged single-family-audit degradation + two explicit mitigations (Serilog D-phase, update-check egress transparency). Proceed to L2 with the 8-item batch.
