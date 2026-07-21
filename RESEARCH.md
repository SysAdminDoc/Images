# Research — Images

Date: 2026-07-21 — replaces all prior research (supersedes the 2026-07-20 pass, whose entire V120 actionable block — ML session-reuse batching, cancellable ML CLIs, distinct model-load exit codes, face-NMS survivor cap, semantic candidate ceiling, pHash/Exif31 tests, SharpCompress detection pin, GitHub-Actions workflow removal — all shipped in v0.2.31).

## Executive Summary

Images is a Windows-only, local-first .NET 10/WPF viewer (v0.2.31, 155 services, ~1,231 first-party sources, ~1,360 test methods, zero real TODO/FIXME markers) whose feature depth exceeds every free competitor. The 2026-07-20 research pass's items all landed. The codebase remains correctness-solid: a fresh audit found **no data-loss/security landmines**, clean disposal on all new batch paths, and no stubs. The residual work is **finishing an incomplete rollout, one real perf/UX gap in the single in-app ML surface, and cheap additive wins** — not a new feature layer. The V120 batch/cancellation/exit-code pattern reached only the CLI drivers; the audit shows it did **not** reach (a) the one in-app ML consumer, which still thrashes sessions and can't be interrupted, or (b) two of the six ML CLIs. In priority order: (1) make the GUI face-review workbench use the batched `AnalyzeMany` and a cancel token (today it opens ~200 ONNX sessions for a 100-image folder and cannot be stopped); (2) bring `ObjectCli`/`OrientationCli` to parity with the other four CLIs (multi-path + Ctrl+C + distinct model-load exit code); (3) persist and surface the semantic candidate ceiling that shipped as a bare static; (4) bound `FaceClusterService`'s unbounded O(n²) all-pairs loop; (5) close the batch-service test gaps; (6) enable `PublishReadyToRun` for cold-start (the gallery already virtualizes and thumbnails already decode-downscale, so R2R is the only unclaimed perf lever); (7) a concrete WPF-accessibility pass (live-region announcements + explicit tab order). The one strategic new option: `imazen/ultrahdr` (Apache-2.0, Windows CI) now enables ISO 21496-1 gain-map **write**, upgrading Images from gain-map inspection to UltraHDR authoring via a shell-out CLI matching the existing binary pattern.

Top opportunities: GUI ML batch+cancel, Object/Orientation CLI parity, semantic-ceiling settings wiring, FaceCluster O(n²) bound, batch-service tests, ReadyToRun, WPF a11y pass. Strategic (larger bet): gain-map write via imazen/ultrahdr. Decision-gated (unchanged): inline video, gain-map *display*.

## Product Map

- **Core workflows:** open files/folders/clipboard/archives; navigate, zoom, compare (N-pane synced + diff), loupe, present; inspect OCR/metadata/C2PA/gain-map; non-destructive edit + export; catalog + search (text + CLIP), dedup/near-dup stacks, trip/event grouping, import, recover; offline ML review CLIs (face/object/scene/aesthetic/orientation/safety) + the in-app face-region review workbench.
- **User personas:** Windows power users replacing Photos/ImageGlass; photographers/archivists working directly in folders; comic/manga/webtoon readers; privacy-conscious users who value portable builds and visible runtime provenance.
- **Platforms & distribution:** Windows 10/11 x64; `net10.0-windows10.0.22621.0`; MIT; self-contained installer + portable ZIP; permanently unsigned by owner policy. Checksum-pinned WinGet/Scoop manifests supported; account-based submission is external follow-up, not a v1.0 gate.
- **Key integrations & data flows:** WIC-first decode with Magick.NET 14.15.0 (ImageMagick 7.1.2-27, libjxl 0.12.0) fallback; SharpCompress 0.50.0 read-only archives; SQLite (`Microsoft.Data.Sqlite` 10.0.10) settings/catalog/semantic index (all `Private` cache + WAL); XMP sidecars; **Windows ML** (`Microsoft.Windows.AI.MachineLearning` 2.1.74 stable, DirectML EP appended at runtime) for ONNX; SkiaSharp 4.150.1 software presenter; optional Ghostscript/jpegtran/ExifTool/c2patool child processes; Windows OCR; default-off GitHub release checks.

