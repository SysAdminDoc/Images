# Research - Images

## Executive Summary
Images is a Windows-only, local-first WPF image viewer/editor that has moved past a simple viewer baseline into review labels, inline rename, archive/document viewing, batch/export workflows, catalog search, semantic search, local model management, diagnostics, and explicit network-egress transparency. The strongest direction is not another cloud DAM or AI subscription tool; it is a trustworthy local power-user viewer that borrows the best reliability, metadata, culling, batch, and diagnostics patterns from ImageGlass, FastStone, XnView, digiKam, PhotoDemon, Mylio, and Squoosh while preserving offline ownership. Top opportunities: fix stale release-readiness gates, keep the blocked Ghostscript 10.07.1 runtime refresh visible until binaries are staged, add SBOM/provenance attestations, make WPF smoke/accessibility coverage enforceable, complete XMP metadata write-through, surface local ML runtime validation instead of silent fallback, add unified local data controls, harden catalog rescans, and remove theme-token drift.

## Product Map
- Core workflows: open/navigate image folders and archives; inline rename and review-label culling; inspect metadata/OCR/codecs/network activity; apply non-destructive edits; batch convert/export with preview; import, dedupe, catalog, and semantic-search local collections.
- User personas: Windows power user replacing Photos/IrfanView/FastStone; privacy-sensitive organizer; creator who needs repeatable batch/export workflows; archive/comic reader; local-AI early adopter who wants explicit model provenance.
- Platforms and distribution: Windows 10/11 desktop, WPF on `net10.0-windows10.0.22621.0`, GitHub release ZIP and Inno installer, generated WinGet/Scoop manifests, future signing/Store tracks blocked in `Roadmap_Blocked.md`.
- Key integrations and data flows: WIC/Magick.NET/SharpCompress/Ghostscript/jpegtran decode and writeback; SQLite settings/catalog/semantic indexes under app data; XMP sidecars as portable review metadata; Windows OCR; ONNX Runtime DirectML/CPU with future Windows ML; GitHub Releases update check only when opted in.

## Competitive Landscape
- ImageGlass: strong broad-format and HDR/vector momentum plus modern release cadence. Learn from its format transparency and command surface; avoid ambiguity around paid Store distribution, unsigned beta trust, and runtime dependency expectations.
- FastStone, IrfanView, and XnView MP: mature table-stakes for batch convert/rename, metadata, contact sheets, and simple support workflows. Learn their metadata and batch breadth; avoid silent destructive metadata writes or hidden advanced options.
- digiKam and darktable: strongest fit for sidecar-first, non-destructive metadata workflows. Learn granular XMP read/write and catalog rebuilding; avoid making the SQLite catalog more authoritative than files and sidecars.
- PhotoDemon: a good model for approachable advanced editing, macros, presets, and batch workflows. Learn real-time previews and understandable macro/batch UX; avoid turning Images into a full Photoshop-class editor.
- NeeView, PicView, qView, and JPEGView: focused viewer UX with book/archive modes, customization, minimal chrome, and fast local browsing. Learn viewer-speed expectations; avoid overloading the first screen with organizer/editor controls.
- Mylio, Eagle, ACDSee, and Lightroom: commercial evidence that local AI search, culling, face/location organization, and guided import are premium/paywalled value. Learn trust cues and guided workflows; avoid cloud lock-in, subscription identity, and unverified AI scoring.
- Immich, PhotoPrism, Hydrus, and Czkawka: adjacent evidence for local tags, CLIP search, dedupe, and large-library jobs. Learn transparent job state and incremental scans; avoid confusing tag models and silent machine-learning failures.

## Security, Privacy, and Reliability
- Verified: `scripts/Test-ReleaseReadiness.ps1` still requires `ROADMAP.md` to mention `PROJECT_CONTEXT.md` and reads `PROJECT_CONTEXT.md`, while `AGENTS.md` now marks `PROJECT_CONTEXT.md` as forbidden sprawl and the live `ROADMAP.md` contains only actionable items. This can block releases despite a valid current roadmap.
- Verified: docs and changelog state official releases bundle Ghostscript 10.07.0, while Artifex lists Ghostscript 10.07.1 as a maintenance release that addresses potential security issues. The runtime provenance process is good; the approved version floor is now stale, and the implementation row belongs in `Roadmap_Blocked.md` until a 10.07.1 runtime is staged.
- Verified: release workflow emits checksums and a transitive dependency tree, but does not generate a first-class SBOM or GitHub artifact attestations for the ZIP, setup EXE, checksums, or package manifests.
- Verified: CI runs WPF smoke tests with `continue-on-error: true`; rendered UI regressions and accessibility regressions can pass main CI.
- Verified: `ClipEmbeddingProvider.TryCreate` catches all setup/runtime failures and silently falls back to deterministic metadata embeddings through `SemanticSearchService`; users cannot distinguish missing files, shape mismatch, provider failure, or CPU fallback.
- Verified: privacy documentation lists settings, logs, crash dumps, thumbnails, catalog, semantic index, model storage, recovery records, wallpaper copies, and email drafts, but Settings only opens app data/logs and About clears thumbnails/network history. There is no unified local data management surface.

