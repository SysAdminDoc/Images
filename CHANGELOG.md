# Changelog

All notable changes to **Images** are documented here.

## Unreleased

### Changed

- Folder watcher (`FileSystemWatcher`) now actually runs — external add/delete/rename from Explorer or another app refreshes the list without pressing F5. The position chip updates live, and if the currently displayed file vanishes, the viewer advances to the next slot the navigator lands on.
- `BoolToVis` converter is now declared in `Themes/DarkTheme.xaml` (single source of truth, available to any view) instead of being redeclared per-window.
- `ImageLoader.Load` narrows its WIC catch to decode/format exceptions — `OutOfMemoryException` and thread aborts now propagate instead of silently falling through to Magick.NET. The WIC-path `MemoryStream` is disposed deterministically.
- `DirectoryNavigator.Open` short-circuits when called with a path inside the already-watched folder — no more full rescans on repeat drops from the same directory.
- `DirectoryNavigator.Rescan` catches transient IO / ACL / disconnection errors and keeps the prior list instead of throwing to the UI thread.

### Docs

- README zoom row clarifies wheel-zoom anchors on the cursor; removed the stray `Ctrl+wheel` alias claim that the code never honored.

## v0.1.0 — 2026-04-24

Initial release.

- WPF / .NET 9 image viewer with WIC + Magick.NET decode pipeline (~100 formats incl. BMP/JPG/PNG/GIF/TIFF/WEBP/HEIC/ICO/JXL/AVIF/PSD/TGA/RAW).
- Windows 7 Photo Viewer–inspired chrome in Catppuccin Mocha dark theme.
- Hover-reveal left/right navigation arrows; Left/Right/Home/End/Space/Backspace keyboard navigation with wrap-around.
- Natural-sort directory scan on open; auto-refresh when files are added/removed.
- Split stem/extension rename editor with 600 ms debounced auto-save, live conflict preview, commit-on-navigation.
- Recent Renames panel with one-click undo for the last 10 renames.
- Zoom (wheel / Ctrl+wheel), pan (drag), fit-to-window, 1:1, rotate, delete-to-recycle-bin.
- Command-line: `Images.exe "C:\path\to\image.jpg"` opens file and populates directory.
