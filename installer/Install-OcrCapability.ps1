#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

$logDirectory = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

Start-Transcript -Path $LogPath -Force | Out-Null

try {
    $candidateLanguages = New-Object System.Collections.Generic.List[string]
    foreach ($tag in @(
        [System.Globalization.CultureInfo]::CurrentUICulture.Name,
        [System.Globalization.CultureInfo]::CurrentCulture.Name,
        'en-US'
    )) {
        if (-not [string]::IsNullOrWhiteSpace($tag) -and -not $candidateLanguages.Contains($tag)) {
            $candidateLanguages.Add($tag)
        }
    }

    Write-Host "Installing Windows OCR optional capability if needed."
    Write-Host ("Candidate OCR languages: {0}" -f ($candidateLanguages -join ', '))

    foreach ($language in $candidateLanguages) {
        $capabilityName = "Language.OCR~~~$language~0.0.1.0"
        $capability = Get-WindowsCapability -Online -Name $capabilityName -ErrorAction SilentlyContinue |
            Select-Object -First 1

        if ($capability -and $capability.State -eq 'Installed') {
            Write-Host "$capabilityName is already installed."
            continue
        }

        try {
            Write-Host "Installing $capabilityName..."
            $result = Add-WindowsCapability -Online -Name $capabilityName -ErrorAction Stop
            Write-Host ("Install result for {0}: State={1}; RestartNeeded={2}" -f $capabilityName, $result.State, $result.RestartNeeded)
        }
        catch {
            Write-Warning ("Failed to install {0}: {1}" -f $capabilityName, $_.Exception.Message)
        }
    }

    $installedOcrCapabilities = @(Get-WindowsCapability -Online -Name 'Language.OCR*' -ErrorAction Stop |
        Where-Object { $_.State -eq 'Installed' })

    if ($installedOcrCapabilities.Count -eq 0) {
        throw 'No Windows OCR optional capability is installed after provisioning.'
    }

    Write-Host 'Installed Windows OCR optional capabilities:'
    foreach ($capability in $installedOcrCapabilities) {
        Write-Host ("  {0}" -f $capability.Name)
    }
}
finally {
    Stop-Transcript | Out-Null
}
