[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$previousSmokeFlag = $env:RUN_BACKGROUND_SMOKE_TESTS

Push-Location $repoRoot
try {
    dotnet build Images.sln -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE."
    }

    $env:RUN_BACKGROUND_SMOKE_TESTS = '1'
    $resultsDirectory = Join-Path $repoRoot 'artifacts\wpf-background-smoke'
    dotnet test Images.sln -c Release --no-build `
        --filter 'Category=BackgroundSmoke' `
        --logger 'trx;LogFileName=wpf-background-smoke.trx' `
        --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Background WPF smoke failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:RUN_BACKGROUND_SMOKE_TESTS = $previousSmokeFlag
    Pop-Location
}
