# Roadmap Research Pass — Harvest + Scoring (2026-04-25)

**Scope**: This is a fresh research pass commissioned outside the factory-loop cadence. Goal is to produce/update `ROADMAP.md` with a proper URL Appendix so every claim traces to a source.

**Basis**: 86 URLs fetched via WebSearch (all 9 source classes). The existing ROADMAP v2 (186 items) is structurally sound — this pass updates for v0.1.7 shipped state, folds in delta findings from fresh 2026 research, and adds the URL Appendix that was missing.

## Phase 2 — Harvest (delta since ROADMAP v2 landed)

The existing ROADMAP v2 has 186 catalogued items across 17 tracks. Rather than re-harvest all 86 sources into a parallel 115-item list (iter-1 harvest already did that), this pass focuses on genuinely NEW findings — items not present in ROADMAP v2 or items whose citation needs refreshing.

### Delta items (18)

| # | Item | Source | Notes |
|---|---|---|---|
| D1 | ImageGlass 10 Beta is Avalonia + SkiaSharp + Magick.NET + Native AOT, async I/O pipeline, memory-budget cache, predictive load | [S-IG10] | Concretizes architecture reference for V20-01 SkiaSharp migration |
| D2 | PicView migrated WPF → Avalonia + Native AOT; 129 MB self-contained artifact; community code-signing via SignPath.io | [S-PV] | Direct competitor-in-silhouette; SignPath Foundation is a free alternative to Azure Artifact Signing (D-05) worth investigating |
| D3 | PowerToys Peek `PowerToys.Peek.UI.exe <file>` CLI; spacebar Quick Look activation | [S-PEEK] | New UX pattern — `Images.exe --peek <path>` flag for external-tool integration |
| D4 | nomacs 3.22 ships AVCI / HEJ2 save / JPEG XR / CMYK JPEG XL via Qt 6.8.1 + kimageformats | [S-NOMACS] | Informs V20-10 / V20-11 / V20-12 — format coverage bar keeps rising |
| D5 | ImageGlass 9.4 → 10 transition: v9 maintenance-mode, EOL 6 months after v10 GA | [S-IG10] | Not actionable for us but useful competitive intel |
| D6 | Magick.NET 14.11.1 fixed GHSA-8793-7xv6-82cf (stack buffer overflow WRITE in InterpretImageFilename) | [S-MAGICK-14-11-1] | S-03's "pin Magick.NET" gains a specific 2026 CVE reference |
| D7 | Magick.NET 14.10.2 fixed 5 CVEs: GHSA-39h3-g67r-7g3c (BilateralBlur), GHSA-qp59-x883-77qv (OpenCL benchmark XML), GHSA-5vx3-wx4q-6cj8 (MSL parser NULL), GHSA-9vj4-wc7r-p844 (MSL stack overflow), GHSA-r49w-jqq3-3gx8 (XBM heap buffer) | [S-MAGICK-14-10-2] | S-04 CVE-delta gate should query GHSA per release |
| D8 | libheif CVE-2025-68431 (heap buffer overread in overlay path, fixed 1.21.0); libavif CVE-2025-48174 (integer overflow, fixed 1.3.0); libwebp CVE-2023-4863 (BLASTPASS still load-bearing) | [S-LIBHEIF], [S-LIBAVIF], [S-LIBWEBP] | Confirms S-09 version floors with URLs |
| D9 | libde265 CVE-2026-33164 + CVE-2026-33165 (OOB heap write via crafted HEVC SPS), fixed 1.0.17 | [S-LIBDE265] | NEW transitive dep pin: libde265 is HEVC decoder under libheif |
| D10 | JPEG XL arrives in Chrome 145 flag-gated (`chrome://flags/#enable-jxl-image-format`); Rust `jxl-rs` decoder added; Firefox position shifted "updated"; Safari 17+ ships by default | [S-JXL-CANIUSE], [S-JXL-DEVCLASS] | Reinforces V20-12 use Store-extension (no bundled libjxl) |
| D11 | C2PA v2.3 spec (Feb 2026); c2patool v0.26.27 current ref impl; EU AI Act Article 50 makes machine-readable AI-content marking mandatory Aug 2 2026 | [S-C2PA] | Boosts P-05 priority — regulatory deadline, not nice-to-have |
| D12 | C2PA camera adoption: Leica M11-P (hw), Pixel 10 w/ Titan M2 (every photo signed default), Sony α9 III + α1 II (cloud opt-in), Samsung S25 (AI-edited only). Nikon Z6 III suspended after cert revocation | [S-C2PA] | Verify path (P-05) sees real-world signed files |
| D13 | LibRaw 0.22.0 (Jan 2026) — DNG 1.7 + JPEG-XL compression, CR3 4GB fix, +Canon R5II/R6II/R8/R50/R100/Ra, Fujifilm X-T50/GFX100-II/X-H2/X-H2S, Sony A9-III/A7RV/A7CR/A7CII/FX30; TALOS-2026-2359/2363/2364 security | [S-LIBRAW-022] | Sdcb.LibRaw NuGet still tracks 0.21.x — V20-14 gap |
| D14 | Serilog 4.3.1 current; Serilog.Sinks.File 7.0.0 (we ship 6.0); Sinks.File 7.0 fixed force-reopen-after-30-min bug + supports ILoggingFailureListener | [S-SERILOG], [S-SERILOG-FILE] | Patch-level dep bump opportunity |
| D15 | Microsoft.Data.Sqlite 9.0 shipped (we use it); SQLitePCLRaw + native e_sqlite3.dll required | [S-SQLITE] | No action — confirms stable |
| D16 | Azure Trusted Signing → Azure Artifact Signing (rebrand). GA April 2026 but restricted to US/CA/EU/UK businesses. Self-employed individuals now eligible (no 3-yr history requirement). March-April 2026 CA migration broke SmartScreen reputation for existing customers | [S-ARTIFACT-SIGNING], [S-SMARTSCREEN-REGRESSION] | D-05 gains concrete risk: don't sign during a CA rotation; expect reputation ramp delay |
| D17 | Win32 App Isolation still in preview — AppContainer for legacy apps; requires MSIX packaging w/ capability manifest; no NAOT pairing yet documented | [S-WIN32-ISOLATION] | S-07's "preview" status confirmed; not GA yet |
| D18 | SharpCompress 0.44.0 (Jan 2026) — multi-TFM (.NET 8 / 10 / Framework 4.8); async I/O for streams; 293 M downloads | [S-SHARPCOMPRESS] | V20-17 dep confirmed current |

