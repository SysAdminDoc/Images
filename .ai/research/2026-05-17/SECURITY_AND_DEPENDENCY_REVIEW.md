# Security And Dependency Review

Date: 2026-05-17

## Summary

The only actionable vulnerable NuGet finding during this run was SharpCompress 0.47.4. It was upgraded to 0.48.1 and the vulnerability gate was re-run successfully.

The affected advisory, GHSA-6c8g-7p36-r338 / CVE-2026-44788, concerns path traversal in the `WriteToDirectory()` extraction helper. Images uses SharpCompress for read-only archive page streams and local source search found no `WriteToDirectory()` use in `src`. Upgrading is still correct because the project treats a clean vulnerability gate as a release requirement.

Follow-up on 2026-05-17 closed the first hardening opportunity from this review: About, `--system-info`, and `--codec-report` now share a structured dependency/runtime/model provenance surface with source URLs, versions, paths, SHA-256 values where available, advisory status, and missing-runtime action copy.

## Local Package State

Main app package references from `src/Images/Images.csproj`:

| Package | Local version | Review |
| --- | ---: | --- |
| `Magick.NET-Q16-AnyCPU` | 14.13.0 | Current/recent NuGet version; previous vulnerable floors are below this. |
| `Magick.NET.Core` | 14.13.0 | Kept aligned with `Magick.NET-Q16-AnyCPU`. |
| `Microsoft.Data.Sqlite` | 9.0.0 | Outdated; evaluate with .NET 10 package strategy rather than automatic major update. |
| `Microsoft.Extensions.Logging` | 9.0.0 | Outdated; evaluate with .NET 10 package strategy. |
| `Microsoft.VisualBasic` | 10.3.0 | Framework helper dependency used by the app; keep unless a build warning or advisory requires change. |
| `Serilog` | 4.2.0 | Minor update to 4.3.1 available. Low priority unless release notes or advisories require it. |
| `Serilog.Extensions.Logging` | 9.0.0 | 10.x available; coordinate with framework/package strategy. |
| `Serilog.Sinks.File` | 6.0.0 | 7.x available; update should be regression-tested against log retention/concurrent read behavior. |
| `SharpCompress` | 0.48.1 | Upgraded in this run to clear GHSA-6c8g-7p36-r338 / CVE-2026-44788. |

Test package opportunities:

| Package | Local version | Update | Recommendation |
| --- | ---: | ---: | --- |
| `coverlet.collector` | 6.0.2 | 10.0.0 | Defer unless CI coverage format changes are reviewed. |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | 18.5.1 | Candidate after full test run. |
| `xunit` | 2.9.2 | 2.9.3 | Low-risk patch candidate. |
| `xunit.runner.visualstudio` | 2.8.2 | 3.1.5 | Major runner jump; test in CI and local VS discovery. |

## Vulnerability Gate Evidence

Before the fix:

- `dotnet list Images.sln package --vulnerable --include-transitive` flagged `SharpCompress 0.47.4` with moderate advisory `GHSA-6c8g-7p36-r338`.

Change made:

- `SharpCompress` updated to `0.48.1` in `src/Images/Images.csproj`.
- `CHANGELOG.md` updated under Unreleased Security.
- `docs/archive-runtime-review.md` updated to the new version and advisory context.
- `docs/integration-policy.md` updated to the new accepted integration row.

After the fix:

```text
The given project `Images` has no vulnerable packages given the current sources.
The given project `Images.Tests` has no vulnerable packages given the current sources.
```

## Runtime Sidecars

### Ghostscript

Current project claim:

- Release artifacts bundle Ghostscript 10.07.0 app-local under `Codecs\Ghostscript`.
- The matching source archive is attached to the GitHub release.
- License is installed at `Codecs\Ghostscript\doc\COPYING`.

External evidence:

- Ghostscript release page lists Ghostscript 10.07.0 as the latest release dated 2026-03-16.
- Ghostscript maintains a CVE/fixes page.

Recommendations:

- Keep Ghostscript version, source URL, binary SHA-256, source SHA-256, license path, and smoke result in release notes.
- Add a recurring or release-time advisory check against Ghostscript CVE pages.
- Keep binaries out of source control unless the exact redistribution path is approved.

### jpegtran

Current project state:

- The app can resolve an optional app-local libjpeg-turbo `jpegtran.exe` sidecar or `IMAGES_JPEGTRAN_EXE`.
- Diagnostics expose path, version, and SHA-256.
- Lossless JPEG crop/rotation writeback can use the runtime when available.
- Follow-up on 2026-05-17 approved libjpeg-turbo 3.1.4.1 `libjpeg-turbo-3.1.4.1-vc-x64.exe` for release staging, with installer SHA-256 `2bb347f106473c12635bdd414b1f289de9f4d6dea4a496d3f9dd212db9eda0dc` and extracted `jpegtran.exe` SHA-256 `2000c205ed99fe2409e42a6cb87c19d88e33e516d5d40ff11bb19b7830e3ee33`.

Release policy:

- The runtime binary remains intentionally ignored by git. Release packaging stages it through `scripts\Prepare-JpegTranBundle.ps1`; `scripts\Test-ReleaseDiagnostics.ps1` and the release workflow validate portable and installed diagnostics after staging.

### OCR

Current project state:

- Uses Windows.Media.Ocr and installed Windows OCR language capabilities.
- Installer provisions UI language and `en-US` fallback where needed.

Recommendation:

- Keep OCR capability status in diagnostics and release smoke tests.
- Do not bundle Microsoft OCR language packs.

## Platform Support

External evidence:

- Official .NET support policy says .NET 9 is STS, active, and supported until 2026-11-10.
- .NET 10 is LTS and active until 2028-11-14.

Recommendation:

- Schedule a .NET 10 migration spike before .NET 9 enters late lifecycle.
- Do not mix major Microsoft.Extensions 10.x package updates into the current app without a build/test/package smoke pass.

## Hardening Opportunities

1. Expand release smoke to launch `--system-info` and `--codec-report` from both portable and installed outputs.
2. Add assertions over the new dependency provenance rows in release smoke, including Ghostscript presence, jpegtran status, and model runtime disabled state.
3. Add source-scanning assertions for known dangerous APIs where possible:
   - no SharpCompress `WriteToDirectory()` in app code.
   - no unreviewed native sidecar resolver.
   - no automatic model download path.
4. Add optional-runtime test fixtures that can simulate missing, system, app-local, bad-hash, and wrong-version runtimes.
5. Add a `docs/runtime-provenance.md` ledger or generate it from release metadata.

## Residual Risk

- Magick.NET bundles native codecs. NuGet advisory gates help, but release builds should still track ImageMagick/Magick.NET release notes.
- Ghostscript processes complex document formats and has recurring CVE history. The app-local bundle must stay current.
- Future local model runtimes can introduce large binaries, derived user data, and hardware-specific failure modes. Do not implement model features before model provenance, delete controls, and opt-in download/import UX exist.
