# Phase 2-5 condensed — iter-3 delta research

**Delta mode**: same-session; time-since-iter-2 ≈ 30 min; real external delta ≈ 0. Phase 1 source delta is zero usable new URLs (no dep releases, no CVEs, no competitor posts in the same-session window). Reusing `iter-1-sources.md` (60 entries) + `iter-2-sources.md` (+7) as the source base. This file combines Phase 2 (harvest) + Phase 3 (scoring) + Phase 5 (audit) into a single artifact since the delta is small.

## Phase 2 — Harvest delta (10 items)

I1. **V20-02 `SettingsService`** — SQLite at `%LOCALAPPDATA%\Images\settings.db`. Schema: `settings(key TEXT PK, value TEXT)`, `recent_folders(path TEXT PK, last_opened INTEGER)`, `hotkeys(action TEXT PK, key TEXT, modifiers TEXT)`. Migration 0001 seeds default keys (`telemetry.enabled=false`, `update-check.enabled=true`, `zoom.mode=Fit`). `IValueStore` abstraction over the table so callers don't see SQL. Provenance: ROADMAP V20-02.

I2. **Window-state persistence** — save `Window.Left / Top / Width / Height / WindowState` to `settings` on `Closing`; restore on `Loaded`. Clamp to current screen bounds before apply (last-shown-on-2nd-monitor may now be single-monitor). Provenance: iter-1 NOW-05 + competitor baseline.

I3. **Update-check opt-out toggle** — new `update-check.enabled` setting default true. `UpdateCheckService.IsDueForBackgroundCheck` short-circuits on false. Exposed in the About dialog so the user can flip it without a settings UI yet. Provenance: P-04's charter obligation.

I4. **Recent-folders persistence** — write to `recent_folders` on every `OpenFile`; cap at 10 entries (delete oldest). Expose `GetRecentFolders()` for v0.1.8's Recent menu. Provenance: iter-1 A17.

I5. **V20-03 preload ring** — new `PreloadService` with `Enqueue(path)` running on a background `Task` with a `CancellationTokenSource`. Cache max 3 items (N-1, N, N+1). On nav, request cancel + new enqueue. Eviction = LRU within the 3 slots. Provenance: ROADMAP V20-03.

I6. **V20-04 thumbnail cache** — `%LOCALAPPDATA%\Images\thumbs\<sha1(path+mtime+size).substring(0,2)>\<full-sha1>.webp`. Git-like 2-char partition to avoid directory explosion. 512 MB disk cap, LRU eviction on next write after cap exceeded. Writes via Magick.NET (already present) since WPF has no native WebP encoder. Used by filmstrip (not shipped this iter). Provenance: ROADMAP V20-04.

I7. **A-01 `ImageAutomationPeer`** — subclass `FrameworkElementAutomationPeer`. `GetNameCore` = filename; `GetHelpTextCore` = "Image N of M, width × height pixels"; override `GetAutomationControlTypeCore` → `Image`; `GetAutomationIdCore` = stable path hash. Wire by setting `AutomationPeer` override on `ZoomPanImage`. Provenance: ROADMAP A-01.

I8. **Settings-service logging** — route via `Log.For<SettingsService>()` so any SQLite open/close/migration failure is visible in Serilog's rolling file. Provenance: O-01 integration.

I9. **Settings migration pattern** — `PRAGMA user_version`; migrations apply in order on open. v1 seeds the three tables. Future bumps hop — never jump. Provenance: SCH-04.

I10. **Settings recovery** — if `settings.db` is corrupt, rename to `settings-corrupt-<ts>.db`, start fresh with schema v1, log the recovery. Charter: sidecar/cache philosophy — DB is disposable (SCH-01). Provenance: SCH-01 applied.

## Phase 3 — Scoring delta

All 10 items score in the NOW tier (composite ≥ 22) because they're all sub-tasks of ROADMAP V20-02 / V20-03 / V20-04 / A-01 which were already scored as NOW-tier material in prior iters. Grouped scoring:

| Group | Composite | Reasoning |
|---|---|---|
| I1 V20-02 + I8-I10 (plumbing) | 27 | Unlocks 5+ downstream items, zero risk if the sidecar-authoritative philosophy holds |
| I2 Window state | 25 | Quality-of-life baseline every Windows app has; single binding point |
| I3 Update-check opt-out | 24 | Charter obligation; one setting key |
| I4 Recent folders | 24 | Three call sites (OpenFile / OpenFileDialog / drag-drop); no UI yet |
| I5 V20-03 Preload | 25 | Perceived perf win; independent module |
| I6 V20-04 Thumbnail cache | 24 | Blocks filmstrip v0.1.8 but ships as pure disk infra |
| I7 A-01 UIA peer | 25 | Differentiator, pure additive, ~40 LOC |

## Phase 5 — Self-audit (7 checks)

1. **Source traceability** — every item traces to a ROADMAP tag or prior iter artifact. Pass.
2. **Tier placement** — all NOW; reasoning per group. Pass.
3. **Category coverage** — security (I3 opt-out consent), observability (I8 Serilog integration), persistence (I1-I4 foundation), accessibility (I7 peer), perf (I5 preload + I6 thumbnail cache). Solid cross-cut.
4. **Internal consistency** — V20-02 plumbing enables V20-02a..c concrete tasks; V20-04 disk layer ships without V20-21 filmstrip UI, not contradiction — it's deliberate staging.
5. **Adversarial review**:
   - I1 SQLite dep addition: must scan for CVE on release. Done via iter-2's D-phase rotation schedule.
   - I2 window-state clamp: off-screen saved state from a prior multi-monitor setup would vanish the window. Must clamp via `SystemParameters.WorkArea` on restore. Noted in impl.
   - I5 preload: memory pressure on a folder of 100 MP PSDs × 3 slots = GB of pixel data. Mitigation: skip preload when source pixel count > 40 MP; rely on demand load.
   - I6 thumb cache: SHA1 key collision is astronomically unlikely but possible. Mitigation: include file size in the key input so even if two files hash the same path+mtime string, different sizes produce different keys.
   - I7 UIA peer: if the automation tree is wrong, screen readers misread. Mitigation: add `AutomationProperties.Name` bindings in XAML as the fallback path (already present from v0.1.2 a11y pass); peer augments, doesn't replace.
6. **Charter alignment** — "local-first, zero telemetry, dark, Windows-only, keyboard-driven". All items additive to that charter; I1 is the first write to user-data (`settings.db`) but it's local-only and user-scoped. No charter break.
7. **File-on-disk** — artifacts written at start of impl.

## Verdict
All 7 checks pass with acknowledged single-family-audit degradation (same as iter-1 + iter-2). Proceed to L2 with the 10-item batch scoped into 7 concrete shipments:
1. V20-02 `SettingsService` + schema v1 (covers I1/I8/I9/I10)
2. Window-state persistence (I2)
3. Update-check opt-out toggle (I3)
4. Recent-folders MRU data (I4)
5. V20-03 `PreloadService` (I5)
6. V20-04 thumbnail cache disk layer (I6)
7. A-01 `ImageAutomationPeer` (I7)
