# Research — Images

Date: 2026-07-14 (pass 4) — replaces all prior research.

## Executive Summary
Images is a mature, Windows-only, local-first WPF/.NET 10 image viewer + light DAM + ML editor (v0.2.26, ~1017 tests, 100+ services). It already **leads the OSS Windows-viewer field** on ML/editing (CLIP semantic search over ONNX DirectML — the README's "future work" note is stale, `ClipEmbeddingProvider` runs real ViT-B/32 inference and only falls back to deterministic embeddings when models are absent; LaMa inpaint, super-resolution, background removal, non-destructive levels/curves/HSL/retouch/dodge-burn, C2PA provenance) and matches incumbents on format breadth, archives, OCR, tags, duplicate cleanup, batch, compare, slideshow, and gigapixel tiling. The remaining opportunity is **not more features** — it is **display-pipeline fidelity and untrusted-input hardening**, the two axes where every serious color/photo viewer (FastStone, BandiView, nomacs) still beats it. Most of the marquee display items (true HDR output, full monitor color management, GPS map overlay, Explorer thumbnail handler) are already correctly parked in `Roadmap_Blocked.md`, gated on a SkiaSharp canvas rewrite (V20-01), code signing (D-05), or an ExifTool write wrapper. This pass surfaces the **unblocked, incremental** wins those blocked epics overlook.

Top opportunities, priority order:
1. **ImageMagick read-coder allowlist + delegate lockdown** — the one real, unblocked security gap (P1).
2. **Better SDR tonemapping at decode** for HDR/EXR/Radiance/16-bit content — pure `ImageLoader` change, no renderer rewrite (P2).
3. **Legacy-mode monitor-ICC display output** — wide-gamut accuracy achievable in the *current* WPF pipeline, ahead of the blocked SkiaSharp epic (P2).
4. **Focus-peaking + highlight/shadow-clipping overlays** for RAW/photo culling (P2).
5. **HDR-display detection + status badge** (IDXGIOutput6) — cheap, honest, sets up the future path (P2).
6. **SharpCompress 0.50.0 bump** (CRC verification + truncated-stream tolerance for comic archives) (P3).
7. **Native-dependency version assertions in diagnostics** (SQLite, ImageMagick) (P3).

## Product Map
- **Core workflows:** open files/folders/sessions/archives/comic-books; navigate; inline rename-while-viewing; compare; inspect metadata/provenance/pixels/loupe; non-destructive edit + Save-a-copy; batch/macro pipelines; semantic/duplicate/health scans; recover destructive actions.
- **Personas:** Windows power users replacing Photos/ImageGlass/FastStone; photographers/archivists working in local folders; comic/manga readers; technical users valuing portable artifacts, checksums, provenance, and visible network behavior.
- **Platforms/distribution:** Windows 10/11 x64, `net10.0-windows10.0.22621.0`, MIT; Inno installer + portable ZIP; scripted local release gates; GitHub Releases; framework-dependent; **unsigned** (code signing is a standing blocker for shell-integration features).
- **Integrations/data flows:** WIC first → Magick.NET-Q16 14.15.0 fallback (embedded-ICC → sRGB); SharpCompress read-only archives; SQLite settings/catalog/semantic-index; XMP sidecars; optional Ghostscript 10.07.0 / jpegtran 3.1.4.1 / ExifTool / c2patool / Windows OCR / ONNX-DirectML CLIP; opt-in GitHub release check logged by `NetworkEgressService`.

## Competitive Landscape
- **FastStone / BandiView / nomacs (color viewers):** all ship **display-ICC monitor-profile** color management; Images converts only to sRGB (`ImageLoader.cs`), so it over-saturates on wide-gamut (P3/AdobeRGB) monitors. Learn: convert embedded → *monitor* profile in legacy (Advanced-Color-off) mode; it's a WPF-native change. Avoid: their heavier, dated UIs. (nomacs #394; FastStone monitor-profile thread.)
- **ImageGlass 10 / BandiView / MS HDRImageViewer:** do **HDR display** (scRGB/HDR10 swapchain) and SDR tonemapping. Images hard-clips HDR/EXR to sRGB. Learn: at minimum tonemap (ACES/Hable) at decode — every serious viewer looks better for it. Avoid full HDR-swapchain in pure WPF (airspace tax — see Architecture).
- **FastRawViewer:** focus-peaking + exposure-clipping overlays for fast RAW culling — Images has curves/levels but no culling overlays. Learn: cheap edge/threshold overlays on the decoded buffer.
- **Honeyview / BandiView (comic):** webtoon/continuous-vertical-scroll reading. Images has RTL + two-page spreads but only paged navigation. Learn: add a continuous-scroll archive mode.
- **ImageGlass / Pictus:** Explorer **thumbnail provider** for HEIC/AVIF/JXL/RAW/CBZ. Genuinely valuable but already parked (V70-04, blocked on plugin boundary + MSIX + code signing). Avoid re-litigating until D-05 unblocks.
- **Do NOT adopt (unchanged):** WebView2 dependency, cross-platform rewrite, cloud/multi-user/telemetry.

## Security, Privacy, and Reliability
- **[Verified — real gap] No ImageMagick read-coder allowlist.** `MagickSecurityPolicy.cs` sets thorough `ResourceLimits` (memory/disk/area/dimensions/time/threads/list) and a *write*-format blocklist, but no read-side coder allowlist and no `ConfigurationFiles.Policy`/delegate `rights="none"`. Crafted exotic-format inputs (MNG, TIM/PSX, SF3, MSL, Log-colorspace) can still reach the native decoder — precisely the 2025-2026 ImageMagick heap-overflow CVE class (CVE-2025-55004/55005/53014, CVE-2025-66628). Mitigation: inject a deny-all-then-permit-web-safe coder policy + delegate lockdown at init. Effort S-M. (imagemagick.org security-policy; GHSA advisories.)
- **[Verified — mostly mitigated] Ghostscript.** Bundled **10.07.0** ≥ 10.06.0, so the 2025 GS RCEs (CVE-2025-59798-59801) are patched, and ImageMagick document delegates are gated behind explicit Ghostscript availability. Residual hardening (confirm `-dSAFER`, no network, scratch-dir path sandbox, low-priv token, magic-byte gate before dispatch) is worthwhile but lower urgency given the version floor. (Artifex hardening blog.)
- **[Verified — not exposed] SharpCompress zip-slip CVE-2026-44788** affects only `WriteToDirectory()`; Images streams archive entries without extracting to disk, so it is safe by construction. Do not introduce `WriteToDirectory` on untrusted archives.
- **[Likely — verify] Native dependency floors.** Confirm the ImageMagick core inside Magick.NET-Q16 14.15.0 is ≥ 7.1.2-2 (CVE-2025-57803 BMP-encoder, CVSS 9.8) and the SQLite inside `bundle_e_sqlite3 3.0.3` is ≥ 3.50.2 (CVE-2025-6965, CVSS 9.8; low exposure — DB SQL is app-authored). No startup version assertion exists; add one to diagnostics.
- **Reliability:** several already-tracked hot-path items remain (gallery virtualization, per-nav UI-thread I/O, RAW double-tail) in the existing ROADMAP; not re-listed here.

## Architecture Assessment
- **Display pipeline is the ceiling.** `ImageLoader.cs` decodes → `TransformColorSpace(SRGB, SRGB)` → 8-bit `WriteableBitmap` Bgra32. This forecloses HDR and monitor-gamut accuracy. Two increments land in this one file **without** the blocked SkiaSharp rewrite: (a) a tonemap operator before 8-bit quantization; (b) a monitor-ICC destination profile (read via `GetICMProfile`/`WcsGetDefaultColorProfile`) in legacy (Advanced-Color-off) mode. The full HDR/managed-display epic (V100-05/06) still needs the new canvas.
- **HDR in WPF is an XL trap.** Every Windows app doing true HDR display uses a DXGI flip swapchain (UWP/WinUI/native/browser); WPF cannot host one in its compositor. An `HwndHost` swapchain works but incurs the **airspace** problem — Images' rich WPF overlay stack (`ZoomPanImage`, OCR/crop/selection/exposure/red-eye/retouch overlays) can't composite over an HDR child HWND without being ported into D3D. Confirms parking V100-06; the SDR-tonemap + detection increments deliver ~80% of the perceived benefit for ~5% of the effort.
- **Test/observability gaps:** no runtime assertion of native decoder/SQLite versions; the new tonemap/peaking/coder-policy work should each ship focused tests mirroring `ImageAdjustmentServiceTests` / `MagickSecurityPolicy` patterns.

## Rejected Ideas
- **True HDR display via `HwndHost` swapchain (now):** XL, breaks WPF overlay compositing (airspace). Already parked as V100-06; do not pull forward. Source: dotnet/wpf #4569, MS Advanced-Color doc.
- **Explorer thumbnail/preview handler (now):** valuable but blocked on plugin-boundary design + MSIX AppContainer + code signing (D-05). Already parked (V70-04, Scout). Do not duplicate. Source: MS Building Thumbnail Providers.
- **GPS map overlay (now):** already parked (V20-23) behind ExifTool GPS write wrapper; also, online map tiles conflict with local-first unless offline-tiled or "open in browser." Source: blocked roadmap.
- **Screen capture:** out of scope — the user maintains a separate tool (SwiftShot); duplicating it bloats the viewer. Source: internal.
- **SkiaSharp canvas as a research item:** already the P0 linchpin (V20-01) in the blocked roadmap; not re-proposed. Source: blocked roadmap.
- **Slideshow / MP4 export as net-new:** slideshow already exists (`ToggleSlideshow`/interval/shuffle in `MainViewModel`); only transition shaders + MP4 encode would be new, and MP4 encode needs an encoder dependency — low ROI. Source: code scan.
- **SQLite FTS5 / SQLCipher encryption:** interesting but the catalog is a rebuildable cache, not a system of record; encryption adds a native-swap (`bundle_e_sqlcipher`) for little gain on local-only data. Source: MDS encryption docs.

## Sources
Code (primary evidence):
- src/Images/Services/MagickSecurityPolicy.cs (ResourceLimits + write blocklist; no read-coder allowlist)
- src/Images/Services/ImageLoader.cs (TransformColorSpace→sRGB→Bgra32; no tonemap)
- src/Images/Services/ClipEmbeddingProvider.cs (real ONNX ViT-B/32 inference)
- src/Images/Services/ArchiveBookService.cs (streamed, no WriteToDirectory)
- src/Images/Images.csproj (dep versions); README.md (GS 10.07.0)

Competitors:
- https://github.com/d2phap/ImageGlass/releases
- https://github.com/nomacs/nomacs/issues/394
- https://www.faststone.org/FSViewerDetail.htm
- https://www.bandisoft.com/bandiview/
- https://www.fastrawviewer.com/usermanual17/focus-peaking-and-overlay-grid
- https://github.com/13thsymphony/HDRImageViewer

HDR / color / platform:
- https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range
- https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/nf-dxgi1_6-idxgioutput6-getdesc1
- https://learn.microsoft.com/en-us/windows/win32/wcs/advanced-color-icc-profiles
- https://learn.microsoft.com/en-us/windows/win32/wcs/windows-color-system
- https://github.com/dotnet/wpf/issues/4569

Security / deps:
- https://imagemagick.org/script/security-policy.php
- https://imagemagick.org/source/policy-secure.xml
- https://github.com/advisories/GHSA-hm4x-r5hc-794f (CVE-2025-53014 ImageMagick)
- https://security.snyk.io/vuln/SNYK-DEBIAN11-IMAGEMAGICK-12202814 (CVE-2025-57803)
- https://github.com/advisories/GHSA-6c8g-7p36-r338 (CVE-2026-44788 SharpCompress — not exposed)
- https://github.com/advisories/GHSA-2m69-gcr7-jv3q (CVE-2025-6965 SQLite)
- https://ghostscript.com/releases/cve/index.html
- https://github.com/adamhathcock/sharpcompress/releases/tag/0.50.0

## Open Questions
- Monitor-ICC in legacy mode: apply per-monitor on the current window's display (re-transform on monitor change) or a single primary-display profile? Decides whether the transform is cached per-image or re-run on `WM_DISPLAYCHANGE`/window move — the difference between an M and an L.
- Tonemap default: ship Reinhard (safe, neutral) as always-on for HDR-class inputs, or expose an operator picker (Reinhard/Hable/ACES) and leave off by default? Decides whether this is a silent quality fix or a user-facing setting.
