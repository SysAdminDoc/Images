# Phase 2 — Raw harvest delta (Factory iter-2, 2026-04-24)

Merge-into-iter-1 mode. New items below; iter-1-harvest.md remains the full catalogue.

## Bucket G — Net-new delta from iter-1 closures (8 items)

G1. **V20-06 large-file "decoding…" toast** — iter-1 A4 got filed but wasn't implemented. Make it concrete: `if (fi.Length >= MemoryMapThreshold) Toast("Decoding large image…");`. Provenance: own iter-1 A4 + V20-06 UX gap.

G2. **Installer: existing-portable detector** (iter-1 A7, never scheduled). Query `HKCU\Software\Microsoft\Windows\CurrentVersion\App Paths\Images.exe` on install; if present, prompt. Provenance: iter-1 A7.

G3. **V15-10 `PrintCurrentImage`** — `PrintDialog` + `FixedDocument` with single page scaled fit-to-page, orientation matches image aspect. Provenance: iter-1 NOW-tier deferred.

G4. **V02-06 `Serilog.LoggerConfiguration`** — `WriteTo.File(Path.Combine(%LOCALAPPDATA%, "Images", "Logs", "images-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)`. Wrapped behind a static `Log` helper so callers stay simple. Provenance: O-01 impl.

G5. **V02-07 `MiniDumpWriteDump` P/Invoke** — `dbghelp.dll`; writes `%LOCALAPPDATA%\Images\Logs\crash-<ts>.dmp`. GUI dialog after shows the path + a "Copy details to clipboard" button + "Open GitHub Issue" (opens pre-filled URL). Provenance: O-04 impl.

G6. **E6 `SaveAsCopyCommand`** — "Save as…" dialog, default filename `<stem>_copy<ext>`, writes the CURRENT BitmapSource (after rotation/flip) using an encoder matched to the extension. New file becomes the selected file in the folder nav. Provenance: iter-1 E6.

G7. **P-04 `UpdateCheckService`** — background Task, fires once per 24 h + on manual "Check for updates" menu item. `HttpClient.GetAsync("https://api.github.com/repos/SysAdminDoc/Images/releases/latest")`; compare tag vs AppInfo.DisplayVersion; toast if newer. Opt-out setting stored in `%LOCALAPPDATA%\Images\state.json` (first use of the state.json file). Provenance: iter-1 D10 / P-04.

G8. **V20-20 zoom modes (partial)** — a minimal four: Fit, 1:1, Fit-Width, Fit-Height. Add cycle button + menu + `ZoomPanImage.SetZoomMode(enum)`. Provenance: iter-1 C14 — shrunk.

## Bucket H — Delta audit of iter-1 items (not previously scheduled) (4 items)

H1. **I-04 date parsing / display** — use `DateTimeOffset` for anywhere the code reads EXIF `DateTimeOriginal`. Current code: zero sites use DateTime yet (rename service is path-based, not time-based) — so this is purely defensive for when metadata display lands. Ship a `MetadataDate` value type that wraps `DateTimeOffset?` and convenience `.ToLocalDisplay(CultureInfo)`. Provenance: iter-1 D5 / I-04.

H2. **E17 High-DPI hard-coded pixel audit** — grep for `Width="` / `Height="` / `Margin="` with literal px > 0 in all XAML; count occurrences; note which ones are actually safe (baseline 96 DPI units resolve fine) vs which are fragile. Deliverable: `docs/dpi-audit.md`. Provenance: iter-1 E17.

H3. **Installer: Check for Updates command line** — when a user clicks "Check for updates" from the About dialog, the action lives in `UpdateCheckService` per G7. Provenance: G7 dependency.

H4. **About-dialog: open-crash-log-folder button** — already shipped in V15-06 but wasn't tagged to any ROADMAP item; document the cross-cut so iter-2 state captures it. Provenance: v0.1.5 retrospective.

## Harvest delta total: 12 new items
