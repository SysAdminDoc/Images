# Screen Reader Manual Test Matrix

Run this checklist before each release to verify screen reader compatibility. Leave result columns blank until tested; mark Pass/Fail/Partial per reader.

Tested against the UIA tree documented in [accessibility.md](accessibility.md).

## Environment setup

| Reader | Version | Notes |
|---|---|---|
| Windows Narrator | (built-in) | Win+Ctrl+Enter to toggle. Use Scan Mode (Caps+Space) for element navigation. |
| NVDA | Latest stable | Free/OSS. Browse mode (Insert+Space) for document nav; focus mode for controls. |
| JAWS | Latest stable | Commercial. Virtual cursor (Insert+Z) for element nav. |

## Test matrix

| # | Scenario | Action | Expected announcement | Narrator | NVDA | JAWS | Notes |
|---|---|---|---|---|---|---|---|
| 1 | Image load | Open a JPEG via File > Open or drag-drop | Window title: "{filename} -- Images". Canvas: "Image, {W} by {H} pixels". | | | | HelpText includes arrow/zoom/pan hints. |
| 2 | Navigation | Press Right arrow to advance to next image | New window title with next filename. Canvas Name updates to new dimensions. Position chip (Polite) announces "{N} of {M}". | | | | Left arrow, Home, End, Space, Backspace also navigate. |
| 3 | Rename | Click the filename TextBox, type a new name | TextBox announces current stem text. Character echo on each keystroke. Caret tracking fires `TextSelectionChanged` for Magnifier (A-04). Status dot announces "Saved" after debounce. | | | | Extension lock/unlock state should be announced. |
| 4 | Metadata HUD | Toggle the metadata HUD from settings or the command palette | "Metadata HUD" region is present when enabled, exposes current file facts, and updates politely as images change. | | | | Verify it can be disabled without leaving an empty focus stop. |
| 5 | Export preview | Open the export preview from the command palette | Export preview window title, preset selector, extension field, warning list, and action buttons are readable. | | | | Warnings should be announced as plain text, not color alone. |
| 6 | Delete to Recycle Bin | Press Delete key | Confirmation dialog announced with file name, "Delete" and "Cancel" buttons focusable. "Don't ask again" checkbox announced. All options keyboard-reachable. | | | | Verify dialog title and body text are read. |
| 7 | Gallery | Press G to open gallery overlay | "Gallery workbench" announced (LiveSetting=Polite). Thumbnails ListBox: "Gallery thumbnails" with status text. Each item announces filename and position. | | | | Sort/filter controls should be navigable and labeled. |
| 8 | Settings | Open Settings (Ctrl+,) | Window title "Settings" announced. Tab sections (General, Appearance, Accessibility, etc.) navigable. Each control label and current value announced. | | | | Toggle switches should announce on/off state. |
| 9 | About dialog | Open About from side-panel header | Window title "About" announced. Version string, diagnostics status, and action buttons (Copy system info, Open data folder) all readable. | | | | Codec capability matrix rows should be navigable. |
| 10 | Toast notifications | Trigger any status toast (e.g., paste, rename save, GPS strip) | Toast text announced via LiveSetting=Polite without interrupting current speech. | | | | Toasts should not steal focus. |

## Supplementary checks

| # | Area | Check | Expected | Narrator | NVDA | JAWS | Notes |
|---|---|---|---|---|---|---|---|
| S1 | Filmstrip | Tab into bottom filmstrip, arrow through thumbnails | Each thumbnail announces filename and position. | | | | |
| S2 | OCR overlay | Press E to extract text, tab through regions | OCR regions announce recognized text. Copy with Enter/Space. | | | | |
| S3 | Keyboard cheatsheet | Press ? to open cheatsheet | Cheatsheet content readable. Any key dismisses. | | | | |
| S4 | Crop mode | Press C, drag a selection, press Enter | Crop mode entry/exit announced. Selection controls labeled. | | | | |
| S5 | Compare mode | Enter compare with Ctrl+Alt+C or from duplicate cleanup | "Compare" mode announced. A/B swap and opacity controls labeled. | | | | |

## Process

1. Launch Images with each screen reader active (one at a time).
2. Walk through scenarios 1-10 in order, filling in Pass/Fail/Partial.
3. Run supplementary checks S1-S5 if time permits.
4. Log any regressions as GitHub issues tagged `accessibility`.
5. Attach this completed matrix to the release checklist.

## Known limitations

- JAWS may require Forms Mode to interact with WPF custom controls.
- Narrator Scan Mode may skip some custom automation peers; switch to item navigation (Tab/Shift+Tab) if a control is missed.
- NVDA sometimes double-announces WPF LiveRegion updates; this is an NVDA/WPF interop quirk, not an Images bug.
