param(
    [string]$ResourcesRoot = (Join-Path $PSScriptRoot '..\src\Images\Localization'),
    [string]$SourceRoot = (Join-Path $PSScriptRoot '..\src\Images'),
    [string]$PseudoCulture = 'qps-ploc',
    [switch]$GeneratePseudoLocale,
    [switch]$SkipPseudoLocale
)

$ErrorActionPreference = 'Stop'

function ConvertTo-PseudoText {
    param([AllowEmptyString()][string]$Value)

    if ([string]::IsNullOrEmpty($Value)) { return $Value }

    $placeholderPattern = '\{[0-9]+(?:[^{}]*)\}'
    $matches = [regex]::Matches($Value, $placeholderPattern)
    $builder = [System.Text.StringBuilder]::new()
    $lastIndex = 0

    foreach ($match in $matches) {
        if ($match.Index -gt $lastIndex) {
            $segment = $Value.Substring($lastIndex, $match.Index - $lastIndex)
            [void]$builder.Append($segment)
        }

        [void]$builder.Append($match.Value)
        $lastIndex = $match.Index + $match.Length
    }

    if ($lastIndex -lt $Value.Length) {
        $segment = $Value.Substring($lastIndex)
        [void]$builder.Append($segment)
    }

    $pseudo = $builder.ToString()
    if ($Value -notmatch '[A-Za-z0-9]') { return $pseudo }

    $padLength = [Math]::Min(12, [Math]::Max(2, [int][Math]::Ceiling($Value.Length * 0.25)))
    $padding = '!' * $padLength
    return "[$padding $pseudo $padding]"
}

function Get-FormatPlaceholders {
    param([AllowEmptyString()][string]$Value)

    [regex]::Matches($Value, '\{[0-9]+(?:[^{}]*)\}') |
        ForEach-Object { $_.Value }
}

function Write-PseudoLocale {
    param(
        [Parameter(Mandatory)][string]$BasePath,
        [Parameter(Mandatory)][string]$OutputPath
    )

    [xml]$document = Get-Content -LiteralPath $BasePath
    foreach ($data in $document.root.data) {
        if (-not $data.name -or $null -eq $data.value) { continue }
        $data.value = ConvertTo-PseudoText ([string]$data.value)
    }

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    $settings.NewLineChars = "`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace

    $writer = [System.Xml.XmlWriter]::Create($OutputPath, $settings)
    try {
        $document.Save($writer)
    } finally {
        $writer.Dispose()
    }
}

function Test-PseudoLocale {
    param(
        [Parameter(Mandatory)][string]$BasePath,
        [Parameter(Mandatory)][string]$PseudoPath
    )

    [xml]$baseDocument = Get-Content -LiteralPath $BasePath
    [xml]$pseudoDocument = Get-Content -LiteralPath $PseudoPath
    $pseudoByKey = @{}
    foreach ($data in $pseudoDocument.root.data) {
        if ($data.name) { $pseudoByKey[$data.name] = [string]$data.value }
    }

    $failures = New-Object System.Collections.Generic.List[string]
    foreach ($data in $baseDocument.root.data) {
        if (-not $data.name) { continue }

        $key = [string]$data.name
        $baseValue = [string]$data.value
        $pseudoValue = [string]$pseudoByKey[$key]

        $basePlaceholders = @(Get-FormatPlaceholders $baseValue)
        $pseudoPlaceholders = @(Get-FormatPlaceholders $pseudoValue)
        if (($basePlaceholders -join '|') -ne ($pseudoPlaceholders -join '|')) {
            $failures.Add("$key changed format placeholders.")
        }

        if ($baseValue -match '[A-Za-z]' -and $pseudoValue -eq $baseValue) {
            $failures.Add("$key was not pseudo-localized.")
        }

        if ($baseValue -match '[A-Za-z]' -and $pseudoValue.Length -lt $baseValue.Length) {
            $failures.Add("$key pseudo value is shorter than the base value.")
        }
    }

    if ($failures.Count -gt 0) {
        throw "Pseudo-locale validation failed: $($failures -join '; ')"
    }
}

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

$pseudoPath = Join-Path $ResourcesRoot "Strings.$PseudoCulture.resx"
if ($GeneratePseudoLocale) {
    Write-PseudoLocale -BasePath $basePath -OutputPath $pseudoPath
    Write-Host "Generated pseudo-locale resource: $pseudoPath"
}

if (-not $SkipPseudoLocale) {
    if (-not (Test-Path -LiteralPath $pseudoPath)) {
        throw "Pseudo-locale resource missing: $pseudoPath. Run this script with -GeneratePseudoLocale."
    }

    Test-PseudoLocale -BasePath $basePath -PseudoPath $pseudoPath
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

Write-Host "Localization resource parity passed. Base keys: $($baseKeys.Count); locale files: $($localeFiles.Count); Strings.cs properties verified; pseudo-locale: $PseudoCulture."
