# Changelog

All notable changes to **Images** are documented here.

## Unreleased

### Security

- **S-02** — Argv-open hardening. `App.xaml.cs` normalizes `argv[0]` through `Path.GetFullPath` + `File.Exists` and rejects device-namespace (`\\?\`, `\\.\`) shapes outright. `MainViewModel.RevealInExplorer` switches from `UseShellExecute=true` + embedded-quote `Arguments` string to `UseShellExecute=false` + `ArgumentList.Add("/select," + Path.GetFullPath(CurrentPath))`, so filenames with commas, quotes, or trailing spaces cannot compose an injection against `CommandLineToArgvW` quoting rules.

### Accessibility

- **A-03** — Keyboard focus + Escape discipline. New shared `FocusVisual` style in `Themes/DarkTheme.xaml` (2 px inset dashed `FocusRingBrush` rect, ~7:1 contrast on the Catppuccin base — WCAG-AA pass) is wired via `FocusVisualStyle` setters on `ChromeButton` / `PrimaryButton` / `NavArrowButton` / `ToolbarButton` / the ambient `TextBox` style. The `RecentRenames` ItemsControl gains `KeyboardNavigation.DirectionalNavigation="Cycle"` + `TabNavigation="Continue"` + `AutomationProperties.Name`. `Window_KeyDown` now handles `Escape` to dismiss an active drop overlay and return focus to the shell.

### CI

- Bumped release workflow actions off the deprecated Node 20 runner: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. GitHub removes Node 20 runners on 2026-06-02 — this clears that deprecation well ahead of the deadline.
- **S-03** — `.github/dependabot.yml` added for the two ecosystems in use (`nuget`, `github-actions`). Weekly sweep on Monday, grouped by package family (`Magick.NET*`, `Microsoft.*`, `actions/*`), commit prefixes `chore(deps)` / `chore(ci)`. Security-advisory PRs bypass the 5-PR throttle per Dependabot defaults.

### Dependencies

- `Magick.NET-Q16-AnyCPU` 14.12.0 → 14.13.0 and `Magick.NET.Core` 14.12.0 → 14.13.0 (minor bump; keeps the bundled native decoder stack current for ImageGlass-advisory CVE hygiene per ROADMAP S-03). Build clean, 0 warnings.

### Branding

- Added the project logo. `src/Images/Resources/logo.png` ships as a WPF `<Resource>` for in-app use; `src/Images/Resources/icon.ico` is a 7-frame multi-resolution Windows app icon (16/24/32/48/64/128/256, Catmull-Rom downscale from a square-padded 431×431 source) wired via `<ApplicationIcon>` in `Images.csproj` — the built `Images.exe` now shows the logo in Explorer, the taskbar, and Alt-Tab. `icon.svg` is a PNG-embedded SVG wrapper for web/README contexts.
- Added the project banner at `assets/banner.png` and embedded it at the top of the README.

### CI

- Bumped release workflow actions off the deprecated Node 20 runner: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. GitHub removes Node 20 runners on 2026-06-02 — this clears that deprecation well ahead of the deadline.

### Docs

- `ROADMAP.md` refreshed to v2 (2026-04-24). Seeded v1 covered viewer/editor/organizer/converter/AI/plugins; v2 adds nine cross-cutting tracks — security (S-01–S-10 incl. WIC CVE-2025-50165, libwebp CVE-2023-4863, SharpCompress zip-slip, ExifTool safe invocation, MSIX AppContainer, Wasmtime-hosted decoder spike), privacy (P-01–P-07 incl. strip-location, default-off telemetry, network-egress log panel, C2PA read/verify via `c2patool`), accessibility (A-01–A-06 incl. custom `ImageAutomationPeer`, high-contrast `SystemColors` theme, Magnifier-aware UIA events, published UIA tree), i18n/l10n (I-01–I-05 incl. Crowdin for OSS, RTL audit, `DateTimeOffset` switch), observability (O-01–O-05 incl. Serilog, opt-in Sentry, ETW decode counters, local minidump), testing (T-01–T-05 incl. `Images.Domain` pure lib + FlaUI smoke + golden-image diff), distribution (D-01–D-07 incl. winget via `WinGet Releaser`, Scoop `extras`, Microsoft Store MSIX, Azure Trusted Signing), catalog-schema strategy (SCH-01–SCH-05 — XMP sidecars authoritative, forward-only hop-don't-jump EF Core migrations), and migration-from-competing-tools (M-01–M-06 — Picasa `.picasa.ini`, Lightroom `.lrcat`, digiKam, XnView, Apple Photos, IrfanView). Refreshes AI track with Windows ML dual-path (saves ~150 MB installer on Win11 24H2+ by skipping private ORT), cjpegli export (F-03, ~35% smaller JPEG), LaMa generative erase (U-03), Copilot+ Restyle (U-04); drops HEVC-bundling (Nokia enforcement). Adds Appendix A with 220+ deduplicated source URLs so every item is traceable. Companion research filed under `docs/gap-research-report-1.md` + `docs/gap-research-report-2.md`.
- README architecture tree now shows `Resources/icon.ico`, `icon.svg`, `logo.png` instead of the "not yet added" placeholder.

## v0.1.1 — 2026-04-24

### Changed

- Folder watcher (`FileSystemWatcher`) now actually runs — external add/delete/rename from Explorer or another app refreshes the list without pressing F5. The position chip updates live, and if the currently displayed file vanishes, the viewer advances to the next slot the navigator lands on.
- `BoolToVis` converter is now declared in `Themes/DarkTheme.xaml` (single source of truth, available to any view) instead of being redeclared per-window.
- `ImageLoader.Load` narrows its WIC catch to decode/format exceptions — `OutOfMemoryException` and thread aborts now propagate instead of silently falling through to Magick.NET. The WIC-path `MemoryStream` is disposed deterministically.
- `DirectoryNavigator.Open` short-circuits when called with a path inside the already-watched folder — no more full rescans on repeat drops from the same directory.
- `DirectoryNavigator.Rescan` catches transient IO / ACL / disconnection errors and keeps the prior list instead of throwing to the UI thread.

### Docs

- README zoom row clarifies wheel-zoom anchors on the cursor; removed the stray `Ctrl+wheel` alias claim that the code never honored.
- README architecture tree no longer claims a shipped `Resources/icon.ico` (icon is a v0.1.2 follow-up).

## v0.1.0 — 2026-04-24

Initial release.

- WPF / .NET 9 image viewer with WIC + Magick.NET decode pipeline (~100 formats incl. BMP/JPG/PNG/GIF/TIFF/WEBP/HEIC/ICO/JXL/AVIF/PSD/TGA/RAW).
- Windows 7 Photo Viewer–inspired chrome in Catppuccin Mocha dark theme.
- Hover-reveal left/right navigation arrows; Left/Right/Home/End/Space/Backspace keyboard navigation with wrap-around.
- Natural-sort directory scan on open; auto-refresh when files are added/removed.
- Split stem/extension rename editor with 600 ms debounced auto-save, live conflict preview, commit-on-navigation.
- Recent Renames panel with one-click undo for the last 10 renames.
- Zoom (wheel / Ctrl+wheel), pan (drag), fit-to-window, 1:1, rotate, delete-to-recycle-bin.
- Command-line: `Images.exe "C:\path\to\image.jpg"` opens file and populates directory.
