#requires -Version 7.0
<#
.SYNOPSIS
校验并打包双版本模组；传入 -Build 时只重新编译 1.7 DLL。
.DESCRIPTION
默认保留 1.6 的 2.4.1 冻结成品，并使用 1.7 中的预编译 DLL。
改动 C# 后必须先编译，或使用 -Build。所有模式均检查冻结基线、布局及当前 1.7 回归。
1.7 直接引用游戏程序集的构建通过 Build-Mod.ps1 完成，需要匹配的 .NET SDK。
全部回归统一由 Test-All.ps1 执行；HarmonyAssemblyPath 或 SSC_TEST_HARMONY_PATH
必须指向测试使用的 net9.0 Harmony，ValidationReportDirectory 可指定验证报告目录。
RepositoryRef 默认 main，开发包资料链接随主分支更新；正式发布可显式传入对应 tag。
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [string]$ReferenceTargetsPath,
    [string]$MSBuildPath,
    [string]$GameRoot,
    [string]$RjwAssemblyPath,
    [string]$AnimationsAssemblyPath,
    [string]$GameHarmonyAssemblyPath,
    [string]$DotnetPath,
    [string]$HarmonyAssemblyPath = $env:SSC_TEST_HARMONY_PATH,
    [string]$ValidationReportDirectory,
    [string]$OutputDirectory,
    [ValidateNotNullOrEmpty()][string]$RepositoryRef = 'main',
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RepositoryRef -match '[\x00-\x20\x7f]') { throw 'RepositoryRef must not contain whitespace or control characters.' }
$encodedRepositoryRef = [Uri]::EscapeDataString($RepositoryRef)
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = (Get-Content -LiteralPath (Join-Path $repoRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw 'VERSION must contain a three-part numeric release version, for example 2.2.9.'
}

# VERSION 是发布版本的基准；先检查源文件，避免完成编译和压缩后才发现版本未同步。
$assemblySource = Get-Content -LiteralPath (Join-Path $repoRoot 'Sexslavecraft/Properties/AssemblyInfo.cs') -Raw
$fileVersion = "$version.0"
if ($assemblySource -notmatch ('AssemblyFileVersion\("' + [regex]::Escape($fileVersion) + '"\)') -or
    $assemblySource -notmatch ('AssemblyInformationalVersion\("' + [regex]::Escape($version) + '"\)')) {
    throw 'AssemblyInfo.cs file/product versions do not match VERSION.'
}
$changelog = Get-Content -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Raw
if ($changelog -notmatch ('(?m)^## \[' + [regex]::Escape($version) + '\] — \d{4}-\d{2}-\d{2}')) {
    throw 'CHANGELOG.md has no dated section for this release.'
}
[xml]$about = Get-Content -LiteralPath (Join-Path $repoRoot 'About/About.xml') -Raw
if ($about.ModMetaData.description -notmatch ('SexSlaveCraft ' + [regex]::Escape($version) + '(?![\d.])')) {
    throw 'About/About.xml description does not match VERSION.'
}
if ($ReferenceTargetsPath -or $MSBuildPath) {
    throw 'New-Release -Build builds only 1.7 and does not accept legacy MSBuild options. Maintain 1.6 in a separate checkout of the original 2.4.1 source; this workspace preserves its frozen release artifacts.'
}
if (-not $Build -and ($GameRoot -or $RjwAssemblyPath -or $AnimationsAssemblyPath -or $GameHarmonyAssemblyPath -or $DotnetPath)) {
    throw 'Build options require -Build.'
}
foreach ($requiredDirectory in @('Common/Textures', '1.6/Defs', '1.6/Languages', '1.7/Defs', '1.7/Languages')) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $requiredDirectory) -PathType Container)) {
        throw "Required runtime directory is missing: $requiredDirectory"
    }
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'Releases' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$packageName = "RJW-SexSlaveCraft-TieJin-Modify-$version"
$zipPath = Join-Path $outputRoot "$packageName.zip"
$checksumPath = "$zipPath.sha256"
if (-not $Force -and ((Test-Path -LiteralPath $zipPath) -or (Test-Path -LiteralPath $checksumPath))) {
    throw "Release already exists: $zipPath. Use -Force to replace this version's artifacts."
}

if ($Build) {
    $buildOptions = @{ GameVersion = '1.7' }
    foreach ($parameter in @('GameRoot', 'RjwAssemblyPath', 'AnimationsAssemblyPath', 'GameHarmonyAssemblyPath', 'DotnetPath')) {
        $value = Get-Variable -Name $parameter -ValueOnly
        if ($value) { $buildOptions[$parameter] = $value }
    }
    & (Join-Path $PSScriptRoot 'Build-Mod.ps1') @buildOptions
}

