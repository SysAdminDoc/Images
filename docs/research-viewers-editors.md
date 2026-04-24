# Images — Research: Viewers & Light Editors

Research pass dated 2026-04-24. Competitive landscape for Windows image viewers + light editors we can mine for features.

## Tool-by-tool inventory

### IrfanView (freeware, v4.73)
The 30-year incumbent. Tiny footprint, massive capability via plugins. Killer features: best-in-class **Batch Conversion/Rename** (lossless JPEG rotate/crop/EXIF-date in batch, date-taken placeholders like `$E36868(%Y_%m_%d)`, TWAIN/WIA batch scanning to multipage TIF), **standalone-executable slideshows** with MP3, **Adobe 8BF filter** host, first to support animated GIF / multipage TIF / multi-icon ICO. Plugin pack adds ~200+ formats incl. RAW, JXL, HEIC, PSD, EPS, video, audio. Free for personal.

### XnView MP (freeware, v1.10.5, cross-platform)
Free ACDSee equivalent. **~500 read / ~70 write formats** — widest support. Browser view, FullScreen, Filmstrip, Slideshow with effects, **image compare up to 4 side-by-side**. Full IPTC/XMP/EXIF editor, batch convert/rename/timestamp, duplicate-file finder, Photoshop filter compatibility, face detection.

### ImageGlass (FOSS, v9.4.1.15, .NET/Windows)
Our closest peer. 90+ formats. **Six zoom modes** (Auto/Lock/Scale to Width/Height/Fit/Fill). Full animated support (GIF/WebP/APNG/SVG). Real-time change monitoring (our FSW equivalent), **frame navigation for multi-frame TIF/ICO/GIF/WebP**, built-in color picker. Unique: **embedded motion-video inside Live Photo JPEGs** (v9.3+), **individual color-channel isolation**, **File Explorer sort-order sync** (v9.3+), enterprise config file deployment, toolbar/layout/theme/icon-pack customization. Lossless compression (v9.1+), resize (v9.2+). 12.5k stars.

### nomacs (FOSS GPLv3, v3.22.1, Qt)
**Unique moat**: multi-instance synchronization — locally or over **LAN**. Connect two nomacs windows, panning/zooming/file-switching mirrors. **Opacity-overlay comparison**, transmit-image-over-network (Alt+I), per-client permissions. OpenCV for adjustments + thumbnails, LibRAW for RAW, KImageFormats for AVIF/HEIF/JXL/EXR, Quazip for **images out of ZIPs**. Plugin system (paint, composite, affine, miniature). Histogram, multi-page TIFF.

### qView (FOSS GPLv3, Qt)
Minimalist: "no toolbars, just the image." Instant cold start. **Multithreaded preloading**, animated GIF transport controls, rotation/mirror, file history.

### JPEGView (FOSS, v1.3.46)
**Lightning-fast decode** — explicit **AVX2/SSE2 + up to 4 CPU cores**. Exceptional real-time **inline editing with no modal dialog**: sharpness, contrast, color balance, rotation/perspective, **local exposure compensation** (dodge/burn regions without entering a tool). High-quality sharpness-preserving resample. JPEG/GIF/BMP/PNG/TIFF/PSD/WebP/JXL/HEIF/HEIC/AVIF/TGA/WDP/HDP/JXR + RAW from 20+ mfrs.

### FastStone Image Viewer (freeware personal/edu)
**Most editing-heavy** viewer. **Clone stamp, healing brush, red-eye, levels, curves, hue**; drop-shadow/frame/sketch/oil-paint effects; text/line/shape annotation. **150+ slideshow transitions** with music, contact sheet, **4-image compare**, dual-monitor, hidden-toolbar-at-screen-edges full-screen UI. RAW CR2/CR3/NEF/DNG + HEIC/WEBP/PSD.

### Honeyview (freeware, v5.53 — discontinued, migrating to BandiView)
Unique: reads images **directly from ZIP/RAR/7Z/LZH/TAR/CBR/CBZ** no extraction — default comic viewer for many users. EXIF + **GPS coordinates overlay**, batch convert + resize, animated GIF/WebP/BPG/PNG.

