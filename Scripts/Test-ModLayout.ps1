#requires -Version 7.0
<#
.SYNOPSIS
Validate explicit version loading, the frozen 2.4.1 baseline and binary provenance.
.DESCRIPTION
Checks file bytes and directory selection without loading mod assemblies or starting a game.
The frozen snapshot includes the original shared textures. Change a texture for 1.7 by
putting the replacement at the same relative path under 1.7/Textures, leaving Common intact.
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [switch]$PassThru
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ModLayout.ps1')
$layout = Get-SscModLayout -RepositoryRoot $RepositoryRoot
$repoRoot = $layout.RepositoryRoot
$boundary = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$checked = 0

foreach ($oldRoot in @('Assemblies', 'Defs', 'Languages', 'Patches', 'Resources', 'Textures', 'Sounds')) {
    if (Test-Path -LiteralPath (Join-Path $repoRoot $oldRoot)) {
        throw "Unversioned runtime directory is not allowed: $oldRoot. Use Common or a version directory."
    }
}
[xml]$loadFolders = Get-Content -LiteralPath (Join-Path $repoRoot 'LoadFolders.xml') -Raw
[xml]$about = Get-Content -LiteralPath (Join-Path $repoRoot 'About/About.xml') -Raw
if ($about.ModMetaData.packageId -cne 'rjw.sexslavecraft.TieJinVer') {
    throw 'The existing packageId must be preserved during this directory migration.'
}
foreach ($gameVersion in @('1.6', '1.7')) {
    $entries = @($loadFolders.SelectNodes("/loadFolders/v$gameVersion/li"))
    if ($entries.Count -ne 2 -or $entries[0].InnerText -cne 'Common' -or
        $entries[1].InnerText -cne $gameVersion -or @($entries | Where-Object { $_.Attributes.Count -gt 0 }).Count -gt 0) {
        throw "LoadFolders.xml must explicitly select Common and $gameVersion, in that order."
    }
    if ($gameVersion -notin @($about.ModMetaData.supportedVersions.li)) {
        throw "About.xml is missing the intended game version $gameVersion."
    }
    $versionLayout = Get-SscModLayout -RepositoryRoot $repoRoot -GameVersion $gameVersion
    foreach ($directory in @('Defs', 'Languages', 'Patches', 'Assemblies')) {
        $path = Join-Path $versionLayout.RuntimeRoot $directory
        if (-not (Test-Path -LiteralPath $path -PathType Container) -or
            @(Get-ChildItem -LiteralPath $path -Recurse -File).Count -eq 0) {
            throw "Required nonempty runtime directory is missing: $gameVersion/$directory"
        }
    }
    if (-not (Test-Path -LiteralPath $versionLayout.AssemblyPath -PathType Leaf)) {
        throw "The $gameVersion SSC assembly is missing."
    }
    $dlls = @(Get-ChildItem -LiteralPath $versionLayout.RuntimeRoot -Recurse -File -Filter '*.dll')
    if ($dlls.Count -ne 1 -or $dlls[0].FullName -ne $versionLayout.AssemblyPath) {
        throw "Only $gameVersion/Assemblies/Sexslavecraft.dll may be present; do not copy dependencies into the mod."
    }
    $checked += $entries.Count + $dlls.Count
}
if (-not (Test-Path -LiteralPath (Join-Path $layout.CommonRoot 'Textures') -PathType Container)) {
    throw 'Common/Textures is missing.'
}
foreach ($directory in Get-ChildItem -LiteralPath $layout.CommonRoot -Directory) {
    if ($directory.Name -cne 'Textures') { throw "Only shared textures belong in Common: $($directory.Name)" }
}
if (@(Get-ChildItem -LiteralPath $layout.CommonRoot -Recurse -File -Filter '*.dll').Count -gt 0) {
    throw 'Common must not contain assemblies.'
}