# 布局检查验证 LoadFolders、两版隔离、冻结文件哈希及 1.7 二进制来源；不能被测试替身绕过。
$layoutValidation = & (Join-Path $PSScriptRoot 'Test-ModLayout.ps1') -RepositoryRoot $repoRoot -PassThru
if (-not $layoutValidation.Success) { throw 'Mod layout validation failed.' }
$baseline = Get-Content -LiteralPath (Join-Path $repoRoot '1.6/BASELINE.json') -Raw | ConvertFrom-Json
$buildInfo = Get-Content -LiteralPath (Join-Path $repoRoot '1.7/BUILD-INFO.json') -Raw | ConvertFrom-Json
$baselineDll = Join-Path $repoRoot '1.6/Assemblies/Sexslavecraft.dll'
$baselineDllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($baselineDll)
if ($baselineDllVersion.FileVersion -ne "$($baseline.version).0" -or $baselineDllVersion.ProductVersion -ne $baseline.version) {
    throw 'Frozen 1.6 DLL version does not match BASELINE.json.'
}

# 检查真正要交付的二进制；仅改 AssemblyInfo 而未重新编译会在这里被拒绝。
# 版本相同不能证明源码和 DLL 完全一致，故源码改动后的正式发布应使用 -Build。
$dllPath = Join-Path $repoRoot '1.7/Assemblies/Sexslavecraft.dll'
$dllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath)
if ($dllVersion.FileVersion -ne $fileVersion -or $dllVersion.ProductVersion -ne $version) {
    throw "DLL version does not match $version. Rebuild before packaging."
}

# 镜像一致性统一执行并记录；发布另检查 1.7 翻译键／参数。不启动任何游戏版本。
& (Join-Path $PSScriptRoot 'Test-RestrictionLocalization.ps1') -GameVersion '1.7'
$validation = & (Join-Path $PSScriptRoot 'Test-All.ps1') -GameVersion '1.7' -HarmonyAssemblyPath $HarmonyAssemblyPath -ReportDirectory $ValidationReportDirectory -PassThru

$baseCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot read the base Git commit.' }
$workingTreeStatus = & git -C $repoRoot status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'Cannot read the working tree status.' }

