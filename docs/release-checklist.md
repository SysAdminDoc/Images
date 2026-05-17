# Release Checklist

Use this checklist before publishing a stable Images release. The automated gate is `scripts\Test-ReleaseReadiness.ps1`; the human audit items below explain what must be reconciled before that gate is allowed to pass into packaging.

## Current-State Audit

Before bumping or publishing a release:

1. Compare `README.md`, `CHANGELOG.md`, `PROJECT_CONTEXT.md`, and the current top section of `ROADMAP.md`.
2. Check `git log --oneline` since the last release tag and confirm shipped user-facing work is represented in `CHANGELOG.md`.
3. Confirm the "Current verified state" and "Important gaps" sections do not repeat stale claims from the historical roadmap appendix.
4. Confirm any known local-only or ignored working files are intentionally excluded from the release commit.

## Shipped-Roadmap Closure Pass

For each shipped roadmap item:

1. Mark the corresponding roadmap row as `[x]`.
2. Move or summarize superseded historical claims instead of deleting source-heavy research sections.
3. Record evidence in the roadmap row or nearby notes: local file path, commit hash, release tag, workflow, or external source URL.
4. If a roadmap item was only partially completed, leave it unchecked and narrow the remaining acceptance gate.
5. Update `PROJECT_CONTEXT.md` when the next recommended work changes.

## Version/Date Consistency Check

Before running the release workflow:

1. Ensure `src/Images/Images.csproj`, `src/Images/app.manifest`, `installer/Images.iss`, and `README.md` agree on the release version.
2. Ensure every `CHANGELOG.md` release heading uses an actual `YYYY-MM-DD` date that is not in the future.
3. Ensure the target version has a changelog section or a populated `Unreleased` section that will be promoted before publish.
4. Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Test-ReleaseReadiness.ps1 -Version X.Y.Z
```

The GitHub release workflow runs the same gate before build, package, and upload steps.

## Runtime And Artifact Checks

1. Run the vulnerable package gate.
2. Run `scripts\Prepare-JpegTranBundle.ps1 -Force` or confirm the workflow staged the approved libjpeg-turbo artifact from `src\Images\Codecs\JpegTran\PROVENANCE.md`.
3. Confirm optional runtime provenance for Ghostscript and any staged `jpegtran.exe` artifact.
4. Run `scripts\Test-ReleaseDiagnostics.ps1` against the portable and installed outputs. The workflow stores the resulting logs as a `release-diagnostics-*` artifact.
5. Confirm the installer, portable zip, and checksum file names match the release tag.
6. Do not mutate assets attached to an already-published version; publish a patch release instead.
