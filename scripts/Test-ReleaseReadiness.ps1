param(
    [string]$Version = "",
    [string]$RepositoryRoot = "",
    [string]$PortableDir = "",
    [string]$InstalledDir = "",
    [string]$ReleaseAssetsDir = "",
    [string]$ChecksumFile = "",
    [string]$PackageManifestOutputDir = "",
    [string]$ReleaseDiagnosticsLogDir = "",
    [switch]$RequireGhostscript,
    [switch]$RequireJpegTran,
    [switch]$SkipTests,
    [switch]$SkipLocalization,
    [switch]$SkipReleaseDiagnostics,
    [switch]$SkipPackageManifestValidation
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

function Invoke-NativeCommand {
    param([Parameter(Mandatory)][scriptblock]$Command)

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        return & $Command 2>&1
    } finally {
        $script:LastNativeExitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Get-ProjectVersion {
    $projectPath = Resolve-RepoPath "src\Images\Images.csproj"
    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $propertyGroup = @($project.Project.PropertyGroup) | Where-Object { $_.Version } | Select-Object -First 1
    if (-not $propertyGroup) {
        throw "Could not read version metadata from $projectPath."
    }

    $projectVersion = ([string]$propertyGroup.Version).Trim()
    if ($projectVersion -notmatch '^\d+\.\d+\.\d+$') {
        throw "Project version must be plain SemVer X.Y.Z, got '$projectVersion'."
    }

    return $projectVersion
}

function Read-ChecksumEntries {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Checksum file not found: $Path"
    }

    $entries = @{}
    foreach ($line in (Get-Content -LiteralPath $Path -Encoding ascii)) {
        if ($line -match '^([A-Fa-f0-9]{64})\s+(.+)$') {
            $entries[$Matches[2].Trim()] = $Matches[1].ToUpperInvariant()
        }
    }

    return $entries
}

function New-ChecksumFileFromAssets {
    param(
        [Parameter(Mandatory)][string]$AssetsDir,
        [Parameter(Mandatory)][string]$ReleaseVersion
    )

    $resolvedAssetsDir = (Resolve-Path -LiteralPath $AssetsDir).Path
    $zipName = "Images-v${ReleaseVersion}-win-x64.zip"
    $setupName = "Images-v${ReleaseVersion}-setup-win-x64.exe"
    $expectedAssets = @($zipName, $setupName)

    $readinessDir = Resolve-RepoPath "artifacts\release-readiness"
    New-Item -ItemType Directory -Path $readinessDir -Force | Out-Null
    $generatedChecksumFile = Join-Path $readinessDir "Images-v${ReleaseVersion}-checksums.txt"

    $lines = foreach ($assetName in $expectedAssets) {
        $assetPath = Join-Path $resolvedAssetsDir $assetName
        if (-not (Test-Path -LiteralPath $assetPath)) {
            throw "Release asset validation expected '$assetName' in $resolvedAssetsDir."
        }

        $stream = [System.IO.File]::OpenRead($assetPath)
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hashBytes = $sha256.ComputeHash($stream)
            $hash = ([BitConverter]::ToString($hashBytes) -replace "-", "").ToUpperInvariant()
        } finally {
            $sha256.Dispose()
            $stream.Dispose()
        }
        "$hash  $assetName"
    }

    Set-Content -LiteralPath $generatedChecksumFile -Value $lines -Encoding ascii
    return $generatedChecksumFile
}