## Competitive Landscape

(Landscape unchanged from 2026-07-20 — one day elapsed, no competitor movement. Carried forward; do not re-survey.)

- **Lap (julyx10/lap, ~1.4k stars, GPL-3.0):** folder-first Tauri/Rust with CLIP + InsightFace + 4-pane compare **and inline video**. Learn: the CLIP+face+dedup stack Images has is now table-stakes; video is the differentiator rising entrants lead with. Avoid: nothing.
- **LowKey Media Viewer (SteveCastle/loki):** mixed images+video+audio+comics at "tens of thousands" scale. Images matches the comic/scale half; video/audio is the gap. Avoid: the server/web-UI/AI-tagging sprawl.
- **HDRImageViewer (linyusenzz, WinUI3+D3D11 FP16):** proof that ISO 21496-1 gain-maps can be *displayed* on Windows. Learn: the float-swapchain renderer pattern for promoting inspection to display. Avoid: its decode-only narrowness.
- **imazen/ultrahdr (v0.5.0, Apache-2.0, Windows CI):** *new this pass* — pure-Rust UltraHDR encoder+decoder that **reads and writes ISO 21496-1** gain-map metadata. Learn: this is the concrete, Windows-bindable path to gain-map **authoring** (write), which no free Windows viewer offers. Avoid: pulling the WinUI3 stack — bind it as a shell-out CLI instead.
- **FlyPhotos (riyasy, WinUI):** speed-first first-paint architecture; hold-to-fly scrubbing (parked V110-13). Avoid: its thin feature set.
- **RAWviewer / XnView MP:** star-rating/DAM culling — deliberately rejected (see Rejected).

## Security, Privacy, and Reliability

- **[Verified] The new V120 batch code is disposal-clean.** `DetectMany`/`SuggestMany`/`ScoreMany`/`ClassifyMany`/`AnalyzeMany` all wrap the shared `InferenceSession` in `using`/`try-finally`; cancellation `ThrowIfCancellationRequested` sits inside the scope so the session disposes on cancel; empty-list/model-missing/load-failed return before session creation. `CliCancellation` disposes its CTS and unsubscribes the handler, guarding `Cancel()` against `ObjectDisposedException`. No leaked session/CTS/semaphore on any path.
- **[Verified] Dependency posture unchanged and current.** Magick.NET 14.15.0, SharpCompress 0.50.0, `Microsoft.Data.Sqlite` 10.0.10 (no 10.0.11; a `11.0.0-preview.6` exists — do not adopt), SkiaSharp 4.150.1, Windows ML 2.1.74 stable (preview channel moved to 2.4.66-preview — a decision, not a bump). No servicing action.
- **[Verified] C2PA 2.4 (April 2026) is current** — no 2.5, no post-2.4 errata. Images' "2.4" inspect baseline is correct. No action.
- **[Verified/Reliability] `FaceClusterService.Cluster` is unbounded O(n²).** The all-pairs cosine loop (`FaceClusterService.cs:33-44`) runs over every accepted face across the whole folder batch; the GUI review caps *images* at 100 but not faces-per-image, so a crowd-photo folder drives it quadratically — the same worst-case the V120 NMS survivor cap guarded one layer down but did not reach here. Its `Array.IndexOf(accepted, item.Face)` sort key (`:49`) is additionally O(n) per element → O(n²) keying. → V130-04.
- **Recovery/rollback:** corrupt-DB quarantine, atomic sidecar/temp-swap writes, quarantine-over-delete, bounded egress-logged child processes remain the verified fallback contract. No change.

## Architecture Assessment

