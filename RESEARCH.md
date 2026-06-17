# Research — Images

## Executive Summary

Images is a mature, feature-rich WPF image viewer/editor at v0.2.11 with ~90 service classes, 78 test files, and an unusually broad feature surface for a local-first Windows viewer: inline rename, 100+ format decode, archive/book browsing, gallery workbench, duplicate cleanup, file health scan, batch processor, macro actions, import inbox, non-destructive edit stack (crop, resize, adjustments, annotations, effects, perspective, exposure brush, red-eye, clone/heal), OCR, CLIP semantic search foundation, C2PA inspection, reference boards, pinned overlay, pixel inspector, culling/review mode, compare mode, export workbench, deep-zoom tiling, and a network-egress transparency panel no competitor ships.

The project's strongest current position is the combination of broad codec coverage, local-first privacy stance, and viewer-editor-organizer convergence that no single OSS or commercial competitor matches on Windows. Its weakest position is runtime currency: .NET 9 (STS, EOL November 2026), ONNX Runtime 1.24.4 (security fixes in 1.25.0+), Serilog 4.2.0 (current 4.3.1), and Microsoft.Data.Sqlite 9.0.0 (current 10.0.9). The .NET 10 LTS migration is the highest-priority infrastructure work.

**Top 10 opportunities, priority order:**

1. **Migrate to .NET 10 LTS** — .NET 9 EOL is November 2026; .NET 10 brings WPF Fluent text controls, performance improvements, and 3-year support.
2. **Update ONNX Runtime to 1.26.0+** — 1.24.4 has known security vulnerabilities (heap OOB read/write, integer overflow) fixed in 1.25.0.
3. **Update NuGet dependencies** — Serilog 4.2→4.3.1, Microsoft.Data.Sqlite 9.0→10.0.9, Serilog.Sinks.File 6.0→7.0.0.
4. **Activate CLIP/SigLIP semantic search** — the embedding provider seam exists; validated ONNX models are pinned in Model Manager. SigLIP 2 base (~350 MB) outperforms CLIP ViT-B/32 for desktop use.
5. **Ship Squoosh-style visual-diff converter** — no native Windows tool offers this; commercial competitors paywall quality comparison.
6. **Complete operation-chain batch converter** — XnView MP and ACDSee set the bar; the macro/batch foundation exists.
7. **AI-assisted culling** (closed eyes, blur detection) — Adobe and Capture One ship this; open CV/ML models make it feasible locally.
8. **Content-based format detection** — Oculante demonstrates this; Images currently trusts file extensions.
9. **Archive password support** — NeeView ships this; Images' archive mode would benefit.
10. **Ghostscript 10.07.0→10.07.1 update** — minor security hardening (removed `.tempfile` operator).

## Product Map

### Core workflows
- **Browse**: open image, navigate folder with natural sort, zoom/pan/rotate/flip, filmstrip, gallery grid
- **Rename**: live inline rename with debounce, conflict resolution, undo stack
- **Edit**: non-destructive crop/resize/adjust/annotate/effects/perspective/exposure/red-eye/retouch with XMP edit stack
- **Organize**: culling review mode, ratings/tags, duplicate cleanup, file health scan, import inbox, recovery center
- **Export**: codec-aware save-a-copy, batch processor, macro actions, export workbench with quality preview

### User personas
- **Casual viewer**: replaces Windows Photos with faster, darker, more capable viewer
- **File organizer**: renames, tags, rates, deduplicates photo collections locally
- **Content creator**: reference boards, pixel inspector, export workbench, annotations
- **Power user**: OCR, semantic search, compare mode, deep-zoom, archive browsing, C2PA inspection

### Platforms and distribution
- Windows 10/11 (x64), WPF on .NET 9
- Portable zip + Inno Setup installer + Scoop manifest + WinGet manifest (pending credentials)
- Unsigned — SmartScreen warns on first run

