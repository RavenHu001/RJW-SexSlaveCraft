#requires -Version 7.0
<#
.SYNOPSIS
只读检查 Sexslavecraft/Defs 中的现有镜像与根目录 Defs 是否逐字节一致。
.DESCRIPTION
Def 镜像是部分集合：不要求为根目录独有文件创建镜像。镜像缺少根源文件、
内容不同、XML 无法解析、必要目录缺失或镜像集合为空时失败；不自动覆盖或删除文件。
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RepositoryRoot)
$sourceRoot = Join-Path $repoRoot 'Defs'
$mirrorRoot = Join-Path $repoRoot 'Sexslavecraft/Defs'
foreach ($directory in @($sourceRoot, $mirrorRoot)) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Required Def directory is missing: $directory"
    }
}
$mirrors = @(Get-ChildItem -LiteralPath $mirrorRoot -Recurse -File | Sort-Object FullName)
if ($mirrors.Count -eq 0) { throw "Def mirror collection is empty: $mirrorRoot" }

# 根目录是唯一源；枚举镜像端，避免把有意未建立镜像的根目录资源误报为缺失。
$problems = [Collections.Generic.List[string]]::new()
$matched = 0
foreach ($mirror in $mirrors) {
    $relative = [IO.Path]::GetRelativePath($mirrorRoot, $mirror.FullName).Replace('\', '/')
    $source = Join-Path $sourceRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        $problems.Add("Root Def is missing: Defs/$relative (mirror: Sexslavecraft/Defs/$relative)")
        continue
    }
    try {
        # 使用原始文件哈希，文本、字段和换行均须一致；XML 相同但同时损坏也应拒绝。
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $mirror.FullName -Algorithm SHA256).Hash) {
            $problems.Add("Def content differs: Defs/$relative <> Sexslavecraft/Defs/$relative")
        }
        else { $matched++ }
        if ($mirror.Extension -ieq '.xml') {
            foreach ($xmlPath in @($source, $mirror.FullName)) {
                try {
                    $document = [xml]::new()
                    $document.Load($xmlPath)
                }
                catch { $problems.Add("Invalid Def XML: $xmlPath ($($_.Exception.Message))") }
            }
        }
    }
    catch { $problems.Add("Cannot inspect Def mirror $relative ($($_.Exception.Message))") }
}

Write-Host "Def mirrors: $($mirrors.Count) checked, $matched byte-identical, $($problems.Count) problem(s)."
if ($problems.Count -gt 0) {
    throw ("Def mirror validation failed:`n" + ($problems -join "`n"))
}
if ($PassThru) {
    [pscustomobject][ordered]@{ Name = 'DefMirrors'; Success = $true; CheckedFiles = $mirrors.Count }
}