### Windows Photos (Microsoft 2026.11020, built-in)
Baseline. 2026 shipped **Generative Erase** (paint + AI inpaint), **Background Blur** (AI depth-of-field), **Auto Enhance**. Natural-language search, face recognition, OneDrive sync. Format coverage modular via **WIC extensions**: HEIF, AV1 (AVIF), Raw Image Extension (100+), WebP, JPEG XL. Weakness: slow startup, laggy scroll — the gap users complain about loudest.

### QuickLook (FOSS, v4.5.0) / Seer Pro (MS Store) / PowerToys Peek
**Spacebar preview** from Explorer. QuickLook: 100+ formats incl. 3D models, PDF, Office, code, archives, fonts, audio, EPUB/CBZ. Seer Pro: **clipboard-copy of image/video-frame directly from preview**, massive-file handling, scripting. PowerToys Peek (Shift+Space) is Microsoft's official answer.

### Pictus (FOSS, v1.7.0)
Minimalist + **Explorer shell extension** for PSD thumbnails in Explorer. Real-time color adjustments. The shell-extension angle is worth noting — most viewers ignore Explorer thumbnails entirely.

---

## Features Worth Stealing (grouped)

### 1. Viewing
- Image compare 2-up / 4-up (XnView MP, FastStone)
- **Opacity-overlay compare** (nomacs)
- **Multi-instance sync pan/zoom — local and LAN** (nomacs — unique moat)
- Filmstrip view (XnView, FastStone)
- Full-screen with hidden edge-triggered toolbars (FastStone)
- Slideshow with transitions + music + countdown (FastStone 150 transitions; IrfanView MP3)
- **Standalone .exe slideshow export** (IrfanView — unique)
- Dual/multi-monitor (FastStone, IrfanView)
- Histogram overlay (JPEGView, nomacs)
- EXIF overlay with **GPS coords displayed** (Honeyview)
- Lossless JPEG rotate (IrfanView, in batch too)
- **Six zoom modes** (ImageGlass: Auto/Lock/Width/Height/Fit/Fill)
- Frame nav for multi-frame TIF/ICO/GIF/WebP (ImageGlass)
- **Individual color-channel isolation** (ImageGlass — unique)
- Wallpaper preview / set-as-wallpaper (IrfanView, FastStone)

### 2. Light editing inside the viewer
- Crop / resize / rotate / flip (table stakes)
- **Lossless crop** (IrfanView) — `jpegtran` MCU-aligned
- Red-eye removal (FastStone)
- Levels / curves / hue (FastStone, JPEGView)
- **Clone stamp + healing brush** (FastStone — rare at this tier)
- **Local exposure compensation** (JPEGView — regional dodge/burn, no modal)
- Real-time inline adjustments with no modal (JPEGView — UX pattern worth copying)
- Annotation: text/line/shape/arrow (FastStone)
- Redact / paint-on-image (nomacs plugin)
- **AI Generative Erase + Background Blur** (Windows Photos 2026 — ONNX)
- Auto Enhance 1-click (Windows Photos)
- Color picker (ImageGlass)
- Batch rename in place with **date-taken placeholders** (IrfanView)
- **Adobe 8BF filter host** (IrfanView — unlocks entire Photoshop-filter ecosystem)

### 3. File ops
- Copy to folder / Move to folder with recent-folder menu (IrfanView, XnView)
- Rate stars + tags + IPTC/XMP editor (XnView MP)
- Print layouts / contact sheet (FastStone, IrfanView)
- Set as wallpaper (IrfanView)
- Email current image (IrfanView)
- **Copy to clipboard from inside preview** (Seer Pro)
- Send-to-app integration (ImageGlass)
- Scan via TWAIN/WIA (IrfanView)
- Duplicate finder (XnView MP)
- **Explorer thumbnail shell extension** (Pictus)