### Key integrations and data flows
- WIC (OS codecs) → Magick.NET fallback → optional Ghostscript (PDF/EPS) → optional jpegtran (lossless JPEG)
- SQLite settings.db + catalog.db + semantic-index.db at %LOCALAPPDATA%\Images\
- XMP sidecars for ratings/tags/edit history (authoritative over SQLite cache)
- Optional c2patool for C2PA manifest inspection
- Optional ONNX models (CLIP, LaMa, BiRefNet, Real-ESRGAN) via Model Manager

## Competitive Landscape

### ImageGlass (13.4k stars, v10 Beta 2 June 2026)
- **Does well**: Cross-platform Avalonia rewrite, HDR tone-mapping, native SVG rendering, plugin SDK, async I/O pipeline, hardware-aware caching. Largest OSS viewer community.
- **Learn from**: Plugin SDK architecture, HDR/SVG native rendering, Explorer sort-order sync, Motion Photo support, memory budget controls.
- **Avoid**: v9→v10 disruption created a window of user frustration. Don't break existing workflows for architectural purity.

### Czkawka/Krokiet (31.5k stars, v11.0.1 Feb 2026)
- **Does well**: Duplicate/similar-image finding, bad-extension scanner, EXIF remover, video optimizer. Migrated from GTK to Slint. Massive community.
- **Learn from**: Selective EXIF tag removal (not just GPS strip), "bad names" mode for problematic filenames, video property display, hard/symbolic link creation.
- **Avoid**: Scope creep into video processing territory.

### NeeView (873 stars, v45.3 March 2026)
- **Does well**: Best archive/book viewer on Windows. Password-protected archives, thumbnail history panel, breadcrumb navigation, per-user installer. Already on .NET 10 WPF.
- **Learn from**: Archive password support via 7z.dll, breadcrumb address bar, history panel with thumbnails and grouping, .NET 10 migration path.
- **Avoid**: Book-first mental model that makes photo browsing feel awkward.

### ACDSee Photo Studio 2026 ($150)
- **Does well**: Full local AI suite (denoise, hair masking, face edit, super-res, sky replacement, object selection). Background batch processing. 750+ RAW camera models.
- **Learn from**: Every AI feature they charge $150 for can be offered free with open ONNX models. Their multithreaded Activity Manager pattern for background work.
- **Avoid**: Feature bloat — their 2026 edition tries to be Photoshop and Lightroom simultaneously.

### Windows Photos (free, hardware-gated AI)
- **Does well**: Generative erase, relight, super-resolution, AI search. Free with Windows.
- **Learn from**: AI features gated to Copilot+ PCs create an opportunity — Images can offer similar capabilities on any hardware via DirectML.
- **Avoid**: Users widely complain about crashes, "save a copy" UX, and OneDrive lock-in. Don't replicate their cloud dependencies.

### Eagle (design asset manager, $29.95 one-time)
- **Does well**: AI search (offline), plugin system, browser capture, smart folders, 81+ formats. Eagle 5.0 adding AI actions and MCP integration.
- **Learn from**: Natural language search UX, automated tagging/categorization, MCP integration for AI assistants.
- **Avoid**: Design-asset focus that doesn't translate to photo workflows.

### Adobe Lightroom Classic ($10-22/month subscription)
- **Does well**: AI-assisted culling (closed eyes, out-of-focus detection), auto-stacking by visual similarity, auto dust removal, distraction removal.
- **Learn from**: AI culling is becoming expected by photographers. The background AI processing during batch workflows pattern is smart.
- **Avoid**: Subscription lock-in, cloud dependency, generative credits model.

### Capture One 16.8 ($18-49/month)
- **Does well**: Assisted Review (closed eyes, focus detection), Snapdragon NPU acceleration for AI tools, contact sheets, mask export.
- **Learn from**: NPU acceleration labeling ("Running on NPU") — Images planned this but hasn't shipped. Actions system for third-party integrations.
- **Avoid**: Enterprise/studio pricing complexity.

## Security, Privacy, and Reliability

