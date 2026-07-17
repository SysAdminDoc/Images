# Images - Roadmap

Only incomplete, code-ready work is tracked here. Items blocked by credentials, paid-account setup, legal decisions, or required external human action stay in `Roadmap_Blocked.md`.

## v1.0 Milestone

Images ships unsigned on every platform by permanent owner policy; signing is not a release gate and is not backlog. The reachable `1.0` boundary is:

1. GitHub Releases remain the primary unsigned distribution channel (already shipping).
2. Every release generates checksum-pinned WinGet and Scoop manifests through `New-PackageManifests.ps1` and passes the package-manifest hash gate. Account-based submission is optional distribution follow-up, not a version gate.
3. The offscreen, non-activating WPF smoke lane stays green alongside the full unit/integration suite.

Promote to `1.0.0` when a release candidate satisfies those three unsigned checks; do not add certificate, SmartScreen-reputation, Store-account, or package-manager-account requirements to this milestone.

## Actionable Work

- [ ] **V60-10** *P2* — Opt-in auto-rotate suggestions from a reviewed local orientation classifier; never rotate or write a source without confirmation.
- [ ] **V60-11** *P2* — Local NIMA-style aesthetic scoring for trip culling, with visible model provenance and no automatic Pick-label writes.
- [ ] **V60-12** *P2* — Local scene classification feeding smart-album suggestions from reviewed Places365/ANSA-style ONNX weights.
- [ ] **V60-13** *P2* — Optional, default-off local safety classification from reviewed ONNX weights; keep scores private and out of source metadata unless explicitly exported.
- [ ] **V60-14** *P1* — Face-region review workbench over the shipped YuNet/SFace pipeline: inspect detections and clusters visually, accept/reject/name regions, and only then merge reviewed MWG-rs data into XMP sidecars.
- [ ] **V60-15** *P1* — Face-aware culling signals for closed eyes and local blur, presented as review-only hints with no automatic Pick/Reject writes.

The remaining research item is decision-gated (Ghostscript-source AGPL mechanism) and lives in `Roadmap_Blocked.md`.

## Research-Driven Additions

Unblocked, net-new items only. HDR display (V100-06), full color management (V100-05), GPS map overlay (V20-23), and the Explorer thumbnail handler (V70-04) remain correctly parked in `Roadmap_Blocked.md` and are not duplicated here.

### 2026-07-16 research pass (V110 block)

The actionable V110 items shipped (see CHANGELOG and git history). The remaining V110 items (V110-09 MainViewModel extraction, V110-12 archive-to-archive rolling navigation, V110-13 hold-to-scrub fly-through, V110-16 gain-map grayscale overlay) require foreground GUI verification of interactive behavior and are parked in `Roadmap_Blocked.md` until a GUI-verifiable session is available.
