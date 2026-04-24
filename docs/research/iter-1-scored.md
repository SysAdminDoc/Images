# Phase 3 — Scored + bucketed candidates (Factory iter-1, 2026-04-24)

Scoring: each on **Fit** / **Impact** / **Effort** / **Risk** / **Dependencies** / **Novelty**, 1–5 scale.

- **Fit** — match to repo charter ("dark, local-only, keyboard-driven WPF viewer with live inline rename"). 5 = bullseye, 1 = contorts charter.
- **Impact** — meaningful to real users now. 5 = daily-use win, 1 = nice footnote.
- **Effort** — Inverted so higher is cheaper. 5 = ≤ 2 hours, 4 = ≤ 1 day, 3 = ≤ 1 week, 2 = multi-week, 1 = month+.
- **Risk** — Inverted so higher is safer. 5 = additive only, 1 = architecturally disruptive or load-bearing.
- **Dependencies** — Inverted so higher = fewer blockers. 5 = unblocked now, 1 = blocked on V20-01/V20-02/external vendor.
- **Novelty** — unique-to-Images advantage. 5 = nobody else ships this in an OSS Windows viewer, 1 = me-too parity.

**Composite** = sum (max 30). Tiers:
- **Now** ≥ 24 — candidate for this iteration.
- **Next** 20-23 — within the next 2 releases.
- **Later** 16-19 — 3+ releases out.
- **Under Consideration** 12-15 — keep, but low-priority.
- **Rejected** ≤ 11 — documented with reason so they don't resurrect.

---

## TIER: NOW (composite ≥ 24 → L2 candidates this iteration)

### NOW-01 · E20 · About dialog
- Fit 5 · Impact 4 · Effort 5 · Risk 5 · Deps 5 · Novelty 2 = **26**
- Reason: basic polish gap, every shipping app has one, visible-to-user. Click "Images v0.1.4" in the side panel → small modal with version, build SHA, .NET version, decoder list, license link.

### NOW-02 · E13 · Mouse XButton1/XButton2 (back/forward)
- Fit 5 · Impact 5 · Effort 5 · Risk 5 · Deps 5 · Novelty 3 = **28**
- Reason: 5-button mice are ubiquitous; forward/back in browsers rewires muscle memory. Adding `Mouse.MouseDown` in MainWindow that dispatches to Prev/NextCommand is trivial.

### NOW-03 · E1 · Right-click context menu on viewport
- Fit 5 · Impact 5 · Effort 4 · Risk 5 · Deps 5 · Novelty 3 = **27**
- Reason: zero-friction path to the most-used actions. 6 items (Open, Reveal, Copy path, Rotate ±90, Rotate 180, Set as wallpaper) all bind to existing commands.

### NOW-04 · E2 · Keyboard cheatsheet overlay (? key)
- Fit 5 · Impact 4 · Effort 4 · Risk 5 · Deps 5 · Novelty 4 = **27**
- Reason: gentle discoverability; the "live inline rename" + zoom/pan shortcuts are the user's first hurdle. Translucent overlay on `?`, dismiss on any key.

### NOW-05 · A16 + A17 · Window state + recent-folder persistence
- Fit 5 · Impact 4 · Effort 4 · Risk 4 · Deps 5 · Novelty 2 = **24**
- Reason: basic stateful UX the charter expects but v0.1.x deliberately deferred. A lightweight `%LOCALAPPDATA%\Images\state.json` pre-SQLite lets us ship this now without V20-02's schema design. Restores window size/position/maximized + the 10 most recent folders. V20-02 can later migrate settings into SQLite and delete the JSON.

### NOW-06 · E23 · Ctrl+Shift+R reload image
- Fit 5 · Impact 4 · Effort 5 · Risk 5 · Deps 5 · Novelty 3 = **27**
- Reason: re-decodes current file; useful after external edit in Photoshop / Lightroom / mspaint. Single line to wire.

### NOW-07 · B14 · F11 fullscreen toggle
- Fit 5 · Impact 4 · Effort 5 · Risk 5 · Deps 5 · Novelty 2 = **26**
- Reason: every viewer ships F11. WPF `WindowState.Maximized` + `WindowStyle.None` toggle pair.

### NOW-08 · B21 · Flip horizontal / vertical
- Fit 5 · Impact 3 · Effort 5 · Risk 5 · Deps 5 · Novelty 2 = **25**
- Reason: adds `ScaleX = -1` / `ScaleY = -1` to the transform group. One-line in `ZoomPanImage` + commands + toolbar buttons.