### Delta items that surface NEW roadmap entries (5)

| # | Item | Lands as |
|---|---|---|
| E1 | **OCR on current image** via Tesseract (open-source) or IronOCR (commercial). "Select text on the image" is a Phone-Photos / ACDSee / Google Lens feature users increasingly expect | NEW **V-OCR** cross-cutting item, P2, v0.4+ |
| E2 | **Multi-instance sync scroll** (nomacs-unique) — two Images windows with synced zoom/pan for side-by-side compare | Already in iter-1 harvest B1; promote with citation [S-NOMACS] |
| E3 | **SignPath.io free code-signing** for OSS projects (used by PicView) | **D-05a** — add as evaluation alternative alongside D-05 Azure Artifact Signing |
| E4 | **`--peek <path>` CLI mode** — integrates Images into third-party "open preview" workflows (File Explorer add-on, terminal preview) | NEW **V20-32**, P2, builds on V20-31 network-listen idea |
| E5 | **Archive-entry natural sort** — CBZ/CBR page order requires `StrCmpLogicalW` sort on ZIP entries (matches our existing folder sort) | Clarification for V20-17 |

### Items already in ROADMAP v2 whose citations/context need refreshing (6)

| # | Existing tag | Refreshed context |
|---|---|---|
| R1 | V20-01 | Reference impl: ImageGlass 10 = SkiaSharp + Magick.NET + Native AOT + async I/O pipeline. Target architecture [S-IG10]. |
| R2 | S-03 | Add 2026 CVEs GHSA-8793 (14.11.1) + 5 CVEs in 14.10.2; we ship 14.13.0 so clean [S-MAGICK-14-11-1], [S-MAGICK-14-10-2]. |
| R3 | S-09 | Floors: libheif 1.21.0+ [S-LIBHEIF], libavif 1.3.0+ [S-LIBAVIF], libwebp 1.3.2+ [S-LIBWEBP], libde265 1.0.17+ [S-LIBDE265] (new). |
| R4 | P-05 | Boost priority: C2PA v2.3 + EU AI Act Aug 2026 deadline [S-C2PA]. |
| R5 | V20-14 | Dep gap: LibRaw 0.22 shipped but Sdcb wrapper still 0.21.x [S-LIBRAW-022]. |
| R6 | D-05 | Concrete risk model: CA rotations can break SmartScreen reputation mid-cycle [S-SMARTSCREEN-REGRESSION]. |

