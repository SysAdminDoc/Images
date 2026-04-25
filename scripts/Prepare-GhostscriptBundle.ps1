[CmdletBinding()]
param(
    [string] $Source,
    [string] $Destination = (Join-Path $PSScriptRoot "..\src\Images\Codecs\Ghostscript"),
    [switch] $Force
)

$ErrorActionPreference = "Stop"

function Resolve-GhostscriptSource {
    param([string] $RequestedSource)

    if (-not [string]::IsNullOrWhiteSpace($RequestedSource)) {
        return (Resolve-Path -LiteralPath $RequestedSource).Path
    }

    if (-not [string]::IsNullOrWhiteSpace($env:IMAGES_GHOSTSCRIPT_DIR) -and
        (Test-Path -LiteralPath $env:IMAGES_GHOSTSCRIPT_DIR)) {
        return (Resolve-Path -LiteralPath $env:IMAGES_GHOSTSCRIPT_DIR).Path
    }

    $roots = @(
        (Join-Path $env:ProgramFiles "gs"),
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} "gs" })
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($root in $roots) {
        $candidate = Get-ChildItem -LiteralPath $root -Directory -Filter "gs*" |
            Sort-Object {
                try { [version]($_.Name -replace "^gs", "") }
                catch { [version]"0.0" }
            } -Descending |
            Select-Object -First 1

        if ($candidate) {
            $bin = Join-Path $candidate.FullName "bin"
            if (Test-GhostscriptRuntime $candidate.FullName) { return $candidate.FullName }
            if ((Test-Path -LiteralPath $bin) -and (Test-GhostscriptRuntime $bin)) { return $bin }
        }
    }

    throw "Ghostscript source not found. Pass -Source or set IMAGES_GHOSTSCRIPT_DIR."
}

function Test-GhostscriptRuntime {
    param([string] $Path)

    $flatDll = Join-Path $Path "gsdll64.dll"
    $nestedDll = Join-Path $Path "bin\gsdll64.dll"
    $flatDll32 = Join-Path $Path "gsdll32.dll"
    $nestedDll32 = Join-Path $Path "bin\gsdll32.dll"

    return (Test-Path -LiteralPath $flatDll) -or
        (Test-Path -LiteralPath $nestedDll) -or
        (Test-Path -LiteralPath $flatDll32) -or
        (Test-Path -LiteralPath $nestedDll32)
}

$resolvedSource = Resolve-GhostscriptSource $Source
if (-not (Test-GhostscriptRuntime $resolvedSource)) {
    throw "No Ghostscript DLL found in '$resolvedSource'. Expected gsdll64.dll or gsdll32.dll."
}

$destinationFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
New-Item -ItemType Directory -Path $destinationFull -Force | Out-Null

$existing = Get-ChildItem -LiteralPath $destinationFull -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne "README.txt" }

if ($existing -and -not $Force) {
    throw "Destination '$destinationFull' already contains files. Re-run with -Force to merge/overwrite."
}

Get-ChildItem -LiteralPath $resolvedSource -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $destinationFull -Recurse -Force
}

if (-not (Test-GhostscriptRuntime $destinationFull)) {
    throw "Copy completed, but '$destinationFull' does not contain a usable Ghostscript DLL."
}

Write-Host "Ghostscript runtime copied."
Write-Host "Source:      $resolvedSource"
Write-Host "Destination: $destinationFull"
Write-Host "Next: dotnet publish src/Images -c Release -r win-x64 --no-self-contained -o publish"
