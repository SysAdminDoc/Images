# Changelog

All notable changes to **Images** are documented here.

## Unreleased

## v0.1.3 — 2026-04-24

Format-coverage + pixel-hygiene release. Animated GIFs actually animate, toolbar / nav-arrow icons render on enterprise Win11 images that previously showed tofu boxes, and files above 256 MB decode through a memory-mapped view instead of a managed byte[].

### Viewer

- **Animated GIF playback** — `ImageLoader.Load` now probes `.gif` / `.webp` / `.apng` / `.png` via `MagickImageCollection` before falling through to the single-frame WIC path. When a file decodes as a multi-frame sequence, `collection.Coalesce()` resolves each frame's disposal method to a full-canvas BGRA `WriteableBitmap`, and the full list is returned on a new `AnimationSequence` record (frames + per-frame delays + loop count). Fixes [V20-15]. Single-frame GIFs still fast-path through WIC — the animated decoder only pays its cost when there are ≥ 2 frames.
- **Frame-delay clamp** — 0- and sub-20-ms GIF frame delays are promoted to 100 ms the way every shipping browser does, so hostile / malformed GIFs can't pin a CPU core.
- **Loop-count honored** — `AnimationSequence.LoopCount` follows the GIF convention (0 = infinite, any other value = exact iteration count) and feeds `ObjectAnimationUsingKeyFrames.RepeatBehavior` directly, so bounded-loop GIFs stop on the right frame instead of cycling forever.
- **Animated chip** — a compact green `N frames` pill lights up in the bottom toolbar's file-info row whenever `MainViewModel.IsAnimated` is true. Reads at a glance without competing with the primary metadata.
- **V20-06 memory-mapped I/O** — files ≥ 256 MB skip the byte[] round-trip in `ImageLoader.Load` and decode directly from a `MemoryMappedFile` view (`MemoryMappedFileAccess.Read`, `FileShare.ReadWrite | Delete` to preserve the existing rename/delete story). Both the WIC primary and the Magick.NET fallback now take their own `CreateViewStream` per attempt, so a 500 MB RAW or multi-GB PSD no longer lands on the LOH — the OS pages the mapping in on demand. `DecoderUsed` reports `"WIC (memory-mapped)"` / `"Magick.NET (memory-mapped)"` so you can see which path was used.

### UI fix

- **Toolbar + nav-arrow glyphs now render everywhere** — `Themes/DarkTheme.xaml` promotes a shared `IconFontFamily` resource (`"Segoe Fluent Icons, Segoe MDL2 Assets, Symbol"`) and every icon `FontFamily` setter in `DarkTheme.xaml` + `MainWindow.xaml` (10 call sites) resolves through it. On Win11 IoT Enterprise LTSC and a handful of corporate WinPE-derived images, WPF's MDL2-only lookup landed on a text fallback and rendered every icon button as an empty white tofu rectangle; declaring Fluent Icons first + MDL2 second + `Symbol` as a last-ditch fallback collapses all three worlds without touching the glyph codepoints. Same fix applied to the six MDL2 glyph `TextBlock`s (error icon, drop-accept icon, gesture-hint icon, toast icon, extension-lock padlock + unlock pencil).

### Roadmap

- `[x]` **V20-06** — Memory-mapped I/O for files > 256 MB (avoids blowing the managed heap on 500 MP RAW).
- `[x]` **V20-15** — Animated GIF / APNG / animated AVIF playback. Transport controls (play/pause/frame-step/speed) deferred; core playback is live.

## v0.1.2 — 2026-04-24

Security + accessibility + CI hardening plus a three-wave premium-polish pass that elevates the product from functional to intentional.

### UI / UX — premium polish pass (wave 3)

- **Smooth rotate** — `ZoomPanImage.RotationProperty` now animates the `RotateTransform` via an eased (`CubicEase EaseInOut`) `DoubleAnimation` instead of snapping the angle. Duration scales with angular delta (180 ms base + up to 162 ms for a 180-degree flip) so a single rotate-left still feels quick while a 270-degree round trip stays controlled.
- **Extension chip state** — locked vs unlocked now reads at a glance. Unlocked: button border + fill inherit the `YellowBrush` / `WarningPanelBrush` pair used by the warning panel below, so the two surfaces read as a coordinated state. Glyph swaps padlock (`&#xE72E;`) → pencil-edit (`&#xE70F;`) tinted yellow.
- **Window title** — `MainWindow.Title` binds to `MainViewModel.WindowTitle`, which exposes `"{filename} — Images"` when a file is open and falls back to bare `"Images"` otherwise. Standard Windows convention; makes the taskbar label + Alt-Tab card useful.

### UI / UX — premium polish pass (wave 2)

