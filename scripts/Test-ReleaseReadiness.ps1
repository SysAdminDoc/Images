param(
    [string]$Version = "",
    [string]$RepositoryRoot = ""
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

function Read-RepoText {
    param([string]$RelativePath)
    $path = Resolve-RepoPath $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required release-readiness file is missing: $RelativePath"
    }
    return Get-Content -Raw -LiteralPath $path
}

$versionScript = Join-Path $PSScriptRoot "Test-VersionSync.ps1"
if ([string]::IsNullOrWhiteSpace($Version)) {
    & $versionScript -RepositoryRoot $RepositoryRoot
} else {
    & $versionScript -Version $Version -RepositoryRoot $RepositoryRoot
}

$checklist = Read-RepoText "docs\release-checklist.md"
$requiredChecklistSections = @(
    "Current-State Audit",
    "Shipped-Roadmap Closure Pass",
    "Version/Date Consistency Check"
)

foreach ($section in $requiredChecklistSections) {
    if ($checklist -notmatch [regex]::Escape($section)) {
        throw "docs\release-checklist.md is missing required section '$section'."
    }
}

$roadmap = Read-RepoText "ROADMAP.md"
if ($roadmap -notmatch [regex]::Escape("PROJECT_CONTEXT.md")) {
    throw "ROADMAP.md must point release reviewers at PROJECT_CONTEXT.md."
}
if ($roadmap -match "No editor, no organizer, no batch processor") {
    $historicalHeading = "Historical V6 Roadmap And Source Appendix"
    $historicalIndex = $roadmap.IndexOf($historicalHeading, [StringComparison]::Ordinal)
    $staleIndex = $roadmap.IndexOf("No editor, no organizer, no batch processor", [StringComparison]::Ordinal)
    if ($historicalIndex -lt 0 -or $staleIndex -lt $historicalIndex) {
        throw "ROADMAP.md contains stale shipped-state copy above the historical appendix."
    }
}

$projectContext = Read-RepoText "PROJECT_CONTEXT.md"
if ($projectContext -notmatch "Recommended Next Work") {
    throw "PROJECT_CONTEXT.md must include a Recommended Next Work section before release."
}

$changelog = Read-RepoText "CHANGELOG.md"
$headingPattern = "(?m)^## v(?<version>\d+\.\d+\.\d+)\s+.+?\s+(?<date>\d{4}-\d{2}-\d{2})\s*$"
$headings = [regex]::Matches($changelog, $headingPattern)
if ($headings.Count -eq 0) {
    throw "CHANGELOG.md has no release headings matching '## vX.Y.Z - YYYY-MM-DD'."
}

$todayUtc = [DateTime]::UtcNow.Date
foreach ($heading in $headings) {
    $dateText = $heading.Groups["date"].Value
    $releaseVersion = $heading.Groups["version"].Value
    $parsedDate = [DateTime]::ParseExact(
        $dateText,
        "yyyy-MM-dd",
        [Globalization.CultureInfo]::InvariantCulture
    ).Date

    if ($parsedDate -gt $todayUtc) {
        throw "CHANGELOG.md release v$releaseVersion has future date $dateText."
    }
}

if (-not [string]::IsNullOrWhiteSpace($Version) -and
    $changelog -notmatch "(?m)^## v$([regex]::Escape($Version))\s+.+?\s+\d{4}-\d{2}-\d{2}\s*$") {
    Write-Warning "CHANGELOG.md does not yet contain a dated v$Version section. Promote Unreleased before publishing."
}

Write-Host "Release readiness validated."
