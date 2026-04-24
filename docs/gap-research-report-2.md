# Gap Research Report 2 — 2026-04-24

Scope: security/privacy/distribution/WinML/OSS-activity/formats/upscalers gaps not covered by the existing ROADMAP AI/format/organizer/batch/plugins tracks. All claims cited; where 2026 evidence is thin, said explicitly.

## 1. Security hardening

- **WIC CVE-2025-50165** (CVSS 9.8, patched 2025-08-12): uninitialized function pointer in `windowscodecs.dll` `jpeg_finish_compress`; triggers on 12-bit/16-bit JPEG re-encode (not plain decode). Only reliably reachable when a host app re-encodes or regenerates thumbnails. Fixed in `windowscodecs.dll` ≥ 10.0.26100.4946. Root cause was libjpeg-turbo 3.0.2 upstream (fixed in 3.1.1, 2024-12-18) and the zero-init/NULL-checks were never backported into WIC. — [ESET WeLiveSecurity](https://www.welivesecurity.com/en/eset-research/revisiting-cve-2025-50165-critical-flaw-windows-imaging-component/), [Zscaler ThreatLabz](https://www.zscaler.com/blogs/security-research/cve-2025-50165-critical-flaw-windows-graphics-component)
- **WIC CVE-2025-47980** (info disclosure): leaks process memory on malformed image preview in File Explorer/Outlook/Teams. Any WIC-dependent viewer inherits this surface. — [Windows Forum](https://windowsforum.com/threads/understanding-and-mitigating-windows-imaging-component-cve-2025-47980-vulnerability.372759/)
- **libwebp CVE-2023-4863** (BLASTPASS): exploited in the wild via NSO iMessage zero-click. Heap overflow in `BuildHuffmanTable` when `color_cache_bits` indexes an undersized `kTableSize` table. Patched ≥ 1.3.2. Lesson: fuzzing was ineffective — only unit-fuzzing found it. Applies to any app that ships its own libwebp rather than relying on WIC. — [Isosceles blog](https://blog.isosceles.com/the-webp-0day/), [Cloudflare](https://blog.cloudflare.com/uncovering-the-hidden-webp-vulnerability-cve-2023-4863/)
- **libheif CVEs 2024-2026**: CVE-2024-41311 (overlay OOB r/w in 1.17.6), CVE-2025-68431 (heap over-read on `iovl` overlay box, fixed 1.21.0), CVE-2026-3950 (OOB read in `Track::load`, 1.21.2 affected). — [Ubuntu CVE tracker](https://ubuntu.com/security/cves?package=libheif), [Debian tracker](https://tracker.debian.org/pkg/libheif)
- **libavif CVE-2025-48174 / CVE-2025-48175**: integer overflow → heap buffer overflow in `makeRoom` (stream.c) and `avifImageRGBToYUV` (reformat.c). Both fixed in libavif 1.3.0. — [GitHub Advisory](https://github.com/advisories/GHSA-f6x7-5x3c-j3rg), [Snyk](https://security.snyk.io/vuln/SNYK-DEBIAN12-LIBAVIF-10180086)
- **SharpCompress**: no new CVE 2024-2026. Historical zip-slip fix landed in 0.21.0. Downstream `ZipDirectory.ExtractToDirectory` pattern weaponized in ConnectWise ScreenConnect CVE-2024-1708 (actively exploited). Defense: canonicalize and `StartsWith(destRoot, Ordinal)` every entry before write; reject symlinks/hardlinks; enforce max uncompressed size. — [JFrog Research](https://research.jfrog.com/vulnerabilities/archiver-zip-slip/), [Huntress CVE-2024-1708](https://www.huntress.com/threat-library/vulnerabilities/cve-2024-1708)
- **WPF / XAML 2024-2025 CVEs are deserialization-driven, not image-decoder**: CVE-2024-10012 (XAML import via DocumentPartProperties), CVE-2024-8316 (RadDiagram), CVE-2024-10095 (PersistenceFramework), CVE-2024-7575 (command injection via hyperlink in RichTextBox). No 2024-2026 CVEs found specifically against WPF's `BitmapDecoder` / `System.Windows.Media.Imaging` pipeline, but it transitively exposes every WIC bug above. — [Telerik KB CVE-2024-10012](https://docs.telerik.com/devtools/wpf/knowledge-base/kb-security-unsafe-deserialization-cve-2024-10012)
- **Argv-driven open (Images.exe `<path>`)**: typical mistakes are (a) passing through to `Process.Start` with `UseShellExecute=true` rather than decoding as a file path, (b) not normalizing `\\?\` vs `\\.\` vs UNC before validating existence, (c) not rejecting paths with `..` before realpath resolve, (d) following symlinks into restricted areas. Use `Path.GetFullPath` + compare against allowlist roots; drop `UseShellExecute`. — (defensive .NET guidance, [OWASP .NET Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html))
- **ExifTool invocation**: safe pattern is `ProcessStartInfo.UseShellExecute=false` + `ArgumentList` (never string-concatenated `Arguments`) + `-@ argfile.txt` UTF-8 argfile for untrusted inputs (no shell quoting performed inside argfile). Don't shell out via `cmd /c`. Existing wrappers: SharpExifTool, FileMeta/ExifToolWrapper. — [exiftool docs](https://exiftool.org/exiftool_pod.html), [SharpExifTool](https://www.junian.dev/SharpExifTool/)
- **Sandboxing realism for a WPF viewer in 2026**: Windows Sandbox = disposable VM, not viable for daily app. AppContainer + Win32 App Isolation is Microsoft's forward path; already in production in Chrome and Acrobat Reader. MSIX-packaged AppContainer is the practical shipping target, with brokered file access costs via `RuntimeBroker.exe` and forced suspension on minimize. Requires manifest capabilities + drops ambient authority. — [MS Learn: AppContainer isolation](https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation), [Directions on Microsoft: Win32 App Isolation](https://www.directionsonmicrosoft.com/reports/win32-app-isolation-another-sandbox/)
- **Wasm-isolated decoders**: architectural pattern is mature; Wasmtime + capability-only imports + fuel limits. Gobi (USENIX) proved it on libjpeg/libpng/zlib. No public libheif-in-wasmtime drop-in yet. ~1.3× CPU cost. Hyperlight Wasm (CNCF Sandbox 2025) adds a micro-VM underneath for defense-in-depth. — [Wasmtime security](https://docs.wasmtime.dev/security.html), [Microsoft Hyperlight Wasm](https://opensource.microsoft.com/blog/2025/03/26/hyperlight-wasm-fast-secure-and-os-free/)

Roadmap-eligible items extracted:
- **S-01**: Adopt SharpCompress ≥ latest with custom post-extract canonicalization check (`Path.GetFullPath(entry).StartsWith(destRoot, Ordinal)`) + symlink rejection + uncompressed-size cap. Effort: S. — JFrog / Huntress
- **S-02**: Argv open hardening: `Path.GetFullPath`, reject `..` pre-resolve, `UseShellExecute=false`, validate against allowlist, log rejections. Effort: S. — OWASP .NET Cheat Sheet
- **S-03**: Pin Magick.NET ≥ 14.9.1 (ImageGlass 9.4 notes: CVE-2025-53015 / 55004 / 55154 / 55298 / 62594 patched). Add automated Dependabot/Renovate gate. Effort: S. — ImageGlass 9.4 notes
- **S-04**: If bundling libheif/libavif/libwebp (vs relying on WIC+extensions), ship ≥ libheif 1.21+, libavif 1.3+, libwebp 1.3.2+. Add CVE-delta CI on each release. Effort: M. — CVE refs above
- **S-05**: ExifTool invocation uses UTF-8 argfile (`-@ args.txt`) + `ProcessStartInfo.ArgumentList`. No shell. Add fuzz test that injects `\r\n`, `<`, `>`, `|` into filenames and confirms rejection. Effort: S. — exiftool docs
- **S-06**: Ship an MSIX + AppContainer build as a secondary artifact (unpackaged stays primary for file-association UX). Declare only `picturesLibrary` + `broadFileSystemAccess` (brokered). Effort: L. — MS Learn AppContainer
- **S-07**: Research spike on Wasmtime-hosted libheif as an opt-in "paranoid mode" decoder for untrusted files. Effort: L. — Hyperlight / Gobi
- **S-08**: Thumbnail generation path avoids re-encoding JPEGs through the vulnerable WIC `jpeg_finish_compress` surface on pre-patch Windows builds; gate on `windowscodecs.dll` version. Effort: M. — ESET CVE-2025-50165

## 2. Privacy

- **"Phones home on open" defense**: Windows Photos uses signed Microsoft endpoints; ImageGlass opts out of network calls entirely except manual update checks. Precedent for "why did the network light up" UI is Little Snitch / GlassWire — none of the OSS viewers surveyed ship an embedded egress log. — [ImageGlass 9.4](https://imageglass.org/news/announcing-imageglass-9-4-97)
- **EXIF GPS strip UX**: Microsoft Store "EXIF Metadata Editor Pro" and ExifRemover.com web tool represent the one-click baseline; browser extensions do right-click context menus. No current mainstream Windows viewer ships in-app GPS-only strip with a visible confirmation of what was removed. — [Microsoft Store EXIF Metadata Editor Pro](https://www.microsoft.com/en-zw/p/exif-metadata-editor-pro-photo-gps-viewer/9ph7f9zh9z8w), [ExifRemover](https://exifremover.com/)
- **Opt-in vs opt-out telemetry**: VS Code is the reference for GDPR-clean OSS desktop telemetry — in-product banner, global `telemetry.telemetryLevel` setting, granular service opt-outs for update checks, extension marketplace, etc. TelemetryDeck is the third-party no-PII SDK used by indie desktop apps. GDPR Art. 6 requires explicit opt-in for any non-essential personal data; update-check IP + version-UA is typically argued under "legitimate interest" but becomes safer if the UA is version-only with no machine ID. — [VS Code telemetry](https://code.visualstudio.com/docs/configure/telemetry), [TelemetryDeck Privacy FAQ](https://telemetrydeck.com/docs/guides/privacy-faq/)
- **C2PA Content Credentials .NET status**: **no official `c2pa-csharp` / `c2pa-dotnet` SDK exists** as of April 2026. contentauth maintains Rust (`c2pa-rs`), C++, JS, Python, Android — no C#. Viable paths for .NET: (a) P/Invoke the C API exposed by `c2pa-rs` (preferred, in-process), (b) shell out to `c2patool` CLI (simpler for viewers, adequate for read/verify). Spec currently at v2.3 (2025-10); Trust List launched mid-2025, Interim Trust List retired. — [contentauth GitHub](https://github.com/contentauth/), [c2pa-rs](https://github.com/contentauth/c2pa-rs), [C2PA whitepaper Oct 2025](https://c2pa.org/wp-content/uploads/sites/33/2025/10/content_credentials_wp_0925.pdf)

Roadmap-eligible items extracted:
- **P-01**: One-click "Strip location" context menu (toolbar + right-click) that removes `GPSInfo`, `XMP-exif:GPS*`, IPTC location tags only; preserves camera/time/copyright. Shows a diff toast. Effort: S. — ExifRemover pattern
- **P-02**: Default-off telemetry; banner on first run with "Send anonymous usage stats" toggle pointing to a local JSON preview of what would be sent. No IP, no MAC, no hostname. Effort: S. — VS Code model
- **P-03**: "Why is it phoning home?" status bar indicator + log pane showing every network egress (update check, C2PA fetch, linked-metadata fetch) with URL + purpose + timestamp. Effort: M. — no OSS precedent; competitive advantage over ImageGlass/Photos
- **P-04**: Update check is pull-only to GitHub releases API with no PII and an explicit opt-out; store "last checked" locally, no server-side record. Effort: S. — GDPR-clean pattern
- **P-05**: C2PA read/verify support via `c2patool` CLI shellout (simplest path) + "Content Credentials" icon in toolbar when a manifest is present. Signing/write deferred. Effort: M. — c2patool + c2pa-rs
- **P-06**: Spike on P/Invoke binding to `c2pa-rs` C API for in-process verify without shelling out. Effort: L. — c2pa-rs README

## 3. Windows distribution channels in 2026

- **MSIX**: read-only install dir; suspended when minimized (problematic for background thumbnail generation); COM registration isolated (image-format plugin discovery breaks); TWAIN drivers break in AppContainer. Original MSIX app attach was deprecated 2025-06-01 (replaced by AVD unified model). No evidence the file-association-as-default restriction has been relaxed on Win11 as of 2026-04. — [Advanced Installer FAQ](https://www.advancedinstaller.com/user-guide/faq-msix.html), [Turbo.net MSIX Limitations 2025](https://www.turbo.net/blog/posts/2025-06-16-understanding-msix-limitations-enterprise-application-compatibility)
- **winget**: `WinGet Releaser` GitHub Action (vedantmgoyal9) is the community-standard automated publishing route; requires classic PAT with `public_repo` scope and a forked `microsoft/winget-pkgs`. First submission still needs `wingetcreate new` manually; subsequent releases automate via `release: [published]` event. Real-world example: Grafana k6 wired this up in September 2025. — [WinGet Releaser action](https://github.com/marketplace/actions/winget-releaser), [wingetcreate](https://github.com/microsoft/winget-create), [k6 PR #5203](https://github.com/grafana/k6/pull/5203)
- **Scoop**: `extras` bucket is the standard destination for .NET desktop apps; no formal moderation, PR-reviewed by maintainers. Template: PowerShell-style JSON manifest. Private bucket viable for pre-release or corporate use. — [Scoop extras](https://github.com/ScoopInstaller/Extras)
- **Chocolatey**: community package every version goes through moderation; rigorous but slow (often days-to-weeks in 2026). Commercial/Chocolatey for Business bypasses moderation. Distribution-rights friction: community packages typically download from GitHub rather than embedding binaries. — [Chocolatey community packages](https://community.chocolatey.org/packages?q=dotnet)
- **Azure Trusted Signing**: Basic tier $9.99/mo / 5,000 signatures; Premium $99.99/mo / 100,000. OV-only (no EV). Short-lived certs (days) + timestamp; signatures remain valid indefinitely. Available to US/CA/EU/UK orgs + US/CA individuals. No USB token. **Does not confer instant SmartScreen reputation** — still has to build. — [Trusted Signing FAQ](https://learn.microsoft.com/en-us/azure/artifact-signing/faq), [Authenticode in 2025 (Eric Lawrence)](https://textslashplain.com/2025/03/12/authenticode-in-2025-azure-trusted-signing/)
- **DigiCert EV**: ~$500-900/yr; USB token required; as of 2026-02-15 all code-signing CAs capped at 1-year max cert lifespan per CA/Browser Forum. Instant SmartScreen reputation is the last real EV advantage. — [SignMyCode DigiCert EV](https://signmycode.com/digicert-ev-code-signing)
- **`dotnet publish` for .NET 9 WPF**: `PublishTrimmed=true` requires `SelfContained=true` (framework-dependent errors with NETSDK1102). WPF is **historically not trim-safe** — reflection in XAML/bindings needs `[DynamicDependency]` or `ILLink.Descriptors.xml` roots or it will fail at runtime. Compressed single-file self-contained trimmed WPF exe typically 40-60 MB zipped / 100-150 MB extracted. Native AOT not supported for WPF; R2R is the ceiling. Framework-dependent single-file is ~5 MB but requires users to have .NET 9 Desktop Runtime. — [MS Learn single-file](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview), [MS Learn trim self-contained](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained), [dotnet/wpf#3070](https://github.com/dotnet/wpf/issues/3070)

Roadmap-eligible items extracted:
- **D-01**: Primary = framework-dependent single-file win-x64 zip (~5 MB); secondary = self-contained non-trimmed (~70 MB compressed) for no-runtime users. Avoid trimming WPF until WPF trim warnings are upstream-fixed. Effort: S. — MS Learn
- **D-02**: Add `winget` manifest via `WinGet Releaser` GitHub Action on `release: [published]`. Requires forking `microsoft/winget-pkgs` + PAT secret. First submission manual via `wingetcreate new`. Effort: S. — WinGet Releaser
- **D-03**: Add Scoop `extras` bucket manifest (autoupdate section with GitHub release URL template). Effort: S. — Scoop
- **D-04**: Evaluate Chocolatey community package; cost is moderation latency, not money. Defer until v1.0 and only if demand exists. Effort: S. — Chocolatey
- **D-05**: Azure Trusted Signing ($120/yr) for Authenticode on all release artifacts. Don't expect EV reputation. Effort: M (Entra tenant, signing identity, GH Action wiring). — Trusted Signing
- **D-06**: Optional MSIX side-artifact for AppContainer. Accept read-only install dir + minimize-suspend tradeoffs. Ship .appinstaller for sideload. Effort: L. — MSIX
- **D-07**: Trim-warning audit spike — enable `<PublishTrimmed>true</PublishTrimmed>`, collect every IL2026/IL2xxx warning, decide whether WPF is trimmable enough in .NET 9 to justify. If net-negative, park. Effort: M. — dotnet/wpf#3070

## 4. Windows ML vs ONNX Runtime DirectML — 2026 status

- **Windows ML is shipped GA** for C++/C#/Python across Win10+ as of the May 2025 Build announcement. Exposed via WinRT (`Microsoft.Windows.AI.MachineLearning.dll`) and the extended ONNX Runtime API. — [MS Learn: supported EPs](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/supported-execution-providers), [Copilot+ PC developer guide](https://learn.microsoft.com/en-us/windows/ai/npu-devices/)
- **Auto-EP selection works on Win11 24H2+** (build 26100+). CPU + DirectML EPs ship with the OS; NPU-specific EPs (QNN for Qualcomm, OpenVINO for Intel, VitisAI for AMD Ryzen AI, TensorRT-for-RTX for NVIDIA RTX, MIGraphX for AMD ROCm) are **dynamically downloaded** via Windows Update's D-week (optional non-security preview) channel. Fallback graceful to DirectML GPU then CPU. — [MS Learn: supported EPs](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/supported-execution-providers), [NVIDIA dev blog](https://developer.nvidia.com/blog/deploy-ai-models-faster-with-windows-ml-on-rtx-pcs/), [AMD dev article](https://www.amd.com/en/developer/resources/technical-articles/2026/ai-model-deployment-using-windows-ml-on-amd-npu.html)
- **Bundled ORT**: on Win11 24H2+, apps can call Windows ML and get CPU + DirectML EPs without shipping a private ORT. On earlier Windows (Win10 1809 through Win11 23H2), **you still ship your own ORT+DirectML** (≈ 150-200 MB including the DirectML provider DLLs). — [MS Learn](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/supported-execution-providers)
- **DirectML is now maintenance-only** (no new features, security/compliance fixes only). WinML is the forward story. — [GitHub microsoft/DirectML README](https://github.com/microsoft/DirectML)
- **NPU reality on Copilot+ PCs**: WinML handles Qualcomm Hexagon (QNN), Intel AI Boost (OpenVINO), AMD XDNA (VitisAI) automatically if the model's opset is supported by that EP and the accelerator reports ready. Not every model runs on NPU — quant-sensitive, limited opset. Fallback to GPU is transparent. — [MS Learn](https://learn.microsoft.com/en-us/windows/ai/npu-devices/)

Roadmap-eligible items extracted:
- **W-01**: Add `UseWindowsML` build flag; on Win11 24H2+, skip shipping our own ORT and consume the OS one. Saves ~150 MB installer size on modern Windows. Effort: M. — MS Learn
- **W-02**: Detect NPU presence via `ExecutionProviderCatalog.FindDevicePolicy` and label AI features in UI ("Running on NPU" / "GPU" / "CPU") so users see the value of a Copilot+ PC. Effort: S. — Copilot+ dev guide
- **W-03**: Stop shipping DirectML-only models where a WinML-compatible equivalent exists; DirectML is feature-frozen. Effort: S. — DirectML GH README
- **W-04**: Fall-back matrix in CI: smoke test model inference on CPU EP, DirectML EP, and (if available on runner) QNN. Effort: M. — MS Learn

## 5. Recent OSS image viewer activity — 2025/2026

- **ImageGlass** (actively maintained, GPLv3): 9.3 (May 2025 — 15th anniversary, self-contained .NET), 9.4 (Dec 2025 — `MinDimensionToUseWIC` flag, Magick.NET 14.9.1 with 5 CVE fixes, .HIF ext, SVG relative-path via WebView2), 9.4.1.15 (Jan 2026 — hotfix). **ImageGlass 10 Beta 1 released** (2026 roadmap); v9 goes to bugfix-only; 6-month EOL window after v10 GA. — [ImageGlass 9.4 announce](https://imageglass.org/news/announcing-imageglass-9-4-97), [2026 roadmap](https://imageglass.org/news/imageglass-roadmap-update-2026-98)
- **nomacs** (GPLv3, confirmed): 3.22.0 final 2025-12-29 after RC1 (Nov 2025) and RC2 (Dec 2025). Unique feature is multi-instance sync for overlay comparison. Added: dock input steal fix, Quick-Launch plugins, macOS fixes, UI animation disable, RAW loader memory reduction + pink-pixel fix. — [nomacs releases](https://github.com/nomacs/nomacs/releases), [nomacs 3.22.0 final](https://www.neowin.net/software/nomacs-3220-final/)
- **JPEGView (sylikc fork)** (GPLv3): actively maintained. Latest v1.3.46 with ICC profile support + a fix for a long-standing AVX2/4K panning freeze (CPUType=AVX2 + HighQualityResampling=true + window width > 3200). Added animated AVIF, QOI r/w, CTRL+SHIFT+LMouseDrag zoom. Win-on-ARM still open (issue #354, Jan 2025). — [sylikc/jpegview](https://github.com/sylikc/jpegview)
- **Pictus**: **discontinued** at 1.7.0 (poppeman). CatKasha fork is a mirror, not active. No 2025-2026 releases. — [Pictus GitHub](https://github.com/poppeman/Pictus), [Portable Freeware](https://www.portablefreeware.com/index.php?id=2666)
- **Oculante** (Rust, MIT): actively maintained, ~monthly releases, latest 0.9.2. Features: non-destructive edit, lossless JPEG edit, DICOM via dicom-rs, network-listen mode, HEIF via libheif-rs on Windows, RAW via quickraw. Privacy stance: no telemetry, no ads ever. — [woelper/oculante](https://github.com/woelper/oculante)
- **pqrs / qview / imv-ng**: `pqrs` appears to refer to the Parquet inspector, not an image viewer. `qview`-in-Rust exists but last updated 2023. `imv-ng` did not surface. No Rust entrants beyond Oculante with meaningful traction as of April 2026. — [github topics image-viewer rust](https://github.com/topics/image-viewer?l=rust)
- **New entrants**: No GitHub breakout image viewer launched in 2025-2026 with notable star velocity surfaced in this pass.

Roadmap-eligible items extracted:
- **O-01**: Differentiator audit against ImageGlass 9.4 / nomacs 3.22 / JPEGView 1.3.46: live rename (unique to us), Win7 Classic chrome + Catppuccin (ours), multi-instance sync (nomacs-only), node-graph editing (none have it — aspirational). Effort: S (doc pass). — release notes above
- **O-02**: Borrow nomacs's multi-instance sync-zoom-pan as an optional "Compare mode" for A/B viewing. Effort: M. — nomacs 3.22
- **O-03**: Borrow JPEGView's lossless JPEG crop/rotate/mirror (no recompress). Effort: M. — sylikc/jpegview v1.3.46
- **O-04**: Borrow Oculante's network-listen mode (`Images.exe -l <port>`) so piped workflows can stream into the viewer. Effort: M. — Oculante README

## 6. File format progress 2025-2026

- **JPEG XL**: partial comeback. Chrome 145 (Feb 2026) ships Rust-based `jxl-rs` decoder **behind a flag** (`chrome://flags/#enable-jxl-image-format`), not default. Firefox Nightly has `jxl-rs` landing (target Firefox 149) with 6 blockers before stable. Safari 17+ has OS-level JPEG XL but no animation, no progressive. Effective global browser share ~12%, almost all Safari. Edge inherits from Chromium but undocumented. Microsoft ships a **JPEG XL Image Extension** for Windows (WIC). No "libjxl in Windows by default" story as of April 2026 — it's an opt-in Store extension. Interop 2026 lists JXL as investigation, not focus. — [CoreWebVitals JXL status](https://www.corewebvitals.io/pagespeed/jpeg-xl-core-web-vitals-support), [Wikipedia JPEG XL](https://en.wikipedia.org/wiki/JPEG_XL), [jpegxl.info software](https://jpegxl.info/resources/supported-software.html), [Phoronix](https://www.phoronix.com/news/JPEG-XL-Possible-Chrome-Back)
- **AVIF**: iPhone 16 still defaults to **HEIC** (not AVIF) — no change in default. Windows AVIF support via AV1 Video Extension (WIC), animation supported but quirky in Photos; Paint.NET AVIF FileType plugin updated 2026-03-29. — [OpenAVIFFile guide](https://openaviffile.com/how-to-open-avif-files-on-windows/), [Paint.NET AVIF plugin](https://forums.getpaint.net/topic/116233-avif-filetype-03-29-2026/)
- **HEIC licensing**: container is Nokia-BSD (royalty-free, commercial redistribution OK with attribution). **HEVC codec is the trap** — separate licensing via MPEG LA / Access Advance / Velos Media / direct Nokia. Nokia enforced against Acer/Asus in Germany in 2024 — real teeth. AVIF sidesteps entirely because AV1 is royalty-free. Practical guidance: don't ship your own HEVC decoder for commercial redistribution; rely on Microsoft's HEIF Image Extension (user-installed from Store) which handles licensing on Windows. — [Nokia HEIF LICENSE.TXT](https://github.com/nokiatech/heif/blob/master/LICENSE.TXT), [Tom's Hardware Acer/Asus](https://www.tomshardware.com/laptops/acer-and-asus-halt-pc-and-laptop-sales-in-germany-amid-h-264-codec-patent-dispute-nokia-wins-patent-ruling-forcing-tech-giants-to-license-hevc-codec)
- **WebP**: nothing material beyond libwebp 1.6.0 in 2025-2026.
- **cjpegli (Jpegli)**: Google's new JPEG encoder (2024) delivers ~35% better compression at high quality with universal decoder compatibility. Adoption growing but not yet in `sharp` / mainstream libraries by default as of late 2025. Real use in photography tooling (DPReview forum workflows, Canon HDR→XYB JPEG pipelines). Recommendation for a viewer: offer cjpegli as the JPEG export encoder. — [Google OSS blog Jpegli](https://opensource.googleblog.com/2024/04/introducing-jpegli-new-jpeg-coding-library.html), [SqueezeJPG 2025](https://www.squeezejpg.com/blog/jpeg-compression-in-2025-best-practices-and-new-formats)
- **C2PA camera adoption**: Leica M11/M11-P/SL2/SL3/SL3-S native. Sony A1 II / A9 III, PXW-Z300 camcorder (IBC 2025 first camcorder with native C2PA). Nikon Z6III firmware 2.00 (Aug 2025) **had its signing cert revoked** after vulnerability — service suspended as of early 2026, no restoration yet; Z9/Z8 still pending. Canon R1 firmware available. Fujifilm/Panasonic general CAI members, products coming. Google Pixel 10 signs by default; Samsung Galaxy S25 signs only AI-edited. Social media strip C2PA on upload — durable credentials (watermark + fingerprint + manifest) solving this. — [AttestTrail C2PA cameras 2026](https://attesttrail.com/blog/c2pa-cameras-support), [C2PA Camera tracker](https://c2pa.camera/), [cookmscott compatibility list](https://github.com/cookmscott/c2pa-compatibility-list)

Roadmap-eligible items extracted:
- **F-01**: Detect and surface Microsoft Store image extensions (HEIF, AV1, WebP, JPEG XL, RAW) that the user hasn't installed; offer one-click deep-link to Store. Effort: S. — Microsoft Photos pattern
- **F-02**: Don't bundle HEVC — rely on OS HEIF extension. Explicitly document licensing rationale in README to preempt "why doesn't it open HEIC" questions. Effort: S. — Nokia HEIF license
- **F-03**: Export-as-JPEG uses cjpegli via `Jpegli.dll` (libjxl-shipped) for 35% smaller output; flag "compatibility JPEG" option that falls back to libjpeg-turbo. Effort: M. — Google Jpegli
- **F-04**: Read C2PA manifests (via `c2patool` CLI) and render a trust badge: green (signed + verified + chain rooted in C2PA Trust List), amber (signed but cert not in list), red (manifest invalid / tampered). Effort: M. — C2PA conformance
- **F-05**: JPEG XL support via Microsoft's WIC extension path (same as AVIF/HEIF): detect, prompt install. Don't bundle libjxl until Microsoft ships it OS-default. Effort: S. — jpegxl.info

## 7. Upscaler / inpainting ecosystem updates 2025-2026

- **Real-ESRGAN** (BSD-3): last meaningful release was 2022; models (2x, 4x, anime) remain widely used and are still the default upscaler shipping in consumer apps. Not dead, but not evolving — the community has moved to newer architectures. — [OpenModelDB RealESRGAN 4x+](https://openmodeldb.info/models/4x-realesrgan-x4plus)
- **chaiNNer** (GPLv3): **actively developed**, node-based GUI, full GPU support (CUDA/TensorRT/NCNN/ROCm/MPS), supports PyTorch + NCNN + ONNX + TensorRT. The de facto "fat client" for running community upscalers. — [chaiNNer-org/chaiNNer](https://github.com/chaiNNer-org/chaiNNer)
- **OpenModelDB**: active community model registry, 2025-2026 updates ongoing. Models like StarSample V2.0 (HAT-L / ESRGAN / SPAN-S) added. Legacy tools (CupScale, ESRGAN CLI Joey fork, IEU) marked unmaintained in the FAQ. — [OpenModelDB](https://openmodeldb.info/), [OpenModelDB FAQ](https://openmodeldb.info/docs/faq)
- **Current best-in-class architectures for photos (not anime)**: SPAN (fast), OmniSR, HAT-L (quality), RealPLKSR. For speed/video: Compact, SPAN. Real-ESRGAN is still viable but no longer SOTA. — [OpenModelDB FAQ](https://openmodeldb.info/docs/faq)
- **SUPIR / diffusion upscalers ONNX-runnable for local app**: no strong ONNX-runnable SUPIR variant ships with a weight budget appropriate for a viewer-sized app as of April 2026 — SUPIR is big, SDXL-dependent, and the distillations that are viewer-sized don't beat Real-ESRGAN/HAT-L consistently on photos. No clear winner in the research surfaced.
- **LaMa inpainting**: still the OSS SOTA for "generative erase" at local-app weight budget. Primary ONNX port is `Carve/LaMa-ONNX` (~208 MB fp32, fixed 512×512 input). **OpenCV 5.0+ ships a LaMa inpainting DNN sample natively** as of Feb 2025 with a quantized model added May 2025 — this is the path of least resistance for a .NET app via OpenCvSharp. Qualcomm LaMa-Dilated targets on-device NPU. — [Carve/LaMa-ONNX](https://huggingface.co/Carve/LaMa-ONNX), [OpenCV inpainting_lama](https://huggingface.co/opencv/inpainting_lama), [OpenCV PR #26736](https://github.com/opencv/opencv/pull/26736)
- **Windows Photos 2026 AI features shipped**: Generative Erase runs locally on Copilot+ PCs, Restyle Image (pre-set artistic styles + custom prompts) added Oct 2025 in Paint 11.2509.441.0 and Photos. Both use Windows App SDK `ImageGenerator` API with `ImageFromImageGenerationStyle.Restyle`. Requires NPU. Model provenance disclosure: these run locally with small language models shipped via Windows; Microsoft does not publish C2PA signing on Restyle output as of April 2026. — [Neowin Paint Restyle](https://www.neowin.net/news/microsoft-now-lets-you-restyle-images-in-paint/), [MS Learn ImageGenerator](https://learn.microsoft.com/en-us/windows/ai/apis/image-generation)

Roadmap-eligible items extracted:
- **U-01**: Default upscaler model = 4x RealESRGAN (proven, legal); add HAT-L + SPAN-S downloadable options ("photo quality" + "fast"). Tile to handle arbitrary sizes. Effort: M. — OpenModelDB
- **U-02**: Model fetch via OpenModelDB JSON index; cache in `%LOCALAPPDATA%`; SHA-256 verify before use. Never bundle models in installer — would bloat to 500+ MB. Effort: M. — OpenModelDB
- **U-03**: Generative Erase via LaMa fp16 ONNX through WinML (auto-EP picks NPU/DirectML/CPU). 512×512 tile + dilated mask. Effort: L. — Carve/LaMa-ONNX, OpenCV lama sample
- **U-04**: On Copilot+ PCs with Windows App SDK ≥ matching version, expose "Restyle Image" via `ImageGenerator` API with preset style prompts; fall back to "not available" banner elsewhere. Effort: M. — MS Learn ImageGenerator
- **U-05**: SUPIR/diffusion upscaler: explicitly deferred as ROADMAP open question. No viewer-sized ONNX weight that beats Real-ESRGAN on photos in 2026. Revisit if one ships. Effort: — (no work, just tracking).

## Sources

- https://www.welivesecurity.com/en/eset-research/revisiting-cve-2025-50165-critical-flaw-windows-imaging-component/
- https://www.zscaler.com/blogs/security-research/cve-2025-50165-critical-flaw-windows-graphics-component
- https://windowsforum.com/threads/understanding-and-mitigating-windows-imaging-component-cve-2025-47980-vulnerability.372759/
- https://blog.isosceles.com/the-webp-0day/
- https://blog.cloudflare.com/uncovering-the-hidden-webp-vulnerability-cve-2023-4863/
- https://ubuntu.com/security/cves?package=libheif
- https://tracker.debian.org/pkg/libheif
- https://github.com/advisories/GHSA-f6x7-5x3c-j3rg
- https://security.snyk.io/vuln/SNYK-DEBIAN12-LIBAVIF-10180086
- https://research.jfrog.com/vulnerabilities/archiver-zip-slip/
- https://www.huntress.com/threat-library/vulnerabilities/cve-2024-1708
- https://docs.telerik.com/devtools/wpf/knowledge-base/kb-security-unsafe-deserialization-cve-2024-10012
- https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html
- https://exiftool.org/exiftool_pod.html
- https://www.junian.dev/SharpExifTool/
- https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation
- https://www.directionsonmicrosoft.com/reports/win32-app-isolation-another-sandbox/
- https://docs.wasmtime.dev/security.html
- https://opensource.microsoft.com/blog/2025/03/26/hyperlight-wasm-fast-secure-and-os-free/
- https://imageglass.org/news/announcing-imageglass-9-4-97
- https://imageglass.org/news/imageglass-roadmap-update-2026-98
- https://www.microsoft.com/en-zw/p/exif-metadata-editor-pro-photo-gps-viewer/9ph7f9zh9z8w
- https://exifremover.com/
- https://code.visualstudio.com/docs/configure/telemetry
- https://telemetrydeck.com/docs/guides/privacy-faq/
- https://github.com/contentauth/
- https://github.com/contentauth/c2pa-rs
- https://c2pa.org/wp-content/uploads/sites/33/2025/10/content_credentials_wp_0925.pdf
- https://www.advancedinstaller.com/user-guide/faq-msix.html
- https://www.turbo.net/blog/posts/2025-06-16-understanding-msix-limitations-enterprise-application-compatibility
- https://github.com/marketplace/actions/winget-releaser
- https://github.com/microsoft/winget-create
- https://github.com/grafana/k6/pull/5203
- https://github.com/ScoopInstaller/Extras
- https://community.chocolatey.org/packages?q=dotnet
- https://learn.microsoft.com/en-us/azure/artifact-signing/faq
- https://textslashplain.com/2025/03/12/authenticode-in-2025-azure-trusted-signing/
- https://signmycode.com/digicert-ev-code-signing
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained
- https://github.com/dotnet/wpf/issues/3070
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/supported-execution-providers
- https://learn.microsoft.com/en-us/windows/ai/npu-devices/
- https://developer.nvidia.com/blog/deploy-ai-models-faster-with-windows-ml-on-rtx-pcs/
- https://www.amd.com/en/developer/resources/technical-articles/2026/ai-model-deployment-using-windows-ml-on-amd-npu.html
- https://github.com/microsoft/DirectML
- https://github.com/nomacs/nomacs/releases
- https://www.neowin.net/software/nomacs-3220-final/
- https://github.com/sylikc/jpegview
- https://github.com/poppeman/Pictus
- https://www.portablefreeware.com/index.php?id=2666
- https://github.com/woelper/oculante
- https://www.corewebvitals.io/pagespeed/jpeg-xl-core-web-vitals-support
- https://en.wikipedia.org/wiki/JPEG_XL
- https://jpegxl.info/resources/supported-software.html
- https://www.phoronix.com/news/JPEG-XL-Possible-Chrome-Back
- https://openaviffile.com/how-to-open-avif-files-on-windows/
- https://forums.getpaint.net/topic/116233-avif-filetype-03-29-2026/
- https://github.com/nokiatech/heif/blob/master/LICENSE.TXT
- https://www.tomshardware.com/laptops/acer-and-asus-halt-pc-and-laptop-sales-in-germany-amid-h-264-codec-patent-dispute-nokia-wins-patent-ruling-forcing-tech-giants-to-license-hevc-codec
- https://opensource.googleblog.com/2024/04/introducing-jpegli-new-jpeg-coding-library.html
- https://www.squeezejpg.com/blog/jpeg-compression-in-2025-best-practices-and-new-formats
- https://attesttrail.com/blog/c2pa-cameras-support
- https://c2pa.camera/
- https://github.com/cookmscott/c2pa-compatibility-list
- https://openmodeldb.info/
- https://openmodeldb.info/docs/faq
- https://github.com/chaiNNer-org/chaiNNer
- https://huggingface.co/Carve/LaMa-ONNX
- https://huggingface.co/opencv/inpainting_lama
- https://github.com/opencv/opencv/pull/26736
- https://www.neowin.net/news/microsoft-now-lets-you-restyle-images-in-paint/
- https://learn.microsoft.com/en-us/windows/ai/apis/image-generation
- https://github.com/advimman/lama
