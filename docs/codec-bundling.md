# Codec bundling

Images ships WIC and Magick.NET support with the app. Official release artifacts also ship Ghostscript app-local so PDF, EPS, PS, and AI previews work on clean machines.

## App-local layout

Place the approved runtime under:

```text
src/Images/Codecs/Ghostscript/
```

Typical 64-bit layout:

```text
src/Images/Codecs/Ghostscript/bin/gsdll64.dll
src/Images/Codecs/Ghostscript/bin/gswin64c.exe
```

`gsdll64.dll` is the required file for Magick.NET. `gswin64c.exe` is optional and only used for displaying the Ghostscript version in About.

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

The project copies `src/Images/Codecs/Ghostscript/**` into build and publish output. The installer packages the self-contained published output, so app-local Ghostscript files are included automatically once present during publish.

The GitHub release workflow also has an optional `ghostscript_bundle_url` input. Point it at an approved private/runtime zip and the workflow will unpack it, locate `gsdll64.dll` or `gsdll32.dll`, prepare the app-local bundle, then publish the portable zip and installer with the runtime included.

## Source-control policy

Runtime binaries are intentionally ignored by `.gitignore`; only the placeholder README is tracked. Release artifacts may include the runtime after license review, but do not commit Ghostscript binaries to git.

Ghostscript is available under the GNU AGPL or a commercial Artifex license. If the AGPL distribution is bundled, keep `Codecs\Ghostscript\doc\COPYING` in the app output and attach or link the matching source archive in the GitHub release.

## Verifying provenance at runtime

The shipped app reports the active decoder runtime through three matching surfaces:

- About → **Runtime provenance** card. Shows Magick.NET version + assembly path, Ghostscript availability, source label (`bundled`/`IMAGES_GHOSTSCRIPT_DIR`/`installed`), Ghostscript version (when `gswin*c.exe` is present), absolute DLL path, and the SHA-256 of the loaded `gsdll64.dll` / `gsdll32.dll`.
- About → **Codec report** button. Copies the same data to the clipboard alongside the per-format capability matrix.
- `Images.exe --system-info` and `Images.exe --codec-report`. Print the same content to stdout for support tickets, CI smoke tests, and offline diagnostics.

Compare the reported SHA-256 against the hash recorded for the approved Ghostscript redistributable in your release notes. A drift means the bundled DLL is not the package that was reviewed — investigate before shipping.
