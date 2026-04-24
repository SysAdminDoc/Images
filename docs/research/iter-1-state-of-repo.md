# Phase 0 — State of Repo (Factory iter-1, 2026-04-24)

## Shipped state
- **Version**: v0.1.4 (Inno Setup installer + portable zip side-by-side)
- **Last commit**: `3abbb50` — release: v0.1.4 — Inno Setup installer + portable zip side-by-side
- **Tag status**: v0.1.2 published, v0.1.3 + v0.1.4 tags not yet pushed (manifest at 0.1.4 but no tag / release workflow run)
- **Working tree**: clean
- **Branch**: `main`, synced with `origin/main`

## Runtime / stack
- .NET 9 WPF (net9.0-windows), framework-dependent publish
- Magick.NET-Q16-AnyCPU 14.13.0 (animated decode via `MagickImageCollection.Coalesce()`)
- Microsoft.VisualBasic 10.3.0 (recycle-bin delete)
- No test project
- Inno Setup 6 installer at `installer/Images.iss`

## Scale gate readings
| Metric | Reading | Threshold | Trip? |
|---|---|---|---|
| Source LOC (`find src -name '*.cs'`) | 1,847 | 50,000 | no |
| Tracked files (`git ls-files`) | 34 | 500 | no |
| Tests | 0 | 1,000 | no |
| Open ROADMAP P0/P1 items | 185 | 30 | **YES** |

Large-Repo Mode auto-engages by ROADMAP item count. Recipe self-modifies per `recipe-factory-loop.md` §Large-Repo-Mode: 1 iteration, 3-5 tasks (raising slightly from the 3 cap given the user's "extensive" default), per-task atomic commits, rotated U/T/D phases, Q3 only if version warrants.

## Phase-rotation state (from `.factory/large-repo-state.yaml`)
| Phase | Last run | Days since | Run this iteration? |
|---|---|---|---|
| UX polish (U*) | 2026-04-24 (three premium-polish waves across v0.1.2) | 0 | no — rotated (recent) |
| Theming (T*) | 2026-04-24 (DarkTheme.xaml rewrite v0.1.2) | 0 | no — rotated (recent) |
| Dep scan (D*) | 2026-04-24 (v0.1.2 — Magick.NET 14.12 → 14.13) | 0 | no — rotated (recent) |
| Modularization (M*) | never | — | skipped (< 5K LOC) |
| Release (Q3) | 2026-04-24 22:25Z (v0.1.2); v0.1.3 + v0.1.4 manifest-bumped but un-tagged | 0 | **PENDING — tag v0.1.4 + run release.yml** |

## Charter (from repo `CLAUDE.md` + `ROADMAP.md`)
Per ROADMAP §Vision: "One Windows app that replaces Photos, IrfanView, XnConvert, Upscayl, and a light Lightroom — by cannibalising the best ideas from a dozen OSS/freeware projects. Local-first, fast, dark-mode, no cloud, no subscription. Killer features are CLIP semantic search on a local library, live inline rename, Squoosh-style visual-diff converter, and the differentiator nobody else ships — network-egress transparency."

Scope guards: Windows-first, local-only, zero telemetry by default, zero cloud, opinionated dark-mode, **keyboard-navigation required**, framework-dependent (no 70 MB self-contained).

## Open work bias (just from reading the top of the ROADMAP)
- **v0.1.2 section** has 2 blockers still open: V02-04 (DPI-aware screenshot recapture), V02-05 (human runtime smoke). Both require a Windows GUI session a headless agent cannot drive.
- **v0.2.0 section** is the next logical ship target: SkiaSharp canvas, persistent settings (SQLite at `%LOCALAPPDATA%\Images\settings.db`), preload next/prev, thumbnail cache, filmstrip, EXIF overlay, HEIC/AVIF via WIC.
- **Cross-cutting tracks**: S-01/S-04/S-05 (security), P-01/P-04 (privacy + update check), A-01/A-02 (accessibility), I-01/I-04 (i18n), O-01/O-04 (observability — Serilog + minidump), T-04 (testing — schema snapshot), D-01/D-02 (distribution — partially shipped v0.1.4 installer).

## What has changed since ROADMAP v2 was seeded (2026-04-24 earlier)
- v0.1.3: animated GIF playback (V20-15 core), memory-mapped I/O for >256 MB files (V20-06), icon-font tofu fix.
- v0.1.4: Inno Setup installer + portable zip dual-ship pipeline (D-01b).

These close concrete ROADMAP items; the ROADMAP reconcile in Phase 4 must mark them and check whether any remaining items in v0.1.2 / v0.2.0 got implicitly addressed.

## Preliminary Phase 1 scope (external scan)
- April 2026 intel: Windows ML GA, WIC CVE-2025-50165, ImageGlass 10 Beta, JPEG XL Chrome-145-flag, cjpegli, C2PA v2.3, Oculante, Copilot+ NPU auto-EP — already referenced in ROADMAP v2 preamble. Delta scan asks: what happened in April-May 2026 that ROADMAP v2 doesn't know about?
- 9 source classes: OSS competitors (ImageGlass 10, nomacs, qView, JPEGView, FastStone, Pictus, Oculante, Lyn, XnView MP), commercial (Windows Photos, Lightroom Classic), adjacent (darktable/digiKam for DAM, Squoosh for converter UX, Upscayl for AI, chaiNNer for plugin host), awesome-lists (`awesome-windows`, `awesome-image-viewer`, `awesome-dotnet-tools`), community signal (r/ImageViewer, HN, Lobsters, Windows Central), standards (C2PA v2.3, EXIF 3.0, JPEG XL, AVIF 1.2), academic (Windows ML inference papers, SkiaSharp benchmark threads), dep changelogs (Magick.NET 14.14?, SharpCompress, SkiaSharp 3, WpfAnimatedGif), CVEs (NVD, GHSA — libheif, libavif, libwebp, libjxl, ImageMagick).
