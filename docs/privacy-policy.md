# Privacy policy

Images is a Windows-only, local-first image viewer. This document lists every network call, every persistent file, and every behavior that could affect your privacy. If something is not documented here, the app does not do it.

## Network behavior

There is exactly **one** network call surface in the shipped product:

### Update check (P-04)

- **Endpoint**: `https://api.github.com/repos/SysAdminDoc/Images/releases/latest`
- **Why**: detects whether a newer release is available.
- **When**: never by default. If you enable automatic checks in Settings, Images checks once at startup and throttles to one call per 24 hours (last-checked timestamp persisted to `update-check.json`). Manual "Check updates" in About bypasses the throttle.
- **What is sent**: a `User-Agent` of the form `Images/<version> (+https://github.com/SysAdminDoc/Images)`, plus the standard HTTP headers GitHub requires. **No** account, telemetry, or analytics token. **No** identifying client ID. **No** image, file, or folder metadata leaves the machine.
- **What is logged locally**: every call writes its URL, response status, byte count, and duration to the structured log so you can audit it. Logs land at `%LOCALAPPDATA%\Images\Logs\images-<yyyy-MM-dd>.log`.
- **How to control it**: Settings → Privacy → "Check for updates automatically". The default is off. With the box cleared, `IsDueForBackgroundCheck` short-circuits and the network call is never made.

That is the entire network surface. No image opens, RAW decodes, codec discoveries, or filename operations contact the network — at all, ever.

## Files written to disk

All paths below are under `%LOCALAPPDATA%\Images\` (with a fallback to `%TEMP%\Images\` when LocalAppData is unwritable). Settings uses the same `LocalDataStoreRegistry` as `--system-info`, support bundles, and the confirmed privacy reset. “Rebuildable” stores can be deleted safely; preserved stores contain preferences, user-authored data, imported models, originals, drafts, or safety copies.

| Path | Class | Contains / clear behavior |
|---|---|---|
| `settings.db` | Preserved preferences/history | Window state, recent paths, archive progress, shortcuts, and preferences |
| `catalog.db` | Rebuildable derived data | Folder catalog and extracted image metadata; cleared by privacy reset |
| `semantic-index.db` | Rebuildable derived data | Local CLIP embeddings and indexed source paths; cleared by privacy reset |
| `tag-graph.json` | Rebuildable derived data | Relationships derived from image tags; cleared by privacy reset |
| `smart-collections.json` | Preserved user content | User-authored saved collection rules |
| `keyword-sets.json` | Preserved user content | User-authored reusable keyword sets |
| `update-check.json` | Rebuildable service metadata | Last-check timestamp and latest known release; cleared by privacy reset |
| `network-egress.jsonl` | Local diagnostics | Audit records for update-check requests; cleared by privacy reset |
| `crash.log` | Local diagnostics | Plain-text fatal exception details; cleared by privacy reset |
| `thumbs/` | Rebuildable derived data | Resized thumbnails; cleared by privacy reset |
| `tiles/` | Rebuildable derived data | Large-image tile pyramids; cleared by privacy reset |
| `models/` | Preserved local models | Models explicitly imported through Model Manager plus `model-manifest.json`; never downloaded or cleared automatically |
| `diagnostics/` | User-requested diagnostics | `images-support-*.zip` bundles; textual paths are replaced with `%PATH%`; cleared by privacy reset |
| `Logs/` | Local diagnostics | Rolling `images-*.log` files and `crash-*.dmp` minidumps; cleared by privacy reset |
| `recovery/` | Local operation history | `recovery-log.jsonl`; cleared by privacy reset after confirmation |
| `wallpaper/` | Preserved user content | Stable image copy selected for Windows wallpaper |
| `email-drafts/` | Preserved user content | User-requested unsent MIME drafts with image attachments and source paths |
| `writeback-backups/` | Preserved safety copies | Backups made before metadata writeback |
| `quarantine/` | Preserved originals | Files moved by duplicate cleanup or health repair |
| `clipboard/` | Rebuildable temporary media | Images materialized from the clipboard; cleared by privacy reset |
| `animation-frames/` | Rebuildable temporary media | User-requested temporary frame exports; cleared by privacy reset |
| `motion-video/` | Rebuildable temporary media | Extracted embedded motion-photo video; cleared by privacy reset |
| `c2patool/` | Rebuildable service metadata | Generated no-network settings for the optional C2PA tool; cleared by privacy reset |

The confirmed privacy reset clears every registry entry marked for reset. It preserves settings, imported models, smart collections, keyword sets, wallpaper, email drafts, writeback backups, and quarantined originals. Original image files outside app data are not removed by this action.

## What does **not** happen

- No telemetry, no analytics, no crash reporter that phones home. Crash dumps stay on your machine until you choose to attach them to a GitHub issue.
- No cloud sync, no account, no login.
- No image is uploaded, indexed remotely, or fingerprinted off-device.
- No EXIF/GPS data is transmitted.
- No file paths or filenames are transmitted.
- OCR, semantic search, face/object tools, and other local analysis run only after an explicit user action. Semantic search uses a model that the user imports through Model Manager; Images does not download models automatically, and local model inference does not contact a network service.
- Safety classification is additionally default-off: it runs only through an explicit `--safety-classify` command, exports its scores only to that command's stdout, and does not store results in source metadata, the catalog, labels, files, or application logs.
- No advertising SDKs, tracking pixels, or third-party JavaScript.
- No browser-style file-association telemetry. The optional installer's "Open with" registration writes `Software\RegisteredApplications` keys, which are local-only and removed cleanly on uninstall.

## How to verify

You can audit the network behavior end-to-end:

1. Open **Settings** → Privacy → "Check for updates automatically" — leave it unchecked. Every silent call stops. The About window exposes the same setting for convenience.
2. Watch `%LOCALAPPDATA%\Images\Logs\images-<date>.log`. Every update-check call writes a line there. There are no other network call sites in the codebase.
3. Run `Images.exe --system-info` to see the local files Images uses.
4. Search the source: `grep -r "HttpClient\|WebClient\|HttpRequestMessage" src/Images/` — should return only `Services/UpdateCheckService.cs`.

## Reporting concerns

If you find a network call or persisted file not documented here, that's a bug. Please open an issue at <https://github.com/SysAdminDoc/Images/issues> — security-sensitive findings can be filed as a private vulnerability report instead.

## Changes to this policy

This policy is versioned with the rest of the repository. Any change that adds a network call site, persists a new file, or expands what is logged must update this document in the same change as the code, and call it out in CHANGELOG.
