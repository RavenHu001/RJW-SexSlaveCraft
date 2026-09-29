#requires -Version 7.0
<#
.SYNOPSIS
同步或检查根目录 Languages 与源码项目中的 Sexslavecraft/Languages。
.DESCRIPTION
默认只读检查，-Sync 仅复制缺失或内容不同的文件；遇到镜像独有文件时停止，
避免自动删除仍需人工确认的资源。
#>
[CmdletBinding()]
param([switch]$Sync)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = Join-Path $repoRoot 'Languages'
$mirrorRoot = Join-Path $repoRoot 'Sexslavecraft/Languages'
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container) -or
    -not (Test-Path -LiteralPath $mirrorRoot -PathType Container)) {
    throw 'Both Languages and Sexslavecraft/Languages must exist.'
}

$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File)
$mirrorFiles = @(Get-ChildItem -LiteralPath $mirrorRoot -Recurse -File)
$sourceRelative = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $sourceFiles) {
    [void]$sourceRelative.Add([IO.Path]::GetRelativePath($sourceRoot, $file.FullName))
}
$extra = @($mirrorFiles | Where-Object {
    -not $sourceRelative.Contains([IO.Path]::GetRelativePath($mirrorRoot, $_.FullName))
})
if ($extra.Count -gt 0) {
    throw "Mirror contains $($extra.Count) file(s) absent from root Languages: $($extra[0].FullName)"
}

$missing = 0
$different = 0
$copied = 0
foreach ($file in $sourceFiles) {
    $relative = [IO.Path]::GetRelativePath($sourceRoot, $file.FullName)
    $target = Join-Path $mirrorRoot $relative
    $exists = Test-Path -LiteralPath $target -PathType Leaf
    if (-not $exists) { $missing++ }
    elseif ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) { $different++ }
    else { continue }

    if ($Sync) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        $copied++
    }
}

if ($Sync) {
    Write-Host "Languages mirror synchronized: $copied copied ($missing missing, $different changed)."
    foreach ($file in $sourceFiles) {
        $target = Join-Path $mirrorRoot ([IO.Path]::GetRelativePath($sourceRoot, $file.FullName))
        if (-not (Test-Path -LiteralPath $target -PathType Leaf) -or
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
            throw "Languages mirror verification failed after sync: $target"
        }
    }
} else {
    Write-Host "Languages mirror: $($sourceFiles.Count) source files, $missing missing, $different different, $($extra.Count) extra."
    if ($missing -gt 0 -or $different -gt 0) { throw 'Languages mirror is out of date. Run Scripts/Sync-LanguagesMirror.ps1 -Sync.' }
}
