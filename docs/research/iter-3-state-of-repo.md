# Phase 0 — State of Repo (Factory iter-3, 2026-04-24, same-day on v0.1.6)

## Shipped state
- **Version**: v0.1.6 (shipped this session). Factory iter-2 closed 8 tasks.
- **Last commit**: `b0d5117` — release: v0.1.6.
- **Working tree**: clean. Synced with `origin/main`.

## Scale gate
| Metric | Reading | Threshold | Trip? |
|---|---|---|---|
| Source LOC | 3,088 (+778 since iter-2) | 50,000 | no |
| Tracked files | ~50 | 500 | no |
| Tests | 0 | 1,000 | no |
| Open ROADMAP P0/P1 | 181 (-5 after iter-2 closures) | 30 | **YES** |

Large-Repo Mode engaged, per iter-1/iter-2.

## Iter-2 delta consumed
- V15-10 Print, V02-06 Serilog, V02-07 minidump, E6 Save-as-copy, P-04 update check, V20-20 partial (4/6 zoom modes), NEXT-11 MetadataDate scaffold, NEXT-12 DPI audit doc all closed.
- Deps added: Serilog 4.2, Serilog.Sinks.File 6.0, Serilog.Extensions.Logging 9.0, Microsoft.Extensions.Logging 9.0.

## Iter-3 theme: v0.2.0 foundations (V20-02 / V20-03 / V20-04)
Per ROADMAP §v0.2.0 intro: "replace the canvas engine, add persistence, match IrfanView / ImageGlass / JPEGView viewer baseline." The canvas engine replacement (V20-01 SkiaSharp) is architectural surgery deferred to its own run. The persistence + preload + thumbnail-cache trio lands now because each enables multiple cross-cutting items that are blocked without them.

Target batch for iter-3:
1. **V20-02 SQLite settings service** — schema v1 (key/value + recent_folders + hotkeys), EF-less ADO.NET via `Microsoft.Data.Sqlite`. SCH-01 (sidecar-authoritative, rebuild-from-disk is valid recovery) applies from day one.
2. **Window-state persistence** — first real V20-02 consumer.
3. **Update-check opt-out toggle** — second V20-02 consumer (Previously P-04 shipped with settings TODO).
4. **Recent folders MRU** — third V20-02 consumer; data only, UI surface lands v0.1.8.
5. **V20-03 preload N±1** — ring buffer of decoded `LoadResult`, cancellation on nav.
6. **V20-04 thumbnail cache disk layer** — `%LOCALAPPDATA%\Images\thumbs\<hash>.webp` keyed by path+mtime+size; used by V20-21 filmstrip (not shipped this iter).
7. **A-01 custom ImageAutomationPeer** — accessibility differentiator; no decoder coupling.

That's 7 concrete items plus plumbing. v0.1.7 patch bump — V20-02 is the sub-task umbrella, not a full minor bump. The minor v0.2.0 waits until the full V20 foundations are mature (SkiaSharp + settings UI + filmstrip shipping).

## Phase-rotation state
| Phase | Last run | Run iter-3? |
|---|---|---|
| UX polish (U*) | iter-2 (CrashDialog) | rotated — no new visual chrome this iter |
| Theming (T*) | iter-2 (crash dialog styles via existing tokens) | rotated |
| Dep scan (D*) | iter-2 (Serilog chain added, implicit scan) | **RUN** (Microsoft.Data.Sqlite added this iter) |
| Modularization (M) | never | skipped (3088 LOC < 5K) |
| Research | iter-2 | **RUN** (delta mode) |

## Estimated burndown
Iter-1 closed 9 tasks. Iter-2 closed 8. Iter-3 targets 7. At ~8 tasks/run the 181-item backlog drains in ~22 more runs assuming no replenishment. Realistic with replenishment = ~30 runs. Each run ships a working build with atomic commits, so partial-burn-down progress is durable.
