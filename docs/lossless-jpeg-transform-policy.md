# Lossless JPEG transform policy

Status: runtime resolver/provenance, exact aligned crop writeback, and exact aligned rotation writeback shipped; bundling and trim confirmation remain blocked until an approved `jpegtran.exe` artifact is staged.
Date: 2026-05-14

## Decision

Images may support lossless JPEG rotate and crop only through an app-local `jpegtran.exe` sidecar after the exact binary passes the optional-runtime review gate in `docs/integration-policy.md`.

No runtime binary is committed to source control. The shipped code contains deterministic MCU-alignment planning, an optional runtime resolver that recognizes only `Codecs\JpegTran\jpegtran.exe` or the explicit `IMAGES_JPEGTRAN_EXE` developer override, and guarded writeback paths for a single exact MCU-aligned JPEG crop or right-angle rotation. About, `--system-info`, and `--codec-report` report path, source, version, and SHA-256 when a runtime is present.

## User contract

- Lossless JPEG writeback is opt-in and JPEG-only.
- The app must not overwrite the source file without an explicit confirmation path and rollback/recovery coverage.
- Crop writeback preserves the user's requested rectangle when it is MCU-aligned. Rotation writeback preserves the whole image when the dimensions are aligned for the requested right-angle rotation. The current runtime path attempts this only for a single enabled crop or rotate operation with no EXIF orientation transform and falls back to normal raster overwrite otherwise.
- When a crop or rotation would require `jpegtran -trim`, Images must show the exact pixel trim before running the command.
- If the selected crop cannot contain at least one aligned MCU block, Images must direct the user to normal export/re-encode instead of silently changing the image.
- Metadata preservation must be explicit: XMP sidecars remain authoritative for app edits, ICC color profiles are preserved in the current crop shell-out, and stale embedded JPEG thumbnails are not copied into cropped originals. Any future original-file metadata write path needs the same confirmation and backup rules as ExifTool.

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

## Integration review: libjpeg-turbo jpegtran

- Version: libjpeg-turbo 3.1.4.1 target artifact.
- Source: https://github.com/libjpeg-turbo/libjpeg-turbo
- Release artifact: `libjpeg-turbo-3.1.4.1-vc-x64.exe` from https://github.com/libjpeg-turbo/libjpeg-turbo/releases/tag/3.1.4.1
- Official binaries documentation: https://libjpeg-turbo.org/Documentation/OfficialBinaries
- License: BSD-style libjpeg-turbo license, with license text from https://github.com/libjpeg-turbo/libjpeg-turbo/blob/main/LICENSE.md.
- Redistribution permission: permissive license appears compatible, but release bundling still requires staging the exact artifact and carrying its license/readme files in `Codecs\JpegTran`.
- Source-use boundary: child-process shell-out only; no in-process native linking and no source copied into Images.
- Update cadence: monitor libjpeg-turbo GitHub releases and security advisories before each Images release that bundles jpegtran.
- CVE/advisory tracking: GitHub security advisories for `libjpeg-turbo/libjpeg-turbo`.
- Binary provenance: record SHA-256 for the staged `jpegtran.exe` in release notes and diagnostics. Do not claim a bundled runtime until the extracted executable hash is recorded.
- Process boundary: child process through `CreateProcess` / `ProcessStartInfo.ArgumentList`.
- File access boundary: source JPEG plus same-volume temp output and rollback files only.
- Network behavior: no network access; Images never downloads the runtime automatically.
- Failure mode: disabled or explanatory UI when unavailable; calm transform-failed status on non-zero exit, invalid output, missing output, or timeout.
- Test corpus: generated JPEGs for MCU planning plus future shell-out tests using a fake process seam and at least one present-runtime smoke case.
- Release impact: app-local sidecar size only when bundled; no startup cost beyond diagnostics probing.
- Decision: conditionally accepted resolver/provenance, exact aligned crop shell-out, and exact aligned rotation shell-out; bundling and trim-confirmation UI remain disabled until the reviewed artifact is staged.
- Owner: Images release maintainer.

## Shell-out rules

- Resolve only app-local `Codecs\JpegTran\jpegtran.exe` or an explicit developer override. Do not auto-download.
- If `IMAGES_JPEGTRAN_EXE` is set but invalid, report the override as unavailable instead of silently falling back to an app-local binary.
- Use a temp output file in the same volume as the target when replacing originals.
- Pass arguments through `ProcessStartInfo.ArgumentList`; never build a shell string.
- Current exact-crop command shape is `jpegtran -copy icc -crop WxH+X+Y -outfile <temp.jpg> <source.jpg>`. Current exact-rotation command shape is `jpegtran -copy icc -rotate 90|180|270 -outfile <temp.jpg> <source.jpg>`. libjpeg-turbo documents `-copy all` as preserving embedded thumbnails as-is, so Images intentionally copies ICC only to avoid Explorer or other shell views showing stale pre-transform thumbnails from embedded metadata. See libjpeg-turbo `doc/usage.txt`: https://raw.githubusercontent.com/libjpeg-turbo/libjpeg-turbo/main/doc/usage.txt.
- Time out and kill the process tree if the transform stalls.
- Treat non-zero exit codes, missing output, empty output, invalid JPEG output, or dimension mismatches as failures.
- Replace originals atomically only after the output validates as JPEG and rollback data exists.

## MCU planning

`LosslessJpegTransformPolicy` deliberately trims inward when a requested crop does not align to the JPEG MCU grid. This avoids writing pixels outside the user's selected area and gives the UI exact copy for a confirmation dialog.

The actual runtime path must replace the conservative default with the MCU size parsed from the JPEG sampling factors before presenting the final confirmation.

## Completion criteria for `V30-02`

- Approved and bundled `jpegtran.exe` artifact with license and SHA-256 provenance.
- Diagnostics surface for runtime path, version, and hash. Shipped 2026-05-14 for app-local/override detection.
- Exact MCU-aligned JPEG crop shell-out, output validation, same-volume atomic replacement, rollback cleanup, and fake-process tests. Shipped 2026-05-14.
- UI command for right-angle rotation writeback with exact aligned JPEG shell-out, output validation, same-volume atomic replacement, rollback cleanup, and fake-process tests. Shipped 2026-05-14.
- Confirmation dialog for any MCU trim, including the exact edge pixel counts.
- Remaining tests for trim confirmation and final staged-runtime smoke coverage.
