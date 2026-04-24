# Images — ROADMAP

Tracks planned work. `[ ]` pending, `[x]` shipped. Priorities `P0` must / `P1` should / `P2` nice.

## v0.1.2 — polish + branding pass

- [ ] **V02-01** *P0* — Bump GitHub Actions: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. Closes Node 20 deprecation (runner removes Node 20 on 2026-06-02).
- [x] **V02-02** *P1* — 5-prompt logo brief at `assets/logo-prompt.md` shipped in the prior commit; user generated the `logo.png` + `banner.png` assets manually and dropped them in.
- [x] **V02-03** *P1* — `<ApplicationIcon>Resources\icon.ico</ApplicationIcon>` wired in `src/Images/Images.csproj`. `icon.ico` generated from `logo.png` as a 7-frame multi-resolution ICO (16/24/32/48/64/128/256, Catmull-Rom downscale from a 431×431 square-padded source). `icon.svg` ships as a PNG-embedded SVG wrapper for web/README contexts. `logo.png` bundled as a WPF `<Resource>` for in-app branding surfaces (splash, about).
- [ ] **V02-04** *P2* — Recapture main screenshot (DPI-aware, `SetProcessDPIAware` + `PrintWindow(hwnd, hdc, 2)`) to replace the deleted `assets/screenshots/v0.1.0-main.png` slot. Requires a Windows GUI session — deferred to manual capture pass.
- [ ] **V02-05** *P2* — Human runtime smoke: prev/next/wrap, rename+undo, drag-drop, Del-to-recycle, external add/delete roundtrip within 250 ms. Headless shells cannot drive WPF.

## Backlog (post-v0.1.2)

- [ ] Persistent settings store (theme toggle, last folder, zoom preference) — currently everything is in-memory.
- [ ] Optional light theme (Catppuccin Latte) in addition to the Mocha default.
- [ ] Keyboard shortcut surface in-app (tooltip / help panel). Per user rules no new shortcuts, just document the existing ones.
