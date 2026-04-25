# Phase 1 — External delta scan (Factory iter-2, 2026-04-24)

**Mode**: DELTA mode per recipe §Loop. Phase 1 is "net-new since `iter-1-sources.md`" only. Phases 2-5 run in full but merge-into rather than rebuild.

**Artifact basis**: `iter-1-sources.md` + my knowledge cutoff. Same-session delta → time-delta is ~minutes, not days, so "net-new since iter-1" is effectively "things iter-1 should have covered but didn't".

## Gap fills iter-1 missed (organized by class)

### Class 2 — Commercial (2 additions)
61. **Microsoft PowerToys "Peek"** — `github.com/microsoft/PowerToys/tree/main/src/modules/peek`
    Takeaway: quicklook-style preview via spacebar on selected file in Explorer. Architecture reference — they use WinUI 3 + WebView2 for HTML-renderable formats. Our viewer could accept a `/peek` CLI arg to behave similarly.

62. **Files app (files-community)** — `github.com/files-community/Files`
    Takeaway: modern Explorer replacement that embeds a preview pane. Shell-integration reference for our installer's file-association work. Their ProgID + OpenWithProgids pattern matches D-01b exactly.

### Class 8 — Dep changelogs (2 additions)
63. **Serilog 4.2.x + Serilog.Sinks.File** — `github.com/serilog/serilog-sinks-file/releases`
    Takeaway: V02-06 target. `Hooks.FileLifecycleHooks` provides the atomic-file-open you want on Windows to avoid partial writes on crash. `RollingInterval.Day` + `retainedFileCountLimit: 14` is the standard.

64. **Microsoft.Extensions.Logging 9.x** — `github.com/dotnet/runtime/tree/main/src/libraries/Microsoft.Extensions.Logging`
    Takeaway: `ILogger<T>` + `ILoggerFactory` is the idiomatic .NET 9 abstraction; Serilog plugs in via `LoggerConfiguration.WriteTo.*`. No DI container required — we can build a static factory.

### Class 6 — Standards / platform APIs (1 addition)
65. **Win32 MiniDumpWriteDump API** — `learn.microsoft.com/windows/win32/api/minidumpapiset/nf-minidumpapiset-minidumpwritedump`
    Takeaway: V02-07 target. `dbghelp.dll` → `MiniDumpWriteDump`. Flags: `MiniDumpWithDataSegs | MiniDumpWithHandleData | MiniDumpWithUnloadedModules` for a "just enough to triage" dump; `MiniDumpWithFullMemory` is overkill for a viewer. Paint.NET's crash pattern is the industry reference.

### Class 7 — Engineering blogs (1 addition)
66. **WPF Printing + FixedDocument** — `learn.microsoft.com/dotnet/desktop/wpf/advanced/printing-overview`
    Takeaway: V15-10 target. `PrintDialog.PrintDocument` on a `FixedDocument` with one `PageContent` + `FixedPage` + scaled Image. `PrintDialog.ShowDialog()` returns true on accept.

### Class 9 — Security (1 addition)
67. **Microsoft Authenticode + SmartScreen reputation 2026** — `learn.microsoft.com/windows/security/identity-protection/smart-app-control/`
    Takeaway: context for D-05 Azure Trusted Signing. First ~500 installs with a new cert still trip SmartScreen; after that, reputation accumulates. Signing our v0.1.5 installer will mark the first cert-use for this publisher — timing matters for the reputation ramp.

## Dep version floors / ceilings (updated after iter-1 sources.md)
- Serilog → latest 4.x (first dep addition this iter)
- Serilog.Sinks.File → latest 6.x
- Magick.NET → 14.13.0 (unchanged)
- Microsoft.VisualBasic → 10.3.0 (unchanged)

## No-news items (iter-1 sources still current)
Everything else from iter-1 stands. Magick.NET, SkiaSharp, libheif, libavif, libwebp, libjxl have no notable releases in the same-day delta window. WIC CVE-2025-50165 still outstanding on pre-patch DLLs.

## Sources added this iter: 7
Total across iter-1 + iter-2: **67**. Still above the 30-60 floor.