# 只在独立临时目录中整理发行内容，不移动工作区中的文件。
# 白名单以运行目录为单位；程序集只取本模组 DLL，防止把游戏或其他模组依赖分发出去。
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$stageRoot = Join-Path $outputRoot ('.staging-' + [guid]::NewGuid().ToString('N'))
$modRoot = Join-Path $stageRoot 'RJW-SexSlaveCraft-TieJin-Modify'
[IO.Directory]::CreateDirectory($modRoot) | Out-Null
try {
    $runtimeDirectories = @('About', 'Common/Textures')
    foreach ($gameVersion in @('1.6', '1.7')) {
        # Waylith 的原生 GLSL 是运行资源，必须与 ShaderDef 一起保留；这不会触发打包或发布。
        foreach ($directory in @('Defs', 'Languages', 'Textures', 'Resources', 'Sounds', 'Patches', 'Shaders')) {
            $runtimeDirectories += "$gameVersion/$directory"
        }
    }
    foreach ($directory in $runtimeDirectories) {
        $sourceRoot = Join-Path $repoRoot $directory
        if (-not (Test-Path -LiteralPath $sourceRoot)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
            # PSD 是美术工程；PNG 与已有 DDS/ZSTD 运行纹理均保留。
            if ($file.Extension -in @('.psd', '.pdb', '.cs', '.dll')) { continue }
            $relativePath = [IO.Path]::GetRelativePath($repoRoot, $file.FullName)
            $destination = Join-Path $modRoot $relativePath
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }
    foreach ($gameVersion in @('1.6', '1.7')) {
        [IO.Directory]::CreateDirectory((Join-Path $modRoot "$gameVersion/Assemblies")) | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot "$gameVersion/Assemblies/Sexslavecraft.dll") -Destination (Join-Path $modRoot "$gameVersion/Assemblies/Sexslavecraft.dll")
    }
    foreach ($metadata in @('LoadFolders.xml', '1.6/BASELINE.json', '1.7/BUILD-INFO.json')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $metadata) -Destination (Join-Path $modRoot $metadata)
    }
    # 文档按文件白名单收集，不能递归复制 Docs（包括被 Git 忽略的本地美术备份）。
    $documents = @(
        'VERSION', 'CHANGELOG.md', 'README.md', 'QUICK_START_EN.md',
        '读我，玩法介绍.md', '机制详解.md', 'PLAYER_GUIDE_EN.md',
        'Docs/模组介绍.md', 'Docs/Players/README.md',
        "Docs/Releases/$version/${version}发布说明.md"
    )
    foreach ($document in $documents) {
        $destination = Join-Path $modRoot $document
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot $document) -Destination $destination
    }
    # 仅改写随包副本：随包文件仍用相对链接，其余资料指向指定分支或标签的源码仓库。
    # 保留锚点、查询参数、标题及尖括号；中文和空格逐路径段进行 URL 编码。
    foreach ($document in $documents | Where-Object { $_ -like '*.md' }) {
        $documentPath = Join-Path $modRoot $document
        $documentDirectory = [IO.Path]::GetDirectoryName($documentPath)
        $rewriteLink = [Text.RegularExpressions.MatchEvaluator]{
            param($match)
            $target = $match.Groups['target'].Value
            $angleWrapped = $target.StartsWith('<')
            if ($angleWrapped) { $target = $target.Substring(1, $target.Length - 2) }
            if ($target -match '^(?:[a-zA-Z][a-zA-Z0-9+.-]*:|//|#|\?)') { return $match.Value }
            $parts = [regex]::Match($target, '^(?<path>[^?#]+)(?<fragment>[?#].*)?$')
            if (-not $parts.Success) { return $match.Value }
            $path = [Uri]::UnescapeDataString($parts.Groups['path'].Value)
            $localPath = [IO.Path]::GetFullPath((Join-Path $documentDirectory $path))
            $relativePath = [IO.Path]::GetRelativePath($modRoot, $localPath).Replace('\', '/')
            if ($relativePath -eq '..' -or $relativePath.StartsWith('../') -or [IO.Path]::IsPathRooted($relativePath)) {
                throw "Documentation link escapes the mod directory: $document -> $target"
            }
            if (Test-Path -LiteralPath $localPath -PathType Leaf) { return $match.Value }
            $view = if (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath) -PathType Container) { 'tree' } else { 'blob' }
            $encodedPath = ($relativePath.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
            $url = "https://github.com/RavenHu001/RJW-SexSlaveCraft/$view/$encodedRepositoryRef/$encodedPath" + $parts.Groups['fragment'].Value
            if ($angleWrapped) { $url = "<$url>" }
            return $match.Groups['prefix'].Value + $url + $match.Groups['suffix'].Value
        }
        $content = Get-Content -LiteralPath $documentPath -Raw
        # 内联链接（含图片）与引用式链接；允许目标中的成对括号和可选标题。
        $content = [regex]::Replace($content, '(?<prefix>\]\([ \t]*)(?<target><[^>\r\n]+>|(?:\\.|[^\\\s()]|\([^()\r\n]*\))+)(?<suffix>[ \t]*(?:"[^"\r\n]*"|''[^''\r\n]*'')?[ \t]*\))', $rewriteLink)
        $content = [regex]::Replace($content, '(?m)^(?<prefix>[ \t]{0,3}\[[^\]\r\n]+\]:[ \t]*)(?<target><[^>\r\n]+>|[^\s]+)(?<suffix>)', $rewriteLink)
        [IO.File]::WriteAllText($documentPath, $content, [Text.UTF8Encoding]::new($false))
    }
    @"
SexSlaveCraft $version (TieJin Modify) / RimWorld 1.6 + 1.7 development

1.6 使用冻结的 $($baseline.version) 基线；1.7 当前二进制来源：$($buildInfo.binaryOrigin)。
游戏按 LoadFolders.xml 选择 Common 和对应版本目录，不要合并两个版本文件夹。
本包通过目录、哈希及 .NET 9 机制回归；1.7 游戏内加载、功能及存读档验证尚未完成。

安装：退出游戏，将 ZIP 中的 RJW-SexSlaveCraft-TieJin-Modify 文件夹放入 RimWorld/Mods。
更新旧版时先将旧模组文件夹移出 Mods，再放入本版，避免遗留已删除的文件。
启用 Harmony、RimJobWorld 以及本模组；其余依赖与玩法说明见随包玩家指南。
本包不含游戏及第三方模组 DLL。更新内容见 CHANGELOG.md。

Install: close the game and extract the enclosed mod folder into RimWorld/Mods.
When upgrading, move the old mod folder out of Mods before installing this version.
Enable Harmony, RimJobWorld and this mod; see the included guides for compatibility.
Game and third-party mod DLLs are not included. See CHANGELOG.md for release notes.
RimWorld selects Common and the matching version folder via LoadFolders.xml; keep them separate.
1.6 retains the frozen $($baseline.version) baseline. 1.7 binary origin: $($buildInfo.binaryOrigin).
The .NET 9 regression suite does not establish in-game 1.7 compatibility; in-game validation is pending.
"@ | Set-Content -LiteralPath (Join-Path $modRoot 'INSTALL.txt') -Encoding utf8NoBOM

    # 每个有效载荷文件均记录大小和哈希，便于定位缺失/损坏资源。清单不计算自身哈希。
    # 未提交工作区明确标记，不能把本地修改的构建误称为某个提交的精确产物。
    $manifestFiles = @(foreach ($file in Get-ChildItem -LiteralPath $modRoot -Recurse -File | Sort-Object FullName) {
        [ordered]@{
            path = [IO.Path]::GetRelativePath($modRoot, $file.FullName).Replace('\', '/')
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    [ordered]@{
        version = $version
        repositoryRef = $RepositoryRef
        builtAtUtc = [DateTime]::UtcNow.ToString('o')
        baseCommit = $baseCommit
        workingTreeDirty = [bool]$workingTreeStatus
        assemblyRebuilt = [bool]$Build
        gameVersions = [ordered]@{
            '1.6' = [ordered]@{
                baselineVersion = $baseline.version
                sourceCommit = $baseline.sourceCommit
                assemblyPath = '1.6/Assemblies/Sexslavecraft.dll'
                fileVersion = $baselineDllVersion.FileVersion
                productVersion = $baselineDllVersion.ProductVersion
                sha256 = (Get-FileHash -LiteralPath $baselineDll -Algorithm SHA256).Hash.ToLowerInvariant()
                validationScope = 'Frozen baseline files and hashes; no 1.6 game session executed.'
            }
            '1.7' = [ordered]@{
                binaryOrigin = $buildInfo.binaryOrigin
                sourceVersion = $buildInfo.sourceVersion
                sourceCommit = $buildInfo.sourceCommit
                assemblyPath = '1.7/Assemblies/Sexslavecraft.dll'
                fileVersion = $dllVersion.FileVersion
                productVersion = $dllVersion.ProductVersion
                sha256 = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
                validationScope = 'Layout, resource checks and .NET 9 regression suite; no 1.7 game session executed.'
            }
        }
        gameValidation = [ordered]@{
            status = 'NotRun'
            reason = 'Packaging does not launch either game version. .NET 9 regression results do not establish RimWorld 1.7 runtime compatibility.'
        }
        layoutValidation = [ordered]@{
            name = $layoutValidation.Name
            status = 'Passed'
            checkedFiles = $layoutValidation.CheckedFiles
        }
        validation = [ordered]@{
            gameVersion = '1.7'
            testRuntime = '.NET 9'
            suiteCount = $validation.SuiteCount
            passedCases = $validation.PassedCases
            totalCases = $validation.TotalCases
            checks = @($validation.Checks | Select-Object Name, Status, CheckedFiles)
        }
        files = $manifestFiles
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $modRoot 'RELEASE-MANIFEST.json') -Encoding utf8NoBOM

    # ZIP 含单一模组根目录，解压到 Mods 即可。先完成临时 ZIP，再替换最终产物。
    $temporaryZip = Join-Path $stageRoot 'release.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($modRoot, $temporaryZip, [IO.Compression.CompressionLevel]::Optimal, $true)
    Move-Item -LiteralPath $temporaryZip -Destination $zipPath -Force:$Force
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$zipHash  $packageName.zip" | Set-Content -LiteralPath $checksumPath -Encoding utf8NoBOM
    Write-Host "Release: $zipPath"
    Write-Host "SHA-256: $zipHash"
    Write-Host "Payload files: $($manifestFiles.Count)"
}
finally {
    # 删除前核对绝对路径边界，且只处理本次随机创建的 staging 目录。
    $resolvedStage = (Resolve-Path -LiteralPath $stageRoot).Path
    $outputBoundary = $outputRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedStage.StartsWith($outputBoundary, [StringComparison]::OrdinalIgnoreCase) -or
        $resolvedStage -ne [IO.Path]::GetFullPath($stageRoot)) {
        throw "Refusing to clean an unexpected staging path: $resolvedStage"
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
