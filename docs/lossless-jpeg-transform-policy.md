# Lossless JPEG transform policy

Status: runtime resolver/provenance, exact aligned crop writeback, exact aligned rotation writeback, interactive trim confirmation, approved artifact provenance, license files, staging script, and release diagnostics smoke shipped.
Date: 2026-05-17

## Decision

Images may support lossless JPEG rotate and crop only through an app-local `jpegtran.exe` sidecar after the exact runtime files pass the optional-runtime review gate in `docs/integration-policy.md`.

No runtime binary is committed to source control. The shipped code contains deterministic MCU-alignment planning, an optional runtime resolver that recognizes only `Codecs\JpegTran\jpegtran.exe` with its adjacent `jpeg62.dll` or the explicit `IMAGES_JPEGTRAN_EXE` developer override, guarded writeback paths for a single exact or confirmed-trim JPEG crop/right-angle rotation, and an interactive choice between trimmed lossless writeback and exact raster re-encode. About, `--system-info`, and `--codec-report` report path, source, version, and SHA-256 when a runtime is present. Official release packaging stages the reviewed libjpeg-turbo artifact with `scripts\Prepare-JpegTranBundle.ps1` before publish.

## User contract

- Lossless JPEG writeback is opt-in and JPEG-only.
- The app must not overwrite the source file without an explicit confirmation path and rollback/recovery coverage.
- Crop writeback preserves the user's requested rectangle when it is MCU-aligned. Rotation writeback preserves the whole image when the dimensions are aligned for the requested right-angle rotation. The current runtime path attempts this only for a single enabled crop or rotate operation with no EXIF orientation transform and falls back to normal raster overwrite otherwise.
- When a crop or rotation would require edge trimming, Images shows the exact pixel trim and runs trimmed `jpegtran` output only after the user chooses the lossless trimmed path. Choosing exact output uses the normal raster re-encode path instead.
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

- Version: libjpeg-turbo 3.1.4.1.
- Source: https://github.com/libjpeg-turbo/libjpeg-turbo
- Release artifact: `libjpeg-turbo-3.1.4.1-vc-x64.exe` from https://github.com/libjpeg-turbo/libjpeg-turbo/releases/tag/3.1.4.1
- Release artifact SHA-256: `2bb347f106473c12635bdd414b1f289de9f4d6dea4a496d3f9dd212db9eda0dc`.
- Extracted `jpegtran.exe` SHA-256: `2000c205ed99fe2409e42a6cb87c19d88e33e516d5d40ff11bb19b7830e3ee33`.
- Extracted `jpeg62.dll` SHA-256: `fc55317c9dee01f0f04a2a669824429086c5d55aa13ad901e2a3bbab33c80853`.
- Source archive: `libjpeg-turbo-3.1.4.1.tar.gz`, SHA-256 `ecae8008e2cc9ade2f2c1bb9d5e6d4fb73e7c433866a056bd82980741571a022`.
- Official binaries documentation: https://libjpeg-turbo.org/Documentation/OfficialBinaries
- License: BSD-style libjpeg-turbo license, with license text from https://github.com/libjpeg-turbo/libjpeg-turbo/blob/main/LICENSE.md.
- Redistribution permission: approved for release staging under the tracked `LICENSE.md` and `README.ijg` files in `Codecs\JpegTran`.
- Source-use boundary: child-process shell-out only; no in-process native linking and no source copied into Images.
- Update cadence: monitor libjpeg-turbo GitHub releases and security advisories before each Images release that bundles jpegtran.
- CVE/advisory tracking: GitHub security advisories for `libjpeg-turbo/libjpeg-turbo`.
- Binary provenance: `src\Images\Codecs\JpegTran\PROVENANCE.md` records artifact URL, artifact SHA-256, source archive SHA-256, extracted executable/dependency paths, and extracted executable/dependency SHA-256 values. Release diagnostics must show the same executable hash.
- Process boundary: child process through `CreateProcess` / `ProcessStartInfo.ArgumentList`.
- File access boundary: source JPEG plus same-volume temp output and rollback files only.
- Network behavior: no network access; Images never downloads the runtime automatically.
- Failure mode: disabled or explanatory UI when unavailable; calm transform-failed status on non-zero exit, invalid output, missing output, or timeout.
- Test corpus: generated JPEGs for MCU planning plus future shell-out tests using a fake process seam and at least one present-runtime smoke case.
- Release impact: app-local sidecar size only when bundled; no startup cost beyond diagnostics probing.
- Decision: accepted for release staging through `scripts\Prepare-JpegTranBundle.ps1`; runtime binary remains ignored by git and is added only to build/publish output.
- Owner: Images release maintainer.

## Shell-out rules

- Resolve only app-local `Codecs\JpegTran\jpegtran.exe` with adjacent `jpeg62.dll`, or an explicit developer override. Do not auto-download.
- If `IMAGES_JPEGTRAN_EXE` is set but invalid, report the override as unavailable instead of silently falling back to an app-local binary.
- Use a temp output file in the same volume as the target when replacing originals.
- Pass arguments through `ProcessStartInfo.ArgumentList`; never build a shell string.
- Current exact-crop command shape is `jpegtran -copy icc -crop WxH+X+Y -outfile <temp.jpg> <source.jpg>`. Confirmed trimmed crops use the inward-aligned `WxH+X+Y` shown to the user. Current exact-rotation command shape is `jpegtran -copy icc -rotate 90|180|270 -outfile <temp.jpg> <source.jpg>`, and confirmed trimmed rotations add `-trim` before `-rotate`. libjpeg-turbo documents `-copy all` as preserving embedded thumbnails as-is, so Images intentionally copies ICC only to avoid Explorer or other shell views showing stale pre-transform thumbnails from embedded metadata. See libjpeg-turbo `doc/usage.txt`: https://raw.githubusercontent.com/libjpeg-turbo/libjpeg-turbo/main/doc/usage.txt.
- Time out and kill the process tree if the transform stalls.
- Treat non-zero exit codes, missing output, empty output, invalid JPEG output, or dimension mismatches as failures.
- Replace originals atomically only after the output validates as JPEG and rollback data exists.

## MCU planning

`LosslessJpegTransformPolicy` deliberately trims inward when a requested crop does not align to the JPEG MCU grid. This avoids writing pixels outside the user's selected area and gives the UI exact copy for a confirmation dialog.

The actual runtime path must replace the conservative default with the MCU size parsed from the JPEG sampling factors before presenting the final confirmation.

## Completion criteria for `V30-02`

- Approved and bundled `jpegtran.exe` artifact plus required `jpeg62.dll` with license and SHA-256 provenance. Shipped 2026-05-17 through release staging script and tracked provenance/license files.
- Diagnostics surface for runtime path, version, and hash. Shipped 2026-05-14 for app-local/override detection.
- Exact MCU-aligned JPEG crop shell-out, output validation, same-volume atomic replacement, rollback cleanup, and fake-process tests. Shipped 2026-05-14.
- UI command for right-angle rotation writeback with exact aligned JPEG shell-out, output validation, same-volume atomic replacement, rollback cleanup, and fake-process tests. Shipped 2026-05-14.
- Confirmation dialog for any MCU trim, including the exact edge pixel counts. Shipped 2026-05-14.
- Release diagnostics smoke for staged runtime in portable and installed outputs. Shipped 2026-05-17 through `scripts\Test-ReleaseDiagnostics.ps1` and now run through local release readiness.