- **Windows 11 dark caption** — new `Services/WindowChrome.cs` calls `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE, 1)` in `Window.SourceInitialized` so the native title bar stops clashing with the Mocha interior. Best-effort P/Invoke — pre-20H1 no-ops cleanly with no visual regression. `DWMWA_SYSTEMBACKDROP_TYPE` (Mica, Win11 22H2+) is documented as a future hook; wiring it wants an alpha-aware window background, deferred to a later pass.
- **Status-dot success pulse** — when `RenameStatus` transitions to `Saved`, the rename-status dot scales 1.0 → 1.5 → 1.0 via a `DataTrigger.EnterActions` storyboard walking the `(UIElement.RenderTransform).(ScaleTransform.ScaleX)` property path. Commit feedback now feels confirmed, not silent.
- **First-run gesture-hint pill** — `MainViewModel` exposes a one-shot `ShowGestureHint` + `_hintTimer` (2.4 s). First image to land in the viewport surfaces an `FloatingPill` reading "Scroll to zoom · drag to pan · double-click to fit", fades in on 280 ms `Motion.Slow`, auto-dismisses. Never shown again in the session.
- **Recent-rename hover affordance** — new `InteractiveCard` style (based on `Card`) animates `BorderBrush.Color` toward `BlueColor` on `IsMouseOver`, with matching exit transition on leave. Recent-rename entries now read as tappable rows rather than static blocks.
- **Decoder badge** — "Decoder" detail row switches from raw text to a compact `Badge` style (Surface0 fill, Hairline border, 4-px radius). WIC / Magick.NET / Unavailable now reads as a label, not a value.
- **Toolbar top highlight** — 1-px inner highlight (`#12CDD6F4` ≈ 7% white-on-Mantle) sits just above the toolbar divider line for a gently lit upper edge — the layered-chrome cue you see in polished macOS / Win11 apps.

### UI / UX — premium polish pass

- **Design token system** — `Themes/DarkTheme.xaml` now exposes explicit radius (`Sm` 6 · `Md` 10 · `Lg` 14 · `Xl` 18), elevation (`Low` / `Medium` / `High` / `Focus` as `DropShadowEffect` resources), and motion tokens (`Motion.Fast` 120 ms · `Motion.Base` 180 ms · `Motion.Slow` 280 ms + shared `CubicEase` easing). Styles now compose from tokens instead of ad-hoc per-element values.
- **Reusable surface styles** — `Card`, `ElevatedCard`, `FloatingPill`, `Toast`, `Divider` styles retire the copy-pasted Border-with-radius blocks scattered through `MainWindow.xaml`. Empty state, decode-error state, drop-confidence panel, rename status card, and recent-rename entries now all inherit a single visual language.
- **Typography scale** — new `Text.Display` / `Text.Title` / `Text.Subtitle` / `Text.Body` / `Text.Caption` / `Text.Hint` styles built on Windows 11's `Segoe UI Variable` (graceful fallback to `Segoe UI` on Win10). `SectionLabel` switches to OpenType small-caps (`Typography.Capitals="AllSmallCaps"`) for a refined tracked look without per-letter hackery.
- **Motion** — `ChromeButton` and `PrimaryButton` hover now cross-fades background color via a 120 ms eased `ColorAnimation` instead of a binary setter flip. `NavArrowButton` hover adds a 1.06× scale cue + border-tint transition + elevation shadow. Toast fades in via Opacity animation from its Style trigger. Nav-arrow viewport fade now uses `CubicEase EaseOut` instead of linear.
- **Floating chrome elevation** — position chip, toast, nav arrows, and the empty / error / drop-overlay cards gain layered `DropShadowEffect` so they read as lifted above the viewport instead of floating flat on the near-black background.
- **Hairline unification** — `HairlineBrush` tuned to `#4045475A` (lower opacity); all 1-px dividers now inherit the new `Divider` style so separators no longer compete with content. Toolbar top border switches from `Surface0Brush` to `HairlineBrush`.
- **Toolbar polish** — outer padding tightens rhythm (`14,9` → `20,12`), button cluster gaps go `6` → `4` px (denser), divider bar gets more vertical breathing room. `ToolbarButton` ships a transparent resting state so icons sit on the bar rather than on a box.
- **Empty-state invitational card** — larger logo (74 → 84 px), tighter copy, new inline hint line ("Tip — arrow keys browse the folder, Enter commits a rename."). Copy rewritten for warmer, shorter cadence.
- **Decode-error semantic surface** — low-opacity `DangerPanelBrush` background replaces full red fill so the panel reads as informative not alarming; icon sizes up to 38 px.
- **Drop-overlay hierarchy** — inner card now uses `ElevatedCard` with 2-px themed border, keeping the accept / reject color signal while cleaning up the doubled-border construction that previously sat inside another border.
- **Toolbar + right panel microcopy** — warning/hint copy tightened ("Changing the extension renames the file — it won't convert the image."), rename helper collapses three sentences into "Renames save on pause. Enter commits now · Esc reverts.", empty-undo copy trims to "Your undo list will appear here."
- **Escape discipline extended** — `Window_KeyDown` Escape now also dismisses an active toast via `MainViewModel.DismissToast()` (A-03 extension).
- **Right panel spacing** — column width 340 → 360, panel padding 18 → 22, header margin 0,0,0,18 → 0,0,0,22 — tighter rhythm without feeling airy.
- **Form polish** — `TextBox` focused state switches from 2-px ring color-on-color ambiguity to crisp 2-px `AccentBrush` border + hover hint on `Surface2Brush`; selection opacity drops to 35% so highlighted text stays readable.
- **ScrollBar retemplate** — compact pill thumb on transparent track replaces the default Aero chrome.
- **Accessibility extras** — position chip gets `AutomationProperties.LiveSetting="Polite"` so folder-position changes are announced; folder label inherits `ToolTip` so ellipsized paths are fully recoverable.

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
