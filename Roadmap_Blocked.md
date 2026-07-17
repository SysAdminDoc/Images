# Blocked Roadmap Items

This file holds roadmap work that is real but not currently actionable by an
agent. Move an item back to `ROADMAP.md` only when its blocker is cleared.

Permanent owner policy: D-05/D-05a and signing-dependent C2PA write/export work (P-07, V50-25, V50-34) are retired, never release gates; any future Explorer integration must use an unsigned unpackaged path or be dropped.

## Blocked On Local GUI Or Human Runtime Smoke

- [ ] **V02-04** *P2* — Recapture main screenshot (DPI-aware, `PrintWindow(hwnd, hdc, 2)` per `screenshots.md`). Requires Windows GUI session.
  - **Blocked by**: interactive Windows GUI screenshot session.
  - **Unblock when**: a GUI session is available and screenshots can be captured/verified.

- [ ] **V02-05** *P2* — Human runtime smoke: prev/next/wrap, rename+undo, drag-drop, Del-to-recycle, external add/delete 250 ms roundtrip.
  - **Blocked by**: human runtime validation requirement.
  - **Unblock when**: the manual smoke pass is scheduled or can be replaced by an accepted automated UI smoke.

- [ ] **V110-16** *P2* — Render the extracted gain map as a grayscale overlay. `GainMapInspectionService` already detects gain maps and reports their metadata (shipped 2026-07-16); the remaining work extracts the MPF/aux gain-map image to a `BitmapSource` and shows it as a grayscale layer over the base image via a viewer command.
  - **Blocked by**: foreground GUI verification of the overlay rendering (repo rule forbids foreground WPF smoke on an active desktop; the FlaUI smoke harness is skipped in the headless build).
  - **Unblock when**: a GUI session can verify the grayscale overlay renders and the command disables for non-gain-map files.

- [ ] **V110-12** *P2* — Jump to next/previous archive without leaving the reader (at the last page, Next rolls into the first page of the next CBZ/CBR in folder order; symmetrically for Previous). The adjacent-archive resolution over `DirectoryNavigator`'s file list is straightforward, but the integrated rolling UX — paged vs continuous mode, right-to-left turn direction, page-command enablement at boundaries, and landing-page selection — must be validated in the running app.
  - **Blocked by**: foreground GUI verification of the archive-to-archive rolling navigation across paged/continuous/RTL modes.
  - **Unblock when**: a GUI session can confirm the boundary rolling behavior and command enablement match the existing navigation semantics.

- [ ] **V110-13** *P2* — Hold-to-scrub rapid folder fly-through (holding Next/Prev accelerates navigation using preloaded/thumbnail frames without full-decode stutter, settling on a full decode when released). The value is the input-timing feel and stutter-free acceleration, which cannot be judged without running the app.
  - **Blocked by**: foreground GUI verification of the hold-to-scrub input timing and decode smoothness.
  - **Unblock when**: a GUI session can confirm the accelerated scrub feels smooth and single presses are unchanged.

- [ ] **V110-09** *P2* — Continue extracting the `MainViewModel` god object (move slideshow, archive-reader, and rename-editor state/commands into dedicated controllers behind the existing `_uiDispatcher`/`() => _isDisposed` convention). Pure maintainability with no behavior change, but it reshapes interactive subsystems (slideshow timing, rename debounce/undo, archive paging) whose regressions surface only at runtime, and the file is under active parallel-agent editing.
  - **Blocked by**: required runtime validation of the extracted slideshow/rename/archive behaviors plus an exclusive (non-parallel) editing window on this file.
  - **Unblock when**: a dedicated GUI-verifiable session on an unshared checkout is available to refactor and smoke-test the interactive behaviors.

## Blocked On Package-Manager Credentials Or Account Setup