- **The GUI face-review workbench never adopted the V120 batch/cancellation work.** `FaceReviewWindow.AnalyzeAsync` (`FaceReviewWindow.xaml.cs:150`) runs `Task.Run(() => _analyze(paths))` with **no CancellationToken**, no CTS, no cancel affordance — only a post-hoc `if (_closed) return`. `FaceReviewService.Analyze` (`FaceReviewService.cs:51-61`) defaults to per-image `FaceRecognitionService.Analyze(path)` applied with `.Select(analyzer)`, whose `Func<string, FaceRecognitionResult>` signature structurally blocks `AnalyzeMany`. Result: a 100-image folder opens ~200 ONNX sessions (100 YuNet + 100 SFace) instead of 2, and cannot be interrupted. This is the single highest-leverage gap — the one interactive, repeated, long-running ML surface uses neither the batched session reuse nor the cancellation the V120 work built. → V130-01.
- **The V120 CLI pattern reached only 4 of 6 ML CLIs.** `ObjectCli` (`ObjectCli.cs:21`, `args.Length != 2`) and `OrientationCli` (`OrientationCli.cs:19`) remain single-image with no `CliCancellation`, no multi-path batch, and no `ModelLoadFailed`/exit-3 — even though `DetectMany`/`SuggestMany` already support batches. `ModelLoadFailed` (exit 3) exists only on Aesthetic/Scene/Safety; Face/Object/Orientation collapse a broken model into generic `Failed`. → V130-02.
- **The semantic candidate ceiling shipped as a bare static with no wiring.** `SemanticSearchService.DefaultMaxSearchCandidates` (`SemanticSearchService.cs:63`) is read only internally (`:224`); no `SettingsService` key sets it, no startup assigns it, and `SemanticSearchWindow` (`SemanticSearchWindow.xaml.cs:290`) never passes `maxCandidates` — every GUI search uses the hardcoded 50k. The doc-comment claiming "Configurable at startup" is aspirational. The V120-05 "settings surface" is unmet. → V130-03.
- **Cold-start has one unclaimed lever.** `PublishReadyToRun`/`ServerGarbageCollection`/`TieredPGO` are **not set** (`Images.csproj`/`Directory.Build.props` clean); the release publish (`dotnet publish ... -p:PublishSingleFile=false`) ships JIT-only. The gallery/archive lists **already** use `VirtualizingPanel.VirtualizationMode="Recycling"` (`MainWindow.xaml:507-517,1673-1675`) and thumbnails **already** decode-downscale (`ImageLoader.cs:992-994`), so virtualization and decode-sizing are done; R2R is the remaining measurable first-paint win (WPF is not Native-AOT-compatible, so R2R is the ceiling). → V130-06.
- **Accessibility is good but has two concrete, implementable gaps.** All 22 interactive windows carry `AutomationProperties`; the 7 files without them are ResourceDictionaries/non-interactive overlays (not real gaps — the prior "26/33" framing overcounted). The real findings: (a) **zero explicit `KeyboardNavigation.TabIndex` anywhere** — tab order is implicit declaration order across the 408 KB `MainWindow.xaml` and 70 KB `SettingsWindow.xaml`; (b) status/toast/search-count text has no UIA live-region announcement (WCAG 2.2 SC 4.1.3): WPF requires `AutomationProperties.LiveSetting` **plus** an explicit `peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)` after mutating text — the second half is what apps miss. → V130-07.
- **Test gaps on the just-shipped batch surface.** No test asserts any `*Many` overload reuses one `InferenceSession` (no session-count spy; `CreateSession` is a static seam). `ObjectDetectionServiceTests`/`OrientationSuggestionServiceTests` have no `DetectMany`/`SuggestMany` empty-list/model-missing/cancellation coverage. CLI cancel tests inject fakes and exercise the CLI's own loop, not the production `AnalyzeMany`. → V130-05.
- **Category audit:** reliability = FaceCluster bound; perf = ReadyToRun (virtualization/decode already done); UX = GUI ML batch+cancel + CLI parity; testing = batch-service tests; accessibility = live-region + tab order; distribution/i18n/plugins = external-gated (unchanged); docs = none new to sync. `MainViewModel` decomposition (V110-09), inline video, and gain-map *display* remain GUI-/renderer-/decision-gated in `Roadmap_Blocked.md` — not re-added.

