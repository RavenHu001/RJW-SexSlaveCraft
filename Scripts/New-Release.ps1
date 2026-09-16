#requires -Version 7.0
<#
.SYNOPSIS
校验并打包当前模组；传入 -Build 时先重新编译运行时 DLL。
.DESCRIPTION
默认使用仓库中的预编译 DLL，因此没有游戏依赖的机器也能重复打包。
改动 C# 后必须先编译，或使用 -Build。所有模式均运行回归测试和版本检查。
ReferenceTargetsPath 可指向本机的 MSBuild 引用覆盖文件，避免把个人安装路径写入项目。
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [string]$ReferenceTargetsPath,
    [string]$MSBuildPath,
    [string]$OutputDirectory,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
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
if (-not $Build -and ($ReferenceTargetsPath -or $MSBuildPath)) {
    throw 'Build options require -Build.'
}
foreach ($requiredDirectory in @('Defs', 'Languages', 'Textures', 'Resources')) {
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
    if (-not $IsWindows) { throw '-Build requires Windows and Visual Studio MSBuild.' }
    # 旧式 .NET Framework 项目使用 Visual Studio MSBuild。先检查 PATH，再通过
    # vswhere 找到实际安装目录，不假定 Visual Studio 安装在系统盘。
    if (-not $MSBuildPath) {
        $msbuildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
        if ($msbuildCommand) { $MSBuildPath = $msbuildCommand.Source }
        else {
            $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
            if (Test-Path -LiteralPath $vswhere) {
                # 先等待 vswhere 完整退出再取第一项；管道直接 Select-Object -First 1
                # 会提前截断原生命令，导致严格模式下 LASTEXITCODE 尚未设置。
                $msbuildCandidates = @(& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe')
                if ($LASTEXITCODE -ne 0) { throw 'vswhere failed.' }
                if ($msbuildCandidates.Count -gt 0) { $MSBuildPath = $msbuildCandidates[0] }
            }
        }
    }
    if (-not $MSBuildPath -or -not (Test-Path -LiteralPath $MSBuildPath -PathType Leaf)) {
        throw 'Visual Studio MSBuild was not found. Supply -MSBuildPath.'
    }
    $buildArguments = @(
        (Join-Path $repoRoot 'Sexslavecraft/SexSlaveCraft_Alpha.csproj'),
        '/t:Rebuild', '/p:Configuration=Release', '/nologo', '/verbosity:minimal'
    )
    if ($ReferenceTargetsPath) {
        $targetsPath = (Resolve-Path -LiteralPath $ReferenceTargetsPath).Path
        $buildArguments += "/p:CustomAfterMicrosoftCommonTargets=$targetsPath"
    }
    & $MSBuildPath @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "Release build failed ($LASTEXITCODE)." }
}

# 检查真正要交付的二进制；仅改 AssemblyInfo 而未重新编译会在这里被拒绝。
# 版本相同不能证明源码和 DLL 完全一致，故源码改动后的正式发布应使用 -Build。
$dllPath = Join-Path $repoRoot 'Assemblies/Sexslavecraft.dll'
$dllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath)
if ($dllVersion.FileVersion -ne $fileVersion -or $dllVersion.ProductVersion -ne $version) {
    throw "DLL version does not match $version. Rebuild before packaging."
}

