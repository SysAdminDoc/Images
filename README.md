<div align="center">

<img src="assets/banner.png" alt="Images — a Windows 7–style classic image viewer, reimagined in dark mode" width="100%" />

# Images

[![Version](https://img.shields.io/badge/version-0.2.9-89b4fa?style=flat-square)](https://github.com/SysAdminDoc/Images/releases)
[![License](https://img.shields.io/badge/license-MIT-a6e3a1?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-cba6f7?style=flat-square)](#)
[![.NET](https://img.shields.io/badge/.NET-9.0-f38ba8?style=flat-square)](#)

A Windows 7–style classic image viewer, reimagined in dark mode, with inline rename-while-viewing.

</div>

---

## Why another image viewer?

Because sometimes you don't know what to call a photo until you actually *see* it — and the existing dark-mode viewers on Windows make you close the image, rename the file, and reopen. **Images** fixes that: the filename lives in a side panel right next to the photo. Type; the file is renamed 600 ms after you stop typing. Change your mind? Hit **Undo** in the Recent Renames list.

## Features

- **Broad format coverage** via WPF's built-in WIC plus [Magick.NET](https://github.com/dlemstra/Magick.NET): JPG, PNG/APNG, GIF, TIFF, WEBP, HEIC, AVIF, JXL, PSD/PSB, TGA, DDS, QOI, EXR, HDR, DPX, JPEG 2000, DICOM, FITS, XCF/ORA, SVG, WMF/EMF, WPG, RAW/DNG/NEF/CR2/CR3/ARW/RW2/RAF/ORF/PEF, legacy production formats, and more.
- **Document/vector previews** for PDF, EPS, PS, and AI when Ghostscript is bundled app-local or installed on the machine. Images auto-detects `Codecs\Ghostscript`, `IMAGES_GHOSTSCRIPT_DIR`, and standard Ghostscript installs.
- **Multi-page navigation** for documents and layered/page-based image formats. PDF, TIFF, PSD/PSB, ICO, DICOM, FITS, DCX, and related formats surface page/frame controls only when the current file has more than one page.
- **Archive book previews** for ZIP and CBZ. Images opens supported image entries as read-only pages, ignores unsafe or nested archive entries, and keeps archive navigation inside the existing page controls with a scrubber for quick jumps.
- **Animated GIFs play inline** — multi-frame GIFs (and animated WebP / APNG when the Magick build supports them) decode via `MagickImageCollection.Coalesce()` and cycle through `ZoomPanImage` with the original per-frame delays + loop count intact. A green "N frames" chip in the bottom toolbar marks animated files.
- **Classic Windows 7 Photo Viewer layout** — centered image, bottom toolbar, hover-reveal circular arrows on the left and right edges. But in **Catppuccin Mocha** dark.
- **Peek mode** — `Images.exe --peek "C:\path\to\image.jpg"` opens a chromeless, topmost preview window that closes with Escape and leaves normal window settings alone.
- **Live inline rename** — split stem + extension editor on the right. Extension is locked by default (no more accidentally renaming `photo.jpg` → `photo.jp`). Debounced auto-save; no Save button.
- **Conflict-safe** — if a target name already exists in the folder, the rename preview shows exactly what it will become (`name (2).jpg`) before it commits.
- **Recent Renames panel** — the last 10 renames are stacked on the side with **Undo** buttons.
- **Full directory navigation** — open one photo, scroll through the whole folder with ← / → keys or the hover arrows. Wraps at the ends. Natural-sorted so `IMG_2.jpg` comes before `IMG_10.jpg`.
- **Text extraction (OCR)** — press `E` to overlay selectable text boxes directly over detected text regions. Highlight any recognized text manually and copy it with Ctrl+C or the context menu. Uses Windows.Media.Ocr for local, offline processing with installed Windows OCR language packs. No cloud, no network, no bloat.
- **Togglable folder filmstrip** — a compact, virtualized, cached thumbnail rail spans the current folder, keeps the current item centered, supports right-click Open/Reveal/Copy actions, and falls back to the side panel when hidden.
- **Photo metadata at a glance** — the Details panel and optional `I` metadata HUD surface embedded EXIF date, camera, lens, exposure, focal length, and GPS coordinates when present, without opening a separate info window or sending location data anywhere.
- **Zoom + pan** — mouse wheel to zoom in/out about the cursor, drag to pan, double-click to toggle fit/1:1.
- **Export a copy** to JPEG, PNG, WebP, AVIF, JXL, TIFF, BMP, GIF/APNG, PSD/PSB, PDF/EPS/SVG, TGA, DDS, QOI, EXR, HDR, JPEG 2000, X11/Magick, production/scientific, and portable bitmap formats.
- **Rotate**, **delete-to-Recycle-Bin**, **Reveal in Explorer**, **Copy path**.
- **Calm confirmations and recovery** — destructive file actions confirm before moving anything to Recycle Bin; routine actions complete with toast feedback.
- **Network-quiet by default** — automatic update checks are disabled until enabled in Settings; manual About checks remain available.

## Install

Both artifacts ship alongside every release. They're the same build — pick whichever fits your workflow.

### Installer (recommended for most users)

1. Grab `Images-vX.Y.Z-setup-win-x64.exe` from [Releases](https://github.com/SysAdminDoc/Images/releases).
2. Run it. Installs to `%ProgramFiles%\Images`, removes any older per-user or machine-wide Images install first, and provisions the Windows OCR language capability for the current UI language plus `en-US` fallback. No separate .NET runtime install is required.
3. Optional boxes on the wizard: **Desktop icon**, **Add to "Open with" menu** (non-destructive — adds *Images* to the Windows "Open with" list without overriding whatever you currently have set as default for those extensions). If an older install already had Images associations enabled, the installer carries them forward automatically.
4. Uninstalls cleanly from Settings → Apps → Installed apps.

The installer is self-contained: the .NET Desktop runtime and bundled codecs ship inside the app folder.

### Portable (zero install)

1. Grab `Images-vX.Y.Z-win-x64.zip` from [Releases](https://github.com/SysAdminDoc/Images/releases).
2. Extract anywhere.
3. Run `Images.exe`. Leaves no registry writes.

To associate file types from a portable install: right-click any image → **Open with** → **Choose another app** → browse to `Images.exe` → tick **Always use this app**.

### From source

```bash
git clone https://github.com/SysAdminDoc/Images.git
cd Images
dotnet build -c Release
dotnet run --project src/Images
```

### Optional bundled Ghostscript

PDF, EPS, PS, and AI previews require Ghostscript. For a self-contained release experience, place the approved Ghostscript runtime under `src/Images/Codecs/Ghostscript` before publishing; the project copies that folder into the app output automatically. A typical layout is `Codecs/Ghostscript/bin/gsdll64.dll` with the matching Ghostscript support files beside `bin`; `gswin64c.exe` is optional and only used for version display.

Images also detects `IMAGES_GHOSTSCRIPT_DIR` and normal system installs under `%ProgramFiles%\gs`. Keep third-party binaries out of source control unless redistribution rights for the exact package are already approved.

Release builders can use `scripts/Prepare-GhostscriptBundle.ps1`; see `docs/codec-bundling.md`.

To build the installer locally, install [Inno Setup 6](https://jrsoftware.org/isdl.php), run `dotnet publish src/Images -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish`, then `iscc /DMyAppVersion=0.2.9 installer\Images.iss`. Output lands at `installer\output\Images-vX.Y.Z-setup-win-x64.exe`.

OCR depends on Microsoft Windows OCR optional capabilities. The installer installs the current Windows UI language OCR capability plus `en-US` fallback when needed; Images cannot legally bundle those Microsoft language packs inside the app folder.

## Keyboard

| Key | Action |
| --- | --- |
| **← / →** | Previous / next image in folder |
| **Home / End** | First / last image |
| **Space / Backspace** | Next / previous image |
| **Delete** | Send current image to Recycle Bin |
| **E** | Extract text (OCR) — toggle overlay |
| **F5** | Rescan current directory |
| **I** | Toggle metadata HUD |
| **Enter** (in rename box) | Commit rename now (skip debounce) |
| **Esc** (in rename box) | Cancel edit, revert textbox to disk name |
| **+ / -** | Zoom in / out |
| **0** | Fit to window |
| **1** | 100% zoom |

*(Navigation keys are swallowed by the rename textbox while it has focus, so you can freely use arrow keys inside the filename editor.)*

## Architecture

```
src/Images/
├── App.xaml                    # Entry point, theme merge
├── MainWindow.xaml             # Layout: image canvas + side rename panel + bottom toolbar
├── ViewModels/
│   ├── ObservableObject.cs     # INotifyPropertyChanged base
│   ├── RelayCommand.cs         # ICommand impl
│   └── MainViewModel.cs        # All view state + commands
├── Services/
│   ├── ImageLoader.cs          # WIC-first, Magick.NET fallback, cached decoding
│   ├── ArchiveBookService.cs   # Read-only ZIP/CBZ page discovery for archive books
│   ├── ImageExportService.cs   # Codec-aware Save a copy / conversion output
│   ├── ImageMetadataService.cs # Read-only EXIF summary for the Details panel and HUD
│   ├── CodecCapabilityService.cs # About-window codec summary and copyable diagnostics
│   ├── SupportedImageFormats.cs # Central extension catalog for discovery/dialogs
│   ├── CodecRuntime.cs         # Optional app-local Ghostscript runtime discovery
│   ├── AppStorage.cs           # LocalAppData/Temp storage fallback for caches and logs
│   ├── DirectoryNavigator.cs   # Natural-sort folder scan, prev/next/wrap, FileSystemWatcher
│   ├── ThumbnailCache.cs       # Disposable WebP thumbnail cache for the folder preview strip
│   └── RenameService.cs        # Debounced File.Move, conflict resolution, undo history
├── Controls/
│   └── ZoomPanImage.cs         # Wheel-zoom + drag-pan image host
├── Themes/
│   └── DarkTheme.xaml          # Catppuccin Mocha tokens + control styles
└── Resources/                  # icon.ico (app icon), icon.svg (vector wrapper), logo.png
tests/Images.Tests/             # focused regression tests for file-operation services
```

## Diagnostics

Images carries its own diagnostics surface — no terminal required for the common cases:

- **About → Save system info** writes the same content as `Images.exe --system-info` to a file in `%TEMP%` and reveals it in Explorer. Attach the file to a bug report.
- **About → Open data folder** opens `%LOCALAPPDATA%\Images\` so logs (`Logs\images-<date>.log`), crash records (`crash.log`, `crash-*.dmp`), settings (`settings.db`), and caches (`thumbs/`, `update-check.json`) are reachable in one click.
- **About → Codec report** copies the per-format capability matrix and supported-extension list to the clipboard.
- `Images.exe --system-info` and `Images.exe --codec-report` print the same content to stdout for support tickets and CI smoke tests.

## Policies

- [Release support policy](docs/release-support-policy.md) — what versions get servicing and for how long.
- [Codec support policy](docs/codec-support-policy.md) — bundled-vs-optional tiers and the gate every new optional decoder must pass.
- [Privacy policy](docs/privacy-policy.md) — exactly one network call (the opt-out update check), every file persisted to disk, and a four-step verification recipe.
- [Distribution trust plan](docs/distribution-trust.md) — WinGet/Scoop scope, checksum continuity, signing options, and verification copy.
- [Optional runtime and integration policy](docs/integration-policy.md) — license, provenance, CVE, process-boundary, and release gates for external runtimes.
- [Peek mode](docs/peek-mode.md) — shell-helper invocation, local startup timing diagnostics, and manual smoke steps.

## Credits / inspiration

Architectural inspiration taken from existing OSS viewers (no code copied, both are GPL-3):

- [**ImageGlass**](https://github.com/d2phap/ImageGlass) — Windows-native decoding model, format breadth, toolbar ergonomics.
- [**nomacs**](https://github.com/nomacs/nomacs) — side-panel information architecture, filename-edit UX.

## License

[MIT](LICENSE) © SysAdminDoc
