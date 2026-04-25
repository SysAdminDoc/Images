# Privacy policy

Images is a Windows-only, local-first image viewer. This document lists every network call, every persistent file, and every behavior that could affect your privacy. If something is not documented here, the app does not do it.

## Network behavior

There is exactly **one** network call surface in the shipped product:

### Update check (P-04)

- **Endpoint**: `https://api.github.com/repos/SysAdminDoc/Images/releases/latest`
- **Why**: detects whether a newer release is available.
- **When**: once at startup, throttled to one call per 24 hours (last-checked timestamp persisted to `update-check.json`). Manual "Check updates" in About bypasses the throttle.
- **What is sent**: a `User-Agent` of the form `Images/<version> (+https://github.com/SysAdminDoc/Images)`, plus the standard HTTP headers GitHub requires. **No** account, telemetry, or analytics token. **No** identifying client ID. **No** image, file, or folder metadata leaves the machine.
- **What is logged locally**: every call writes its URL, response status, byte count, and duration to the structured log so you can audit it. Logs land at `%LOCALAPPDATA%\Images\Logs\images-<yyyy-MM-dd>.log`.
- **How to turn it off**: About → "Automatically check for updates" checkbox. With the box cleared, `IsDueForBackgroundCheck` short-circuits and the network call is never made.

That is the entire network surface. No image opens, RAW decodes, codec discoveries, or filename operations contact the network — at all, ever.

## Files written to disk

All under `%LOCALAPPDATA%\Images\` (with a fallback to `%TEMP%\Images\` when LocalAppData is unwritable). Every entry is **disposable** — deleting it is a safe recovery, the app rebuilds whatever it needs.

| Path | Why | Contains |
|---|---|---|
| `settings.db` | Window state, recent folders, hotkey overrides | Window geometry, last-N folders, keybindings |
| `Logs/images-<date>.log` | Structured Serilog log, rolling daily, 14-day retention | App version, runtime, OS, error traces, update-check call records |
| `Logs/crash-<timestamp>.dmp` | Minidump on fatal exception | Process state at crash time (no image bytes) |
| `crash.log` | Plain-text fatal-exception record | Stack traces, no image bytes |
| `update-check.json` | Last-checked timestamp + latest known tag | The most recent update-check result |
| `thumbs/<aa>/<sha1>.webp` | Disk thumbnail cache | Resized thumbnails of files you have opened |
| `wallpaper/current.<ext>` | Stable copy for "Set as wallpaper" | A copy of the image you last set as wallpaper |

You can delete any entry above without losing data the app actually owns. Originals on disk are never modified by these caches.

## What does **not** happen

- No telemetry, no analytics, no crash reporter that phones home. Crash dumps stay on your machine until you choose to attach them to a GitHub issue.
- No cloud sync, no account, no login.
- No image is uploaded, indexed remotely, or fingerprinted off-device.
- No EXIF/GPS data is transmitted.
- No file paths or filenames are transmitted.
- No OCR / face / object detection runs in the background. There is no AI/ML pipeline in the shipped product.
- No advertising SDKs, tracking pixels, or third-party JavaScript.
- No browser-style file-association telemetry. The optional installer's "Open with" registration writes `Software\RegisteredApplications` keys, which are local-only and removed cleanly on uninstall.

## How to verify

You can audit the network behavior end-to-end:

1. Open **About** → "Automatically check for updates" — uncheck it. Every silent call stops.
2. Watch `%LOCALAPPDATA%\Images\Logs\images-<date>.log`. Every update-check call writes a line there. There are no other network call sites in the codebase.
3. Run `Images.exe --system-info` to see the local files Images uses.
4. Search the source: `grep -r "HttpClient\|WebClient\|HttpRequestMessage" src/Images/` — should return only `Services/UpdateCheckService.cs`.

## Reporting concerns

If you find a network call or persisted file not documented here, that's a bug. Please open an issue at <https://github.com/SysAdminDoc/Images/issues> — security-sensitive findings can be filed as a private vulnerability report instead.

## Changes to this policy

This policy is versioned with the rest of the repository. Any change that adds a network call site, persists a new file, or expands what is logged must update this document in the same change as the code, and call it out in CHANGELOG.