### Dependency currency (action required)
- **ONNX Runtime 1.24.4**: Has known vulnerabilities fixed in 1.25.0 (heap OOB read/write, integer truncation, Pad Reflect vulnerability). Update to 1.26.0+. Path: `src/Images/Images.csproj` line 39.
- **Serilog 4.2.0**: Current is 4.3.1. No security issues but minor improvements. Path: `src/Images/Images.csproj` line 42.
- **Serilog.Sinks.File 6.0.0**: Current is 7.0.0 (major version bump). Path: `src/Images/Images.csproj` line 44.
- **Microsoft.Data.Sqlite 9.0.0**: Current is 10.0.9 (tracks .NET 10 servicing). Path: `src/Images/Images.csproj` line 37.
- **Ghostscript 10.07.0** (bundled): Current is 10.07.1. Removed `.tempfile` operator attack surface. Minor update.

### .NET 9 end-of-life
- .NET 9 STS support extended to 24 months, EOL **November 10, 2026** — 5 months away.
- .NET 10 LTS (GA November 2025) supported through November 2028.
- .NET 10 WPF brings Fluent text control styles, faster pixel format conversions, unified clipboard API.
- NeeView (direct WPF peer) already migrated to .NET 10.

### Native codec floor updates
- **libheif 1.22.0** needed for CVE-2026-32740 (heap buffer overflow, CVSS 8.8). Current S-09 floor is 1.21.0.
- **libjxl** has CVE-2026-1837 (use-after-free in LCMS2 color transforms). No bundled libjxl in Images, but relevant if bundling is considered.
- **Magick.NET 14.14.0** is current and covers all 2026 CVEs including CVE-2026-46557 (stack overflow in fx operation).
- **SharpCompress 0.48.1** is current. CVE-2026-44788 affects `WriteToDirectory()` which Images does not call (read-only archive streams only).

### Missing guardrails
- No content-based format validation — files trusted by extension only. Oculante demonstrates content-based detection as a safety feature.
- No granular EXIF tag removal — only GPS strip exists. Czkawka ships selective tag removal.
- No archive password support — password-protected archives fail silently or with generic error.

## Architecture Assessment

### Strengths
- Clean service-per-feature architecture with 90+ focused services
- Comprehensive test coverage (78 test files) with regression tests for domain logic
- XMP sidecar-authoritative design — SQLite is always a rebuildable cache
- Embedding provider seam ready for CLIP→SigLIP upgrade
- Well-documented integration policy for optional runtimes

### Migration priority
- **MainViewModel.cs** remains the largest file — further controller extraction would improve maintainability (IP-02 partially complete per `docs/improvement-plan.md`)
- The `.NET 10` migration touches `Images.csproj` (TargetFramework), all Microsoft.* package versions, and potentially WPF Fluent style adoption

### Refactor candidates
- `src/Images/Images.csproj:5` — `net9.0-windows10.0.22621.0` → `net10.0-windows10.0.22621.0`
- `src/Images/Images.csproj:37-44` — all NuGet versions need updating post-.NET-10 migration
- `src/Images/Services/SemanticSearchService.cs` — embedding provider needs CLIP→SigLIP upgrade path