## Architecture Assessment
- `src/Images/Services/XmpSidecarImportService.cs` parses ratings, color labels, keywords, hierarchical keywords, and IPTC/Photoshop location fields, but `ApplyFolderRatings` only writes ratings. This leaves sidecar interoperability visibly incomplete.
- `src/Images/Services/CatalogService.cs` performs full recursive rebuilds, hashes every candidate, clears the catalog, then inserts current rows. For large libraries this risks slow first-run/rescan behavior seen in competing viewers.
- `src/Images/Services/SemanticSearchService.cs` rebuilds from catalog output and clears its index in one transaction; it needs staging/reuse semantics before model-backed search scales.
- `src/Images/Services/ModelManagerService.cs` verifies file SHA-256 and size, but runtime compatibility lives in `ClipEmbeddingProvider`; model health should be a visible validation step with exact provider/failure copy.
- `tests/Images.Tests/WpfSmokeTests.cs` covers launch, fixture open, next/previous, and Escape only. It does not assert documented UIA names/help text from `docs/accessibility.md` or smoke Settings/About/Command Palette.
- Theme dictionaries are strong, but raw alpha colors remain in surface XAML (`MainWindow.xaml`, `SettingsWindow.xaml`, `AboutWindow.xaml`, secondary tools). High-contrast behavior should not depend on Catppuccin-only literals.
- Docs are stale in places: `docs/release-support-policy.md` still says `net9.0-windows` and `0.1.x`, and `docs/release-checklist.md` still references `PROJECT_CONTEXT.md`.

## Rejected Ideas
- Cloud sync, accounts, and multi-user sharing: rejected because the repo philosophy and privacy policy are explicitly local-first; Mylio/Immich are useful as indexing/UX references only.
- Public galleries/web sharing: rejected because Piwigo/Immich solve a different server-hosted problem and would add account/network scope.
- Face recognition as immediate work: rejected until local model validation, privacy controls, and sidecar migration are stronger.
- Full MSIX/Store migration: rejected for this roadmap because signing, Store account, and package-model choices are already blocked externally.
- Full plugin host or native Bio-Formats/OpenSlide/libvips/OIIO/OCIO adoption: rejected for active roadmap because those runtime decisions remain in `Roadmap_Blocked.md`; only sandbox/provenance guardrails should precede them.
- Cloud/generative AI culling or photo-to-video features: rejected because they contradict zero-cloud, no-telemetry expectations and would create subscription/model-credit semantics.
- C2PA signing: rejected for active roadmap because signing credentials and publisher identity remain blocked; read-only inspection should continue.

## Sources
Competitors:
- https://github.com/d2phap/ImageGlass/releases
- https://github.com/nomacs/nomacs
- https://github.com/Ruben2776/PicView
- https://neeview.org/
- https://github.com/jurplel/qView
- https://github.com/tannerhelland/photodemon
- https://github.com/sylikc/jpegview
- https://github.com/qarmin/czkawka
- https://docs.digikam.org/en/setup_application/metadata_settings.html
- https://www.darktable.org/about/

Commercial:
- https://www.faststone.org/FSViewerDetail.htm
- https://www.irfanview.com/faq.htm
- https://www.xnview.com/en/faq/
- https://www.acdsee.com/en/products/photo-studio-home/features/
- https://helpx.adobe.com/lightroom-classic/help/assisted-culling.html
- https://en.eagle.cool/
- https://support.mylio.com/what-is-mylio-photos

Adjacent, platform, and security:
- https://github.com/ibaaj/awesome-OpenSourcePhotography
- https://github.com/meichthys/foss_photo_libraries
- https://www.libvips.org/
- https://openimageio.readthedocs.io/
- https://opencolorio.org/
- https://openslide.org/
- https://www.openmicroscopy.org/bio-formats/
- https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://spec.c2pa.org/specifications/specifications/2.4/specs/C2PA_Specification.html
- https://docs.github.com/en/actions/concepts/security/artifact-attestations
- https://github.com/dlemstra/Magick.NET/releases
- https://ghostscript.readthedocs.io/en/latest/News.html

## Open Questions
- Is the publisher willing to treat Ghostscript 10.07.1 as the new minimum approved bundled runtime for the next release, or should releases temporarily require an externally supplied runtime ZIP?
- Should a local data deletion control preserve settings by default, or should it offer a separate "factory reset" path that removes settings, hotkeys, and recent folders too?