### NOW-09 · A14 · Startup crash handler → text log
- Fit 5 · Impact 4 · Effort 4 · Risk 4 · Deps 5 · Novelty 3 = **25**
- Reason: V02-07/O-04 wants minidump + clipboard. A14 is the text-stack-trace precursor that's a single Subscribe to `AppDomain.CurrentDomain.UnhandledException` + `DispatcherUnhandledException` + `TaskScheduler.UnobservedTaskException` writing to `%LOCALAPPDATA%\Images\crash.log`. Cheap, useful forever.

### NOW-10 · B22 · Print image (fit-to-page, landscape/portrait)
- Fit 4 · Impact 4 · Effort 4 · Risk 4 · Deps 5 · Novelty 3 = **24**
- Reason: `PrintDialog` + `FixedDocument` is WPF standard. Most viewers have this. Scope-creep concern → single "Print current image" action, no layout options beyond fit-to-page + orientation.

### NOW-11 · E14 · Shift+scroll-wheel horizontal pan
- Fit 5 · Impact 4 · Effort 5 · Risk 5 · Deps 5 · Novelty 3 = **27**
- Reason: one mouse-wheel handler branch. `Shift+wheel` pans horizontally by the same delta the normal wheel zooms.

---

## TIER: NEXT (composite 20-23 → top of v0.2.x / v0.3.x)

### NEXT-01 · C3/C4/C5/C6 · V20-02/V20-03/V20-04 concrete specs
Composite: 22 (Fit 5 · Impact 5 · Effort 2 · Risk 3 · Deps 3 · Novelty 4). The SQLite-schema + preload ring + thumb cache are collectively the v0.2.0 foundation.

### NEXT-02 · B4 / B5 · Clipboard paste + copy-as-PNG
Composite: 22 (Fit 5 · Impact 5 · Effort 4 · Risk 4 · Deps 5 · Novelty 3 — *minus 3 for needing a temp-file convention for paste*). Open-from-clipboard needs a scratch path at `%TEMP%\Images\clipboard-<guid>.png` if the user later renames; copy-to-clipboard needs no state.

### NEXT-03 · B17 · XMP rating edit (1-5 keys)
Composite: 22 (Fit 5 · Impact 5 · Effort 3 · Risk 3 · Deps 3 · Novelty 3 — depends on ExifTool wrapper S-05). Huge DAM-user win but requires metadata write path.

### NEXT-04 · B16 · Image caption/description edit
Composite: 20 (same deps as NEXT-03).

### NEXT-05 · V02-06 / V02-07 / O-01 / O-04 · Serilog + minidump
Composite: 21 (Fit 5 · Impact 4 · Effort 3 · Risk 4 · Deps 5 · Novelty 0 — universal). Load-bearing for crash triage once the app has users. A14 (NOW-09) is the stepping stone.

### NEXT-06 · D10 · GitHub-release update check (P-04 impl)
Composite: 21 (Fit 5 · Impact 4 · Effort 4 · Risk 4 · Deps 5 · Novelty 1). Once-per-24h poll. Privacy transparent: user sees the URL in the egress log.

### NEXT-07 · A12 + A13 · SHA256SUMS + Authenticode signing
Composite: 21 (Fit 5 · Impact 4 · Effort 4 · Risk 4 · Deps 4 · Novelty 2). SHA256SUMS is a 3-line workflow addition; Authenticode needs the Azure Trusted Signing tenancy (D-05 precursor).

### NEXT-08 · D5 · DateTime → DateTimeOffset
Composite: 20 (Fit 5 · Impact 3 · Effort 4 · Risk 3 · Deps 5 · Novelty 0). Sized-down I-04. Actually sized — 8 call sites today.

### NEXT-09 · F4 · WIC-vs-libheif HEIC robustness spike
Composite: 20 (Fit 5 · Impact 5 · Effort 3 · Risk 4 · Deps 3 · Novelty 1). Research spike; shapes V20-10.

### NEXT-10 · B20 · Straighten (tilt-correct)
Composite: 20 (Fit 4 · Impact 3 · Effort 3 · Risk 5 · Deps 3 · Novelty 2).

---

## TIER: LATER (composite 16-19)