### Test gaps
- No FlaUI/UI automation smoke tests (T-01/T-02 planned but not shipped)
- No integration tests for the full decode→display→export pipeline
- No archive password handling tests
- No content-based format detection tests (feature doesn't exist yet)

## Rejected Ideas

| Idea | Reason | Source |
|------|--------|--------|
| Video playback/library parity | Scope creep — contradicts viewer-first charter | Czkawka video optimizer, community requests |
| Cloud sync / account system | Contradicts local-first philosophy | Immich/PhotoPrism model |
| Mobile backup client | Requires server/mobile stack — product shift | Immich pattern |
| Plugin marketplace | Trust/moderation burden too high for current team size | Eagle plugin center |
| WinUI 3 migration | WPF is stable, mature, and receiving .NET 10 investment; WinUI 3 migration risk outweighs benefits for existing codebase | FlyPhotos uses WinUI 3 but is greenfield |
| Electron/web-based UI | Performance regression, binary bloat, contradicts native Windows goal | Squoosh is web-only |
| Full Lightroom-style RAW development | XL effort, niche audience, better served by darktable/RawTherapee | Commercial tools |
| SkiaSharp canvas migration | WPF canvas works well; SkiaSharp adds complexity without clear user benefit at current scale | ImageGlass v10 uses SkiaSharp but is greenfield Avalonia |
| Automatic model downloads | Contradicts privacy-first principle — model imports must be manual | Eagle auto-download pattern |

## Sources

### Direct OSS competitors
- https://github.com/d2phap/ImageGlass/releases (v9.5, v10 Beta 2)
- https://github.com/Ruben2776/PicView/releases (v4.2.0)
- https://github.com/nomacs/nomacs/releases (v3.22.1)
- https://github.com/jurplel/qView/releases (v7.1)
- https://github.com/sylikc/jpegview/issues/362 (maintenance status)
- https://github.com/woelper/oculante/releases (v0.9.2)
- https://github.com/neelabo/NeeView/releases (v45.3)
- https://github.com/tannerhelland/PhotoDemon/releases (v2025.12)
- https://github.com/qarmin/czkawka/releases (v11.0.1)
- https://github.com/voidtools/voidImageViewer (v1.0.0.15)

### Commercial competitors
- https://www.xnview.com/xnviewmp_update.txt (v1.11.2)
- https://www.faststone.org/FSViewerDetail.htm (v8.4)
- https://www.acdsee.com/en/photo-studio/ai/ (2026 local AI)
- https://www.eagle.cool/ (v4.0, 5.0 beta)
- https://www.pureref.com/handbook/2.0/features/ (v2.1.2)
- https://helpx.adobe.com/lightroom-classic/using/whats-new.html (v15.3)
- https://support.captureone.com/hc/en-us/articles/360002718798 (v16.8.1)

### Standards and platform
- https://caniuse.com/jpegxl (JPEG XL browser support)
- https://spec.c2pa.org/specifications/specifications/2.4/ (C2PA 2.4)
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview (Windows ML)
- https://dotnet.microsoft.com/en-us/download/dotnet/10.0 (.NET 10 LTS)
- https://github.com/microsoft/onnxruntime/releases (v1.26.0)
- https://ghostscript.readthedocs.io/en/gs10.07.1/News.html (v10.07.1)

### Security advisories
- https://github.com/microsoft/onnxruntime/releases/tag/v1.25.0 (security fixes)
- https://github.com/advisories/GHSA-rcr6-g7jc-f57g (CVE-2026-46557 Magick.NET)
- https://www.thehackerwire.com/libheif-heap-buffer-overflow-cve-2026-32740/
- https://github.com/advisories/GHSA-6c8g-7p36-r338 (CVE-2026-44788 SharpCompress)

### Community and ecosystem
- https://www.reddit.com/r/software/comments/1bkcctt (Windows viewer complaints)
- https://www.reddit.com/r/foss/comments/1qdpfz6/ (FOSS viewer recommendations)
- https://news.ycombinator.com/item?id=46794971 (Immich discussion)
- https://news.ycombinator.com/item?id=46087549 (local image search discussion)
- https://github.com/Eventual-Inc/local-image-search (CLIP MCP server)
- https://github.com/riyasy/FlyPhotos (WinUI 3 viewer)

### Models and AI
- https://huggingface.co/deepghs/siglip_onnx (SigLIP 2 ONNX)
- https://openmodeldb.info/ (Real-ESRGAN, upscaling models)
- https://github.com/ZhengPeng7/BiRefNet (background removal)

## Open Questions

1. **SigLIP vs CLIP model choice**: The semantic search foundation pins CLIP ViT-B/32. SigLIP 2 base offers better zero-shot performance at similar size (~350 MB). Should the project support both or migrate to SigLIP only? Affects Model Manager pinned candidates.
2. **Serilog.Sinks.File 6→7 breaking changes**: Major version bump may require migration work. Needs changelog review before update.
3. **.NET 10 WPF Fluent styles**: .NET 10 brings Fluent overhauls for TextBox, DatePicker, RichTextBox. Do these conflict with the Catppuccin Mocha custom theme, or can they coexist?
4. **Archive password UX**: NeeView delegates to 7z.dll. Images uses SharpCompress (managed). Does SharpCompress support password-protected archives, or does this require a runtime addition?
