param(
    [string]$ResourcesRoot = (Join-Path $PSScriptRoot '..\src\Images\Localization'),
    [string]$SourceRoot = (Join-Path $PSScriptRoot '..\src\Images')
)

$ErrorActionPreference = 'Stop'

function Get-ResxKeys {
    param([Parameter(Mandatory)][string]$Path)

    [xml]$document = Get-Content -LiteralPath $Path
    $document.root.data |
        Where-Object { $_.name } |
        ForEach-Object { $_.name } |
        Sort-Object -Unique
}

function Get-StringsCsProperties {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    $content = Get-Content -LiteralPath $Path -Raw
    $matches = [regex]::Matches($content, 'public\s+static\s+string\s+(\w+)\s*=>\s*Get\(nameof\(')
    $matches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
}

# --- Check base resx exists ---
$basePath = Join-Path $ResourcesRoot 'Strings.resx'
if (-not (Test-Path -LiteralPath $basePath)) {
    throw "Base localization resource missing: $basePath"
}

$baseKeys = @(Get-ResxKeys -Path $basePath)
if ($baseKeys.Count -eq 0) {
    throw "Base localization resource has no keys: $basePath"
}

# --- Check locale file parity ---
$localeFiles = @(Get-ChildItem -LiteralPath $ResourcesRoot -Filter 'Strings.*.resx' -File |
    Where-Object { $_.Name -ne 'Strings.resx' })

$failed = $false

foreach ($localeFile in $localeFiles) {
    $localeKeys = @(Get-ResxKeys -Path $localeFile.FullName)
    $missing = @($baseKeys | Where-Object { $_ -notin $localeKeys })
    $extra = @($localeKeys | Where-Object { $_ -notin $baseKeys })

    if ($missing.Count -gt 0) {
        Write-Error "$($localeFile.Name) is missing keys: $($missing -join ', ')"
        $failed = $true
    }
    if ($extra.Count -gt 0) {
        Write-Error "$($localeFile.Name) has unknown keys: $($extra -join ', ')"
        $failed = $true
    }
}

# --- Check Strings.cs has properties for all resx keys ---
$stringsCs = Join-Path $ResourcesRoot 'Strings.cs'
if (Test-Path -LiteralPath $stringsCs) {
    $csProperties = @(Get-StringsCsProperties -Path $stringsCs)
    $orphanedProperties = @($csProperties | Where-Object { $_ -notin $baseKeys })

    if ($orphanedProperties.Count -gt 0) {
        Write-Error "Strings.cs has properties with no matching resx key: $($orphanedProperties -join ', ')"
        $failed = $true
    }

    $resxOnlyCount = ($baseKeys | Where-Object { $_ -notin $csProperties }).Count
    if ($resxOnlyCount -gt 0) {
        Write-Host "Note: $resxOnlyCount resx keys have no Strings.cs property (used via XAML {Loc} only)."
    }
}

# --- Scan XAML for obvious hard-coded UI strings ---
$xamlFiles = @(Get-ChildItem -LiteralPath $SourceRoot -Filter '*.xaml' -Recurse -File |
    Where-Object { $_.Name -notmatch 'Theme|\.g\.' })

$hardCodedCount = 0
foreach ($xamlFile in $xamlFiles) {
    $content = Get-Content -LiteralPath $xamlFile.FullName -Raw
    $hardCodedMatches = [regex]::Matches($content,
        '(?:Content|Text|Header|Title|ToolTip|AutomationProperties\.Name)\s*=\s*"([^"{][^"]{3,})"')
    foreach ($m in $hardCodedMatches) {
        $value = $m.Groups[1].Value
        if ($value -match '^\{' -or $value -match '^\d' -or $value -match '^#' -or
            $value -match '^pack://' -or $value -match '^\*$' -or $value -match '^Auto$' -or
            $value -match '^\d+(\.\d+)?$' -or $value -match '^[A-Z]:\\' -or
            $value -match 'Segoe|Consolas|Courier') {
            continue
        }
        $hardCodedCount++
    }
}

if ($hardCodedCount -gt 0) {
    Write-Warning "Found $hardCodedCount potential hard-coded UI strings in XAML files. Review for localization."
}

if ($failed) {
    exit 1
}

Write-Host "Localization resource parity passed. Base keys: $($baseKeys.Count); locale files: $($localeFiles.Count); Strings.cs properties verified."
