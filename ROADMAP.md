# Images — ROADMAP

Tracks planned work. `[ ]` pending, `[x]` shipped. Priorities `P0` must / `P1` should / `P2` nice.

## v0.1.2 — polish + branding pass

- [ ] **V02-01** *P0* — Bump GitHub Actions: `actions/checkout@v4` → `@v6`, `actions/setup-dotnet@v4` → `@v5`. Closes Node 20 deprecation (runner removes Node 20 on 2026-06-02).
- [ ] **V02-02** *P1* — Write the 5-prompt logo brief to `assets/logo-prompt.md` (minimal icon / app icon / wordmark / emblem / abstract, dark background, SVG-friendly, 16/32/48/128/512 sizes). Single-session defers actual image generation; next run wires the generated `icon.ico` in.
- [ ] **V02-03** *P1* — Wire `<ApplicationIcon>src/Images/Resources/icon.ico</ApplicationIcon>` in `Images.csproj` once the logo asset exists (blocked by V02-02 output).
- [ ] **V02-04** *P2* — Recapture main screenshot (DPI-aware, `SetProcessDPIAware` + `PrintWindow(hwnd, hdc, 2)`) to replace the deleted `assets/screenshots/v0.1.0-main.png` slot. Requires a Windows GUI session — deferred to manual capture pass.
- [ ] **V02-05** *P2* — Human runtime smoke: prev/next/wrap, rename+undo, drag-drop, Del-to-recycle, external add/delete roundtrip within 250 ms. Headless shells cannot drive WPF.

## Backlog (post-v0.1.2)

- [ ] Persistent settings store (theme toggle, last folder, zoom preference) — currently everything is in-memory.
- [ ] Optional light theme (Catppuccin Latte) in addition to the Mocha default.
- [ ] Keyboard shortcut surface in-app (tooltip / help panel). Per user rules no new shortcuts, just document the existing ones.