function Assert-PackageManifestOutput {
    param(
        [Parameter(Mandatory)][string]$OutputDir,
        [Parameter(Mandatory)][string]$ReleaseVersion,
        [Parameter(Mandatory)][hashtable]$Checksums
    )

    $zipName = "Images-v${ReleaseVersion}-win-x64.zip"
    $setupName = "Images-v${ReleaseVersion}-setup-win-x64.exe"
    if (-not $Checksums.ContainsKey($zipName)) {
        throw "Checksum file missing entry for portable ZIP: $zipName"
    }
    if (-not $Checksums.ContainsKey($setupName)) {
        throw "Checksum file missing entry for installer: $setupName"
    }

    $wingetDir = Join-Path $OutputDir "winget\manifests\s\SysAdminDoc\Images\$ReleaseVersion"
    $versionManifestPath = Join-Path $wingetDir "SysAdminDoc.Images.yaml"
    $installerManifestPath = Join-Path $wingetDir "SysAdminDoc.Images.installer.yaml"
    $localeManifestPath = Join-Path $wingetDir "SysAdminDoc.Images.locale.en-US.yaml"
    $scoopManifestPath = Join-Path $OutputDir "scoop\images.json"

    foreach ($path in @($versionManifestPath, $installerManifestPath, $localeManifestPath, $scoopManifestPath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Package manifest output missing: $path"
        }
    }

    $versionManifest = Get-Content -Raw -LiteralPath $versionManifestPath
    if ($versionManifest -notmatch "PackageIdentifier:\s+SysAdminDoc\.Images" -or
        $versionManifest -notmatch "PackageVersion:\s+$([regex]::Escape($ReleaseVersion))") {
        throw "WinGet version manifest does not match SysAdminDoc.Images v$ReleaseVersion."
    }

    $installerManifest = Get-Content -Raw -LiteralPath $installerManifestPath
    if ($installerManifest -notmatch "InstallerUrl:\s+https://github\.com/SysAdminDoc/Images/releases/download/v$([regex]::Escape($ReleaseVersion))/$([regex]::Escape($setupName))" -or
        $installerManifest -notmatch "InstallerSha256:\s+$($Checksums[$setupName])") {
        throw "WinGet installer manifest does not match the release asset URL/checksum."
    }

    $scoop = Get-Content -Raw -LiteralPath $scoopManifestPath | ConvertFrom-Json
    if ($scoop.version -ne $ReleaseVersion) {
        throw "Scoop manifest version '$($scoop.version)' does not match '$ReleaseVersion'."
    }
    if ($scoop.architecture.'64bit'.url -ne "https://github.com/SysAdminDoc/Images/releases/download/v${ReleaseVersion}/${zipName}" -or
        $scoop.architecture.'64bit'.hash.ToUpperInvariant() -ne $Checksums[$zipName]) {
        throw "Scoop manifest does not match the release ZIP URL/checksum."
    }
}

$versionScript = Join-Path $PSScriptRoot "Test-VersionSync.ps1"
if ([string]::IsNullOrWhiteSpace($Version)) {
    & $versionScript -RepositoryRoot $RepositoryRoot
} else {
    & $versionScript -Version $Version -RepositoryRoot $RepositoryRoot
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion
}

if (-not $SkipPackageManifestValidation) {
    $packageHashScript = Join-Path $PSScriptRoot "Test-PackageManifestHashes.ps1"
    & $packageHashScript `
        -RepositoryRoot $RepositoryRoot `
        -Version $Version
}

$slnPath = Resolve-RepoPath "Images.sln"
if (-not (Test-Path -LiteralPath $slnPath)) {
    throw "Images.sln not found at $slnPath."
}

Write-Host "Validating NuGet package resolution..."
$restoreOutput = Invoke-NativeCommand { dotnet restore $slnPath }
$restoreExitCode = $script:LastNativeExitCode
if ($restoreExitCode -ne 0) {
    $errors = $restoreOutput | Where-Object { $_ -match "error NU" }
    $errorText = ($errors | ForEach-Object { $_.ToString().Trim() }) -join "`n  "
    throw "dotnet restore failed (exit $restoreExitCode). Fix package references before release:`n  $errorText"
}

Write-Host "Validating solution build..."
$buildOutput = Invoke-NativeCommand { dotnet build $slnPath -c Release --no-restore }
$buildExitCode = $script:LastNativeExitCode
if ($buildExitCode -ne 0) {
    $errors = $buildOutput | Where-Object { $_ -match "error CS|error MSB" }
    $errorText = ($errors | Select-Object -First 10 | ForEach-Object { $_.ToString().Trim() }) -join "`n  "
    throw "dotnet build failed (exit $buildExitCode). Fix build errors before release:`n  $errorText"
}

