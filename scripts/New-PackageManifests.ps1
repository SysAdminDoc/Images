param(
    [Parameter(Mandatory)]
    [string]$Version,

    [Parameter(Mandatory)]
    [string]$ChecksumFile,

    [string]$OutputDir = "",

    [string]$RepositoryRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $RepositoryRoot "packaging"
    $OutputDir = Join-Path $OutputDir "output"
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

function Set-Utf8NoBomContent {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Value
    )

    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Value, $encoding)
}

if (-not ($Version -match '^\d+\.\d+\.\d+$')) {
    throw "Version must be in X.Y.Z format. Got: $Version"
}

if (-not (Test-Path -LiteralPath $ChecksumFile)) {
    throw "Checksum file not found: $ChecksumFile"
}

$checksumContent = Get-Content -LiteralPath $ChecksumFile -Encoding ascii
$hashes = @{}
foreach ($line in $checksumContent) {
    if ($line -match '^([A-Fa-f0-9]{64})\s+(.+)$') {
        $hashes[$Matches[2].Trim()] = $Matches[1].ToUpper()
    }
}

$zipName = "Images-v${Version}-win-x64.zip"
$setupName = "Images-v${Version}-setup-win-x64.exe"

if (-not $hashes.ContainsKey($zipName)) {
    throw "Checksum file missing entry for portable ZIP: $zipName"
}
if (-not $hashes.ContainsKey($setupName)) {
    throw "Checksum file missing entry for installer: $setupName"
}

$zipHash = $hashes[$zipName]
$setupHash = $hashes[$setupName]
$ghBase = "https://github.com/SysAdminDoc/Images/releases/download/v${Version}"

# --- WinGet manifests (multi-file format) ---

$wingetDir = Join-Path $OutputDir "winget"
$wingetDir = Join-Path $wingetDir "manifests"
$wingetDir = Join-Path $wingetDir "s"
$wingetDir = Join-Path $wingetDir "SysAdminDoc"
$wingetDir = Join-Path $wingetDir "Images"
$wingetDir = Join-Path $wingetDir $Version
New-Item -ItemType Directory -Path $wingetDir -Force | Out-Null

$versionManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.9.0.schema.json
PackageIdentifier: SysAdminDoc.Images
PackageVersion: ${Version}
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.9.0
"@

$installerManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.9.0.schema.json
PackageIdentifier: SysAdminDoc.Images
PackageVersion: ${Version}
MinimumOSVersion: 10.0.17763.0
InstallerType: inno
Scope: machine
InstallModes:
  - interactive
  - silent
  - silentWithProgress
InstallerSwitches:
  Silent: /VERYSILENT /NORESTART /SKIPOCR
  SilentWithProgress: /SILENT /NORESTART /SKIPOCR
  InstallLocation: /DIR="<INSTALLPATH>"
UpgradeBehavior: install
Installers:
  - Architecture: x64
    InstallerUrl: ${ghBase}/${setupName}
    InstallerSha256: ${setupHash}
ManifestType: installer
ManifestVersion: 1.9.0
"@

$localeManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.9.0.schema.json
PackageIdentifier: SysAdminDoc.Images
PackageVersion: ${Version}
PackageLocale: en-US
Publisher: SysAdminDoc
PublisherUrl: https://github.com/SysAdminDoc
PackageName: Images
PackageUrl: https://github.com/SysAdminDoc/Images
License: MIT
LicenseUrl: https://github.com/SysAdminDoc/Images/blob/main/LICENSE
ShortDescription: Local-first Windows image viewer with broad codec support and inline rename.
Description: >-
  A Windows 7-style classic image viewer reimagined in dark mode. Supports 100+
  formats via WIC and Magick.NET, inline rename-while-viewing with conflict
  resolution and undo, folder navigation with natural sort, animated GIF/WebP/APNG
  playback, text extraction via Windows OCR, archive book previews, gallery and
  review workflows, non-destructive editing, batch processing, and export with
  visual diff preview. Local-first with no cloud, no telemetry, and transparent
  network behavior.
Tags:
  - image-viewer
  - photo-viewer
  - image-editor
  - dark-mode
  - wpf
  - dotnet
  - ocr
  - batch-converter
  - local-first
ReleaseNotesUrl: https://github.com/SysAdminDoc/Images/releases/tag/v${Version}
ManifestType: defaultLocale
ManifestVersion: 1.9.0
"@

Set-Utf8NoBomContent -Path (Join-Path $wingetDir "SysAdminDoc.Images.yaml") -Value $versionManifest
Set-Utf8NoBomContent -Path (Join-Path $wingetDir "SysAdminDoc.Images.installer.yaml") -Value $installerManifest
Set-Utf8NoBomContent -Path (Join-Path $wingetDir "SysAdminDoc.Images.locale.en-US.yaml") -Value $localeManifest

Write-Host "WinGet manifests written to: $wingetDir"

# --- Scoop manifest ---

$scoopDir = Join-Path $OutputDir "scoop"
New-Item -ItemType Directory -Path $scoopDir -Force | Out-Null

$scoopJson = @"
{
  "version": "${Version}",
  "description": "Local-first Windows image viewer with broad codec support and inline rename.",
  "homepage": "https://github.com/SysAdminDoc/Images",
  "license": "MIT",
  "architecture": {
    "64bit": {
      "url": "${ghBase}/${zipName}",
      "hash": "${zipHash}"
    }
  },
  "bin": "Images.exe",
  "shortcuts": [
    ["Images.exe", "Images"]
  ],
  "checkver": {
    "github": "https://github.com/SysAdminDoc/Images"
  },
  "autoupdate": {
    "architecture": {
      "64bit": {
        "url": "https://github.com/SysAdminDoc/Images/releases/download/v`$version/Images-v`$version-win-x64.zip"
      }
    }
  }
}
"@

Set-Utf8NoBomContent -Path (Join-Path $scoopDir "images.json") -Value $scoopJson

Write-Host "Scoop manifest written to: $(Join-Path $scoopDir 'images.json')"

# --- Summary ---

Write-Host ""
Write-Host "=== Package Manifest Summary ==="
Write-Host "Version:        $Version"
Write-Host "Installer hash: $setupHash"
Write-Host "Portable hash:  $zipHash"
Write-Host ""
Write-Host "WinGet submission:"
Write-Host "  1. Install wingetcreate: winget install wingetcreate"
Write-Host "  2. Validate:  winget validate $wingetDir"
Write-Host "  3. Submit PR: wingetcreate submit $wingetDir"
Write-Host ""
Write-Host "Scoop submission:"
Write-Host "  1. Test locally: scoop install $(Join-Path $scoopDir 'images.json')"
Write-Host "  2. Verify:       Images.exe --system-info"
Write-Host "  3. Submit PR to ScoopInstaller/Extras with the manifest"
