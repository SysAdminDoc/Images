[CmdletBinding()]
param(
    [string] $PortableDir,
    [string] $InstalledDir,
    [string] $LogDir = (Join-Path (Get-Location) "release-diagnostics"),
    [string] $ExpectedJpegTranSha256 = "2000c205ed99fe2409e42a6cb87c19d88e33e516d5d40ff11bb19b7830e3ee33",
    [switch] $RequireGhostscript,
    [switch] $RequireJpegTran
)

$ErrorActionPreference = "Stop"

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)] [string] $Text,
        [Parameter(Mandatory = $true)] [string] $Pattern,
        [Parameter(Mandatory = $true)] [string] $Label
    )

    if ($Text -notmatch $Pattern) {
        throw "$Label did not contain required pattern: $Pattern"
    }
}

function Invoke-DiagnosticReport {
    param(
        [Parameter(Mandatory = $true)] [string] $ExePath,
        [Parameter(Mandatory = $true)] [string] $Argument,
        [Parameter(Mandatory = $true)] [string] $OutputPath
    )

    $errorPath = "$OutputPath.err"
    $process = Start-Process `
        -FilePath $ExePath `
        -ArgumentList $Argument `
        -RedirectStandardOutput $OutputPath `
        -RedirectStandardError $errorPath `
        -WindowStyle Hidden `
        -Wait `
        -PassThru

    if ($process.ExitCode -ne 0) {
        throw "$ExePath $Argument failed with exit code $($process.ExitCode). See $OutputPath and $errorPath."
    }

    $item = Get-Item -LiteralPath $OutputPath
    if ($item.Length -le 0) {
        throw "$ExePath $Argument produced an empty diagnostics log at $OutputPath."
    }

    return Get-Content -Raw -LiteralPath $OutputPath
}

function Test-DiagnosticDirectory {
    param(
        [Parameter(Mandatory = $true)] [string] $Label,
        [Parameter(Mandatory = $true)] [string] $Directory
    )

    $resolvedDir = (Resolve-Path -LiteralPath $Directory).Path
    $exe = Join-Path $resolvedDir "Images.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "$Label diagnostics target does not contain Images.exe: $resolvedDir"
    }

    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    $safeLabel = $Label -replace '[^A-Za-z0-9_.-]', '-'
    $systemInfoPath = Join-Path $LogDir "$safeLabel-system-info.txt"
    $codecReportPath = Join-Path $LogDir "$safeLabel-codec-report.txt"

    $systemInfo = Invoke-DiagnosticReport -ExePath $exe -Argument "--system-info" -OutputPath $systemInfoPath
    $codecReport = Invoke-DiagnosticReport -ExePath $exe -Argument "--codec-report" -OutputPath $codecReportPath

    Assert-Contains $systemInfo "Dependency provenance" "$Label --system-info"
    Assert-Contains $systemInfo "Windows OCR:" "$Label --system-info"
    Assert-Contains $systemInfo "Windows.Media.Ocr" "$Label --system-info"
    Assert-Contains $systemInfo "SharpCompress" "$Label --system-info"
    Assert-Contains $systemInfo "jpegtran" "$Label --system-info"

    Assert-Contains $codecReport "Dependency provenance" "$Label --codec-report"
    Assert-Contains $codecReport "Capability matrix" "$Label --codec-report"
    Assert-Contains $codecReport "SharpCompress" "$Label --codec-report"
    Assert-Contains $codecReport "Windows.Media.Ocr" "$Label --codec-report"
    Assert-Contains $codecReport "jpegtran" "$Label --codec-report"

    if ($RequireGhostscript) {
        Assert-Contains $systemInfo "Ghostscript:\s+available" "$Label --system-info"
        Assert-Contains $codecReport "Ghostscript: available" "$Label --codec-report"
    }

    if ($RequireJpegTran) {
        Assert-Contains $systemInfo "jpegtran:\s+available" "$Label --system-info"
        Assert-Contains $codecReport "jpegtran: available" "$Label --codec-report"
        Assert-Contains $systemInfo $ExpectedJpegTranSha256.ToLowerInvariant() "$Label --system-info"
        Assert-Contains $codecReport $ExpectedJpegTranSha256.ToLowerInvariant() "$Label --codec-report"
    }

    Write-Host "$Label diagnostics validated."
    Write-Host "  $systemInfoPath"
    Write-Host "  $codecReportPath"
}

if ([string]::IsNullOrWhiteSpace($PortableDir) -and [string]::IsNullOrWhiteSpace($InstalledDir)) {
    throw "Pass -PortableDir, -InstalledDir, or both."
}

if (-not [string]::IsNullOrWhiteSpace($PortableDir)) {
    Test-DiagnosticDirectory -Label "portable" -Directory $PortableDir
}

if (-not [string]::IsNullOrWhiteSpace($InstalledDir)) {
    Test-DiagnosticDirectory -Label "installed" -Directory $InstalledDir
}

Write-Host "Release diagnostics validated."
