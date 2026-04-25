# Phase 0 — State of Repo (Factory iter-2, 2026-04-24, same-day on v0.1.5)

## Shipped state
- **Version**: v0.1.5 (shipped this same session). Factory iter-1 closed 9/10 NOW-tier items.
- **Last commit**: `fef4b6e` — release: v0.1.5 — input + discovery polish (factory iter-1)
- **Tag status**: v0.1.5 tag pushed; [GitHub Release](https://github.com/SysAdminDoc/Images/releases/tag/v0.1.5) live with portable zip + installer.
- **Working tree**: clean. Synced with `origin/main`.

## Scale gate
| Metric | Reading | Threshold | Trip? |
|---|---|---|---|
| Source LOC | 2,310 (+463 since iter-1) | 50,000 | no |
| Tracked files | 44 (+10) | 500 | no |
| Tests | 0 | 1,000 | no |
| Open ROADMAP P0/P1 | 186 (-10 from iter-1 replenish; +1 from V15-10 reopened pattern) | 30 | **YES** |

Large-Repo Mode engaged.

## Delta from iter-1
- `iter-1-*.md` research set complete (60 sources, 115 harvest, 11 NOW scored).
- 10 V15-NN items added; 9 closed this session.
- V15-10 Print remains; promoted back to NOW-tier for iter-2.

## Phase-rotation state
| Phase | Last run | Days | Run iter-2? |
|---|---|---|---|
| UX polish (U*) | iter-1 (context menu + cheatsheet + About) | 0 | rotated out — fresh today |
| Theming (T*) | iter-1 (ContextMenu/Menu styles) | 0 | rotated out — fresh today |
| Dep scan (D*) | v0.1.2 Magick.NET bump | 0 | **RUN** — >14 days since + new imports this iter (Serilog) |
| Modularization | never | — | skipped (2310 LOC < 5K) |
| Research | iter-1 | 0 | **RUN** — delta mode |

## L2 targets for iter-2 (from iter-1 scored.md NEXT-tier + iter-1 deferrals)
- V15-10 Print (deferred NOW-tier, composite 24)
- V02-06 Serilog / ILogger<T> (O-01 impl; V15-09 precursor in place)
- V02-07 Minidump + "Copy crash details" dialog (O-04 impl)
- E6 Save-as-copy (Ctrl+Shift+S) — Write alternate of current decoded + rotated + flipped state
- P-04 Update check — once-per-24h GitHub Releases poll
- V20-20 zoom modes (partial) — at minimum Fit-to-Viewport / Fit-to-Width / Fit-to-Height / Fill buttons on toolbar
- I-04 DateTime→DateTimeOffset sized pass
- E17 High-DPI pixel-size audit (cheap scan)

Eight target items; LR mode cap is 3-5 but the user's "extensive is the default" override justifies taking 6-8 here same as iter-1. All additive, none touch the decoder's critical path.
