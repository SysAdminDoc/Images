# Codec bundling

Images ships WIC and Magick.NET support with the app. Official release artifacts also ship Ghostscript app-local so PDF, EPS, PS, and AI previews work on clean machines. Lossless JPEG writeback can use an app-local libjpeg-turbo `jpegtran.exe` sidecar for exact MCU-aligned crop overwrite when that runtime is present, but the current source tree contains only the resolver, writeback code, tests, and placeholder docs. It does not contain the runtime binary.

## App-local layout

Place the approved Ghostscript runtime under:

```text
src/Images/Codecs/Ghostscript/
```

Typical 64-bit layout:

```text
src/Images/Codecs/Ghostscript/bin/gsdll64.dll
src/Images/Codecs/Ghostscript/bin/gswin64c.exe
```

`gsdll64.dll` is the required file for Magick.NET. `gswin64c.exe` is optional and only used for displaying the Ghostscript version in About.

Place the approved jpegtran runtime under:

```text
src/Images/Codecs/JpegTran/jpegtran.exe
src/Images/Codecs/JpegTran/LICENSE.md
src/Images/Codecs/JpegTran/README.ijg
```

`jpegtran.exe` is resolved only from this app-local folder or the explicit `IMAGES_JPEGTRAN_EXE` developer override. Images does not search PATH and does not auto-download codec runtimes. When available, the current crop path uses a same-folder temp output and atomic replacement for exact MCU-aligned JPEG crops only; other crop writes use the normal raster overwrite path.

## Prepare a release bundle

Official releases currently bundle Ghostscript 10.07.0 from Artifex's `gs10070` release.

Approved upstream artifacts:

- Runtime: `gs10070w64.exe`
- Runtime SHA-256: `8af854e2d62f9a3a674331321b347118a83928a3726631e458194121cf3bbeec`
- Source: `ghostscript-10.07.0.tar.xz`
- Source SHA-256: `ddace4e1721f967a55039baff564840225e0baa1d4f5432247ca1ccd1473b7c1`

Extract the runtime installer without installing it globally, then stage the extracted runtime:

```powershell
gh release download gs10070 --repo ArtifexSoftware/ghostpdl-downloads --pattern gs10070w64.exe --pattern ghostscript-10.07.0.tar.xz --dir .tmp-ghostscript-bundle
& "C:\Program Files\7-Zip\7z.exe" x .tmp-ghostscript-bundle\gs10070w64.exe -o.tmp-ghostscript-bundle\extracted -y
powershell -ExecutionPolicy Bypass -File scripts/Prepare-GhostscriptBundle.ps1 -Source ".tmp-ghostscript-bundle\extracted" -Force
dotnet publish src/Images -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
```

From a machine with an already-approved Ghostscript runtime available:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Prepare-GhostscriptBundle.ps1 -Source "C:\Program Files\gs\gs10.07.0" -Force
dotnet publish src/Images -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
```

If `-Source` is omitted, the script checks `IMAGES_GHOSTSCRIPT_DIR` and standard `%ProgramFiles%\gs\gs*` installs.

The project copies `src/Images/Codecs/Ghostscript/**` and `src/Images/Codecs/JpegTran/**` into build and publish output. The installer packages the self-contained published output, so app-local runtimes are included automatically once present during publish.

The GitHub release workflow also has an optional `ghostscript_bundle_url` input. Point it at an approved private/runtime zip and the workflow will unpack it, locate `gsdll64.dll` or `gsdll32.dll`, prepare the app-local bundle, then publish the portable zip and installer with the runtime included.

## Source-control policy

Runtime binaries are intentionally ignored by `.gitignore`; only placeholder README files are tracked. Release artifacts may include runtimes after license review, but do not commit Ghostscript or jpegtran binaries to git.

Ghostscript is available under the GNU AGPL or a commercial Artifex license. If the AGPL distribution is bundled, keep `Codecs\Ghostscript\doc\COPYING` in the app output and attach or link the matching source archive in the GitHub release.

libjpeg-turbo is BSD-style licensed. If jpegtran is bundled, keep the libjpeg-turbo license/readme files in `Codecs\JpegTran`, record the exact upstream release URL and staged `jpegtran.exe` SHA-256 in release notes, and confirm the staged executable matches the reviewed artifact before publishing.

## Verifying provenance at runtime

The shipped app reports the active decoder runtime through three matching surfaces:

- About → **Runtime provenance** card. Shows Magick.NET version + assembly path, Ghostscript availability, source label (`bundled`/`IMAGES_GHOSTSCRIPT_DIR`/`installed`), Ghostscript version (when `gswin*c.exe` is present), absolute DLL path, and the SHA-256 of the loaded `gsdll64.dll` / `gsdll32.dll`. It also shows jpegtran availability, source label (`app-local Codecs\JpegTran` or `IMAGES_JPEGTRAN_EXE`), executable path, version, and SHA-256 when present.
- About → **Codec report** button. Copies the same data to the clipboard alongside the per-format capability matrix.
- `Images.exe --system-info` and `Images.exe --codec-report`. Print the same content to stdout for support tickets, CI smoke tests, and offline diagnostics.

Compare the reported SHA-256 against the hash recorded for the approved Ghostscript redistributable in your release notes. A drift means the bundled DLL is not the package that was reviewed — investigate before shipping.
