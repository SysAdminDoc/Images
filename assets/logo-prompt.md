# Images — Logo Brief

**Project:** Images — a fast, minimal WPF/.NET 9 Windows photo viewer with live inline rename-while-viewing. Visual tone: **Windows 7 Photo Viewer** chrome re-skinned in the **Catppuccin Mocha** dark palette (base `#1E1E2E`, mauve `#CBA6F7`, lavender `#B4BEFE`, text `#CDD6F4`).

**Universal requirements (apply to every prompt below):**
- Dark background (`#1E1E2E` base or pure black for app-icon context)
- High contrast, flat vector shapes, no photographic texture
- SVG-friendly geometry (no gradients that can't tokenize; 2-3 colors per mark)
- No text anywhere **except** the wordmark prompt
- Respect the Catppuccin Mocha palette; mauve + lavender are the primary accent colors
- Deliverable sizes: **SVG (source)**, **ICO** with 16/32/48/128/256 px frames, **PNG** at 16/32/48/128/512 px, plus a 1024×1024 master PNG for downscaling
- No polaroid / eye / camera-aperture clip art — those are overused for image apps and clash with the Windows 7 tone

---

## 1. Minimal icon (favicon / tray / toolbar, 16–128 px)

> A flat, single-mauve (`#CBA6F7`) glyph of a stylized picture frame viewed slightly above its diagonal — the frame is an empty rounded square with a subtle highlight on one inner edge to imply a photograph is seated inside. Geometry reads at 16×16 without antialiasing tricks. Solid Catppuccin base (`#1E1E2E`) background, centered with 2-px padding on all sides.

## 2. App icon (taskbar / Start / shortcut, 256–512 px)

> A rounded-square app icon, background a soft vertical gradient from Catppuccin surface1 (`#45475A`) down to base (`#1E1E2E`). Foreground: the same picture-frame glyph from the minimal icon, scaled up, now with a small lavender (`#B4BEFE`) corner accent suggesting a just-renamed filename tag. One subtle inner highlight along the top-left edge of the frame to give depth without a drop shadow. Flat, vector, readable at 32 px. No text.

## 3. Wordmark (README header, splash, installer)

> Horizontal wordmark reading **Images** in a geometric sans-serif with slightly tall x-height (think Inter or Geist). Letter `I` in mauve (`#CBA6F7`), rest of the word in text color (`#CDD6F4`). To the left of the word, a compact version of the minimal picture-frame glyph, 1.2× cap-height tall, same mauve. Dark base (`#1E1E2E`) background. Total aspect ratio roughly 4:1. Vector source.

## 4. Emblem (badge / shield / splash centerpiece)

> A circular badge centered on a dark base. Outer ring thin, lavender (`#B4BEFE`). Inner field Catppuccin mantle (`#181825`). At the center, the mauve picture-frame glyph, slightly larger than it would be in an icon context. Along the inner ring, 12 small evenly spaced tick marks in the muted overlay color (`#6C7086`), suggesting an aperture without being an aperture. No text. Readable as a 128 px splash screen centerpiece, scales cleanly to 512 px.

## 5. Abstract (brand mark, social cards, hero)

> An abstract brand mark: three overlapping thin-stroked rectangles of slightly different aspect ratios, fanned like a stack of framed photographs being shuffled. Colors: mauve (`#CBA6F7`) for the front rectangle, lavender (`#B4BEFE`) for the middle, muted surface1 (`#45475A`) for the back. Each rectangle has a 2-px inner highlight on its top edge. Composition sits on the Catppuccin base (`#1E1E2E`). Works at 1024×1024 for social headers and crops down to a 128 px tile without losing the stacked-frames read.

---

## Integration plan (next factory run once assets land)

1. Drop generated outputs into `src/Images/Resources/` (`icon.ico`, `icon.svg`, `logo-512.png`).
2. Drop README-sized wordmark to `assets/logo-wordmark.png` and wire it into the README header.
3. Wire `<ApplicationIcon>src/Images/Resources/icon.ico</ApplicationIcon>` into `src/Images/Images.csproj` (closes V02-03).
4. Update `CHANGELOG.md` Unreleased with a "Branding: logo, app icon, wordmark" entry.
