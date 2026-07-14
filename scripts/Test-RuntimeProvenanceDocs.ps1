param(
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
        throw "Required runtime provenance file is missing: $RelativePath"
    }

    return Get-Content -Raw -LiteralPath $path
}

function Get-PackageVersion {
    param([Parameter(Mandatory)][string]$PackageId)

    [xml]$project = Read-RepoText "src\Images\Images.csproj"
    $reference = @($project.Project.ItemGroup.PackageReference) |
        Where-Object { $_.Include -eq $PackageId } |
        Select-Object -First 1

    if (-not $reference) {
        throw "PackageReference '$PackageId' is missing from src\Images\Images.csproj."
    }

    return ([string]$reference.Version).Trim()
}

function Assert-ContainsText {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Needle,
        [Parameter(Mandatory)][string]$Label
    )

    $text = Read-RepoText $RelativePath
    if ($text -notmatch [regex]::Escape($Needle)) {
        throw "$RelativePath does not document $Label ('$Needle')."
    }
}

function Assert-DoesNotContainText {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Needle,
        [Parameter(Mandatory)][string]$Label
    )

    $text = Read-RepoText $RelativePath
    if ($text -match [regex]::Escape($Needle)) {
        throw "$RelativePath still documents stale $Label ('$Needle')."
    }
}

function Get-JpegTranVersion {
    $provenance = Read-RepoText "src\Images\Codecs\JpegTran\PROVENANCE.md"
    if ($provenance -notmatch '(?m)^- Version:\s*([0-9]+(?:\.[0-9]+)+)\s*$') {
        throw "Could not read approved jpegtran/libjpeg-turbo version from JpegTran PROVENANCE.md."
    }

    return $Matches[1]
}

function Get-GhostscriptVersion {
    $sbomScript = Read-RepoText "scripts\New-Sbom.ps1"
    if ($sbomScript -notmatch 'Ghostscript\s+([0-9]+(?:\.[0-9]+)+)\s+\(Artifex\)') {
        throw "Could not read approved Ghostscript version from scripts\New-Sbom.ps1."
    }

    return $Matches[1]
}

$magickVersion = Get-PackageVersion "Magick.NET-Q16-HDRI-AnyCPU"
$magickCoreVersion = Get-PackageVersion "Magick.NET.Core"
if ($magickCoreVersion -ne $magickVersion) {
    throw "Magick.NET package versions must stay aligned. Q16-HDRI=$magickVersion Core=$magickCoreVersion."
}

$sharpCompressVersion = Get-PackageVersion "SharpCompress"
$sqliteVersion = Get-PackageVersion "Microsoft.Data.Sqlite"
$sqliteRawVersion = Get-PackageVersion "SQLitePCLRaw.bundle_e_sqlite3"
$jpegTranVersion = Get-JpegTranVersion
$ghostscriptVersion = Get-GhostscriptVersion

Assert-ContainsText "docs\integration-policy.md" "Magick.NET $magickVersion" "current Magick.NET package version"
Assert-ContainsText "docs\integration-policy.md" "SharpCompress $sharpCompressVersion" "current SharpCompress package version"
Assert-ContainsText "docs\integration-policy.md" "Microsoft.Data.Sqlite $sqliteVersion" "current Microsoft.Data.Sqlite package version"
Assert-ContainsText "docs\integration-policy.md" "SQLitePCLRaw.bundle_e_sqlite3 $sqliteRawVersion" "current SQLitePCLRaw package version"
Assert-ContainsText "docs\integration-policy.md" "Ghostscript $ghostscriptVersion" "approved Ghostscript runtime version"
Assert-ContainsText "docs\integration-policy.md" "libjpeg-turbo $jpegTranVersion" "approved jpegtran/libjpeg-turbo runtime version"

Assert-ContainsText "docs\archive-runtime-review.md" "``SharpCompress`` $sharpCompressVersion" "current SharpCompress archive-review version"
Assert-ContainsText "docs\archive-runtime-review.md" "upgraded to $sharpCompressVersion" "current SharpCompress dependency-refresh note"

Assert-ContainsText "docs\codec-bundling.md" "Ghostscript $ghostscriptVersion" "approved Ghostscript bundle version"
Assert-ContainsText "docs\codec-support-policy.md" "Ghostscript $ghostscriptVersion" "approved Ghostscript support-policy version"
Assert-ContainsText "docs\codec-bundling.md" "libjpeg-turbo $jpegTranVersion" "approved jpegtran bundle version"
Assert-ContainsText "docs\lossless-jpeg-transform-policy.md" "libjpeg-turbo $jpegTranVersion" "approved jpegtran policy version"

if ($sharpCompressVersion -ne "0.48.1") {
    Assert-DoesNotContainText "docs\integration-policy.md" "SharpCompress 0.48.1" "SharpCompress version"
    Assert-DoesNotContainText "docs\archive-runtime-review.md" "``SharpCompress`` 0.48.1" "SharpCompress version"
    Assert-DoesNotContainText "docs\archive-runtime-review.md" "0.48.1 from NuGet" "SharpCompress version"
}

Write-Host "Runtime provenance documentation is synchronized."