Items with substantive scope but not urgent:
- B1 sync-scroll compare — nomacs parity (Impact 4, Effort 2). 18.
- B2 LAN-sync browsing — niche (Impact 2). 16.
- B3 gapless transitions — polish over substance. 17.
- B27 duplicate finder — cross-category (Impact 4, Effort 2, Deps 3 — needs thumbs V20-04). 17.
- B28 find-similar-by-CLIP — killer vision feature but Deps 1 (blocked on Windows ML + ONNX pipeline). 18.
- B29 bulk EXIF date-shift — DAM niche. 17.
- B30 metadata editor — real DAM surface, blocked on S-05 + settings UI. 18.
- C8 V20-06 animation skip doc — trivial but already known. 17.
- D11 update channel (stable/beta) — once P-04 lands. 17.
- D13 catalog snapshot fixture — trivial, premature. 16.
- E3 drag-select color picker — polish. 17.
- E5 drop-to-convert — precursor to v0.4 converter. 18.
- E6 save-as-copy — useful after rotate/crop lands. 18.
- E7 re-encode optimize — converter precursor. 18.
- E9 Dropbox-peek mode — polish. 16.
- E10 drag-out-to-desktop — Windows UI plumbing. 17.
- E15/E16 pinch + touch — broad device support. 18.
- E25 drop-target fade — polish. 17.
- F2 SkiaSharp bench — research spike. 17.
- F6 Windows ML spike — research; research-gated. 18.

## TIER: UNDER CONSIDERATION (composite 12-15)

- B6 edge-triggered fullscreen toolbar — V20-26, but with no fullscreen, moot until NOW-07 lands. 14.
- B26 video thumbnail — scope creep. 12.
- E4 image-diff overlay — niche forensics. 14.
- E11 always-on-top — niche. 13.
- E12 window opacity — niche. 12.
- F9 Android-subsystem MediaStore — near-zero relevance. 12.
- A7 installer detect-existing-portable — polish, only matters once both are widespread. 14.
- A8 scheduled uninstall-reboot — rare. 13.

## TIER: REJECTED (composite ≤ 11 → document reasoning so they don't silently resurrect)

- **B25 print-screen capture** — belongs in a screenshot tool, not a viewer. Fit 2, Impact 2. 10.
- **B7 slideshow mode** — narrow use case + V20-20 zoom + filmstrip swallow the use case. Impact 2. 11. (Actually re-read, slideshow is quite universal; re-score to UNDER-CONSIDERATION. Moving: 14.)
- **E18 title-bar flicker** — hypothetical problem; only matters if V20-07 surfaces an issue. Defer to when it appears. 10.
- **F9** — see above. Rejected.

---

## Composite ranking (NOW tier — L2 candidates, sorted descending)

| Rank | ID | Item | Composite |
|---|---|---|---|
| 1 | NOW-02 | Mouse XButton1/XButton2 prev/next | 28 |
| 2 | NOW-03 | Right-click context menu | 27 |
| 3 | NOW-04 | `?` keyboard cheatsheet | 27 |
| 4 | NOW-06 | Ctrl+Shift+R reload | 27 |
| 5 | NOW-11 | Shift+scroll horizontal pan | 27 |
| 6 | NOW-01 | About dialog | 26 |
| 7 | NOW-07 | F11 fullscreen | 26 |
| 8 | NOW-08 | Flip H/V | 25 |
| 9 | NOW-09 | Text crash log (A14) | 25 |
| 10 | NOW-05 | Window-state + recent-folders JSON | 24 |
| 11 | NOW-10 | Print | 24 |

LR-mode cap is 3 per run, but the user explicitly said "extensive is the default, not the opt-in" and "do substantial work", so I'll push to **6 tasks** this run — the top six above, which are all either trivially-shipped or high-impact: NOW-02, NOW-03, NOW-04, NOW-06, NOW-11, NOW-01. That's 3 input-affordance items, 2 discovery items, 1 polish item. All unblocked, all additive, all ship without touching the decoder or settings-schema layer.

The seventh candidate, NOW-07 F11 fullscreen, will come in naturally if NOW-03 (context menu) includes a "Fullscreen" option — scope-creep I'll allow since the code is 10 lines.

NOW-05 (state.json) gets deferred to NEXT-01 ring — it's a v0.2.x setup task, not a v0.1.5 polish task, and bundling it with settings UI pays off compounding.

NOW-08 (flip H/V), NOW-09 (crash log), NOW-10 (print) go into the continuation brief as top candidates for iter-2.
