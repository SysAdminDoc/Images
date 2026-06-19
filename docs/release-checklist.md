# Release Checklist

Use this checklist before publishing a stable Images release. The automated gate is `scripts\Test-ReleaseReadiness.ps1`; the human audit items below explain what must be reconciled before that gate is allowed to pass into packaging.

## Current-State Audit

Before bumping or publishing a release:

1. Compare `README.md`, `CHANGELOG.md`, `ROADMAP.md`, and `Roadmap_Blocked.md`.
2. Check `git log --oneline` since the last release tag and confirm shipped user-facing work is represented in `CHANGELOG.md`.
3. Confirm the "Current verified state" and "Important gaps" sections do not repeat stale claims from the historical roadmap appendix.
4. Confirm any known local-only or ignored working files are intentionally excluded from the release commit.

## Shipped-Roadmap Closure Pass

For each shipped roadmap item:

1. Delete the completed roadmap row from `ROADMAP.md` (git history is the record).
2. Move blocked items to `Roadmap_Blocked.md` with a short blocker and unblock criterion.
3. If a roadmap item was only partially completed, narrow the remaining acceptance gate in the row.
4. Record evidence in the commit message or CHANGELOG entry: commit hash, release tag, workflow, or external source URL.

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
2. Run `scripts\Prepare-JpegTranBundle.ps1 -Force` or confirm the workflow staged the approved libjpeg-turbo artifact from `src\Images\Codecs\JpegTran\PROVENANCE.md`, including `jpegtran.exe` and adjacent `jpeg62.dll`.
3. Confirm optional runtime provenance for Ghostscript and any staged `jpegtran.exe` artifact.
4. Run `scripts\Test-ReleaseDiagnostics.ps1` against the portable and installed outputs. The workflow stores the resulting logs as a `release-diagnostics-*` artifact.
5. Confirm the installer, portable zip, and checksum file names match the release tag.
6. Do not mutate assets attached to an already-published version; publish a patch release instead.
