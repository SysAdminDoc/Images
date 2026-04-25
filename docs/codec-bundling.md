# Codec bundling

Images ships WIC and Magick.NET support with the app. PDF, EPS, PS, and AI previews also need a Ghostscript runtime available beside the executable.

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

From a machine with the approved Ghostscript runtime available:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Prepare-GhostscriptBundle.ps1 -Source "C:\Program Files\gs\gs10.05.1" -Force
dotnet publish src/Images -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
```

If `-Source` is omitted, the script checks `IMAGES_GHOSTSCRIPT_DIR` and standard `%ProgramFiles%\gs\gs*` installs.

The project copies `src/Images/Codecs/Ghostscript/**` into build and publish output. The installer packages the self-contained published output, so app-local Ghostscript files are included automatically once present during publish.

The GitHub release workflow also has an optional `ghostscript_bundle_url` input. Point it at an approved private/runtime zip and the workflow will unpack it, locate `gsdll64.dll` or `gsdll32.dll`, prepare the app-local bundle, then publish the portable zip and installer with the runtime included.

## Source-control policy

Runtime binaries are intentionally ignored by `.gitignore`; only the placeholder README is tracked. Do not commit Ghostscript binaries unless the release owner has confirmed redistribution rights for the exact package and license model being shipped.
