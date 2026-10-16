#requires -Version 7.0
<#
.SYNOPSIS
同步或检查根目录 Languages 与源码项目中的 Sexslavecraft/Languages。
.DESCRIPTION
默认只读检查，-Sync 仅复制缺失或内容不同的文件；遇到镜像独有文件时停止，
避免自动删除仍需人工确认的资源。
#>
[CmdletBinding()]
param(
    [switch]$Sync,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RepositoryRoot)
$sourceRoot = Join-Path $repoRoot 'Languages'
$mirrorRoot = Join-Path $repoRoot 'Sexslavecraft/Languages'
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container) -or
    -not (Test-Path -LiteralPath $mirrorRoot -PathType Container)) {
    throw 'Both Languages and Sexslavecraft/Languages must exist.'
}

$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Sort-Object FullName)
$mirrorFiles = @(Get-ChildItem -LiteralPath $mirrorRoot -Recurse -File)
if ($sourceFiles.Count -eq 0) { throw "Root Languages collection is empty: $sourceRoot" }
$sourceRelative = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $sourceFiles) {
    [void]$sourceRelative.Add([IO.Path]::GetRelativePath($sourceRoot, $file.FullName))
}
$extra = @($mirrorFiles | Where-Object {
    -not $sourceRelative.Contains([IO.Path]::GetRelativePath($mirrorRoot, $_.FullName))
})
if ($extra.Count -gt 0) {
    $relativeExtra = [IO.Path]::GetRelativePath($mirrorRoot, $extra[0].FullName).Replace('\', '/')
    throw "Language mirror contains $($extra.Count) file(s) absent from root Languages: Sexslavecraft/Languages/$relativeExtra"
}

$missing = 0
$different = 0
$copied = 0
$problems = [Collections.Generic.List[string]]::new()
foreach ($file in $sourceFiles) {
    $relative = [IO.Path]::GetRelativePath($sourceRoot, $file.FullName)
    $displayRelative = $relative.Replace('\', '/')
    $target = Join-Path $mirrorRoot $relative
    $exists = Test-Path -LiteralPath $target -PathType Leaf
    if (-not $exists) {
        $missing++
        $problems.Add("Language mirror is missing: Sexslavecraft/Languages/$displayRelative")
    }
    elseif ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
        $different++
        $problems.Add("Language content differs: Languages/$displayRelative <> Sexslavecraft/Languages/$displayRelative")
    }
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
    if ($missing -gt 0 -or $different -gt 0) {
        throw ("Languages mirror is out of date:`n" + ($problems -join "`n") + "`nRun Scripts/Sync-LanguagesMirror.ps1 -Sync after reviewing root sources.")
    }
}
# 统一入口只使用默认只读模式；手动 -Sync 完成后的结果也可供维护脚本读取。
if ($PassThru) {
    [pscustomobject][ordered]@{ Name = 'LanguagesMirrors'; Success = $true; CheckedFiles = $sourceFiles.Count; CopiedFiles = $copied }
}