# 测试直接使用仓库生产代码；阶段测试还读取实际 XML。任一套件失败均停止打包。
# NuGet.Config 明确禁用外部源；测试没有包依赖，只要求本机安装 .NET SDK 9。
$testSuites = @(
    @{ Project = 'Tests/RitualProgression/RitualProgression.csproj'; Arguments = @((Join-Path $repoRoot 'Defs/HediffDefs/HediffOfSexSlave.xml')) },
    @{ Project = 'Tests/RitualLifecycle/RitualLifecycle.csproj'; Arguments = @() },
    @{ Project = 'Tests/PersonalityTraits/PersonalityTraits.csproj'; Arguments = @() },
    # 2.2.13 新增修复的回归随打包执行；UAP 兼容用例包含在 RitualLifecycle 中。
    @{ Project = 'Tests/PersonalityInsertion/PersonalityInsertion.csproj'; Arguments = @() },
    @{ Project = 'Tests/GenderChangeMemory/GenderChangeMemory.csproj'; Arguments = @($repoRoot) },
    @{ Project = 'Tests/RitualAnimationFallback/RitualAnimationFallback.csproj'; Arguments = @() },
    @{ Project = 'Tests/PermanentLactation/PermanentLactation.csproj'; Arguments = @($repoRoot) },
    @{ Project = 'Tests/PersonalityMemories/PersonalityMemories.csproj'; Arguments = @() },
    @{ Project = 'Tests/PersonalitySpecializations/PersonalitySpecializations.csproj'; Arguments = @() },
    @{ Project = 'Tests/PersonalityCardLayout/PersonalityCardLayout.csproj'; Arguments = @($repoRoot) },
    @{ Project = 'Tests/RaceInjection/RaceInjection.csproj'; Arguments = @() },
    @{ Project = 'Tests/InteractionProtection/InteractionProtection.csproj'; Arguments = @() },
    @{ Project = 'Tests/BusTrade/BusTrade.csproj'; Arguments = @() }
)
foreach ($testSuite in $testSuites) {
    $testProject = Join-Path $repoRoot $testSuite.Project
    $testArguments = $testSuite.Arguments
    & dotnet restore $testProject --configfile (Join-Path $repoRoot 'Tests/NuGet.Config') --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Test restore failed: $testProject ($LASTEXITCODE)." }
    & dotnet run --project $testProject --configuration Release --no-restore -- @testArguments
    if ($LASTEXITCODE -ne 0) { throw "Regression tests failed: $testProject ($LASTEXITCODE)." }
}

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
    foreach ($directory in @('About', 'Defs', 'Languages', 'Textures', 'Resources', 'Sounds', 'Patches')) {
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
    [IO.Directory]::CreateDirectory((Join-Path $modRoot 'Assemblies')) | Out-Null
    Copy-Item -LiteralPath $dllPath -Destination (Join-Path $modRoot 'Assemblies/Sexslavecraft.dll')
    foreach ($document in @('VERSION', 'CHANGELOG.md', 'READ ME!!!.md', '读我，玩法介绍.md', '机制详解.md', 'PLAYER_GUIDE_EN.md')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $document) -Destination $modRoot
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'Docs') -Destination (Join-Path $modRoot 'Docs') -Recurse
    $loadFolders = Join-Path $repoRoot 'LoadFolders.xml'
    if (Test-Path -LiteralPath $loadFolders) { Copy-Item -LiteralPath $loadFolders -Destination $modRoot }
    @"
SexSlaveCraft $version (TieJin Modify) / RimWorld 1.6

安装：退出游戏，将 ZIP 中的 RJW-SexSlaveCraft-TieJin-Modify 文件夹放入 RimWorld/Mods。
更新旧版时先将旧模组文件夹移出 Mods，再放入本版，避免遗留已删除的文件。
启用 Harmony、RimJobWorld 以及本模组；其余依赖与玩法说明见随包玩家指南。
本包不含游戏及第三方模组 DLL。更新内容见 CHANGELOG.md。

Install: close the game and extract the enclosed mod folder into RimWorld/Mods.
When upgrading, move the old mod folder out of Mods before installing this version.
Enable Harmony, RimJobWorld and this mod; see the included guides for compatibility.
Game and third-party mod DLLs are not included. See CHANGELOG.md for release notes.
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
        builtAtUtc = [DateTime]::UtcNow.ToString('o')
        baseCommit = $baseCommit
        workingTreeDirty = [bool]$workingTreeStatus
        assemblyRebuilt = [bool]$Build
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
