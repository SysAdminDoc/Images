[CmdletBinding()]
param(
    [string] $Source,
    [string] $Destination = "",
    [string] $ArtifactUrl = "https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/3.1.4.1/libjpeg-turbo-3.1.4.1-vc-x64.exe",
    [string] $ArtifactSha256 = "2bb347f106473c12635bdd414b1f289de9f4d6dea4a496d3f9dd212db9eda0dc",
    [string] $ExpectedJpegTranSha256 = "2000c205ed99fe2409e42a6cb87c19d88e33e516d5d40ff11bb19b7830e3ee33",
    [string] $ExpectedJpeg62Sha256 = "fc55317c9dee01f0f04a2a669824429086c5d55aa13ad901e2a3bbab33c80853",
    [switch] $Force
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $PSScriptRoot "..\src\Images\Codecs\JpegTran"
}

function Get-SevenZipPath {
    $candidates = @(
        (Get-Command 7z.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source),
        (Join-Path $env:ProgramFiles "7-Zip\7z.exe"),
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} "7-Zip\7z.exe" })
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "7-Zip was not found. Install 7-Zip or put 7z.exe on PATH to extract the libjpeg-turbo installer."
}

function Assert-Sha256 {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $Expected,
        [Parameter(Mandatory = $true)] [string] $Label
    )

    if ($Expected -notmatch '^[A-Fa-f0-9]{64}$') {
        throw "$Label expected SHA-256 is not a 64-character hex digest."
    }

    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedLower = $Expected.ToLowerInvariant()
    if ($actual -ne $expectedLower) {
        throw "$Label SHA-256 mismatch. Expected $expectedLower, got $actual."
    }

    return $actual
}

function Get-DefaultWorkRoot {
    return Join-Path (Split-Path -Parent $PSScriptRoot) ".tmp-jpegtran-bundle"
}

function Download-ApprovedArtifact {
    param([string] $WorkRoot)

    $uri = [System.Uri]::new($ArtifactUrl)
    if ($uri.Scheme -ne "https") {
        throw "ArtifactUrl must use HTTPS."
    }

    New-Item -ItemType Directory -Path $WorkRoot -Force | Out-Null
    $artifact = Join-Path $WorkRoot (Split-Path $uri.AbsolutePath -Leaf)
    Invoke-WebRequest -Uri $ArtifactUrl -OutFile $artifact
    Assert-Sha256 -Path $artifact -Expected $ArtifactSha256 -Label "libjpeg-turbo artifact" | Out-Null
    return $artifact
}

function Expand-Installer {
    param(
        [string] $InstallerPath,
        [string] $WorkRoot
    )

    $sevenZip = Get-SevenZipPath
    $extractRoot = Join-Path $WorkRoot "extracted"
    New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null

    & $sevenZip x $InstallerPath "-o$extractRoot" -y | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "7-Zip extraction failed with exit code $LASTEXITCODE."
    }

    return $extractRoot
}

function Resolve-JpegTranSourceRoot {
    $workRoot = Get-DefaultWorkRoot

    if ([string]::IsNullOrWhiteSpace($Source)) {
        $artifact = Download-ApprovedArtifact -WorkRoot $workRoot
        return Expand-Installer -InstallerPath $artifact -WorkRoot $workRoot
    }

    $resolved = (Resolve-Path -LiteralPath $Source).Path
    if (Test-Path -LiteralPath $resolved -PathType Leaf) {
        Assert-Sha256 -Path $resolved -Expected $ArtifactSha256 -Label "libjpeg-turbo artifact" | Out-Null
        return Expand-Installer -InstallerPath $resolved -WorkRoot $workRoot
    }

    return $resolved
}

function Find-RequiredFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Root,
        [Parameter(Mandatory = $true)] [string] $Name
    )

    $match = Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $Name |
        Select-Object -First 1
    if (-not $match) {
        throw "Could not find $Name under '$Root'."
    }

    return $match.FullName
}

$sourceRoot = Resolve-JpegTranSourceRoot
$jpegTran = Find-RequiredFile -Root $sourceRoot -Name "jpegtran.exe"
$jpeg62 = Find-RequiredFile -Root $sourceRoot -Name "jpeg62.dll"
$license = Find-RequiredFile -Root $sourceRoot -Name "LICENSE.md"
$readmeIjg = Find-RequiredFile -Root $sourceRoot -Name "README.ijg"

$stagedHash = Assert-Sha256 -Path $jpegTran -Expected $ExpectedJpegTranSha256 -Label "jpegtran.exe"
$jpeg62Hash = Assert-Sha256 -Path $jpeg62 -Expected $ExpectedJpeg62Sha256 -Label "jpeg62.dll"

$destinationFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
New-Item -ItemType Directory -Path $destinationFull -Force | Out-Null

$runtimeDestination = Join-Path $destinationFull "jpegtran.exe"
$jpeg62Destination = Join-Path $destinationFull "jpeg62.dll"
if (((Test-Path -LiteralPath $runtimeDestination) -or (Test-Path -LiteralPath $jpeg62Destination)) -and -not $Force) {
    throw "Destination already contains jpegtran runtime files. Re-run with -Force to overwrite them."
}

Copy-Item -LiteralPath $jpegTran -Destination $runtimeDestination -Force
Copy-Item -LiteralPath $jpeg62 -Destination $jpeg62Destination -Force
Copy-Item -LiteralPath $license -Destination (Join-Path $destinationFull "LICENSE.md") -Force
Copy-Item -LiteralPath $readmeIjg -Destination (Join-Path $destinationFull "README.ijg") -Force

Assert-Sha256 -Path $runtimeDestination -Expected $ExpectedJpegTranSha256 -Label "staged jpegtran.exe" | Out-Null
Assert-Sha256 -Path $jpeg62Destination -Expected $ExpectedJpeg62Sha256 -Label "staged jpeg62.dll" | Out-Null

Write-Host "jpegtran runtime copied."
Write-Host "Source root:       $sourceRoot"
Write-Host "Destination:       $destinationFull"
Write-Host "Artifact URL:      $ArtifactUrl"
Write-Host "Artifact SHA-256:  $($ArtifactSha256.ToLowerInvariant())"
Write-Host "jpegtran SHA-256:  $stagedHash"
Write-Host "jpeg62 SHA-256:    $jpeg62Hash"
Write-Host "Next: dotnet publish src/Images -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish"