## Rejected Ideas

- **Star-rating / color-label / review-DAM culling lane** (RAWviewer, XnView MP): rejected again — repository history deliberately removed the Review/rating workflow; `TagGraphService` covers hierarchical tags without it.
- **Riding Windows ML 2.4.66-preview** to advance the ONNX EPs: rejected as a preview dependency — the stable pin (2.1.74) is current and the app inherits evergreen system-managed EPs anyway. Source: nuget.org Microsoft.Windows.AI.MachineLearning.
- **Bumping to `Microsoft.Data.Sqlite` 11.0.0-preview.6 / adopting Native AOT for the viewer:** rejected — preview-only, and WPF is not AOT-compatible; R2R is the supported ceiling. Sources: nuget.org; MS Learn ReadyToRun.
- **Server GC globally:** rejected for the viewer process — Workstation-concurrent (WPF default) keeps scroll/decode latency low; Server GC only suits the separate ML/dedup CLI processes. Source: MS Learn Workstation vs Server GC.
- **Gain-map DISPLAY as near-term work:** deferred (not rejected) — still needs the blocked float-swapchain renderer; gain-map *write* (V130-08) is the achievable half now that a Windows-bindable writer exists.

## Under Consideration (decision-gated — not scheduled)

- **Inline video/MP4/WebM playback.** Unchanged from prior pass: most-validated community gap, headline feature of Lap/LowKey/LightningView, but XL and tensions the "image viewer" identity (pulls in Media Foundation/FFmpeg). Owner product decision, not autonomous work.
- **Gain-map DISPLAY.** Renderer-blocked (float swapchain); HDRImageViewer proves feasibility. The *write* half is now actionable (V130-08); display remains parked.

## Sources

### Dependencies and advisories
- https://www.nuget.org/packages/Magick.NET-Q16-AnyCPU
- https://www.nuget.org/packages/SharpCompress
- https://www.nuget.org/packages/Microsoft.Data.Sqlite
- https://www.nuget.org/packages/SkiaSharp
- https://www.nuget.org/packages/Microsoft.Windows.AI.MachineLearning

### Accessibility (WPF + WCAG 2.2)
- https://learn.microsoft.com/en-us/accessibility-tools-docs/items/wpf/control_automationproperties
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationlivesetting
- https://learn.microsoft.com/en-us/accessibility-tools-docs/items/wpf/text_livesetting
- https://learn.microsoft.com/en-us/archive/blogs/winuiautomation/common-approaches-for-enhancing-the-programmatic-accessibility-of-your-win32-winforms-and-wpf-apps-part-4-wpf
- https://www.w3.org/TR/WCAG22/

### Performance (.NET / WPF)
- https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run
- https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation
- https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/workstation-server-gc
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/application-startup-time

### Standards and gain-map libraries
- https://spec.c2pa.org/specifications/specifications/2.4/index.html
- https://github.com/imazen/ultrahdr
- https://docs.rs/ultrahdr
- https://github.com/google/libultrahdr/releases

## Open Questions

- **Inline video playback** remains an owner product-identity decision (Media Foundation preview vs. full player vs. stay image-only) — blocks correct prioritization of the largest opportunity.
- **Gain-map write (V130-08) delivery shape:** whether to bundle a Rust-built `ultrahdr` CLI (new toolchain dependency + SHA-pinned binary provenance like jpegtran/Ghostscript) or defer until a .NET-native ISO 21496-1 writer exists. This is a build-toolchain/provenance call, not a research gap.
- All other V130 items are code-ready and dependency-free; a few (tab-order reading order, cancel-button UX) need a GUI-verifiable session to confirm interactive behavior, noted per item.
