# Research - Images
Date: 2026-07-12 (pass 2) - replaces all prior research.

## Executive Summary
Verified: Images is a mature, Windows-only, local-first WPF/.NET 10 image viewer/workbench (v0.2.25, ~1007 tests) with broad local codec support (WIC-first, Magick.NET fallback), archive/book viewing, inline rename, compare, metadata/provenance inspection, opt-in color management, loupe, live pixel readout, SQLite settings/catalog, optional local ONNX models, recovery tooling, and offline-first privacy defaults. This is a same-day second research pass: the earlier pass's competitor/format/community landscape still holds and its highest-value items were implemented today (color management, loupe, pixel readout, zoom-lock, checkerboard, zoom-to-selection, session restore, stop-at-ends, metadata-preserving Save-a-copy, Magick.NET 14.15 security bump). The project is now at a feature plateau: the remaining large feature bets (SkiaSharp/HDR renderer, SQLite catalog + watched folders, faces, GPS/map, metadata editor, plugin SDK, extra locales, Store/WinGet/signing) are already tracked in `Roadmap_Blocked.md` and are genuinely gated on credentials, hardware/GUI validation, or renderer decisions. The net-new *actionable* surface this pass is therefore engineering quality and release hygiene, not features. Top opportunities in priority order: (1) fix the verified parallel-load test flakes by serializing timing-sensitive process/stream tests; (2) cut the accumulated v0.2.26 release (unsigned local artifacts, not blocked); (3) minor dependency currency (Serilog 4.4.0). The strategic forward decision (not actionable now) is migrating the frozen ONNX DirectML dependency to Windows ML.

## Product Map
- Core workflows: open files/folders/sessions/archives/books; view/navigate (wrap / stop-at-ends / sibling-folder); inline rename; compare (side-by-side + overlay + difference); inspect metadata/provenance + live pixel readout + loupe; export/batch/Save-a-copy (metadata-preserving); recover destructive actions.
- User personas: Windows power users replacing Photos/ImageGlass/FastStone; photographers/archivists in local folders; technical users who value portable artifacts, checksums, runtime provenance, and visible network behavior.
- Platforms and distribution: Windows 10/11 x64 WPF targeting `net10.0-windows10.0.22621.0`; MIT; Inno installer + portable ZIP; local release scripts (SBOM/provenance, WinGet/Scoop manifests, package-hash + provenance gates); GitHub Releases primary channel; framework-dependent (no runtime pin in csproj).
- Key integrations and data flows: WIC first, Magick.NET fallback (JXL/AVIF/HEIC/PSD/RAW; opt-in embedded-ICC→sRGB); SharpCompress read-only archives; SQLite settings/catalog/cache; XMP sidecars; optional Ghostscript/jpegtran/ExifTool/c2patool/OCR/ONNX-DirectML local paths; opt-in GitHub release checks logged by `NetworkEgressService`.

