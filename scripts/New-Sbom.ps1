param(
    [string]$RepositoryRoot = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $RepositoryRoot "artifacts\release-readiness"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

function ConvertFrom-JsonCompat {
    param([Parameter(Mandatory)][string]$Json)

    $convertCommand = Get-Command ConvertFrom-Json
    if ($convertCommand.Parameters.ContainsKey("Depth")) {
        return $Json | ConvertFrom-Json -Depth 50
    }

    return $Json | ConvertFrom-Json
}

$projectPath = Join-Path $RepositoryRoot "src\Images\Images.csproj"
if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

[xml]$project = Get-Content -Raw -LiteralPath $projectPath
$propertyGroup = @($project.Project.PropertyGroup) | Where-Object { $_.Version } | Select-Object -First 1
$version = ([string]$propertyGroup.Version).Trim()

$nugetBomPath = Join-Path $OutputDir "bom.json"
$toolManifestPath = Join-Path $RepositoryRoot ".config\dotnet-tools.json"
if (-not (Test-Path -LiteralPath $toolManifestPath)) {
    throw "Local tool manifest not found: $toolManifestPath"
}

Write-Host "Restoring pinned local SBOM tool..."
$toolRestoreOutput = & dotnet tool restore --tool-manifest $toolManifestPath 2>&1
if ($LASTEXITCODE -ne 0) {
    $toolRestoreText = ($toolRestoreOutput | ForEach-Object { $_.ToString().Trim() }) -join "`n  "
    throw "Local tool restore failed (exit $LASTEXITCODE):`n  $toolRestoreText"
}

Write-Host "Generating CycloneDX SBOM from NuGet dependencies..."

$previousLocation = Get-Location
try {
    Set-Location -LiteralPath $RepositoryRoot
    $cdxOutput = & dotnet tool run dotnet-CycloneDX $projectPath -o $OutputDir --filename bom.json -F Json -t -sn Images -sv $version 2>&1
    $cdxExitCode = $LASTEXITCODE
} finally {
    Set-Location -LiteralPath $previousLocation
}
if ($cdxExitCode -ne 0) {
    $cdxText = ($cdxOutput | ForEach-Object { $_.ToString().Trim() }) -join "`n  "
    throw "CycloneDX generation failed (exit $cdxExitCode):`n  $cdxText"
}

if (-not (Test-Path -LiteralPath $nugetBomPath)) {
    throw "CycloneDX output not found at expected path: $nugetBomPath"
}

$bom = ConvertFrom-JsonCompat -Json (Get-Content -Raw -LiteralPath $nugetBomPath)

$nativeComponents = @()

$jpegTranProvPath = Join-Path $RepositoryRoot "src\Images\Codecs\JpegTran\PROVENANCE.md"
if (Test-Path -LiteralPath $jpegTranProvPath) {
    $nativeComponents += @{
        type = "library"
        name = "jpegtran"
        version = "3.1.4.1"
        group = "libjpeg-turbo"
        description = "Lossless JPEG crop and rotation runtime"
        hashes = @(
            @{
                alg = "SHA-256"
                content = "2000c205ed99fe2409e42a6cb87c19d88e33e516d5d40ff11bb19b7830e3ee33"
            }
        )
        licenses = @(
            @{
                license = @{
                    id = "IJG"
                    name = "Independent JPEG Group License"
                }
            }
        )
        externalReferences = @(
            @{
                type = "distribution"
                url = "https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/3.1.4.1/libjpeg-turbo-3.1.4.1-vc-x64.exe"
            }
        )
        scope = "optional"
        purl = "pkg:github/libjpeg-turbo/libjpeg-turbo@3.1.4.1"
    }
}

$gsDir = Join-Path $RepositoryRoot "src\Images\Codecs\Ghostscript\bin"
if (Test-Path -LiteralPath $gsDir) {
    $nativeComponents += @{
        type = "library"
        name = "ghostscript"
        version = "10.07.0"
        group = "artifex"
        description = "PDF/EPS/PS/AI preview runtime"
        licenses = @(
            @{
                license = @{
                    id = "AGPL-3.0-only"
                    name = "GNU Affero General Public License v3.0"
                }
            }
        )
        externalReferences = @(
            @{
                type = "distribution"
                url = "https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/download/gs10070/gs10070w64.exe"
            }
        )
        scope = "optional"
        purl = "pkg:github/ArtifexSoftware/ghostpdl-downloads@gs10070"
    }
}

$modelDefinitions = @(
    @{ name = "inpainting_lama_2025jan.onnx"; purpose = "Content-aware repair"; license = "Apache-2.0"; sha256 = "7df918ac3921d3daf0aae1d219776cf0dc4e4935f035af81841b40adcf74fdf2"; source = "https://huggingface.co/opencv/inpainting_lama" },
    @{ name = "lama_fp32.onnx"; purpose = "Content-aware repair fallback"; license = "Apache-2.0"; sha256 = "1faef5301d78db7dda502fe59966957ec4b79dd64e16f03ed96913c7a4eb68d6"; source = "https://huggingface.co/Carve/LaMa-ONNX" },
    @{ name = "clip-vit-b32-text.onnx"; purpose = "Semantic search text embeddings"; license = "MIT"; sha256 = "4dbe762b11e36488304471e439cde89da053ad7acaddbf9e096745d142ec8d8b"; source = "https://huggingface.co/Qdrant/clip-ViT-B-32-text" },
    @{ name = "clip-vit-b32-vision.onnx"; purpose = "Semantic search image embeddings"; license = "MIT"; sha256 = "c68d3d9a200ddd2a8c8a5510b576d4c94d1ae383bf8b36dd8c084f94e1fb4d63"; source = "https://huggingface.co/Qdrant/clip-ViT-B-32-vision" }
)

foreach ($model in $modelDefinitions) {
    $nativeComponents += @{
        type = "machine-learning-model"
        name = $model.name
        description = $model.purpose
        hashes = @(
            @{
                alg = "SHA-256"
                content = $model.sha256
            }
        )
        licenses = @(
            @{
                license = @{
                    id = $model.license
                }
            }
        )
        externalReferences = @(
            @{
                type = "distribution"
                url = $model.source
            }
        )
        scope = "optional"
    }
}

if ($null -eq $bom.components) {
    $bom | Add-Member -MemberType NoteProperty -Name "components" -Value @()
}

$existingComponents = @($bom.components)
$bom.components = $existingComponents + $nativeComponents

$bom.metadata.component = @{
    type = "application"
    name = "Images"
    version = $version
    description = "A premium, local-first Windows image viewer with an image-first workspace"
    licenses = @(
        @{
            license = @{
                id = "MIT"
            }
        }
    )
    purl = "pkg:github/SysAdminDoc/Images@v$version"
}

$sbomPath = Join-Path $OutputDir "Images-v${version}-sbom.cdx.json"
$bom | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $sbomPath -Encoding utf8

$provenancePath = Join-Path $OutputDir "Images-v${version}-provenance.txt"
$provenanceLines = @(
    "Images v$version - Release Provenance Summary"
    "Generated: $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss UTC', [Globalization.CultureInfo]::InvariantCulture))"
    ""
    "== NuGet Dependencies =="
)

foreach ($comp in $existingComponents) {
    $compName = if ($comp.group) { "$($comp.group).$($comp.name)" } else { $comp.name }
    $compVersion = $comp.version
    $provenanceLines += "  $compName $compVersion"
}

$provenanceLines += ""
$provenanceLines += "== Native Runtimes =="

if (Test-Path -LiteralPath $jpegTranProvPath) {
    $provenanceLines += "  jpegtran 3.1.4.1 (libjpeg-turbo) - optional, lossless JPEG writeback"
    $provenanceLines += "    SHA-256: 2000c205ed99fe2409e42a6cb87c19d88e33e516d5d40ff11bb19b7830e3ee33"
}
if (Test-Path -LiteralPath $gsDir) {
    $provenanceLines += "  Ghostscript 10.07.0 (Artifex) - optional, PDF/EPS/PS/AI preview"
}

$provenanceLines += ""
$provenanceLines += "== Approved Model Definitions (user-supplied, not bundled) =="
foreach ($model in $modelDefinitions) {
    $provenanceLines += "  $($model.name) [$($model.license)] - $($model.purpose)"
    $provenanceLines += "    SHA-256: $($model.sha256)"
}

$provenanceLines += ""
$provenanceLines += "== Verification =="
$provenanceLines += "  Run: Images.exe --system-info"
$provenanceLines += "  Run: Images.exe --codec-report"
$provenanceLines += "  SBOM: $([System.IO.Path]::GetFileName($sbomPath))"

Set-Content -LiteralPath $provenancePath -Value ($provenanceLines -join "`n") -Encoding utf8

Write-Host "SBOM written to: $sbomPath"
Write-Host "Provenance summary written to: $provenancePath"
