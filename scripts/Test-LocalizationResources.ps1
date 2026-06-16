param(
    [string]$ResourcesRoot = (Join-Path $PSScriptRoot '..\src\Images\Localization')
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

$basePath = Join-Path $ResourcesRoot 'Strings.resx'
if (-not (Test-Path -LiteralPath $basePath)) {
    throw "Base localization resource missing: $basePath"
}

$baseKeys = @(Get-ResxKeys -Path $basePath)
if ($baseKeys.Count -eq 0) {
    throw "Base localization resource has no keys: $basePath"
}

$localeFiles = @(Get-ChildItem -LiteralPath $ResourcesRoot -Filter 'Strings.*.resx' -File |
    Where-Object { $_.Name -ne 'Strings.resx' })

foreach ($localeFile in $localeFiles) {
    $localeKeys = @(Get-ResxKeys -Path $localeFile.FullName)
    $missing = @($baseKeys | Where-Object { $_ -notin $localeKeys })
    $extra = @($localeKeys | Where-Object { $_ -notin $baseKeys })

    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        if ($missing.Count -gt 0) {
            Write-Error "$($localeFile.Name) is missing keys: $($missing -join ', ')"
        }
        if ($extra.Count -gt 0) {
            Write-Error "$($localeFile.Name) has unknown keys: $($extra -join ', ')"
        }
        exit 1
    }
}

Write-Host "Localization resource parity passed. Base keys: $($baseKeys.Count); locale files: $($localeFiles.Count)."
