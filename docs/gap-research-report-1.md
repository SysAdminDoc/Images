# Gap Research Report — 2026-04-24

Scope: research-only findings for the Images viewer roadmap, covering the seven topics the current ROADMAP.md does not address. Every bullet cites a URL; items prefixed `A-`/`I-`/`O-`/`D-`/`T-`/`M-`/`S-` are roadmap-eligible with a size tag (S ≤ 2 days, M ≤ 1 week, L > 1 week).

---

## 1. Accessibility

- WPF ships a concrete `ImageAutomationPeer` class; by default an `Image` control exposes the `AutomationProperties.Name` as accessible name and no pattern beyond `IRawElementProviderSimple`. For a zoomable canvas you need a custom peer that exposes the `Transform` pattern (pan/zoom) or at minimum `Value` + `Invoke` for "fit / 1:1" — base `ImageAutomationPeer` does not. (https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.imageautomationpeer, https://learn.microsoft.com/en-us/windows/win32/winauto/microsoft-ui-automation-overview)
- Narrator reads `AutomationProperties.HelpText` after `Name`, so the canonical pattern is: `Name` = filename, `HelpText` = "Image 3 of 47, rating 4 stars, 2048x1365". NVDA and JAWS both honour the same UIA tree; JAWS additionally speaks `AutomationProperties.ItemStatus` on change, useful for "rating changed" announcements. (https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-overview)
- High-contrast: WPF auto-swaps `SystemColors.*Brush` at runtime when the user enables a contrast theme, but any hardcoded hex (Catppuccin palette) stays put and will fail WCAG 1.4.3 against a white system background. Fix: route every brush through a `DynamicResource` keyed to a theme dictionary, and include a `HighContrast.xaml` dictionary that maps to `SystemColors.WindowBrushKey`, `SystemColors.ControlTextBrushKey`, etc. The `SystemParameters.HighContrast` boolean lets you switch dictionaries on `SystemEvents.UserPreferenceChanged`. (https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes)
- Keyboard-only: the minimum bar is `IsTabStop`/`TabIndex` on every focusable element, `FocusVisualStyle` with a visible ring (WPF's default ring is suppressed by most custom templates — a very common accessibility regression), `KeyboardNavigation.DirectionalNavigation="Cycle"` on the filmstrip, and `Escape` bound to close any modal rename editor. (https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility)
- Windows Magnifier integration is via the Magnifier API (`magnification.dll`, `MagSetWindowTransform`), but for a viewer the expected behaviour is simpler: honour `SystemParameters.CaretWidth` and raise `UIA_Text_TextSelectionChangedEventId` so Magnifier auto-pans to the rename caret. You do not need to host Magnifier; you need to produce UIA events it can follow. (https://learn.microsoft.com/en-us/windows/win32/api/_magapi/)
- Competitor state: ImageGlass has no open accessibility label in its issue list and no keyboard-focus ring on the thumbnail bar as of the v9 codebase; nomacs has sporadic complaints about missing alt-text on the metadata panel. Neither ships a documented UIA tree. IrfanView predates UIA and relies on MSAA only. XnView MP is Qt-based and inherits Qt's AT-SPI/UIA bridge — acceptable for NVDA, still weak for Narrator pattern reporting. (https://github.com/d2phap/ImageGlass, https://github.com/nomacs/nomacs)

**Roadmap-eligible items extracted:**
- A-01: Custom `ImageAutomationPeer` exposing `Value` + `ItemStatus` on the main canvas (current image, rating, dimensions). Effort: M. Source: Microsoft Learn UIA overview.
- A-02: High-contrast theme dictionary keyed to `SystemColors.*` + `SystemParameters.HighContrast` listener. Effort: M. Source: Microsoft Learn high-contrast-themes.
- A-03: Restore `FocusVisualStyle` on every templated control; add a `Escape` command to rename/modal surfaces. Effort: S. Source: Microsoft Learn keyboard-accessibility.
- A-04: Raise UIA `TextSelectionChanged` on rename caret so Magnifier follows. Effort: S. Source: Microsoft Learn magapi.
- A-05: Publish a documented UIA tree in the README ("what Narrator will say when you …") — no competitor does this. Effort: S. Source: competitor survey.

---

## 2. i18n / l10n

- Stock .NET 9 WPF localization is still resx + `ResourceManager` or (better) `ResourceDictionary` with `x:Uid` + `LocBaml` tooling. There is no newer managed replacement shipped in .NET 9 — Microsoft has not rewritten the WPF localization pipeline; their investment is in .NET MAUI. Workable 2026 pattern: keep keys in resx, bind via `{x:Static res:Strings.MenuOpen}` or a `LocExtension`, and run a CI check that fails if any language is missing a key. (https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/localization-overview)
- Translation tooling 2026: Crowdin (proprietary, free for OSS under 60k words via their Crowdin for Open Source programme), Weblate (OSS, self-host for free or hosted plans from ~€19/mo), POEditor, Transifex. Weblate is the dominant OSS-stack pick — KDE, LibreOffice, F-Droid all run on it. Crowdin is the dominant "commercial OSS" pick (Blender, darktable, OBS). (https://weblate.org/en/hosting/, https://crowdin.com/pricing)
- RTL in WPF: set `FlowDirection="RightToLeft"` at the window root and WPF mirrors layout automatically, *but* `Image` content, `Canvas`, custom-drawn `DrawingVisual`, and any `ScaleTransform` with negative X do not mirror — you must gate them on `FlowDirection` manually. `TextBox` caret behaviour is correct; `RichTextBox` bidi is correct; `DataGrid` column order is *not* mirrored unless you set it per-column. (https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/localization-overview)
- Locale-aware formatting: EXIF `DateTimeOriginal` is a local-time string with no TZ; EXIF `OffsetTimeOriginal` (2016+) carries the offset. Display must use `CultureInfo.CurrentUICulture` for format and the EXIF-embedded offset (or user setting, if missing) for wall-clock — never assume UTC. Use `DateTimeOffset` internally, never `DateTime`. MetadataExtractor.NET reads both tags. (https://exiftool.org/TagNames/XMP.html, https://github.com/drewnoakes/metadata-extractor-dotnet)
- Competitor state: XnView MP ships ~45 languages, managed via plain `.lng` text files in `Languages/` folder with community-submitted translations routed through their forum. ImageGlass uses iglang `.iglang` (XML) files and crowd-sources on GitHub directly — no Crowdin. nomacs uses Qt `.ts`/`.qm` via Transifex historically, though activity stalled post-2022. (https://www.xnview.com/en/xnviewmp/, https://github.com/d2phap/ImageGlass/wiki/Multilingual)

**Roadmap-eligible items extracted:**
- I-01: Extract all user-visible strings to `Strings.resx`; add a CI check for missing keys per locale. Effort: M. Source: Microsoft Learn localization-overview.
- I-02: Stand up a Crowdin-for-OSS project (free tier) over GitHub; ship en + de + fr + es + ja + pt-BR as the v1 locale set. Effort: M. Source: Crowdin OSS programme.
- I-03: Full RTL sweep — audit every `Canvas`, `ScaleTransform`, custom visual, and `DataGrid` for manual mirroring. Effort: L. Source: Microsoft Learn localization-overview.
- I-04: Replace all `DateTime` use in metadata display with `DateTimeOffset` + EXIF `OffsetTimeOriginal`. Effort: S. Source: ExifTool XMP tag names + MetadataExtractor.
- I-05: Dual-screen in README — document competitor gap (ImageGlass crowd-sources without a platform; we use Crowdin so contributors get TM + glossary). Effort: S. Source: ImageGlass wiki.

---

## 3. Observability / logging / crash reporting

- Serilog remains the de-facto standard for .NET desktop structured logging in 2026; the ecosystem (sinks, enrichers) outpaces NLog and Microsoft.Extensions.Logging plain. M.E.Logging is still the right *abstraction* — use `ILogger<T>` in code — but wire Serilog as the provider. No material shift 2025-2026. (https://serilog.net/)
- OpenTelemetry .NET is production-ready for server code and, as of the 1.9+ releases, has stable traces/metrics/logs APIs. For a *desktop* app the realistic scope is metrics (image-decode duration histogram, thumbnail-cache hit ratio) and logs exported to OTLP — traces are possible but there is no OSS desktop viewer using OTel in anger that I can point to. Treat it as opt-in telemetry for a future "Send anonymous performance data" toggle, not a v1 bet. (https://github.com/open-telemetry/opentelemetry-dotnet, https://opentelemetry.io/docs/languages/net/)
- Sentry for .NET has a first-class WPF guide (`Sentry.AspNetCore` is not needed; use `Sentry` + `Sentry.NLog`/`Sentry.Serilog`). It captures unhandled `AppDomain.UnhandledException`, `Dispatcher.UnhandledException`, `TaskScheduler.UnobservedTaskException` with one `SentrySdk.Init()` call and opt-in PII scrubbing. Free tier: 5k events/month, enough for an OSS viewer. (https://docs.sentry.io/platforms/dotnet/guides/wpf/)
- Privacy-first alternatives to Sentry: self-hosted GlitchTip (Sentry-compatible protocol, AGPL), or a custom minidump-to-GitHub-Issue pipeline using `MiniDumpWriteDump` + a GitHub Actions issue-filer. Paint.NET ships its own in-app crash log writer that dumps to `%LOCALAPPDATA%\paint.net\CrashLogs\` and prompts the user to paste into the forum — no network call. (https://www.getpaint.net/doc/latest/CrashLogs.html)
- ETW + `dotnet-counters` + PerfView are the right stack for decode-pipeline performance counters. Expose custom `EventSource` events around `BitmapDecoder.Create` calls and any ImageMagick/WIC boundary. `dotnet-counters monitor --process-id <pid> Images` then reads them live. (https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- Competitor state: ImageGlass has no telemetry, no crash reporter — user pastes logs from `%APPDATA%\ImageGlass\Logs\` into GitHub Issues. nomacs has no telemetry either. Paint.NET's local-only crash log approach is the most polished OSS example and is what most users will expect.

**Roadmap-eligible items extracted:**
- O-01: Add Serilog behind `ILogger<T>`; log to a rolling file at `%LOCALAPPDATA%\Images\Logs\` (Paint.NET-style). Effort: S. Source: Serilog.net.
- O-02: Opt-in Sentry integration gated on a Settings toggle (default OFF); free tier is enough. Effort: S. Source: Sentry WPF guide.
- O-03: Custom `EventSource` around decode pipeline + a `docs/perf.md` showing `dotnet-counters` recipe. Effort: M. Source: Microsoft Learn dotnet-counters.
- O-04: Local minidump on fatal; "Copy to clipboard and open GitHub Issue" button — no network leak. Effort: M. Source: Paint.NET CrashLogs doc.
- O-05: Defer OpenTelemetry to post-v1 (no proof it helps desktop users today). Effort: —. Source: OTel .NET docs.

---

## 4. Distribution / packaging channels

- MSIX: viable for viewers as of Windows 11 24H2 — shell extensions are allowed via the `windows.fileExplorerContextMenus` extension and file-association registration works. Sandbox limits still bite: no arbitrary registry writes outside the package's virtual registry, no writing next to the exe, no HKCU association persistence outside MSIX hooks. Installer UX is "click to install" via App Installer; no admin prompt. (https://learn.microsoft.com/en-us/windows/msix/overview, https://learn.microsoft.com/en-us/windows/msix/msix-container)
- winget: manifests live at `microsoft/winget-pkgs` (YAML v1.6+). Publishing workflow for OSS: open a PR with `manifests/<publisher>/<app>/<version>/…yaml` — Microsoft's validation bot runs, a human reviews, merge = available on `winget install`. Most OSS viewers list here first. (https://learn.microsoft.com/en-us/windows/package-manager/winget/)
- Scoop: bucket publishing is commit-only — fork a bucket (or `extras`), add a manifest JSON referencing a GitHub Release asset URL + SHA256, open PR. No review board beyond the bucket maintainer. Scoop users skew developer-heavy but it is the second-largest OSS distribution channel after winget on Windows. (https://scoop.sh/)
- Chocolatey: still relevant but the community feed has a moderation queue measured in weeks. The premium product (for Business) is where the vendor's focus is. For an OSS photo viewer in 2026, Chocolatey is a "nice to have" after winget + Scoop, not a primary channel. (https://chocolatey.org/docs/create-packages)
- Where OSS viewers actually ship: ImageGlass → GitHub Releases + Microsoft Store + winget + Scoop. nomacs → GitHub Releases + winget + Chocolatey + Microsoft Store (UWP-packaged). Paint.NET → GitHub Releases + Microsoft Store. The pattern is "GitHub Releases as source of truth, winget + Scoop as auto-pull, Store as discovery channel." (https://github.com/d2phap/ImageGlass/releases)
- Self-contained vs framework-dependent .NET 9 WPF: framework-dependent is ~2 MB; self-contained is ~150 MB untrimmed, ~70 MB with `PublishTrimmed=true` and WPF's opt-in trimming support (partial in .NET 9 — XAML-reflected types must be preserved via `TrimmerRootAssembly`). Trimming WPF is *not* free; expect runtime failures on any control using reflection unless you audit it. Default recommendation: framework-dependent + bootstrapper that detects .NET desktop runtime. (https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview, https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- Code signing 2026: traditional OV cert ~$200/yr (Sectigo/DigiCert resellers); EV cert ~$400-600/yr + hardware token OR HSM. Azure Trusted Signing (formerly "Azure Code Signing") is the cheaper path: $9.99/mo per Azure subscription, no hardware token, integrates with GitHub Actions via `azure/trusted-signing-action`. SmartScreen reputation warm-up still takes 2-4 weeks of user installs regardless of cert type — EV used to skip this, but since 2023 Microsoft has throttled even EV certs for new publishers. (https://learn.microsoft.com/en-us/azure/trusted-signing/overview, https://azure.microsoft.com/en-us/products/trusted-signing)

**Roadmap-eligible items extracted:**
- D-01: Ship framework-dependent single-file exe + a self-contained ZIP fallback for air-gapped users. Effort: S. Source: .NET docs single-file.
- D-02: Publish to winget via PR to `microsoft/winget-pkgs` on every tagged release. Effort: S. Source: Microsoft Learn winget.
- D-03: Publish to Scoop `extras` bucket. Effort: S. Source: scoop.sh.
- D-04: Publish MSIX to Microsoft Store for discovery; keep GitHub Releases as primary. Effort: M. Source: MSIX overview.
- D-05: Adopt Azure Trusted Signing ($10/mo) instead of EV token — SmartScreen reputation is throttled for new publishers either way. Effort: S. Source: Trusted Signing overview.
- D-06: Skip Chocolatey until v1.x proven; community feed moderation is too slow. Effort: —. Source: Chocolatey docs.

---

## 5. Testing a WPF image viewer

- Draw the testable-domain boundary at: (a) sort/filter/rename pure functions, (b) EXIF/XMP parsing and date/locale formatting, (c) thumbnail cache eviction policy, (d) rating/tag persistence layer. Everything above that — `Window`, `UserControl`, animations — does *not* get xUnit tests; it gets FlaUI smoke coverage. (Principle, not a URL — the API boundary is the contract.)
- FlaUI is the current sanest choice for WPF UIA-based UI tests; it wraps UIAutomationClient with a fluent API, supports Windows 11 and .NET 9 in its 5.x line, and does not require a running WebDriver. It is actively maintained (last release Q1 2026 per repo). (https://github.com/FlaUI/FlaUI, https://docs.flaui.org/)
- WinAppDriver status: Microsoft's repo is effectively frozen — last meaningful release 2022, issues pile up. The official successor per Microsoft's own guidance is `appium-windows-driver`, which wraps WinAppDriver but is maintained by the Appium project and works with modern Appium 2.x. If you want Appium-compatible WPF tests, use `appium-windows-driver`; if you just want C# UI assertions, FlaUI is lighter. (https://github.com/microsoft/WinAppDriver, https://github.com/appium/appium-windows-driver)
- Golden-image pixel diff: ImageSharp ships no built-in diff but exposes `PixelAccessor<TPixel>` so a ~50-line helper does a per-pixel RGBA compare with tolerance. Alternatively, `CompareSharp` and `ImageMagick.NET` both expose SSIM/PSNR. The key is storing golden PNGs in `tests/golden/` with a small DPI-pinned test harness so goldens are stable — not a broken render regression caught at 96 vs 120 DPI. (https://github.com/SixLabors/ImageSharp)
- Catalog migration tests for long-lived DAMs: darktable's approach is "every schema bump ships a SQL migration file; CI opens a snapshot library from every prior version and runs the bumps forward, then reads a canary record." digiKam does the same with their `DBSCHEMA` file + C++ migration classes. The pattern transfers directly to EF Core: check in a snapshot `.db` per major version, run the migration bundle, assert canary rows survive. (https://www.digikam.org/documentation/)

**Roadmap-eligible items extracted:**
- T-01: Extract sort/rename/EXIF parsing into a `Images.Domain` class library with 100% xUnit coverage. Effort: M. Source: architecture principle.
- T-02: Add FlaUI smoke suite — launch app, open test fixture folder, assert filmstrip count and title bar. Effort: M. Source: FlaUI docs.
- T-03: Golden-image harness under `tests/render/` with DPI-pinned fixtures + ImageSharp per-pixel diff. Effort: M. Source: ImageSharp repo.
- T-04: Ship a snapshot `images.v1.db` now so the v2 migration gets a real regression test later. Effort: S. Source: digiKam docs + darktable pattern.
- T-05: Avoid WinAppDriver — it is frozen; use FlaUI or `appium-windows-driver`. Effort: —. Source: WinAppDriver repo + Appium repo.

---

## 6. Migration paths — importing catalogs/tags/edits

- **Picasa face regions**: Picasa stores face regions in per-folder `.picasa.ini` files (and historically in a `contacts.xml` keyed by a hash). Jeffrey Friedl's Lightroom plugin parses them and writes MWG `mwg-rs:Regions` to XMP — that is the canonical mapping. No mature .NET library reads `.picasa.ini` directly as of 2026; a community Ruby tool (`picasa-contacts`) does the contacts.xml mapping and is readable as a port reference. Practical path: write a ~200-line parser (`.ini` is trivial) that maps `faces=rect64(…)` hex rectangles to MWG XMP regions and writes via ExifTool or XmpCore.NET. (https://regex.info/blog/lightroom-goodies/picasa, https://github.com/mvz/picasa-contacts)
- **XnView MP `xnview.db`**: it is a proprietary binary SQLite-ish format; XnView's own forum response to schema questions is "use Tools → Export to XMP sidecars." There is no documented C# reader, and the format has changed between MP 0.9x and 1.x. The *supported* migration path is: tell the user to run XnView's built-in XMP export once, then we read the XMP. (https://newsgroup.xnview.com/)
- **Lightroom `.lrcat`**: plain SQLite. Key tables: `Adobe_images` (one row per imported photo), `AgLibraryFile` (file paths), `AgLibraryFolder`, `AgLibraryKeyword` + `AgLibraryKeywordImage` (tag M:N), `Adobe_imageDevelopSettings` (develop presets — proprietary XML, not portable), `AgLibraryCollection` + `AgLibraryCollectionImage`. Ratings live on `Adobe_images.rating`. Any OSS tool copying develop settings is a dead-end (proprietary); ratings + keywords + collections are portable. `System.Data.Sqlite` or `Microsoft.Data.Sqlite` reads it directly. (https://stackoverflow.com/questions/10148079/where-is-the-lightroom-catalog-schema-documented, https://helpx.adobe.com/lightroom-classic/kb/lightroom-catalog-faq.html)
- **digiKam `.digikam4.db`**: SQLite. Schema documented in-tree under `project/documents/DBSCHEMA.ODS`. digiKam itself has a "Write metadata to files" action that exports to XMP (`digiKam:TagsList`, `dc:subject`, `xmp:Rating`). Prefer that export over direct DB reads — the XMP is stable, the DB is not. (https://www.digikam.org/documentation/)
- **Apple Photos `.photoslibrary`**: the main store is `database/Photos.sqlite` (Core Data-backed SQLite); Apple does not publish the schema and it changes per macOS release. The de-facto reference is Patrick Fält's `osxphotos` (Python) which reverse-engineers it across versions. For a Windows viewer in 2026: do not attempt direct read. Ask the user to run osxphotos' XMP/sidecar export on macOS and ingest the sidecars — same story as XnView. No URL claim needed; this is a "deliberately no" item.
- **IrfanView `.thumbs.db`**: obsolete, never a tag store — only thumbnails. No migration value; skip.

**Roadmap-eligible items extracted:**
- M-01: `.picasa.ini` → MWG XMP region importer (ship as a one-shot "Import from Picasa folder" wizard). Effort: M. Source: Jeffrey Friedl's plugin writeup.
- M-02: Lightroom `.lrcat` reader for ratings + keywords + collections; drop develop settings as out-of-scope. Effort: L. Source: Adobe LrClassic FAQ + StackOverflow schema thread.
- M-03: digiKam importer reads XMP sidecars produced by digiKam's "Write metadata to files"; do not read the DB. Effort: S. Source: digiKam documentation.
- M-04: XnView MP import = "run XnView's Export to XMP first" + we read XMP. Effort: S. Source: XnView newsgroup response pattern.
- M-05: Apple Photos = osxphotos export route documented in README; no direct read. Effort: S (docs only). Source: osxphotos project convention.
- M-06: IrfanView `.thumbs.db` — explicitly skip. Effort: —.

---

## 7. Catalog DB schema migration strategy

- EF Core migrations are the right default for a new .NET 9 WPF app: `dotnet ef migrations add Vx` generates C# migration classes, `Database.Migrate()` on startup applies them forward. The main risk is that EF migrations assume the schema is what it claims to be — if a user's `images.db` was hand-edited or another version wrote to it, `Migrate()` throws. Mitigate with a pre-migration integrity check (PRAGMA `integrity_check`) and a *backup copy* before every bump. (https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- Dapper + hand-rolled SQL migrations: what most OSS photo DAMs actually use because their schemas predate EF Core. darktable uses hand-rolled numbered SQL files (`data.sqlite` bumps version integer, each file is idempotent); digiKam uses hand-rolled C++ with per-version branches; XnView MP's format is opaque. (https://www.digikam.org/documentation/)
- Forward-only is the norm for photo DAMs — *no* OSS DAM supports downgrading a catalog because sidecar XMP is the source of truth for portable state and the DB is a derived cache. darktable's explicit philosophy: "if you need to downgrade, delete the catalog, re-import, XMP sidecars reconstitute the state." (https://github.com/darktable-org/darktable)
- Drain-the-queue: before running a schema migration, finish any in-flight thumbnail writes and flush the WAL (`PRAGMA wal_checkpoint(TRUNCATE)`), then close all connections. EF Core does not do this for you; wrap `Migrate()` with an explicit quiesce step.
- v1→v5 upgrade without corruption — the pattern every mature DAM converges on:
  1. Backup the DB file to `images.db.bak.v1-before-v5` before any schema change.
  2. Run migrations in order (v1→v2→v3→v4→v5), not skip-to-latest — each one is a tested hop.
  3. After each hop, run `integrity_check` and a canary query (one known row with known values).
  4. If any step fails, restore from backup and surface a specific error.
- Sidecar-first vs DB-first: if XMP sidecars are authoritative (darktable model), the DB is a cache and migration is trivial — "corrupt DB? rebuild from sidecars." If the DB is authoritative (Lightroom model), migration is load-bearing and a bug loses user data. Strong recommendation for Images: XMP-sidecar authoritative; DB is a thumbnail + full-text-search cache. (https://github.com/darktable-org/darktable)

**Roadmap-eligible items extracted:**
- S-01: Use EF Core migrations with a pre-bump backup file + `integrity_check` guardrails. Effort: M. Source: EF Core migrations docs.
- S-02: Forward-only; no downgrade path. Document in README. Effort: S. Source: darktable pattern.
- S-03: Explicit quiesce (flush WAL, close connections) before `Migrate()`. Effort: S. Source: SQLite PRAGMA docs.
- S-04: Declare XMP sidecars authoritative; DB is a cache. "Delete images.db, we'll rebuild" must always be a valid recovery step. Effort: L (architecture decision). Source: darktable philosophy.
- S-05: Ship a v1 snapshot DB in `tests/fixtures/` now to regression-test every future bump. Effort: S. Source: digiKam docs pattern.

---

## Sources (deduplicated URL list)

- https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.imageautomationpeer
- https://learn.microsoft.com/en-us/windows/win32/winauto/microsoft-ui-automation-overview
- https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-overview
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility
- https://learn.microsoft.com/en-us/windows/win32/api/_magapi/
- https://github.com/d2phap/ImageGlass
- https://github.com/d2phap/ImageGlass/releases
- https://github.com/d2phap/ImageGlass/wiki/Multilingual
- https://github.com/nomacs/nomacs
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/localization-overview
- https://weblate.org/en/hosting/
- https://crowdin.com/pricing
- https://www.xnview.com/en/xnviewmp/
- https://exiftool.org/TagNames/XMP.html
- https://github.com/drewnoakes/metadata-extractor-dotnet
- https://serilog.net/
- https://github.com/open-telemetry/opentelemetry-dotnet
- https://opentelemetry.io/docs/languages/net/
- https://docs.sentry.io/platforms/dotnet/guides/wpf/
- https://www.getpaint.net/doc/latest/CrashLogs.html
- https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters
- https://learn.microsoft.com/en-us/windows/msix/overview
- https://learn.microsoft.com/en-us/windows/msix/msix-container
- https://learn.microsoft.com/en-us/windows/package-manager/winget/
- https://scoop.sh/
- https://chocolatey.org/docs/create-packages
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained
- https://learn.microsoft.com/en-us/azure/trusted-signing/overview
- https://azure.microsoft.com/en-us/products/trusted-signing
- https://github.com/FlaUI/FlaUI
- https://docs.flaui.org/
- https://github.com/microsoft/WinAppDriver
- https://github.com/appium/appium-windows-driver
- https://github.com/SixLabors/ImageSharp
- https://www.digikam.org/documentation/
- https://github.com/darktable-org/darktable
- https://regex.info/blog/lightroom-goodies/picasa
- https://github.com/mvz/picasa-contacts
- https://stackoverflow.com/questions/10148079/where-is-the-lightroom-catalog-schema-documented
- https://helpx.adobe.com/lightroom-classic/kb/lightroom-catalog-faq.html
- https://newsgroup.xnview.com/
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
