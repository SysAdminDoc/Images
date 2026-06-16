# libjpeg-turbo jpegtran provenance

Status: approved release-staging artifact; executable is not committed to git.
Reviewed: 2026-05-17

## Approved Artifact

- Project: libjpeg-turbo
- Version: 3.1.4.1
- Release: https://github.com/libjpeg-turbo/libjpeg-turbo/releases/tag/3.1.4.1
- Windows artifact: https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/3.1.4.1/libjpeg-turbo-3.1.4.1-vc-x64.exe
- Windows artifact SHA-256: `2bb347f106473c12635bdd414b1f289de9f4d6dea4a496d3f9dd212db9eda0dc`
- Extracted executable path in artifact: `bin\jpegtran.exe`
- Extracted `jpegtran.exe` SHA-256: `2000c205ed99fe2409e42a6cb87c19d88e33e516d5d40ff11bb19b7830e3ee33`
- Extracted dependency path in artifact: `bin\jpeg62.dll`
- Extracted `jpeg62.dll` SHA-256: `fc55317c9dee01f0f04a2a669824429086c5d55aa13ad901e2a3bbab33c80853`
- Source archive: https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/3.1.4.1/libjpeg-turbo-3.1.4.1.tar.gz
- Source archive SHA-256: `ecae8008e2cc9ade2f2c1bb9d5e6d4fb73e7c433866a056bd82980741571a022`

## License Files

These files are tracked next to this provenance note and must be copied with any staged runtime:

- `LICENSE.md`
- `README.ijg`

## Release Staging

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Prepare-JpegTranBundle.ps1 -Force
```

The script downloads only the HTTPS artifact above, verifies the installer SHA-256, extracts `bin\jpegtran.exe` and `bin\jpeg62.dll`, verifies both SHA-256 values, and stages the complete runtime under `src\Images\Codecs\JpegTran` for build/publish output. Runtime binaries remain ignored by git.

After staging, verify:

```powershell
dotnet build Images.sln -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Test-ReleaseDiagnostics.ps1 -PortableDir src\Images\bin\Release\net9.0-windows10.0.22621.0 -RequireGhostscript -RequireJpegTran
```