$baseline = Get-Content -LiteralPath (Join-Path $repoRoot '1.6/BASELINE.json') -Raw | ConvertFrom-Json
if ($baseline.schemaVersion -ne 1 -or $baseline.gameVersion -ne '1.6' -or $baseline.version -ne '2.4.1' -or
    $baseline.assemblyPath -cne '1.6/Assemblies/Sexslavecraft.dll' -or $baseline.sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'The 1.6 baseline must identify the original 2.4.1 runtime and source commit.'
}
$baselinePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $baseline.files) {
    $relative = [string]$entry.path
    if ($relative -notmatch '^(?:1\.6/(?:Defs|Languages|Patches|Resources|Assemblies)/|Common/Textures/)' -or
        $relative.Contains('\') -or $relative.Split('/') -contains '..') {
        throw "Invalid frozen baseline path: $relative"
    }
    if (-not $baselinePaths.Add($relative)) { throw "Duplicate frozen baseline path: $relative" }
    $path = [IO.Path]::GetFullPath((Join-Path $repoRoot $relative))
    if (-not $path.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw "Baseline path escapes the repository: $relative" }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-Item -LiteralPath $path).Length -ne $entry.bytes -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.sha256) {
        throw "Frozen 1.6 baseline file changed or is missing: $relative"
    }
    $checked++
}
if (-not $baselinePaths.Contains('1.6/Assemblies/Sexslavecraft.dll') -or
    @($baseline.files | Where-Object path -Like 'Common/Textures/*').Count -eq 0) {
    throw 'The frozen baseline must include its DLL and shared texture hashes.'
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repoRoot '1.6') -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/')
    if ($relative -cne '1.6/BASELINE.json' -and -not $baselinePaths.Contains($relative)) {
        throw "Unexpected file added to the frozen 1.6 baseline: $relative"
    }
}
$oldDllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $repoRoot $baseline.assemblyPath))
if ($oldDllVersion.ProductVersion -ne '2.4.1' -or $oldDllVersion.FileVersion -ne '2.4.1.0') {
    throw 'The frozen 1.6 DLL must keep its 2.4.1 metadata.'
}
$buildInfo = Get-Content -LiteralPath (Join-Path $repoRoot '1.7/BUILD-INFO.json') -Raw | ConvertFrom-Json
$currentVersion = (Get-Content -LiteralPath (Join-Path $repoRoot 'VERSION') -Raw).Trim()
if ($buildInfo.gameVersion -ne '1.7' -or $buildInfo.binaryOrigin -notin @('baseline-copy', 'rebuilt') -or
    $buildInfo.assemblyPath -cne '1.7/Assemblies/Sexslavecraft.dll' -or
    $buildInfo.sourceVersion -ne $currentVersion -or $buildInfo.sourceCommit -notmatch '^[0-9a-f]{40}$' -or
    $buildInfo.gameRuntimeVerified -isnot [bool]) {
    throw 'The 1.7 BUILD-INFO.json contains invalid version or binary provenance metadata.'
}
if ((Get-FileHash -LiteralPath $layout.AssemblyPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $buildInfo.sha256) {
    throw 'The 1.7 DLL hash does not match BUILD-INFO.json. Rebuild using Scripts/Build-Mod.ps1.'
}
if ($buildInfo.binaryOrigin -eq 'baseline-copy' -and
    ($buildInfo.sourceCommit -ne $baseline.sourceCommit -or $buildInfo.sourceVersion -ne $baseline.version -or
     $buildInfo.sha256 -cne ($baseline.files | Where-Object path -CEQ $baseline.assemblyPath).sha256 -or
     $buildInfo.gameRuntimeVerified)) {
    throw 'An initial 1.7 baseline copy must match the frozen DLL and remain unverified in game.'
}
$checked++
Write-Host "Version layout and frozen 1.6 baseline: $checked files/entries checked. 1.7 binary: $($buildInfo.binaryOrigin)."
if ($PassThru) {
    [pscustomobject]@{ Name = 'ModLayout'; Success = $true; CheckedFiles = $checked; GameVersions = @('1.6', '1.7'); BaselineVersion = '2.4.1' }
}
