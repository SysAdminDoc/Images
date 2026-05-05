# Lossless JPEG transform policy

Status: scoped for roadmap `V30-02`; runtime integration blocked until an approved `jpegtran.exe` artifact exists.
Date: 2026-05-05

## Decision

Images may support lossless JPEG rotate and crop only through an app-local `jpegtran.exe` sidecar after the exact binary passes the optional-runtime review gate in `docs/integration-policy.md`.

No runtime binary is committed to source control in this slice. The shipped code only contains deterministic MCU-alignment planning so future UI and shell-out work can warn users before a lossless transform trims pixels.

## User contract

- Lossless JPEG writeback is opt-in and JPEG-only.
- The app must not overwrite the source file without an explicit confirmation path and rollback/recovery coverage.
- Crop writeback must preserve the user's requested rectangle when it is MCU-aligned.
- When a crop or rotation would require `jpegtran -trim`, Images must show the exact pixel trim before running the command.
- If the selected crop cannot contain at least one aligned MCU block, Images must direct the user to normal export/re-encode instead of silently changing the image.
- Metadata preservation must be explicit: EXIF orientation is normalized with the transform result, XMP sidecars remain authoritative for app edits, and any original-file metadata write path needs the same confirmation and backup rules as ExifTool.

## Runtime contract

Required before enabling the feature:

| Field | Required answer |
| --- | --- |
| Name and version | Exact `jpegtran.exe` build from libjpeg-turbo. |
| Source URL | Canonical upstream release URL and source tag. |
| License | BSD-style libjpeg-turbo license, with bundled license text. |
| Redistribution permission | Confirmed for the exact Windows artifact. |
| Binary provenance | SHA-256 recorded in release notes and shown in diagnostics. |
| Process boundary | Child process through `CreateProcess`; no in-process native loading. |
| Network behavior | No network access; process runs only against app temp/input/output files. |
| Failure mode | Calm status explaining missing runtime, failed transform, or canceled trim. |
| Test corpus | Generated JPEGs covering 4:2:0 and 4:4:4 MCU sizes, odd dimensions, EXIF orientation, and metadata sidecars. |

## Shell-out rules

- Resolve only app-local `Codecs\JpegTran\jpegtran.exe` or an explicit developer override. Do not auto-download.
- Use a temp output file in the same volume as the target when replacing originals.
- Pass arguments through `ProcessStartInfo.ArgumentList`; never build a shell string.
- Time out and kill the process tree if the transform stalls.
- Treat non-zero exit codes, missing output, empty output, or unchanged temp files as failures.
- Replace originals atomically only after the output validates as JPEG and rollback data exists.

## MCU planning

`LosslessJpegTransformPolicy` deliberately trims inward when a requested crop does not align to the JPEG MCU grid. This avoids writing pixels outside the user's selected area and gives the UI exact copy for a confirmation dialog.

The actual runtime path must replace the conservative default with the MCU size parsed from the JPEG sampling factors before presenting the final confirmation.

## Completion criteria for `V30-02`

- Approved and bundled `jpegtran.exe` artifact with license and SHA-256 provenance.
- Diagnostics surface for runtime path, version, and hash.
- UI commands for rotate 90/180/270 and crop writeback that are disabled or explanatory when the runtime is unavailable.
- Confirmation dialog for any MCU trim, including the exact edge pixel counts.
- Tests for command construction, unavailable runtime, trim confirmation, failed process output, atomic replacement, and metadata/sidecar preservation.