if (-not $SkipTests) {
    Write-Host "Running release test suite..."
    $testOutput = Invoke-NativeCommand { dotnet test $slnPath -c Release --no-build }
    $testExitCode = $script:LastNativeExitCode
    if ($testExitCode -ne 0) {
        $failures = $testOutput | Where-Object { $_ -match "Failed|Error Message|Failed!" }
        $failureText = ($failures | Select-Object -First 20 | ForEach-Object { $_.ToString().Trim() }) -join "`n  "
        throw "dotnet test failed (exit $testExitCode). Fix local tests before release:`n  $failureText"
    }
}

Write-Host "Checking for vulnerable NuGet packages..."
$vulnOutput = Invoke-NativeCommand { dotnet list $slnPath package --vulnerable --include-transitive }
$vulnExitCode = $script:LastNativeExitCode
if ($vulnExitCode -ne 0) {
    $vulnErrorText = ($vulnOutput | Select-Object -First 20 | ForEach-Object { $_.ToString().Trim() }) -join "`n  "
    throw "dotnet vulnerability scan failed (exit $vulnExitCode). Fix package scanning before release:`n  $vulnErrorText"
}

$vulnLines = $vulnOutput | Where-Object { $_ -match ">\s+\S+" -and $_ -match "High|Critical" }
if ($vulnLines) {
    $vulnText = ($vulnLines | ForEach-Object { $_.ToString().Trim() }) -join "`n  "
    throw "High/Critical vulnerable transitive packages detected:`n  $vulnText"
}

if (-not $SkipLocalization) {
    Write-Host "Validating localization resources..."
    $localizationScript = Join-Path $PSScriptRoot "Test-LocalizationResources.ps1"
    & $localizationScript `
        -ResourcesRoot (Resolve-RepoPath "src\Images\Localization") `
        -SourceRoot (Resolve-RepoPath "src\Images")
}

$checklist = Read-RepoText "docs\release-checklist.md"
$requiredChecklistSections = @(
    "Current-State Audit",
    "Shipped-Roadmap Closure Pass",
    "Version/Date Consistency Check",
    "Runtime And Artifact Checks"
)

foreach ($section in $requiredChecklistSections) {
    if ($checklist -notmatch [regex]::Escape($section)) {
        throw "docs\release-checklist.md is missing required section '$section'."
    }
}

$roadmapBlockedPath = Resolve-RepoPath "Roadmap_Blocked.md"
if (-not (Test-Path -LiteralPath $roadmapBlockedPath)) {
    throw "Roadmap_Blocked.md is missing. Blocked items must be tracked separately from ROADMAP.md."
}

$requiredRuntimeFiles = @(
    "scripts\Prepare-JpegTranBundle.ps1",
    "scripts\Test-ReleaseDiagnostics.ps1",
    "src\Images\Codecs\JpegTran\PROVENANCE.md",
    "src\Images\Codecs\JpegTran\LICENSE.md",
    "src\Images\Codecs\JpegTran\README.ijg"
)

foreach ($relativePath in $requiredRuntimeFiles) {
    $path = Resolve-RepoPath $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required runtime provenance file is missing: $relativePath"
    }
}