## Competitive Landscape
The earlier-today pass covered ImageGlass 10, PicView, nomacs, JPEGView, qView, Oculante, FastStone, Honeyview, and Windows 11 Photos exhaustively; that landscape is unchanged within the day. Net position after today's work:
- ImageGlass (d2phap/ImageGlass): still the Windows benchmark. Images has now closed several of ImageGlass's *open* gaps (color-profile honoring #1433, metadata-preserving save #496, loupe #1425, pixel info) — the remaining ImageGlass-led areas (in-archive browsing already shipped; native crisp SVG at any zoom; gallery-cache tuning) are either done or low-value. Continue to avoid its WebView2 dependency.
- PicView / nomacs / JPEGView: the comfort features they lead on (zoom-lock, checkerboard, zoom-to-selection, session restore, stop-at-ends) were implemented today. Remaining nomacs-led idea (always-on-top frameless reference window, #607) overlaps Images' existing peek mode + reference board and is low incremental value.
- What to learn now: nothing feature-shaped is outstanding from the OSS field that isn't already blocked (HDR display, faces, catalog). The differentiation lever left is *trustworthiness and polish* — reliability (no flaky tests), honest release cadence, and correctness of the opt-in color pipeline under live validation.

## Security, Privacy, and Reliability
- Verified (first-hand, this session): the full test suite intermittently fails 1-2 timing-sensitive tests under parallel load — `tests/Images.Tests/CodecRuntimeTests.cs:44` `RunVersionProbe_DrainsStderrWhileWaiting` (child-process stdout/stderr drain timing) and `tests/Images.Tests/UpdateCheckServiceTests.cs:49` `CheckAsync_WhenContentLengthIsUnknown_RecordsActualBytesRead` (HTTP stream byte-count timing). Both pass in isolation and on clean re-runs; they only starve under full-suite CPU saturation. There is no `xunit.runner.json` and only STA tests use `[Collection("WpfSmoke")]`; the process/stream-timing classes are unserialized. This is a real CI-trust bug (a green build can flip red on unrelated PRs).
- Verified: dependency pins are current as of 2026-07-12 — Magick.NET 14.15.0 (released today), Microsoft.Data.Sqlite 10.0.9, SharpCompress 0.49.1, ONNX DirectML 1.24.4 (latest available; package is feature-frozen — `microsoft/DirectML` is in maintenance mode). Only Serilog is behind: 4.3.1 vs 4.4.0 (2026-07-10, routine minor, no security advisory).
- Verified: `dotnet list --vulnerable` was clean at the last check; .NET 10.0.9 (2026-06-09) is the current servicing level; the June CVEs are general-runtime/ASP.NET, none WPF-specific, and a pure WPF client is not exposed to the HTTP-stack ones. July servicing (~10.0.10) was not yet published at time of research.
- Verified: the codebase has zero `TODO`/`FIXME`/`HACK`/`NotImplemented` markers in `src/Images` (excluding vendored Ghostscript) — the deferred-work surface lives entirely in `Roadmap_Blocked.md`, not in the code.
- Likely: the opt-in color-managed display path (embedded ICC→sRGB via Magick.NET, shipped today) is mechanically tested but not visually validated on a wide-gamut monitor; correctness of on-screen appearance is a live-validation item before it should be promoted from opt-in to default.

## Architecture Assessment
- Test isolation is the one concrete structural gap: introduce a `[CollectionDefinition("Timing-Sensitive", DisableParallelization = true)]` and apply `[Collection("Timing-Sensitive")]` to the process-spawn/stream-timing classes, optionally adding `tests/Images.Tests/xunit.runner.json` (`parallelAlgorithm: conservative`, a `maxParallelThreads` cap) with `CopyToOutputDirectory=PreserveNewest`. Grounded in xUnit docs; conservative algorithm + a non-parallel collection is the canonical fix for exactly this failure mode.
- Release hygiene: the `## Unreleased` CHANGELOG section has accumulated ~29 lines of user-facing features across today's work with no version cut since v0.2.25. The local release path (version sync across csproj/manifest/installer/README + tests + vulnerable/deprecated gates + localization parity + provenance + checksum + WinGet/Scoop manifest validation) is scripted and does not require signing for the unsigned ZIP/installer artifacts. A v0.2.26 cut is straightforward and overdue.
- No renderer/module refactor is warranted at this maturity; the WIC-first/Magick-fallback loader, MVVM shell, and service layout are coherent and well-tested. Larger structural moves (SkiaSharp canvas, catalog schema) remain deliberately blocked pending dedicated runs.

## Rejected Ideas
- Re-run a broad competitor/format/community sweep: sourced from the research brief's exhaustiveness mandate, rejected because it was completed hours ago (same day) and re-mining reproduces the existing Sources without new signal; new queries stop returning new information.
- Windows ML migration now: sourced from Microsoft Learn (Windows ML GA in WinApp SDK 1.8.1, 2.x EP catalog), rejected as *actionable* because it forces a Windows App SDK dependency onto an otherwise self-contained WPF exe and only benefits NPU/GPU EPs on Windows 11 24H2 (build 26100)+; the InferenceSession API is identical but the packaging cost + version floor make it a gated forward decision, not a code-ready task. Belongs in `Roadmap_Blocked.md` as a research spike.
- Always-on-top frameless reference window (nomacs #607): sourced from nomacs open issues, rejected because Images' existing peek mode + Reference Board already cover the pinned-reference use case.
- Promote color-managed display to default-on: sourced from the color-accuracy complaint cluster, rejected until wide-gamut visual validation exists; shipping an unvalidated always-on color transform risks regressing every user's color (kept opt-in by design).
- .NET runtime version bump as a roadmap item: rejected because the app is framework-dependent with no runtime pin in `Images.csproj`; it uses the installed 10.x servicing level automatically, so there is no code change to make.
- Serilog major/behavioral changes: only the routine 4.3.1→4.4.0 minor is warranted; no advisory justifies more.

## Sources
Test reliability / xUnit:
- https://xunit.net/docs/config-xunit-runner-json
- https://xunit.net/docs/running-tests-in-parallel
- https://github.com/xunit/xunit/issues/1999

Dependency currency:
- https://www.nuget.org/packages/Magick.NET-Q16-AnyCPU/
- https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML/
- https://www.nuget.org/packages/SharpCompress/
- https://www.nuget.org/packages/Microsoft.Data.Sqlite/
- https://www.nuget.org/packages/Serilog/
- https://github.com/microsoft/DirectML

Platform / runtime:
- https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-june-2026-servicing-updates/
- https://github.com/dotnet/core/releases
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/migrate-to-windows-ml
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/supported-execution-providers

Competitive (carried from same-day pass 1, unchanged):
- https://github.com/d2phap/ImageGlass/issues/1433
- https://github.com/d2phap/ImageGlass/issues/496
- https://github.com/Ruben2776/PicView/releases
- https://github.com/nomacs/nomacs/issues/607
- https://github.com/sylikc/jpegview/issues/43

## Open Questions
- Windows ML forward migration: is the team willing to take a Windows App SDK dependency and set a Windows 11 24H2 (build 26100) floor to gain evergreen NPU/GPU execution providers, or does the pre-24H2 support requirement keep the app on the frozen DirectML 1.24.4 pin indefinitely? This blocks whether a Windows ML spike enters `Roadmap_Blocked.md` as prioritized.
- Color-managed display promotion: is a wide-gamut/P3 monitor available to visually validate the opt-in embedded-ICC→sRGB transform so it could become default-on, or does it stay opt-in until such validation exists?
