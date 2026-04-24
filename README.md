<div align="center">

<img src="assets/banner.png" alt="Images — a Windows 7–style classic image viewer, reimagined in dark mode" width="100%" />

# Images

[![Version](https://img.shields.io/badge/version-0.1.1-89b4fa?style=flat-square)](https://github.com/SysAdminDoc/Images/releases)
[![License](https://img.shields.io/badge/license-MIT-a6e3a1?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-cba6f7?style=flat-square)](#)
[![.NET](https://img.shields.io/badge/.NET-9.0-f38ba8?style=flat-square)](#)

A Windows 7–style classic image viewer, reimagined in dark mode, with inline rename-while-viewing.

</div>

---

## Why another image viewer?

Because sometimes you don't know what to call a photo until you actually *see* it — and the existing dark-mode viewers on Windows make you close the image, rename the file, and reopen. **Images** fixes that: the filename lives in a side panel right next to the photo. Type; the file is renamed 600 ms after you stop typing. Change your mind? Hit **Undo** in the Recent Renames list.

## Features

- **~100 formats** via WPF's built-in WIC (BMP/JPG/PNG/GIF/TIFF/WEBP/HEIC/ICO) plus [Magick.NET](https://github.com/dlemstra/Magick.NET) fallback (JXL, AVIF, PSD, TGA, RAW / DNG / NEF / CR2 / ARW / RW2, and ~90 more).
- **Classic Windows 7 Photo Viewer layout** — centered image, bottom toolbar, hover-reveal circular arrows on the left and right edges. But in **Catppuccin Mocha** dark.
- **Live inline rename** — split stem + extension editor on the right. Extension is locked by default (no more accidentally renaming `photo.jpg` → `photo.jp`). Debounced auto-save; no Save button.
- **Conflict-safe** — if a target name already exists in the folder, the rename preview shows exactly what it will become (`name (2).jpg`) before it commits.
- **Recent Renames panel** — the last 10 renames are stacked on the side with **Undo** buttons.
- **Full directory navigation** — open one photo, scroll through the whole folder with ← / → keys or the hover arrows. Wraps at the ends. Natural-sorted so `IMG_2.jpg` comes before `IMG_10.jpg`.
- **Zoom + pan** — mouse wheel to zoom in/out about the cursor, drag to pan, double-click to toggle fit/1:1.
- **Rotate**, **delete-to-Recycle-Bin**, **Reveal in Explorer**, **Copy path**.
- **No confirmation dialogs** — actions happen immediately with toast feedback.

## Install

### From release (recommended)

1. Grab the latest `Images-vX.Y.Z-win-x64.zip` from [Releases](https://github.com/SysAdminDoc/Images/releases).
2. Extract anywhere — it's framework-dependent, so make sure **[.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)** is installed.
3. Run `Images.exe`.

To associate file types: right-click any image → **Open with** → **Choose another app** → browse to `Images.exe` → tick **Always use this app**.

### From source

```bash
git clone https://github.com/SysAdminDoc/Images.git
cd Images
dotnet build -c Release
dotnet run --project src/Images
```

## Keyboard

| Key | Action |
| --- | --- |
| **← / →** | Previous / next image in folder |
| **Home / End** | First / last image |
| **Space / Backspace** | Next / previous image |
| **Delete** | Send current image to Recycle Bin |
| **F5** | Rescan current directory |
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
│   ├── DirectoryNavigator.cs   # Natural-sort folder scan, prev/next/wrap, FileSystemWatcher
│   └── RenameService.cs        # Debounced File.Move, conflict resolution, undo history
├── Controls/
│   └── ZoomPanImage.cs         # Wheel-zoom + drag-pan image host
├── Themes/
│   └── DarkTheme.xaml          # Catppuccin Mocha tokens + control styles
└── Resources/                  # icon.ico (app icon), icon.svg (vector wrapper), logo.png
```

## Credits / inspiration

Architectural inspiration taken from existing OSS viewers (no code copied, both are GPL-3):

- [**ImageGlass**](https://github.com/d2phap/ImageGlass) — Windows-native decoding model, format breadth, toolbar ergonomics.
- [**nomacs**](https://github.com/nomacs/nomacs) — side-panel information architecture, filename-edit UX.

## License

[MIT](LICENSE) © SysAdminDoc