- [ ] **D-02** *P0* — **`winget` publishing** via `WinGet Releaser` GitHub Action (`vedantmgoyal9/winget-releaser`). First submission manual via `wingetcreate new`; subsequent releases auto-fire on `release: [published]`. Requires classic PAT + forked `microsoft/winget-pkgs`. Effort: S. [WinGet Releaser action; Grafana k6 PR #5203]
  - **Blocked by**: first manual `SysAdminDoc.Images` submission to `microsoft/winget-pkgs`, fork ownership, and classic `public_repo` PAT stored as `WINGET_TOKEN`.
  - **Unblock when**: the package exists in WinGet and the repository secret/fork are configured.

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

- [ ] *P3* — **Reconcile the README's Ghostscript-source-archive claim with actual releases**. `README.md` states the AGPL Ghostscript "matching source archive is attached to the GitHub release", but releases v0.2.20–v0.2.26 attach only the ZIP, installer, and checksums. Where: `README.md`, release asset set, `scripts/Prepare-GhostscriptBundle.ps1`.
  - **Blocked by**: AGPL compliance (legal) decision — whether to satisfy the corresponding-source obligation by attaching the Ghostscript source archive to each release, by a written offer, or by documented upstream availability. The correct wording/mechanism is a licensing judgment, not a code change.
  - **Unblock when**: the compliant source-availability mechanism is chosen; then either attach the source archive or update the README to describe the actual offer.

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

- [ ] **V40-02** *P0* — **Watched folders** — add/remove library roots, scan-on-start, FSW for deltas. Multi-root w/ offline-prompt behavior (don't delete records on drive eject).
  - **Blocked by**: catalog maturity — V40-01 SQLite catalog schema needs to be stable before watched-folder ingest.
  - **Unblock when**: V40-01 catalog schema is stable and tested.

## Blocked On Predecessor Features

- [ ] *P3* — **Contact sheet caller should pass theme colors**. `ContactSheetOptions` accepts TextColor/PlaceholderColor/BackgroundColor but no caller passes theme-appropriate colors yet. Where: `src/Images/Services/ContactSheetService.cs`.
  - **Blocked by**: no contact-sheet UI exists in the main app, so the service currently has no caller to wire theme colors through.
  - **Unblock when**: a contact-sheet UI/command is added; wire Catppuccin palette colors through at that time.

- [ ] **S-06** *P1* — **WIC JPEG re-encode gate**. On pre-patch `windowscodecs.dll` (< 10.0.26100.4946), thumbnails skip the 12-bit / 16-bit re-encode path that triggers CVE-2025-50165. Toast "Windows update recommended" once. Effort: M. [ESET CVE-2025-50165]
  - **Blocked by**: Windows version detection research — needs reliable `windowscodecs.dll` version probing.
  - **Unblock when**: version-detection approach validated and tested.

- [ ] **T-03** *P2* — **Golden-image render tests** under `tests/render/`, DPI-pinned, per-pixel RGBA compare with tolerance via ImageSharp. Catches canvas-engine regressions when SkiaSharp lands in V20-01. Effort: M. [ImageSharp repo]
  - **Blocked by**: ImageSharp NuGet reference + V20-01 SkiaSharp canvas (render tests primarily guard SkiaSharp regressions).
  - **Unblock when**: V20-01 SkiaSharp canvas ships and ImageSharp is added.

- [ ] **I-03** *P2* — **RTL audit pass**. `FlowDirection="RightToLeft"` at window root mirrors layout, but `Canvas`, `Image`, custom `DrawingVisual`, negative-`X` `ScaleTransform`, and `DataGrid` column order need manual mirroring. Arabic + Hebrew test fixtures. Effort: L. [MS Learn localization-overview]
  - **Blocked by**: locale infrastructure maturity — I-01 string extraction + I-02 Crowdin should be stable first.
  - **Unblock when**: I-01 fully complete and at least one RTL locale has translators.

## Blocked On ExifTool S-05 Safe Write Wrapper

- [ ] **V40-12** *P1* — **Metadata templates** — save/apply IPTC copyright/creator blocks across N files atomically (Bridge pattern). Effort: M.
  - **Blocked by**: ExifTool S-05 safe write wrapper not yet implemented — atomic multi-file metadata writes depend on it.
  - **Unblock when**: S-05 ExifTool safe write wrapper ships.

- [ ] **V40-14** *P1* — Full IPTC / XMP / EXIF editor pane (dockable, XnView MP style). Effort: M.
  - **Blocked by**: ExifTool S-05 safe write wrapper — write-back to embedded metadata requires S-05.
  - **Unblock when**: S-05 ExifTool safe write wrapper ships.

- [ ] **V40-30** *P1* — **EXIF GPS read/write** (ExifTool shell-out S-05 for writes; MetadataExtractor for reads). Effort: M.
  - **Blocked by**: ExifTool S-05 safe write wrapper — GPS write-back requires S-05.
  - **Unblock when**: S-05 ExifTool safe write wrapper ships.

## Blocked On WebView2 Map + Egress Consent

- [ ] **V20-23** *P1* — **GPS coordinates overlay** with click-to-open-in-map (Honeyview). P-01 "Strip location" one click away. Effort: M.
  - **Blocked by**: WebView2 map integration + network egress consent UX — click-to-open-in-map requires a map surface and explicit egress disclosure.
  - **Unblock when**: WebView2 Leaflet map pane and egress consent flow are implemented.

- [ ] **V40-31** *P1* — **Interactive map pane** with clustering at zoom (Leaflet via WebView2, OpenStreetMap tiles). Egress logged by P-03. Effort: M.
  - **Blocked by**: WebView2 Leaflet integration + OSM tile egress — map pane requires WebView2 hosting and network egress for tile loads.
  - **Unblock when**: WebView2 hosting is implemented and OSM tile egress consent is designed.

## Blocked On V40-01 SQLite Catalog

> Data-layer note (catalog schema v3, 2026-07-17): the catalog is built and stable, and now
> indexes rating, tags, palette, file timestamps, **GPS latitude/longitude, capture time, and
> camera/lens/ISO/focal/aperture/shutter EXIF**, with a `FindWithinBounds` geo query. The remaining
> blocker for the geo/time-dependent items below is therefore the **GUI surface** (and, where noted,
> a still-missing model): near-duplicate stacking additionally needs a perceptual-hash proximity
> model (the indexed fingerprint is an exact-match SHA-256, not perceptual); smart collections'
> face-count criterion still needs V60 face detection; auto-albums still need content classification.

- [ ] **V40-13** *P1* — **Category Sets** — saveable tag-panel layouts, swap per job type (XnView). Effort: M.
  - **Blocked by**: V40-01 SQLite catalog — category sets need catalog-backed tag persistence.
  - **Unblock when**: V40-01 catalog schema is stable and tag storage is operational.

- [ ] **V40-22** *P1* — **Near-duplicate stacking** — auto-stack by time+location+hash proximity (PhotoPrism, Google Photos Photo Stacks). Effort: M.
  - **Blocked by**: V40-01 catalog + time/location proximity model — stacking needs indexed metadata for time+location queries.
  - **Unblock when**: V40-01 catalog ships with indexed timestamps and GPS coordinates.

- [ ] **V40-23** *P2* — **Sketch-based fuzzy search** — draw rough color blobs, match (digiKam Sketch tab — delightful differentiator). Effort: L.
  - **Blocked by**: V40-01 catalog + palette extraction engine — sketch search needs indexed color data to query against.
  - **Unblock when**: V40-01 catalog and color palette indexing are operational.

- [ ] **V40-40** *P1* — **Smart Collections** criteria builder — date, rating, label, keyword, camera, lens, ISO, geo bbox, face count (Lightroom). Effort: L.
  - **Blocked by**: V40-01 SQLite catalog — smart collections query against catalog-indexed metadata.
  - **Unblock when**: V40-01 catalog ships with indexed metadata fields.

- [ ] **V40-41** *P2* — **Auto-albums by pattern** — screenshots, receipts, notes, documents (Google Photos OCR+shape). Effort: L.
  - **Blocked by**: V40-01 catalog + OCR/shape classification — auto-album detection needs indexed content analysis.
  - **Unblock when**: V40-01 catalog and content classification pipeline are operational.

- [ ] **V40-42** *P2* — **Trip detection** — contiguous days + distance >threshold from home (Apple Memories engine). Effort: L.
  - **Blocked by**: V40-01 catalog + GPS/date indexing — trip detection needs indexed geo+temporal data.
  - **Unblock when**: V40-01 catalog ships with geo and date indexing.

- [ ] **V40-43** *P2* — **Events view** — date-based clusters with key-photo thumbnail (Shotwell). Effort: M.
  - **Blocked by**: V40-01 catalog — event clustering queries against catalog-indexed dates.
  - **Unblock when**: V40-01 catalog ships with date indexing.

## Blocked On GeoNames Offline Database

- [ ] **V40-32** *P2* — **Reverse geocoding** — local offline DB (GeoNames CC-BY) — privacy-first, no API calls. Effort: M.
  - **Blocked by**: GeoNames offline database bundling — needs download/staging strategy and CC-BY attribution.
  - **Unblock when**: GeoNames DB packaging and attribution are designed.

## Blocked On V40-30 GPS Write

- [ ] **V40-33** *P2* — **GPX track-log sync** — match photo timestamps to GPS track, backfill EXIF. Effort: M.
  - **Blocked by**: V40-30 GPS read/write — GPX sync writes GPS EXIF, which depends on V40-30.
  - **Unblock when**: V40-30 GPS write capability ships.

## Blocked On ONNX Model Downloads And Validation

- [ ] P1 — **Upgrade semantic search from CLIP ViT-B/32 to SigLIP 2**
  Why: SigLIP 2 base (~350 MB, 768-dim) outperforms CLIP ViT-B/32 on zero-shot image-text retrieval. The embedding provider seam already exists.
  - **Blocked by**: requires downloading SigLIP 2 ONNX models from HuggingFace, computing SHA-256 hashes, and pinning them in Model Manager.
  - **Unblock when**: SigLIP 2 base ONNX models are downloaded and SHA-256 values are validated.

- [ ] P2 — **Local face-region review workflow**
  Why: Images has `person:` tag namespaces and planned Picasa face-region migration, but modern photo managers treat face grouping as core organization; this should land as an explicit local review lane, not an automatic write.
  - **Blocked by**: requires downloading SCRFD ONNX face detection models, validating SHA-256 hashes, and pinning them in Model Manager.
  - **Unblock when**: SCRFD face detection ONNX models are downloaded and validated for local inference, and inference runtime is operational.

- [ ] P1 — **AI-assisted culling quality signals**
  Why: Adobe LrC and Capture One ship AI-assisted review that flags closed eyes and out-of-focus shots. SCRFD face detection models (2-15 MB ONNX) make this feasible locally.
  - **Blocked by**: requires downloading SCRFD ONNX models, validating SHA-256 hashes, pinning in Model Manager, and designing the review-mode quality signal UX.
  - **Unblock when**: SCRFD face detection ONNX models are downloaded and validated for local inference.

## Blocked On External Binary Downloads

- [ ] P1 — **Update bundled Ghostscript 10.07.0 → 10.07.1**
  Why: 10.07.1 removed the `.tempfile` PostScript operator and restricted temp directory permissions, reducing attack surface for crafted PS/PDF files.
  - **Blocked by**: requires downloading Ghostscript 10.07.1 binaries from ghostscript.com and re-running `Prepare-GhostscriptBundle.ps1` with updated SHA-256 hashes.
  - **Unblock when**: the Ghostscript 10.07.1 archive is downloaded and staged locally.

## Blocked On Bundled CLI Tools Not Yet Staged

- [ ] **V50-02** *P0* — **Output formats** with per-format quality controls: JPEG (MozJPEG + cjpegli), PNG (OxiPNG), WebP (cwebp), AVIF (avifenc), JXL (cjxl), HEIC (libheif), TIFF, BMP, GIF. Effort: L. [stack: bundled CLIs + Magick.NET core]
  - **Blocked by**: bundled CLI tools not yet staged — MozJPEG, cjpegli, OxiPNG, cwebp, avifenc, cjxl binaries need provenance/license/staging work.
  - **Unblock when**: CLI binaries are staged with SHA-256 validation, license files, and provenance tracking (similar to jpegtran V7-06 pattern).

- [ ] **V50-21** *P1* — **Lossless re-pack chain** per format (bundled CLIs): PNG (OxiPNG/ECT/pngquant), JPEG (jpegtran/jpegoptim/cjpegli/MozJPEG), GIF (gifsicle), WebP/AVIF/JXL max-effort. Effort: L.
  - **Blocked by**: bundled CLI tools not yet staged — the full re-pack chain requires multiple CLI binaries with provenance.
  - **Unblock when**: required CLI binaries are staged per the bundled-binary provenance policy.

- [ ] **V50-22** *P1* — **"Best-of" mode** — run N encoders in parallel, pick smallest under target SSIMULACRA2 score (FileOptimizer philosophy). Effort: L.
  - **Blocked by**: bundled CLI tools (V50-21 re-pack chain) + SSIMULACRA2 metric library (V50-23) — needs multiple encoders and quality metrics.
  - **Unblock when**: V50-21 CLI chain and V50-23 metric library are available.

## Blocked On Quality Metric Library

- [ ] **V50-23** *P2* — **SSIMULACRA2 + Butteraugli** quality metric alongside raw slider (2026 codec-comp community standard). Effort: M.
  - **Blocked by**: SSIMULACRA2/Butteraugli .NET binding or CLI — no production-ready .NET library exists; needs evaluation of native interop or bundled metric CLI.
  - **Unblock when**: a .NET-compatible SSIMULACRA2 implementation or bundled metric CLI is identified and staged.

## Blocked On Layout Engine

- [ ] **V50-30** *P1* — **Contact sheets to PDF**: grid, header/footer, metadata captions (Bridge Output). Effort: M. [stack: PdfSharpCore MIT]
  - **Blocked by**: layout engine — multi-image page composition with grid/header/footer/captions needs a layout subsystem.
  - **Unblock when**: a PDF/page layout engine (PdfSharpCore or equivalent) is integrated.

- [ ] **V50-31** *P1* — **Print layout** — multi-image/page with margins + alignment (Lightroom Print module). Effort: L.
  - **Blocked by**: layout engine — multi-image print composition shares the same layout subsystem as contact sheets.
  - **Unblock when**: layout engine from V50-30 is available.

- [ ] **V50-32** *P2* — **Web gallery** — static HTML + thumbs + lightbox (digiKam HTMLGallery). Effort: M.
  - **Blocked by**: layout/template engine — web gallery generation needs an HTML templating and thumbnail composition pipeline.
  - **Unblock when**: gallery template engine is designed and thumbnail batch pipeline is mature.

## Blocked On OAuth / External API Credentials

- [ ] **V50-33** *P2* — **Direct publish** — Flickr/Imgur/Pinterest/Dropbox/OneDrive/SMB/FTP (OAuth + known APIs). Every egress call is logged by P-03. Effort: L.
  - **Blocked by**: OAuth integration + external API credentials — each publish target needs OAuth flow, API keys, and consent UX.
  - **Unblock when**: at least one OAuth publish target is designed with token storage and egress consent.

## Blocked On Batch Pipeline Maturity

- [ ] **V50-07** *P1* — **Watch-folder** auto-apply (XnConvert Watch). Effort: M.
  - **Blocked by**: batch pipeline maturity — watch-folder needs a proven, resilient batch execution engine before auto-applying on file changes.
  - **Unblock when**: V50-01 operation-chain builder and batch execution are stable and tested.

## Blocked On V30-33 Slideshow

- [ ] **V30-34** *P2* — **Standalone .exe slideshow export** (IrfanView — unique) — packs N images + runtime into a self-extracting viewer. Effort: L.
  - **Blocked by**: V30-33 slideshow — export depends on the slideshow playback engine being built first.
  - **Unblock when**: V30-33 slideshow feature ships.

## Blocked On Window/Session Channel Design

- [ ] **V30-32** *P2* — **Multi-instance LAN sync** lite — optional "Compare mode" syncs pan/zoom across two open windows (local machine; network sync is a v1.0 item). Effort: L. [nomacs 3.22 pattern]
  - **Blocked by**: window/session communication channel — syncing state between instances requires a local IPC or shared-memory design.
  - **Unblock when**: inter-instance communication channel is designed and implemented.

## Blocked On TWAIN SDK + MSIX Compatibility

- [ ] **V30-24** *P2* — Scan via TWAIN/WIA to image (IrfanView pattern — `Saraff.Twain.NET` NuGet). Breaks under MSIX AppContainer (S-07) — unpackaged build only. Effort: M.
  - **Blocked by**: TWAIN SDK evaluation + MSIX AppContainer compatibility — `Saraff.Twain.NET` needs evaluation, and TWAIN breaks under MSIX sandbox.
  - **Unblock when**: TWAIN SDK evaluated and MSIX compatibility path decided.

## Blocked On Copilot+ NPU Hardware

- [ ] **V60-09** *P2* — **Restyle Image** (Copilot+ PCs only). Windows App SDK `ImageGenerator` API. Requires NPU. Effort: M.
  - **Blocked by**: no Copilot+ NPU is available in the safe background test environment, and the Windows App SDK `ImageGenerator` path requires device evidence.
  - **Unblock when**: Copilot+ PC testing is available without touching the user's active desktop.

## Blocked On X-01 Plugin Boundary Design Doc

- [ ] **V70-01** *P0* — **Roslyn C# scripting plugin API**. `Microsoft.CodeAnalysis.CSharp.Scripting` + Westwind.Scripting wrapper. User writes snippets against `IImageContext` host API. Sandbox: whitelist namespaces, restrict reflection. Effort: L.
  - **Blocked by**: X-01 plugin boundary design doc — plugin API needs stable extension points and trust model defined first.
  - **Unblock when**: X-01 design doc ships with extension-point definitions and trust model.

- [ ] **V70-02** *P1* — **G'MIC shell-out** — bundle `gmic.exe` (CeCILL/LGPL, license-isolated). 640+ filters. Effort: L.
  - **Blocked by**: X-01 plugin boundary design doc — G'MIC integration needs the plugin/filter host boundary defined.
  - **Unblock when**: X-01 design doc ships.

- [ ] **V70-03** *P2* — **Adobe 8BF filter host** — PICA suites + FilterRecord struct. Unlocks Nik Collection, Topaz legacy. Effort: XL.
  - **Blocked by**: X-01 plugin boundary design doc — 8BF hosting needs the plugin trust model and process isolation boundary.
  - **Unblock when**: X-01 design doc ships.

- [ ] **V70-04** *P2* — **Explorer shell extension** — PSD/RAW/JXL/AVIF thumbnails in Explorer (Pictus pattern). Separate DLL, IThumbnailProvider. Breaks under MSIX. Effort: M.
  - **Blocked by**: X-01 plugin boundary design doc + MSIX compatibility (S-07) — shell extension is a plugin-class component that breaks under AppContainer.
  - **Unblock when**: X-01 design doc ships and MSIX/unpackaged build strategy is decided.

## Blocked On Multiple Predecessors (Lightroom-Class)

- [ ] **V100-02** *P0* — **RAW development pipeline** beyond LibRaw basic conversion. Demosaic + WB + exposure + shadows/highlights + S-curve + clarity + lens correction (lensfun) + noise reduction (BM3D via G'MIC). Effort: XL.
  - **Blocked by**: V20-14 RAW decode + V70-02 G'MIC integration + V100-05 color management — full RAW development needs the decode pipeline, filter bus, and color-managed rendering.
  - **Unblock when**: V20-14 RAW decode, V70-02 G'MIC, and V100-05 color management are operational.

- [ ] **V100-03** *P1* — **Panorama stitching** via bundled Hugin CLI chain. All GPL, all shell-out, all license-isolated. Effort: L.
  - **Blocked by**: bundled Hugin CLI tools (`align_image_stack`, `autooptimiser`, `hugin_executor`, `enblend`) not yet staged — needs binary provenance and license-isolation staging.
  - **Unblock when**: Hugin CLI binaries are staged with provenance tracking.

- [ ] **V100-04** *P1* — **HDR merge** via bundled `enfuse` (Mertens-Kautz-Van Reeth). RAW bracket set needs LibRaw. Effort: L.
  - **Blocked by**: V20-14 RAW decode + bundled `enfuse` CLI not yet staged — HDR merge from RAW brackets needs both.
  - **Unblock when**: V20-14 RAW decode ships and enfuse binary is staged.

- [ ] **V100-05** *P1* — **Color management** — lcms2 (MIT) P/Invoke or Magick.NET profile conversion. Wide-gamut display support. Effort: L.
  - **Blocked by**: V80-26 OCIO/ACES color-management roadmap — color management needs the planning document produced first.
  - **Unblock when**: V80-26 color pipeline plan document is produced.

- [ ] **V100-06** *P2* — **HDR display** (PQ/HLG) via SkiaSharp native HDR path or Direct2D interop swap chain. Effort: XL.
  - **Blocked by**: V20-01 SkiaSharp canvas + V100-05 color management — HDR display needs the new canvas engine and color pipeline.
  - **Unblock when**: V20-01 SkiaSharp canvas and V100-05 color management ship.

- [ ] **V100-07** *P2* — **Multi-instance LAN sync** (nomacs moat, full version) — pan/zoom/image-send mirror between instances on same network. Builds on V30-32 local lite. Effort: L.
  - **Blocked by**: V30-32 local multi-instance sync — the full LAN version needs the local sync foundation first.
  - **Unblock when**: V30-32 local multi-instance sync ships.

## Blocked On V20-01 SkiaSharp Canvas And Renderer Maturity

- [ ] P1 — **Color-management truth mode and HDR/wide-gamut guardrails**
  Why: Images advertises broad HDR/EXR/JXL/AVIF support and reports ICC data, but the current WPF display path does not soft-proof or apply managed display transforms.
  - **Blocked by**: V20-01 SkiaSharp canvas — full managed sRGB display transform and HDR rendering need the new canvas engine. Guardrail status badges are possible in the current pipeline but the full acceptance (profiled previews, display truth status per format) needs renderer changes.
  - **Unblock when**: V20-01 SkiaSharp canvas ships, providing a display pipeline that can apply ICC transforms.

- [ ] P1 — **Light Table / Select Set comparison queue**
  Why: The current compare mode handles a pair, but top culling tools use temporary sets for 3-8 near-duplicates.
  - **Blocked by**: requires major new multi-image layout surface (2-up and 4-up linked pan/zoom/rotate) that is better built on V20-01 SkiaSharp canvas for consistent rendering across tiles.
  - **Unblock when**: V20-01 SkiaSharp canvas ships or the WPF bitmap-based multi-tile layout is validated for linked transforms.

