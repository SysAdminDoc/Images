param(
    [string]$RepositoryRoot = "",
    [string]$Version = "",
    [string]$ChecksumFile = "",
    [switch]$SkipCommittedManifests
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

function Resolve-RepoPath {
    param([string]$RelativePath)
    return Join-Path $RepositoryRoot $RelativePath
}

function Get-ProjectVersion {
    $projectPath = Resolve-RepoPath "src\Images\Images.csproj"
    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $propertyGroup = @($project.Project.PropertyGroup) | Where-Object { $_.Version } | Select-Object -First 1
    if (-not $propertyGroup) {
        throw "Could not read version metadata from $projectPath."
    }

    return ([string]$propertyGroup.Version).Trim()
}

function Assert-PackageHash {
    param(
        [Parameter(Mandatory)][string]$Hash,
        [Parameter(Mandatory)][string]$Context
    )

    $normalized = $Hash.Trim()
    if ($normalized -notmatch '^[A-Fa-f0-9]{64}$') {
        throw "$Context must be a real SHA-256 hash, got '$Hash'."
    }

    if ($normalized -match '^([A-Fa-f0-9])\1{63}$') {
        throw "$Context uses a placeholder SHA-256 hash."
    }
}

function Assert-ChecksumFileHashes {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Checksum file not found: $Path"
    }

    $entryCount = 0
    foreach ($line in (Get-Content -LiteralPath $Path -Encoding ascii)) {
        if ($line -match '^(\S+)\s+(.+)$') {
            $entryCount++
            Assert-PackageHash -Hash $Matches[1] -Context "Checksum entry '$($Matches[2].Trim())'"
        }
    }

    if ($entryCount -eq 0) {
        throw "Checksum file contains no package hash entries: $Path"
    }
}

function Assert-CommittedScoopManifest {
    param([Parameter(Mandatory)][string]$ReleaseVersion)

    $manifestPath = Resolve-RepoPath "packaging\scoop\images.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return
    }

    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    if ($manifest.version -ne $ReleaseVersion) {
        throw "Committed Scoop manifest version '$($manifest.version)' does not match project version '$ReleaseVersion'."
    }

    $zipName = "Images-v${ReleaseVersion}-win-x64.zip"
    $expectedUrl = "https://github.com/SysAdminDoc/Images/releases/download/v${ReleaseVersion}/${zipName}"
    $actualUrl = [string]$manifest.architecture.'64bit'.url
    if ($actualUrl -ne $expectedUrl) {
        throw "Committed Scoop manifest URL '$actualUrl' does not match '$expectedUrl'."
    }

    Assert-PackageHash `
        -Hash ([string]$manifest.architecture.'64bit'.hash) `
        -Context "Committed Scoop manifest hash"
}

function Assert-CommittedWingetManifests {
    param([Parameter(Mandatory)][string]$ReleaseVersion)

    $wingetRoot = Resolve-RepoPath "packaging\winget"
    if (-not (Test-Path -LiteralPath $wingetRoot)) {
        return
    }

    $manifestFiles = @(Get-ChildItem -LiteralPath $wingetRoot -Recurse -File -Filter "*.yaml")
    if ($manifestFiles.Count -eq 0) {
        return
    }

    foreach ($file in $manifestFiles) {
        $text = Get-Content -Raw -LiteralPath $file.FullName
        if ($text -match "PackageVersion:\s*(\S+)" -and $Matches[1] -ne $ReleaseVersion) {
            throw "Committed WinGet manifest '$($file.FullName)' version '$($Matches[1])' does not match project version '$ReleaseVersion'."
        }

        foreach ($match in [regex]::Matches($text, "InstallerSha256:\s*(\S+)")) {
            Assert-PackageHash -Hash $match.Groups[1].Value -Context "Committed WinGet manifest hash '$($file.FullName)'"
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ChecksumFile)) {
    Assert-ChecksumFileHashes -Path $ChecksumFile
}

if (-not $SkipCommittedManifests) {
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = Get-ProjectVersion
    }

    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version must be in X.Y.Z format. Got: $Version"
    }

    Assert-CommittedScoopManifest -ReleaseVersion $Version
    Assert-CommittedWingetManifests -ReleaseVersion $Version
}

Write-Host "Package manifest hashes validated."
