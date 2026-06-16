# Blocked Roadmap Items

This file holds roadmap work that is real but not currently actionable by an
agent. Move an item back to `ROADMAP.md` only when its blocker is cleared.

## Blocked On Local GUI Or Human Runtime Smoke

- [ ] **V02-04** *P2* — Recapture main screenshot (DPI-aware, `PrintWindow(hwnd, hdc, 2)` per `screenshots.md`). Requires Windows GUI session.
  - **Blocked by**: interactive Windows GUI screenshot session.
  - **Unblock when**: a GUI session is available and screenshots can be captured/verified.

- [ ] **V02-05** *P2* — Human runtime smoke: prev/next/wrap, rename+undo, drag-drop, Del-to-recycle, external add/delete 250 ms roundtrip.
  - **Blocked by**: human runtime validation requirement.
  - **Unblock when**: the manual smoke pass is scheduled or can be replaced by an accepted automated UI smoke.

## Blocked On Package-Manager Credentials Or Account Setup

- [ ] **D-02** *P0* — **`winget` publishing** via `WinGet Releaser` GitHub Action (`vedantmgoyal9/winget-releaser`). First submission manual via `wingetcreate new`; subsequent releases auto-fire on `release: [published]`. Requires classic PAT + forked `microsoft/winget-pkgs`. Effort: S. [WinGet Releaser action; Grafana k6 PR #5203]
  - **Blocked by**: first manual `SysAdminDoc.Images` submission to `microsoft/winget-pkgs`, fork ownership, and classic `public_repo` PAT stored as `WINGET_TOKEN`.
  - **Unblock when**: the package exists in WinGet and the repository secret/fork are configured.

## Blocked On Code-Signing Identity Or External Approval

- [ ] **D-05** *P0* — **Azure Artifact Signing** (rebrand of Azure Trusted Signing, now GA April 2026) via `azure/artifact-signing-action` in the release workflow. SmartScreen reputation warm-up still applies (since 2023 even EV is throttled for new publishers) — so no reason to pay for EV. Self-employed individuals now eligible (no 3-yr history requirement); restricted to US/CA/EU/UK businesses/individuals. Effort: M. [[S-ARTIFACT-SIGNING]](https://azure.microsoft.com/en-us/products/artifact-signing) [[S-SMARTSCREEN-REGRESSION]](https://learn.microsoft.com/en-us/answers/questions/5855708/trusted-signing-regression-in-smartscreen-reputati) *Risk flagged 2026-03/04: Microsoft silently rotated issuing CAs (EOC CA 02 → AOC CA 03 → EOC CA 04) which broke SmartScreen reputation for existing customers. Expect the first ~500 installs to trip "Unrecognized app" even with a valid cert. Hanselman has the working GitHub-Actions setup [[S-HANSELMAN-SIGN]](https://www.hanselman.com/blog/automatically-signing-a-windows-exe-with-azure-trusted-signing-dotnet-sign-and-github-actions).*
  - **Blocked by**: Azure Artifact Signing account, certificate profile, tenant/app credentials, and repository secrets.
  - **Unblock when**: signing identity and GitHub Actions secrets are provisioned.

- [ ] **D-05a** *P1* — **SignPath.io OSS code-signing evaluation** (new, 2026-04-25 research). Free certificate via SignPath Foundation for OSS projects (used by PicView). Pre-requisite: GitHub Actions integration + SignPath-approved project status. Evaluate in parallel with D-05 — whichever lands first wins; both are fine to keep running simultaneously (dual-signing is supported by Authenticode). Effort: S (application) + M (pipeline). [[S-PV]](https://github.com/Ruben2776/PicView)
  - **Blocked by**: external SignPath Foundation application and approval.
  - **Unblock when**: project approval and integration credentials are available.

- [ ] **P-07** *P2* — **C2PA write-on-export** — stamp "edited with Images v0.x" + operation list on every export from v0.3/v0.5. Requires signing identity (Azure Trusted Signing works). Defers until P-05 is stable. Effort: M.
  - **Blocked by**: C2PA signing identity and certificate choice.
  - **Unblock when**: D-05 or another accepted signing identity is available.

- [ ] **V50-25** *P2* — **C2PA write-on-export** (P-07). Per-op, opt-in; embeds operation manifest + signing identity. Requires D-05 (Trusted Signing) for the cert.
  - **Blocked by**: D-05 signing certificate.
  - **Unblock when**: signing credentials are available and P-07 is active again.

- [ ] **V50-34** *P2* — **Configurable C2PA signing identity** — default to Azure Trusted Signing cert (D-05); allow user-supplied identity.
  - **Blocked by**: at least one approved signing identity path.
  - **Unblock when**: Azure Artifact Signing, SignPath, or another accepted signing path is provisioned.

## Blocked On External Accounts Or Credentials

- [ ] **I-02** *P1* — **Crowdin for OSS** (free tier under 60k words) over GitHub. Ship en + de + fr + es + ja + pt-BR + zh-Hans as v1 locale set. Effort: M. [Crowdin OSS programme]
  - **Blocked by**: Crowdin OSS account setup and project creation.
  - **Unblock when**: Crowdin OSS application approved and project configured.

- [ ] **O-02** *P2* — **Opt-in Sentry** (free tier 5k events/month) wired via `Sentry.Serilog` sink, gated on the default-off privacy toggle (P-02). Effort: S. [Sentry WPF guide]
  - **Blocked by**: Sentry account creation and privacy toggle (P-02) implementation.
  - **Unblock when**: Sentry account provisioned and privacy toggle UI ships.

- [ ] **D-04** *P1* — **Microsoft Store via MSIX** for discovery, paired with S-07 AppContainer work. GitHub Releases stays primary. Effort: M. [MS Learn MSIX overview]
  - **Blocked by**: S-07 MSIX packaging work + Microsoft Store developer account + Store submission.
  - **Unblock when**: S-07 ships and Store account is provisioned.

## Blocked On Research / Evaluation (Not Code-Ready)

- [ ] **V80-21** *P1* — **OpenSlide Lab Pack evaluation**. Evaluate optional bundled support for Aperio SVS, Hamamatsu NDPI, Leica SCN, MIRAX, Philips TIFF, Sakura SVSLIDE, Ventana BIF, Zeiss CZI, DICOM WSI, and generic tiled TIFF. Treat as an optional "Lab Pack" because the UX, file sizes, licensing, and test corpus are different from consumer photos. Effort: L. [[S-OPENSLIDE]](https://openslide.org/) [[S-QUPATH]](https://qupath.github.io/)
  - **Blocked by**: research spike — needs evaluation of licensing, test corpus sourcing, and UX scope before code.
  - **Unblock when**: evaluation document produced with go/no-go decision.

- [ ] **V80-22** *P2* — **Bio-Formats bridge spike**. Research a Java sidecar or service boundary for Bio-Formats to preview proprietary microscopy formats and normalize metadata to OME concepts. Must be opt-in and process-isolated: JVM startup cost, GPL/commercial licensing paths, and untrusted-file attack surface make in-process loading inappropriate for the main viewer. Effort: L. [[S-BIOFORMATS]](https://www.openmicroscopy.org/bio-formats/)
  - **Blocked by**: research spike — JVM isolation architecture, GPL licensing path, and attack surface review needed.
  - **Unblock when**: spike document produced with architecture decision and license path.

- [ ] **V80-23** *P2* — **Multidimensional image navigator**. Add channel toggles, Z-stack slider, time slider, intensity range, and overlay/annotation layers for formats that expose multiple dimensions. napari is the interaction reference; Images should implement the smallest Windows-native subset that makes scientific stacks understandable. Effort: XL. [[S-NAPARI]](https://napari.org/stable/) [[S-BIOFORMATS]](https://www.openmicroscopy.org/bio-formats/)
  - **Blocked by**: V80-22 Bio-Formats bridge (provides the multidimensional format data) + XL effort needs dedicated planning run.
  - **Unblock when**: V80-22 spike completes and multidim format data is accessible.

- [ ] **V80-24** *P1* — **Streaming batch backend spike**. Evaluate NetVips/libvips for resize, thumbnail, pyramid generation, and batch export where demand-driven/horizontally threaded processing can beat full-frame Magick.NET pipelines. Do not replace Magick.NET by default; test on huge TIFF/PSD/EXR and 10k-file batch workloads first. Effort: M. [[S-LIBVIPS]](https://www.libvips.org/)
  - **Blocked by**: evaluation — needs benchmark harness and comparison against current Magick.NET pipelines.
  - **Unblock when**: benchmark comparison document produced with concrete perf numbers.

- [ ] **V80-25** *P2* — **OpenImageIO toolchain evaluation**. Evaluate OIIO as an optional pro/VFX backend for EXR, DPX, Cineon, PSD, OpenVDB/Ptex metadata, `idiff`-style image comparison, `iinfo` metadata, tiled MIP generation, and format-plugin architecture. This is likely a sidecar/tool boundary, not a direct WPF dependency. Effort: L. [[S-OIIO]](https://openimageio.readthedocs.io/en/latest/)
  - **Blocked by**: evaluation — sidecar/tool boundary design and VFX format demand assessment.
  - **Unblock when**: evaluation document produced with integration architecture decision.

- [ ] **V80-26** *P1* — **OCIO/ACES color-management roadmap**. Add an explicit color pipeline plan for ICC display transform, OpenColorIO config selection, ACES 2.0 display/view transforms, proofing warnings, and "unmanaged vs managed" status badges. This should land before serious RAW/VFX workflows. Effort: L. [[S-OCIO]](https://opencolorio.org/)
  - **Blocked by**: planning document — needs ICC pipeline maturity and OCIO native binding research.
  - **Unblock when**: color pipeline plan document produced.

- [ ] **X-01** *P1* — **Plugin boundary design doc**. Study napari, OpenImageIO, Eagle, and Hydrus before implementing plugins. The first version should define stable extension points (reader, metadata extractor, export action, batch action, library panel) and a trust model (signed/local-only/disabled-by-default), not a marketplace. Effort: M. [[S-NAPARI]](https://napari.org/stable/) [[S-OIIO]](https://openimageio.readthedocs.io/en/latest/) [[S-EAGLE]](https://www.eagle.cool/) [[S-HYDRUS]](https://hydrusnetwork.github.io/hydrus/)
  - **Blocked by**: design research — extension point inventory and trust model needed before code.
  - **Unblock when**: design doc produced with extension-point definitions and trust model.

- [ ] **S-07** *P1* — **MSIX + Win32 App Isolation** side-artifact. AppContainer, declare `picturesLibrary` + `broadFileSystemAccess` brokered. Unpackaged zip stays the primary artifact for file-association UX. Effort: L. [MS Learn AppContainer; Win32 App Isolation report]
  - **Blocked by**: research — AppContainer capability declarations, brokered access patterns, and file-association behavior need investigation.
  - **Unblock when**: AppContainer research spike complete with capability manifest draft.

- [ ] **S-08** *P2* — **Wasmtime-hosted libheif / libavif / libwebp spike** — opt-in "Paranoid Mode" routes untrusted-source decode through a capability-only Wasm sandbox (~1.3× CPU cost). Research spike; prototype only until a proven libheif-in-wasmtime crate exists. Effort: L. [Wasmtime security model; Hyperlight Wasm; Gobi USENIX]
  - **Blocked by**: research spike — no proven libheif-in-wasmtime crate exists yet.
  - **Unblock when**: upstream Wasm sandbox crate for image decoders matures, or prototype proves viability.

- [ ] **S-09** *P1* — **On bundled decoders**: if we ever vendor libheif / libavif / libwebp / libjxl / libde265 directly (today they come via WIC + Store extensions), minima are: libheif >= 1.21.0, libavif >= 1.3.0, libwebp >= 1.3.2, libde265 >= 1.0.17, libjxl current. Effort: conditional.
  - **Blocked by**: conditional — no bundled decoders yet; floors apply only when/if bundling decision is made.
  - **Unblock when**: a decision to bundle any of these decoders is made (currently relying on WIC + Store extensions).

- [ ] **S-10** *P2* — **libwebp-in-WIC isolation** — prefer Microsoft's shipped WebP path (OS-patched) over bundling `libwebp.dll`; if forced to bundle for non-Windows or MSIX sandbox, keep version current and consider the same wasm-sandbox route in S-08. Effort: conditional. [[S-LIBWEBP-ORCA]](https://orca.security/resources/blog/understanding-libwebp-vulnerability/)
  - **Blocked by**: conditional — only relevant if forced to bundle libwebp (currently using WIC path).
  - **Unblock when**: a bundling decision or MSIX sandbox forces direct libwebp dependency.

- [ ] **P-06** *P2* — **C2PA P/Invoke spike** — bind directly to `c2pa-rs` C API for in-process verify instead of shelling out to `c2patool`. Eliminates ~30 ms per-file process spawn. Effort: L. [c2pa-rs README]
  - **Blocked by**: research spike — c2pa-rs C API binding feasibility and .NET interop approach.
  - **Unblock when**: spike document with P/Invoke binding strategy produced.

- [ ] **V20-01** *P0* — **SkiaSharp canvas** replacing `WriteableBitmap` in `ZoomPanImage`. `SKCodec` decodes to target size (1000x800 buffer for 800x600 view of 4000x3600 source). ~2x load, ~4x thumbnail gen vs ImageSharp. MIT, no strings. Unlocks HDR path and every AI overlay later. [stack: `SkiaSharp`]
  - **Blocked by**: major architectural change — needs dedicated implementation run; current WPF `WriteableBitmap` pipeline is stable.
  - **Unblock when**: a dedicated multi-day implementation run is scheduled for the canvas swap.

- [ ] **V40-02** *P0* — **Watched folders** — add/remove library roots, scan-on-start, FSW for deltas. Multi-root w/ offline-prompt behavior (don't delete records on drive eject).
  - **Blocked by**: catalog maturity — V40-01 SQLite catalog schema needs to be stable before watched-folder ingest.
  - **Unblock when**: V40-01 catalog schema is stable and tested.

## Blocked On Predecessor Features

- [ ] **S-06** *P1* — **WIC JPEG re-encode gate**. On pre-patch `windowscodecs.dll` (< 10.0.26100.4946), thumbnails skip the 12-bit / 16-bit re-encode path that triggers CVE-2025-50165. Toast "Windows update recommended" once. Effort: M. [ESET CVE-2025-50165]
  - **Blocked by**: Windows version detection research — needs reliable `windowscodecs.dll` version probing.
  - **Unblock when**: version-detection approach validated and tested.

- [ ] **T-02** *P2* — **FlaUI smoke suite** — launch, open fixture folder, assert filmstrip count + title bar text. Runs as a gated CI job on windows-latest. Effort: M. [FlaUI repo + docs]
  - **Blocked by**: FlaUI test harness setup — NuGet reference, test project scaffold, and CI runner configuration.
  - **Unblock when**: FlaUI NuGet added and basic test project scaffolded.

- [ ] **T-03** *P2* — **Golden-image render tests** under `tests/render/`, DPI-pinned, per-pixel RGBA compare with tolerance via ImageSharp. Catches canvas-engine regressions when SkiaSharp lands in V20-01. Effort: M. [ImageSharp repo]
  - **Blocked by**: ImageSharp NuGet reference + V20-01 SkiaSharp canvas (render tests primarily guard SkiaSharp regressions).
  - **Unblock when**: V20-01 SkiaSharp canvas ships and ImageSharp is added.

- [ ] **I-03** *P2* — **RTL audit pass**. `FlowDirection="RightToLeft"` at window root mirrors layout, but `Canvas`, `Image`, custom `DrawingVisual`, negative-`X` `ScaleTransform`, and `DataGrid` column order need manual mirroring. Arabic + Hebrew test fixtures. Effort: L. [MS Learn localization-overview]
  - **Blocked by**: locale infrastructure maturity — I-01 string extraction + I-02 Crowdin should be stable first.
  - **Unblock when**: I-01 fully complete and at least one RTL locale has translators.