### 4. Format / decoding
- HEIC/HEIF (WIC or libheif)
- AVIF (libavif)
- JPEG XL (libjxl — Apple iPhone 16 default)
- WebP + animated WebP
- RAW (LibRAW — Canon/Nikon/Sony/Fuji/Pentax/Olympus)
- PSD layer composite (nomacs, JPEGView, Pictus)
- Animated GIF/APNG/WebP/AVIF (ImageGlass)
- Animated SVG (ImageGlass)
- Multipage TIFF + ICO frame nav (IrfanView, ImageGlass)
- Video thumbnails + still-frame grab (QuickLook, Seer)
- **Images inside archives** — ZIP/RAR/7Z/CBR/CBZ no extraction (Honeyview, nomacs via Quazip)
- DICOM / EXR / HDR (KImageFormats — niche but free)

### 5. Performance tricks
- **Preload next/previous** in background (qView, JPEGView, Pictus)
- **SIMD decode — AVX2/SSE2 + multi-core** (JPEGView explicitly)
- Persistent thumbnail cache (XnView MP, nomacs OpenCV-accelerated)
- Async decode off UI (everyone modern)
- **Memory-mapped I/O** for huge files (Seer Pro)
- GPU-accelerated blit (Windows Photos Direct2D, QuickLook Fluent)
- Instant cold-start under 1s (qView, Pictus)
- **File-change detection without full reload** (ImageGlass real-time monitor — we already do this)

### 6. Navigation UX
- Filmstrip at bottom (XnView, FastStone)
- Folder tree sidebar (XnView MP)
- Recent folders jump list (qView)
- **Command palette** — none do this well; greenfield opportunity
- Keyboard-first nav with customizable keys (ImageGlass)
- Drag-and-drop between instances with Ctrl+Alt sync binding (nomacs)
- **Sort order sync with File Explorer** (ImageGlass v9.3+ — unique)
- **Spacebar preview from Explorer** (QuickLook/Seer/Peek)

## Unique / nobody-else list
- nomacs LAN pan/zoom/image-send sync — biggest FOSS moat
- IrfanView standalone-exe slideshow export + date-taken batch placeholders
- JPEGView local exposure compensation (regional dodge/burn no-modal)
- FastStone hidden edge-triggered full-screen toolbar pattern
- ImageGlass File-Explorer-sort sync + color-channel isolation
- Honeyview direct archive (CBR/CBZ/ZIP/RAR/7Z) browsing
- Windows Photos Generative Erase via on-device ONNX
- Seer Pro clipboard-copy-from-preview
- Pictus shell-extension for PSD thumbnails

## Plugin / scripting ecosystems
- **IrfanView**: plugin pack (formats, effects, Adobe 8BF filters, OCR). 8BF loader is the most valuable hook.
- **ImageGlass**: tool/theme/icon-pack + third-party-app delegation.
- **nomacs**: first-class plugin SDK (paint, composite, affine, miniature).
- **QuickLook / Seer**: add-in model; Seer exposes scripting.
- **JPEGView**: INI-driven config, user `IPTCPanel.txt`, customizable keymaps — no true plugin DLLs.
- **Pictus**: no plugin API, but shell-extension is a blueprint.

## Sources
- [IrfanView](https://www.irfanview.com/), [Batch docs](http://irfanview.helpmax.net/en/file-menu/batch-conversionrename/)
- [XnView MP](https://www.xnview.com/en/xnviewmp/)
- [ImageGlass features](https://imageglass.org/docs/features)
- [nomacs GitHub](https://github.com/nomacs/nomacs), [Sync blog](https://nomacs.org/blog/synchronization/)
- [qView](https://interversehq.com/qview/)
- [JPEGView GitHub](https://github.com/sylikc/jpegview)
- [FastStone](https://www.faststone.org/FSViewerDetail.htm)
- [Honeyview](https://www.bandisoft.com/honeyview/)
- [QuickLook](https://github.com/QL-Win/QuickLook), [Seer](https://1218.io/seer/)
- [Pictus](https://poppeman.se/pictus/), [Pictus GitHub](https://github.com/poppeman/Pictus)
