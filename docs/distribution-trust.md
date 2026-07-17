# Distribution Trust Plan

Status: `IP-17A` decision record  
Date: 2026-05-05  
Scope: packaging, package-manager submission, checksum continuity, and the permanent unsigned-release policy.

## Current Official Distribution

The only official distribution channel today is GitHub Releases:

- `Images-vX.Y.Z-win-x64.zip`
- `Images-vX.Y.Z-setup-win-x64.exe`
- `Images-vX.Y.Z-checksums.txt`

Both artifacts are produced locally from the same source checkout. `scripts\Test-ReleaseReadiness.ps1` is the release gate for version sync, restore, build, tests, vulnerable-package scanning, localization parity, release diagnostics, checksums, and package-manifest validation. SBOM/provenance bundle generation is tracked separately and is not currently promised as a shipped artifact.

## Trust Goals

- Keep GitHub Releases as the source of truth for bytes and checksums.
- Add WinGet and Scoop only as install/update entry points that point back to official release artifacts.
- Keep checksum validation in every release and every package-manager manifest.
- State plainly that official artifacts are intentionally unsigned and may trigger SmartScreen reputation warnings.
- Use GitHub release provenance plus SHA-256 continuity as the public verification contract.

## Release Artifact Requirements

Before submitting any package-manager manifest:

- The release tag, app version, installer filename, portable ZIP filename, and checksum file must agree.
- The installer and portable ZIP must be uploaded to GitHub Releases before manifest submission.
- SHA-256 values in the package manifests must be copied from `Images-vX.Y.Z-checksums.txt`, not recomputed by hand from a different machine.
- Silent installer behavior must be validated for the exact Inno artifact:
  - Install: `/VERYSILENT /NORESTART`
  - Uninstall: use the registered Inno uninstaller with silent arguments.
- The portable ZIP must launch `Images.exe` from its extracted directory without registry writes.
- `Images.exe --system-info` must pass on the published build.

## WinGet Scope

Microsoft's Windows Package Manager flow uses manifests submitted to the public `microsoft/winget-pkgs` repository. Microsoft documents `wingetcreate new` as the guided manifest creation path and `winget validate <path-to-the-manifests>` plus sandbox testing before pull request submission.

### Initial Package

| Field | Planned value |
| --- | --- |
| PackageIdentifier | `SysAdminDoc.Images` |
| PackageName | `Images` |
| Publisher | `SysAdminDoc` |
| InstallerType | `inno` |
| Architecture | `x64` |
| InstallerUrl | GitHub Release installer URL |
| InstallerSha256 | SHA-256 from release checksum file |
| License | `MIT` |
| PackageUrl | GitHub repository URL |
| PrivacyUrl | `docs/privacy-policy.md` canonical URL |

### Submission Steps

1. Publish and verify the GitHub Release.
2. Install or update `wingetcreate`.
3. Generate the manifest from the release installer URL.
4. Validate locally with `winget validate`.
5. Run the winget-pkgs sandbox test for the manifest folder.
6. Submit a pull request to `microsoft/winget-pkgs`.
7. Wait for automated validation and moderator review before announcing WinGet availability.

### Update Steps

- Use `wingetcreate update SysAdminDoc.Images -u <installer-url> -v <version>` after each stable release.
- Do not update WinGet for failed, yanked, or draft releases.
- If an installer is re-uploaded for the same version, treat that as a release incident: publish a new patch version instead of mutating package-manager hashes.

## Scoop Scope

Scoop manifests are JSON files that describe how to install an app. The Scoop documentation lists `version`, `url`, `hash`, architecture-specific values, `shortcuts`, `bin`, installer directives, and autoupdate metadata as manifest primitives. Known buckets such as `extras` can be autoupdated by Scoop's automation.

### Recommended Channel

Start with the portable ZIP in the Scoop `extras` bucket. It matches Scoop's strengths better than the Inno installer:

- No elevation required.
- No registry writes.
- Easy update by replacing the extracted app directory.
- Shortcut can point directly at `Images.exe`.

### Initial Manifest Shape

```json
{
  "version": "X.Y.Z",
  "description": "Local-first Windows image viewer with broad codec support and inline rename.",
  "homepage": "https://github.com/SysAdminDoc/Images",
  "license": "MIT",
  "architecture": {
    "64bit": {
      "url": "https://github.com/SysAdminDoc/Images/releases/download/vX.Y.Z/Images-vX.Y.Z-win-x64.zip",
      "hash": "<sha256-from-release-checksums>"
    }
  },
  "bin": "Images.exe",
  "shortcuts": [
    [
      "Images.exe",
      "Images"
    ]
  ],
  "checkver": {
    "github": "https://github.com/SysAdminDoc/Images"
  },
  "autoupdate": {
    "architecture": {
      "64bit": {
        "url": "https://github.com/SysAdminDoc/Images/releases/download/v$version/Images-v$version-win-x64.zip"
      }
    }
  }
}
```

### Submission Steps

1. Publish and verify the GitHub Release.
2. Draft the manifest against the portable ZIP, not the installer.
3. Test install locally from a custom bucket or local manifest.
4. Verify `Images.exe --system-info` from the Scoop install path.
5. Submit to Scoop `extras` if the package meets bucket expectations.
6. If `extras` is rejected, maintain a project-owned bucket rather than weakening the artifact model.

## Permanent Unsigned-Release Decision

### Decision

Images does not sign software on any platform. Do not acquire a certificate, create a signing account, add a signing workflow, or treat signing/SmartScreen reputation as a release or v1.0 gate.

WinGet and Scoop manifests remain valid for unsigned GitHub-hosted artifacts because their install records pin the release URL and SHA-256. Microsoft Store publication is an optional account-gated channel with Store-managed packaging policy; it does not change the unsigned Inno/portable contract.

## User Verification Copy

Release notes should keep a short verification block:

```powershell
Get-FileHash .\Images-vX.Y.Z-win-x64.zip -Algorithm SHA256
Get-FileHash .\Images-vX.Y.Z-setup-win-x64.exe -Algorithm SHA256
```

Release notes must say the build is intentionally unsigned and direct users to the published checksum file.

## Open Follow-Ups

- ~~Create the first WinGet manifest after the next stable release is published.~~ Shipped — `scripts/New-PackageManifests.ps1` generates WinGet multi-file manifests from the local checksum file.
- ~~Create the first Scoop manifest after the next stable release is published.~~ Shipped — same script generates a Scoop portable manifest with autoupdate metadata.
- Add package-manager installation smoke tests to release validation after the first accepted manifests.

## Sources

- Microsoft Learn: WinGet manifest creation - <https://learn.microsoft.com/en-us/windows/package-manager/package/manifest>
- Microsoft Learn: WinGet repository submission - <https://learn.microsoft.com/en-us/windows/package-manager/package/repository>
- Scoop Wiki: App manifests - <https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests>
- Scoop Wiki: App manifest autoupdate - <https://github.com/ScoopInstaller/Scoop/wiki/App-Manifest-Autoupdate>