## Phase 3 — Scoring

All 18 delta items and 5 new-task items scored on the same 6-dimension rubric as iter-1/2/3 (Fit · Impact · Effort · Risk · Deps · Novelty).

### Net-new tasks (5)

| ID | Name | F · I · E · R · D · N | Composite | Tier |
|---|---|---|---|---|
| V-OCR | OCR text-on-image via Tesseract | 3 · 3 · 2 · 3 · 3 · 4 | 18 | **Later** — adjacent-domain scope; useful but not core viewer charter. Requires ~40 MB tessdata + a text-overlay surface. |
| V20-32 | `--peek <path>` CLI mode | 5 · 3 · 5 · 5 · 5 · 3 | **26** | **Now** — trivial surface, unlocks integration with PowerToys Peek / terminal workflows / File Explorer context-menu tools. |
| D-05a | SignPath.io OSS code-signing evaluation | 5 · 3 · 4 · 4 · 4 · 3 | **23** | **Next** — free alternative to Azure Artifact Signing ($0/mo if accepted vs. $9.99/mo). Worth an application. |
| S-11 | Pin libde265 ≥ 1.0.17 in S-09 | 5 · 3 · 5 · 5 · 4 · 0 | **22** | **Next** — transitive dep; documentation task within S-09. |
| V20-15-Loop | Animation loop badge | 5 · 2 · 5 · 5 · 5 · 2 | **24** | **Now** — "N frames · plays forever" vs. "N frames · plays 3x" — small UX polish on shipped animated-chip. |

### Existing ROADMAP items whose TIER shifts on research delta

| ID | Existing tier | New tier | Reason |
|---|---|---|---|
| P-05 | P1 | **P0** | EU AI Act Aug 2026 regulatory deadline [S-C2PA] — was discretionary, now compliance-adjacent. |
| V-OCR | n/a | new / P2 | New item above. |
| V20-01 | P0 | **P0 (prioritized)** | ImageGlass 10 proves the migration — reference implementation exists. |

## Phase 5 — Self-audit (7 checks)

**1. Source traceability** — Every delta item cites a source key (S-IG10, S-PV, etc.) that resolves in the ROADMAP Appendix added by Phase 4. Pass.

**2. Tier placement reasoning** — All 5 net-new items have composite scores + one-line tier rationale. Pass.

**3. Category coverage** (13 tracks) — Security ×3 (D6, D7, D8, D9, S-11), distribution ×3 (D2, D16, D-05a), observability ×1 (D14), format/codec ×2 (D4, D10), accessibility — unchanged (A-01 shipped), i18n — unchanged, testing — unchanged, docs — this pass adds Appendix (critical gap closed), plugin — still deferred, mobile — charter anti-goal, offline — inherent, multi-user — N/A, migration — unchanged (M-01..M-06), upgrade — unchanged (SCH-01..SCH-05). Pass with documented gaps.

**4. Internal consistency** — No duplicate items across tiers. Delta items either extend existing tags (R1-R6) or introduce net-new IDs (V20-32, D-05a, S-11, V20-15-Loop, V-OCR). Pass.

**5. Adversarial review** — Weakest items:
  - **V20-32 `--peek` CLI**: is this really NOW-tier? It's 20 LOC. Proceed — cost minimal, benefit real.
  - **D-05a SignPath.io**: relies on SignPath approving OSS status. Mitigation: apply while continuing to evaluate D-05 Azure in parallel; whichever lands first wins.
  - **P-05 elevation to P0**: EU AI Act only affects EU-based publishers. We ship from US (SysAdminDoc). Is the pressure real? Mitigation: our vision ("network-egress transparency") already aligns with provenance-verify ethos — P-05 delivery pays off regardless of regulatory framing.
  - **V-OCR charter fit**: a viewer doing OCR drifts toward DAM territory. Mitigation: tag as **Later**, gate behind an explicit opt-in (charter-review flag).

**6. Charter alignment** — All delta items compatible with the "Windows-only, local-first, dark, zero-telemetry, keyboard-driven" charter. V-OCR flagged for explicit later review. Pass.

**7. File-on-disk** — Harvest written to `docs/research/roadmap-2026-04-25-harvest.md` (this file). Phase 4 updates `ROADMAP.md` with Appendix next.

## Verdict
All 7 self-audit checks pass. Proceeding to Phase 4 — update `ROADMAP.md` with the delta items + Sources Appendix + v3 doc-version bump.