$jpegTranProvenance = Read-RepoText "src\Images\Codecs\JpegTran\PROVENANCE.md"
if ($jpegTranProvenance -notmatch "libjpeg-turbo-3\.1\.4\.1-vc-x64\.exe" -or
    $jpegTranProvenance -notmatch "2bb347f106473c12635bdd414b1f289de9f4d6dea4a496d3f9dd212db9eda0dc" -or
    $jpegTranProvenance -notmatch "2000c205ed99fe2409e42a6cb87c19d88e33e516d5d40ff11bb19b7830e3ee33" -or
    $jpegTranProvenance -notmatch "jpeg62\.dll" -or
    $jpegTranProvenance -notmatch "fc55317c9dee01f0f04a2a669824429086c5d55aa13ad901e2a3bbab33c80853") {
    throw "jpegtran provenance must include the approved artifact URL and SHA-256 values."
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

if (-not $SkipReleaseDiagnostics) {
    if ([string]::IsNullOrWhiteSpace($PortableDir) -and [string]::IsNullOrWhiteSpace($InstalledDir)) {
        $defaultPortableDir = Resolve-RepoPath "publish"
        if (Test-Path -LiteralPath (Join-Path $defaultPortableDir "Images.exe")) {
            $PortableDir = $defaultPortableDir
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($PortableDir) -or -not [string]::IsNullOrWhiteSpace($InstalledDir)) {
        if ([string]::IsNullOrWhiteSpace($ReleaseDiagnosticsLogDir)) {
            $ReleaseDiagnosticsLogDir = Resolve-RepoPath "artifacts\release-readiness\diagnostics"
        }

        Write-Host "Validating release diagnostics..."
        $diagnosticsScript = Join-Path $PSScriptRoot "Test-ReleaseDiagnostics.ps1"
        $diagnosticsArgs = @{
            LogDir = $ReleaseDiagnosticsLogDir
        }
        if (-not [string]::IsNullOrWhiteSpace($PortableDir)) {
            $diagnosticsArgs["PortableDir"] = $PortableDir
        }
        if (-not [string]::IsNullOrWhiteSpace($InstalledDir)) {
            $diagnosticsArgs["InstalledDir"] = $InstalledDir
        }
        if ($RequireGhostscript) {
            $diagnosticsArgs["RequireGhostscript"] = $true
        }
        if ($RequireJpegTran) {
            $diagnosticsArgs["RequireJpegTran"] = $true
        }

        & $diagnosticsScript @diagnosticsArgs
    } else {
        Write-Warning "Release diagnostics skipped because no portable or installed Images.exe output was provided."
    }
}

if (-not $SkipPackageManifestValidation) {
    $effectiveChecksumFile = $ChecksumFile
    if (-not [string]::IsNullOrWhiteSpace($ReleaseAssetsDir)) {
        Write-Host "Generating checksum file from release assets..."
        $effectiveChecksumFile = New-ChecksumFileFromAssets -AssetsDir $ReleaseAssetsDir -ReleaseVersion $Version
    }

    if (-not [string]::IsNullOrWhiteSpace($effectiveChecksumFile)) {
        if ([string]::IsNullOrWhiteSpace($PackageManifestOutputDir)) {
            $PackageManifestOutputDir = Resolve-RepoPath "artifacts\release-readiness\package_manifests"
        }

        Write-Host "Validating package manifests against checksums..."
        $checksums = Read-ChecksumEntries -Path $effectiveChecksumFile
        $packageScript = Join-Path $PSScriptRoot "New-PackageManifests.ps1"
        $packageArgs = @{
            Version = $Version
            ChecksumFile = $effectiveChecksumFile
            OutputDir = $PackageManifestOutputDir
            RepositoryRoot = $RepositoryRoot
        }
        & $packageScript @packageArgs

        Assert-PackageManifestOutput `
            -OutputDir $PackageManifestOutputDir `
            -ReleaseVersion $Version `
            -Checksums $checksums
    } else {
        Write-Warning "Package manifest/checksum validation skipped because no -ReleaseAssetsDir or -ChecksumFile was provided."
    }
}

Write-Host "Generating SBOM and provenance bundle..."
$sbomScript = Join-Path $PSScriptRoot "New-Sbom.ps1"
$sbomOutputDir = Resolve-RepoPath "artifacts\release-readiness"
& $sbomScript -RepositoryRoot $RepositoryRoot -OutputDir $sbomOutputDir

Write-Host "Release readiness validated."
