param(
    [string]$Version = "",
    [string]$RepositoryRoot = ""
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

$projectPath = Resolve-RepoPath "src\Images\Images.csproj"
[xml]$project = Get-Content -Raw -LiteralPath $projectPath
$propertyGroup = @($project.Project.PropertyGroup) | Where-Object { $_.Version } | Select-Object -First 1
if (-not $propertyGroup) {
    throw "Could not read version metadata from $projectPath."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = [string]$propertyGroup.Version
}

$Version = $Version.Trim()
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be plain SemVer X.Y.Z, got '$Version'."
}

$expectedAssemblyVersion = "$Version.0"
if ($propertyGroup.Version -ne $Version) {
    throw "Images.csproj <Version> is '$($propertyGroup.Version)', expected '$Version'."
}
if ($propertyGroup.FileVersion -ne $expectedAssemblyVersion) {
    throw "Images.csproj <FileVersion> is '$($propertyGroup.FileVersion)', expected '$expectedAssemblyVersion'."
}
if ($propertyGroup.AssemblyVersion -ne $expectedAssemblyVersion) {
    throw "Images.csproj <AssemblyVersion> is '$($propertyGroup.AssemblyVersion)', expected '$expectedAssemblyVersion'."
}

[xml]$manifest = Get-Content -Raw -LiteralPath (Resolve-RepoPath "src\Images\app.manifest")
$ns = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
$ns.AddNamespace("asmv1", "urn:schemas-microsoft-com:asm.v1")
$identity = $manifest.SelectSingleNode("/asmv1:assembly/asmv1:assemblyIdentity", $ns)
if ($identity.version -ne $expectedAssemblyVersion) {
    throw "app.manifest assemblyIdentity version is '$($identity.version)', expected '$expectedAssemblyVersion'."
}

$installer = Get-Content -Raw -LiteralPath (Resolve-RepoPath "installer\Images.iss")
if ($installer -notmatch [regex]::Escape("#define MyAppVersion `"$Version`"")) {
    throw "installer\Images.iss default MyAppVersion is not '$Version'."
}

$readme = Get-Content -Raw -LiteralPath (Resolve-RepoPath "README.md")
if ($readme -notmatch [regex]::Escape("version-$Version-") -or
    $readme -notmatch [regex]::Escape("MyAppVersion=$Version")) {
    throw "README version badge or installer command is not synced to '$Version'."
}

Write-Host "Version sync validated for $Version."
