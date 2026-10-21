#requires -Version 7.0
# Exercises orchestration with a fake dotnet process in an isolated repository, not gameplay tests.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$HarmonyAssemblyPath,
    [Parameter(Mandatory)][string]$FrameworkHarmonyAssemblyPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $IsWindows) { throw 'This orchestration fixture uses a Windows dotnet.cmd shim.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$HarmonyAssemblyPath = (Resolve-Path -LiteralPath $HarmonyAssemblyPath).Path
$FrameworkHarmonyAssemblyPath = (Resolve-Path -LiteralPath $FrameworkHarmonyAssemblyPath).Path
$fixture = Join-Path $repo ('.builds/validation-runner/fixture with spaces ' + [guid]::NewGuid().ToString('N'))
$shim = Join-Path $fixture 'shim'
$pwsh = (Get-Process -Id $PID).Path
$savedPath = $env:PATH
$savedHarmony = $env:SSC_TEST_HARMONY_PATH
$savedMode = $env:SSC_VALIDATION_FIXTURE_MODE
$savedNativeDotnet = $env:SSC_DOTNET_PATH
$savedNativeGameRoot = $env:SSC_GAME_ROOT
$checks = 0
$suiteCount = @(Get-ChildItem -LiteralPath (Join-Path $repo 'Tests') -Recurse -Filter '*.csproj').Count
# Nine registered suites require the test Harmony, including both native 1.7 suites.
$harmonySuiteCount = 9

<#
.SYNOPSIS
检查编排行为断言；成功时累计通过数并打印说明，失败时抛出异常终止夹具。
#>
function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
    Write-Host "PASS $Message"
}

<#
.SYNOPSIS
在隔离仓库中运行指定模拟模式的验证或打包入口，保存控制台日志并读取汇总报告。
.DESCRIPTION
返回退出码、解析后的报告及输出目录，由调用方断言失败传播、依赖校验或打包结果。
#>
function Run-Case([string]$Name, [string]$Mode, [string]$Harmony, [bool]$Package = $false) {
    $env:SSC_VALIDATION_FIXTURE_MODE = $Mode
    $reportDir = Join-Path $fixture "reports/$Name"
    $outputDir = Join-Path $fixture "packages/$Name"
    $scriptPath = Join-Path $fixture 'Scripts/Test-All.ps1'
    $arguments = @('-ReportDirectory', $reportDir)
    if ($Package) {
        $scriptPath = Join-Path $fixture 'Scripts/New-Release.ps1'
        $arguments = @('-ValidationReportDirectory', $reportDir, '-OutputDirectory', $outputDir)
    }
    if ($Harmony) { $arguments += @('-HarmonyAssemblyPath', $Harmony) }
    $output = @(& $pwsh -NoProfile -File $scriptPath @arguments 2>&1)
    $code = $LASTEXITCODE
    $output | Set-Content -LiteralPath (Join-Path $fixture "$Name.console.log") -Encoding utf8
    $reportPath = Join-Path $reportDir 'summary.json'
    $report = if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json } else { $null }
    return [pscustomobject]@{ Code=$code; Report=$report; OutputDir=$outputDir; Console=($output -join "`n") }
}

try {
    foreach ($dir in @('Scripts', 'Tests', 'shim', 'About', '1.7/Assemblies', 'Sexslavecraft/Properties', 'Sexslavecraft/Languages', 'Sexslavecraft/Defs/夹具目录', '1.7/Defs/夹具目录', '1.7/Languages', '1.7/Resources', 'Docs')) {
        [IO.Directory]::CreateDirectory((Join-Path $fixture $dir)) | Out-Null
    }
    foreach ($file in @('Scripts/Test-All.ps1', 'Scripts/Test-DefMirrors.ps1', 'Scripts/New-Release.ps1', 'Scripts/Test-ModLayout.ps1', 'Scripts/ModLayout.ps1', 'Scripts/Sync-LanguagesMirror.ps1', 'Scripts/Test-RestrictionLocalization.ps1', 'Tests/NuGet.Config', 'VERSION', 'CHANGELOG.md', 'LoadFolders.xml',
        'About/About.xml', '1.7/Assemblies/Sexslavecraft.dll', '1.7/BUILD-INFO.json', 'Sexslavecraft/Properties/AssemblyInfo.cs', 'README.md',
        'QUICK_START_EN.md', '读我，玩法介绍.md', '机制详解.md', 'PLAYER_GUIDE_EN.md',
        'Docs/模组介绍.md', 'Docs/Players/README.md',
        ('Docs/Releases/{0}/{0}发布说明.md' -f (Get-Content -LiteralPath (Join-Path $repo 'VERSION') -Raw).Trim()))) {
        $destination = Join-Path $fixture $file
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $destination
    }
    # 使用完整的真实冻结基线与共享贴图，生产布局／哈希验证不能被测试替身代替。
    foreach ($directory in @('1.6', 'Common')) {
        Copy-Item -LiteralPath (Join-Path $repo $directory) -Destination (Join-Path $fixture $directory) -Recurse
    }
    foreach ($directory in @('1.7/Resources', '1.7/Patches')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo $directory) -Recurse -File) {
            $destination = Join-Path $fixture ([IO.Path]::GetRelativePath($repo, $file.FullName))
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }
    # 非白名单文件（包括未跟踪备份）不得进入 ZIP；链接转换只作用于随包副本。
    foreach ($file in @('Docs/Art/Backups/local.png', 'Docs/Development/开发 记录.md', 'Docs/Releases/old/验证.md', '1.7/DeveloperNotes.md', '1.7/Tools/fixture.cs')) {
        $destination = Join-Path $fixture $file
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        [IO.File]::WriteAllText($destination, 'Not for players')
    }
    @'

[Fixture local guide](QUICK_START_EN.md#2-getting-started "Local title")
[Fixture development](<Docs/Development/开发 记录.md#核对> "Dev title")
[Fixture reference][fixture-dev]
[fixture-dev]: <Docs/Development/开发 记录.md#核对> "Reference title"
[Fixture directory](Docs/Development/)
[Fixture external](https://example.com/keep?q=1#anchor)
[Fixture anchor](#fixture-anchor)
'@ | Add-Content -LiteralPath (Join-Path $fixture 'README.md') -Encoding utf8
    $readmeHash = (Get-FileHash -LiteralPath (Join-Path $fixture 'README.md')).Hash
    # 小型真实 XML 镜像覆盖路径含中文的场景；无需复制所有运行 Def 或启动游戏。
    $defSource = Join-Path $fixture '1.7/Defs/夹具目录/Example.xml'
    $defMirror = Join-Path $fixture 'Sexslavecraft/Defs/夹具目录/Example.xml'
    $defText = '<Defs><ThingDef><defName>Fixture</defName></ThingDef></Defs>'
    [IO.File]::WriteAllText($defSource, $defText, [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath $defSource -Destination $defMirror
    # 开发里程碑可使用四段 VERSION，正式打包仍要求三段；夹具按复制 DLL 的真实正式元数据隔离测试。
    # 只改隔离目录，不能为了测试打包编排而改写当前仓库版本或重写生产 DLL 元数据。
    $fixtureVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $fixture '1.7/Assemblies/Sexslavecraft.dll')).ProductVersion
    if ($fixtureVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Fixture requires a DLL with three-part release product metadata.' }
    [IO.File]::WriteAllText((Join-Path $fixture 'VERSION'), $fixtureVersion, [Text.UTF8Encoding]::new($false))
    # 发布前检查限制翻译，统一入口再检查完整语言镜像；夹具提供真实的四语前置文件。
    foreach ($language in @('English', 'ChineseSimplified', 'ChineseTraditional', 'Russian')) {
        $relative = "Languages/$language/Keyed/SSC_Restrictions.xml"
        foreach ($prefix in @('1.7/', 'Sexslavecraft/')) {
            $destination = Join-Path $fixture ($prefix + $relative)
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            Copy-Item -LiteralPath (Join-Path $repo ($prefix + $relative)) -Destination $destination
        }
    }
    # 语言镜像要求完整对应，另加含中文文件名的词条用于差异、缺失与手动同步场景。
    $languageRelative = 'Languages/ChineseSimplified/Keyed/夹具词条.xml'
    $languageSource = Join-Path $fixture ('1.7/' + $languageRelative)
    $languageMirror = Join-Path $fixture ('Sexslavecraft/' + $languageRelative)
    $languageText = '<LanguageData><Fixture>夹具</Fixture></LanguageData>'
    foreach ($path in @($languageSource, $languageMirror)) {
        [IO.File]::WriteAllText($path, $languageText, [Text.UTF8Encoding]::new($false))
    }
    $languageFileCount = 5
    # Use each real project name, but no production code or game runtime in this fixture.
    foreach ($project in Get-ChildItem -LiteralPath (Join-Path $repo 'Tests') -Recurse -Filter '*.csproj') {
        $relative = [IO.Path]::GetRelativePath($repo, $project.FullName)
        $destination = Join-Path $fixture $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        '<Project />' | Set-Content -LiteralPath $destination
    }
    @'
@echo off
"__PWSH__" -NoProfile -File "%~dp0Fake-Dotnet.ps1" %*
exit /b %errorlevel%
'@.Replace('__PWSH__', $pwsh) | Set-Content -LiteralPath (Join-Path $shim 'dotnet.cmd') -Encoding ascii
    @'
$command = $args[0]
$mode = $env:SSC_VALIDATION_FIXTURE_MODE
if ($command -eq '--version') {
    if ($mode -eq 'old-sdk') { '8.0.100' } else { '9.0.308' }
    exit 0
}
if ($command -eq '--list-runtimes') {
    if ($mode -eq 'missing-runtime') { 'Microsoft.NETCore.App 8.0.0 [fixture]' }
    else { 'Microsoft.NETCore.App 9.0.11 [fixture]' }
    exit 0
}
if ($command -in @('restore', 'build')) {
    $name = [IO.Path]::GetFileNameWithoutExtension($args[1])
    if ($mode -eq 'failures' -and (($command -eq 'restore' -and $name -eq 'RitualProgression') -or
        ($command -eq 'build' -and $name -eq 'RitualLifecycle'))) { 'Simulated tool failure'; exit 7 }
    exit 0
}
$name = [IO.Path]::GetFileNameWithoutExtension($command)
if ($mode -eq 'failures') {
    if ($name -eq 'PersonalityTraits') { '0/1 passed'; exit 2 }
    if ($name -eq 'PersonalityInsertion') { 'No summary'; exit 0 }
    if ($name -eq 'GenderChangeMemory') { '1/2 passed'; exit 0 }
}
'1/1 passed'
exit 0
'@ | Set-Content -LiteralPath (Join-Path $shim 'Fake-Dotnet.ps1') -Encoding utf8
    # Native suites use an explicit .NET 10 entry. Keep both SDKs simulated so this
    # orchestration fixture never requires the portable SDK or executes game code.
    @'
@echo off
"__PWSH__" -NoProfile -File "%~dp0Fake-NativeDotnet.ps1" %*
exit /b %errorlevel%
'@.Replace('__PWSH__', $pwsh) | Set-Content -LiteralPath (Join-Path $shim 'native-dotnet.cmd') -Encoding ascii
    @'
if ($args[0] -eq '--version') { '10.0.402'; exit 0 }
& (Join-Path $PSScriptRoot 'Fake-Dotnet.ps1') @args
exit $LASTEXITCODE
'@ | Set-Content -LiteralPath (Join-Path $shim 'Fake-NativeDotnet.ps1') -Encoding utf8
    $nativeGameRoot = Join-Path $fixture 'native-game'
    [IO.Directory]::CreateDirectory($nativeGameRoot) | Out-Null
    [IO.File]::WriteAllText((Join-Path $nativeGameRoot 'Assembly-CSharp.dll'), 'Fixture path sentinel; never loaded.')
    $env:SSC_DOTNET_PATH = Join-Path $shim 'native-dotnet.cmd'
    $env:SSC_GAME_ROOT = $nativeGameRoot
    $env:PATH = $shim + [IO.Path]::PathSeparator + $savedPath
    $env:SSC_TEST_HARMONY_PATH = $null
    # Run outside both repositories to check independence from the caller's working directory.
    Push-Location $shim
    try {
        # 布局失败应先于打包输出；每次只污染隔离副本并逐字节恢复。
        $frozenFile = @(Get-ChildItem -LiteralPath (Join-Path $fixture '1.6/Defs') -Recurse -File)[0].FullName
        $frozenBytes = [IO.File]::ReadAllBytes($frozenFile)
        [IO.File]::AppendAllText($frozenFile, '<!-- changed frozen baseline -->')
        $run = Run-Case 'frozen-baseline-changed' 'pass' $HarmonyAssemblyPath $true
        Assert ($run.Code -ne 0 -and -not (Test-Path -LiteralPath $run.OutputDir)) 'Changing a frozen 1.6 runtime file blocks packaging before ZIP creation.'
        [IO.File]::WriteAllBytes($frozenFile, $frozenBytes)

        $sharedFile = @(Get-ChildItem -LiteralPath (Join-Path $fixture 'Common/Textures') -Recurse -File)[0].FullName
        $sharedBytes = [IO.File]::ReadAllBytes($sharedFile)
        [IO.File]::WriteAllBytes($sharedFile, [byte[]]@(1, 2, 3))
        $run = Run-Case 'shared-baseline-changed' 'pass' $HarmonyAssemblyPath $true
        Assert ($run.Code -ne 0 -and -not (Test-Path -LiteralPath $run.OutputDir)) 'Changing a shared baseline texture blocks packaging and protects 1.6 assets.'
        [IO.File]::WriteAllBytes($sharedFile, $sharedBytes)

        $loadFoldersPath = Join-Path $fixture 'LoadFolders.xml'
        $loadFoldersBytes = [IO.File]::ReadAllBytes($loadFoldersPath)
        [IO.File]::WriteAllText($loadFoldersPath, '<loadFolders><v1.6><li>Common</li><li>1.6</li></v1.6><v1.7><li>Common</li><li>1.6</li></v1.7></loadFolders>')
        $run = Run-Case 'mixed-version-load-folders' 'pass' $HarmonyAssemblyPath $true
        Assert ($run.Code -ne 0 -and -not (Test-Path -LiteralPath $run.OutputDir)) 'LoadFolders cannot select 1.6 runtime files for 1.7.'
        [IO.File]::WriteAllBytes($loadFoldersPath, $loadFoldersBytes)

        $buildInfoPath = Join-Path $fixture '1.7/BUILD-INFO.json'
        $buildInfoBytes = [IO.File]::ReadAllBytes($buildInfoPath)
        $wrongBuildInfo = [Text.Encoding]::UTF8.GetString($buildInfoBytes) | ConvertFrom-Json
        $wrongBuildInfo.sha256 = '0' * 64
        $wrongBuildInfo | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $buildInfoPath -Encoding utf8NoBOM
        $run = Run-Case 'untracked-1.7-binary' 'pass' $HarmonyAssemblyPath $true
        Assert ($run.Code -ne 0 -and -not (Test-Path -LiteralPath $run.OutputDir)) 'A 1.7 DLL whose hash disagrees with BUILD-INFO cannot be packaged.'
        [IO.File]::WriteAllBytes($buildInfoPath, $buildInfoBytes)

        $run = Run-Case 'success' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -eq 0 -and $run.Report.PassedSuites -eq $suiteCount -and $run.Report.TotalCases -eq $suiteCount) 'All suites execute; counts come from actual summaries (not hard-coded 390).'
        Assert ($run.Report.CheckCount -eq 2 -and $run.Report.PassedChecks -eq 2 -and $run.Report.Checks[0].Name -eq 'DefMirrors' -and $run.Report.Checks[0].CheckedFiles -eq 1) 'Def mirrors have a separate successful report; mechanism counts stay unchanged.'
        Assert ($run.Report.Checks[1].Name -eq 'LanguagesMirrors' -and $run.Report.Checks[1].Status -eq 'Passed' -and $run.Report.Checks[1].CheckedFiles -eq $languageFileCount) 'Languages mirrors have their own successful report and file count.'

        # 故意未建镜像的根目录文件可以存在，检查只能读取已有镜像集合。
        $rootOnly = Join-Path $fixture '1.7/Defs/RootOnly.xml'
        [IO.File]::WriteAllText($rootOnly, $defText, [Text.UTF8Encoding]::new($false))
        $standalone = & (Join-Path $fixture 'Scripts/Test-DefMirrors.ps1') -RepositoryRoot $fixture -PassThru
        Assert ($standalone.Success -and $standalone.CheckedFiles -eq 1) 'A root-only Def does not require a new project mirror.'
        Remove-Item -LiteralPath $rootOnly

        # 错误只写入隔离夹具；验证统一入口记录具体路径、标记未执行并保持输入不变。
        [IO.File]::WriteAllText($defMirror, $defText.Replace('Fixture', 'Stale'), [Text.UTF8Encoding]::new($false))
        $staleHash = (Get-FileHash -LiteralPath $defMirror).Hash
        $sourceHash = (Get-FileHash -LiteralPath $defSource).Hash
        $run = Run-Case 'def-different' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and $run.Report.NotRunSuites -eq $suiteCount -and $run.Report.Checks[0].Reason -match 'Def content differs: Defs/夹具目录/Example.xml') 'A stale Def mirror fails validation with a relative path and no mechanism execution.'
        Assert ((Get-FileHash -LiteralPath $defMirror).Hash -eq $staleHash -and (Get-FileHash -LiteralPath $defSource).Hash -eq $sourceHash) 'Def validation is read-only and never silently synchronizes stale content.'
        $run = Run-Case 'def-blocked-package' 'pass' $HarmonyAssemblyPath $true
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and -not (Test-Path -LiteralPath $run.OutputDir)) 'A stale Def mirror blocks ZIP creation even when all test dependencies are valid.'
        Copy-Item -LiteralPath $defSource -Destination $defMirror

        $orphan = Join-Path $fixture 'Sexslavecraft/Defs/Orphan.xml'
        [IO.File]::WriteAllText($orphan, $defText, [Text.UTF8Encoding]::new($false))
        $run = Run-Case 'def-missing-source' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and $run.Report.Checks[0].Reason -match 'Root Def is missing: Defs/Orphan.xml') 'A project mirror without its root source fails validation.'
        Remove-Item -LiteralPath $orphan

        [IO.File]::WriteAllText($defSource, '<Defs>', [Text.UTF8Encoding]::new($false))
        Copy-Item -LiteralPath $defSource -Destination $defMirror
        $run = Run-Case 'def-invalid-xml' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and $run.Report.Checks[0].Reason -match 'Invalid Def XML') 'Byte-identical invalid XML is rejected.'
        [IO.File]::WriteAllText($defSource, $defText, [Text.UTF8Encoding]::new($false))
        Copy-Item -LiteralPath $defSource -Destination $defMirror

        $missingRootRejected = $false
        try { & (Join-Path $fixture 'Scripts/Test-DefMirrors.ps1') -RepositoryRoot (Join-Path $fixture 'missing directory') }
        catch { $missingRootRejected = $_.Exception.Message -match 'Required Def directory is missing' }
        Assert $missingRootRejected 'Missing Def directories cannot silently pass.'
        $emptyRoot = Join-Path $fixture 'empty repository'
        foreach ($dir in @('1.7/Defs', 'Sexslavecraft/Defs')) { [IO.Directory]::CreateDirectory((Join-Path $emptyRoot $dir)) | Out-Null }
        $emptyRejected = $false
        try { & (Join-Path $fixture 'Scripts/Test-DefMirrors.ps1') -RepositoryRoot $emptyRoot }
        catch { $emptyRejected = $_.Exception.Message -match 'Def mirror collection is empty' }
        Assert $emptyRejected 'An empty mirror collection cannot silently pass.'

        $defScript = Join-Path $fixture 'Scripts/Test-DefMirrors.ps1'
        Remove-Item -LiteralPath $defScript
        $run = Run-Case 'def-missing-checker' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and $run.Report.NotRunSuites -eq $suiteCount -and $run.Report.Checks[0].Reason -match 'Test-DefMirrors.ps1') 'A missing checker is recorded as a failed check, not skipped.'
        Copy-Item -LiteralPath (Join-Path $repo 'Scripts/Test-DefMirrors.ps1') -Destination $defScript

        # 所有语言失败场景都经过真实的检查脚本和统一报告；资源错误不得触发自动同步。
        $staleLanguageText = $languageText + '<!-- stale -->'
        [IO.File]::WriteAllText($languageMirror, $staleLanguageText, [Text.UTF8Encoding]::new($false))
        $staleLanguageHash = (Get-FileHash -LiteralPath $languageMirror).Hash
        $languageSourceHash = (Get-FileHash -LiteralPath $languageSource).Hash
        $run = Run-Case 'languages-different' 'pass' $HarmonyAssemblyPath
        $languageCheck = @($run.Report.Checks | Where-Object Name -eq 'LanguagesMirrors')[0]
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and $run.Report.Checks[0].Status -eq 'Passed' -and $run.Report.NotRunSuites -eq $suiteCount -and $languageCheck.Reason -match 'Language content differs: Languages/ChineseSimplified/Keyed/夹具词条.xml') 'A stale Languages mirror fails independently with a relative path and no mechanism execution.'
        Assert ((Get-FileHash -LiteralPath $languageMirror).Hash -eq $staleLanguageHash -and (Get-FileHash -LiteralPath $languageSource).Hash -eq $languageSourceHash) 'Unified Languages validation leaves both source and mirror unchanged.'
        $run = Run-Case 'languages-blocked-package' 'pass' $HarmonyAssemblyPath $true
        Assert ($run.Code -ne 0 -and $run.Report.CheckCount -eq 2 -and $run.Report.FailedChecks -eq 1 -and $run.Report.Checks[1].Name -eq 'LanguagesMirrors' -and -not (Test-Path -LiteralPath $run.OutputDir)) 'A stale Languages mirror is recorded by unified validation and blocks ZIP creation.'

        [IO.File]::WriteAllText($defMirror, $defText.Replace('Fixture', 'Stale'), [Text.UTF8Encoding]::new($false))
        $run = Run-Case 'both-mirrors-different' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.CheckCount -eq 2 -and $run.Report.FailedChecks -eq 2 -and $run.Report.NotRunSuites -eq $suiteCount -and @($run.Report.Checks | Where-Object Status -eq 'Failed').Count -eq 2) 'Both mirror failures are reported; the first failure cannot hide the second check.'
        Copy-Item -LiteralPath $defSource -Destination $defMirror
        Copy-Item -LiteralPath $languageSource -Destination $languageMirror

        Remove-Item -LiteralPath $languageMirror
        $run = Run-Case 'languages-missing-mirror' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and $run.Report.Checks[1].Reason -match 'Language mirror is missing: Sexslavecraft/Languages/ChineseSimplified/Keyed/夹具词条.xml' -and -not (Test-Path -LiteralPath $languageMirror)) 'A missing Languages counterpart fails and is not silently recreated.'
        Copy-Item -LiteralPath $languageSource -Destination $languageMirror

        $extraLanguage = Join-Path $fixture 'Sexslavecraft/Languages/Orphan.xml'
        [IO.File]::WriteAllText($extraLanguage, $languageText, [Text.UTF8Encoding]::new($false))
        $run = Run-Case 'languages-extra-mirror' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and $run.Report.Checks[1].Reason -match 'absent from root Languages: Sexslavecraft/Languages/Orphan.xml') 'An extra Languages mirror with no root source fails.'
        [IO.File]::WriteAllText($languageMirror, $staleLanguageText, [Text.UTF8Encoding]::new($false))
        $extraRejected = $false
        try { & (Join-Path $fixture 'Scripts/Sync-LanguagesMirror.ps1') -RepositoryRoot $fixture -Sync }
        catch { $extraRejected = $_.Exception.Message -match 'absent from root Languages' }
        Assert ($extraRejected -and (Test-Path -LiteralPath $extraLanguage) -and (Get-FileHash -LiteralPath $languageMirror).Hash -eq $staleLanguageHash) 'Manual synchronization refuses orphan files before copying or deleting anything.'
        Remove-Item -LiteralPath $extraLanguage
        $missingLanguage = Join-Path $fixture 'Sexslavecraft/Languages/Russian/Keyed/SSC_Restrictions.xml'
        Remove-Item -LiteralPath $missingLanguage
        $synchronized = & (Join-Path $fixture 'Scripts/Sync-LanguagesMirror.ps1') -RepositoryRoot $fixture -Sync -PassThru
        Assert ($synchronized.Success -and $synchronized.CheckedFiles -eq $languageFileCount -and $synchronized.CopiedFiles -eq 2 -and (Test-Path -LiteralPath $missingLanguage) -and (Get-FileHash -LiteralPath $languageMirror).Hash -eq $languageSourceHash) 'Explicit manual synchronization still repairs missing and stale files.'

        $missingLanguageDirectoryRejected = $false
        try { & (Join-Path $fixture 'Scripts/Sync-LanguagesMirror.ps1') -RepositoryRoot (Join-Path $fixture 'missing language directories') }
        catch { $missingLanguageDirectoryRejected = $_.Exception.Message -match 'Both the selected runtime Languages and Sexslavecraft/Languages must exist' }
        Assert $missingLanguageDirectoryRejected 'Missing Languages directories cannot silently pass.'
        $emptyLanguageRoot = Join-Path $fixture 'empty languages repository'
        foreach ($dir in @('1.7/Languages', 'Sexslavecraft/Languages')) { [IO.Directory]::CreateDirectory((Join-Path $emptyLanguageRoot $dir)) | Out-Null }
        $emptyLanguagesRejected = $false
        try { & (Join-Path $fixture 'Scripts/Sync-LanguagesMirror.ps1') -RepositoryRoot $emptyLanguageRoot }
        catch { $emptyLanguagesRejected = $_.Exception.Message -match 'Root Languages collection is empty' }
        Assert $emptyLanguagesRejected 'An empty Languages source collection cannot silently pass.'

        $languageScript = Join-Path $fixture 'Scripts/Sync-LanguagesMirror.ps1'
        Remove-Item -LiteralPath $languageScript
        $run = Run-Case 'languages-missing-checker' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.FailedChecks -eq 1 -and $run.Report.Checks[0].Status -eq 'Passed' -and $run.Report.NotRunSuites -eq $suiteCount -and $run.Report.Checks[1].Reason -match 'Sync-LanguagesMirror.ps1') 'A missing Languages checker is recorded independently and blocks validation.'
        Copy-Item -LiteralPath (Join-Path $repo 'Scripts/Sync-LanguagesMirror.ps1') -Destination $languageScript

        $run = Run-Case 'missing-harmony' 'pass' ''
        Assert ($run.Code -ne 0 -and $run.Report.PassedSuites -eq ($suiteCount - $harmonySuiteCount) -and $run.Report.NotRunSuites -eq $harmonySuiteCount) 'Missing Harmony is NotRun and fails the overall check.'
        $run = Run-Case 'wrong-harmony' 'pass' $FrameworkHarmonyAssemblyPath
        $frameworkRejections = @($run.Report.Results | Where-Object { $_.Status -eq 'NotRun' -and $_.Reason -match 'net9.0' })
        Assert ($run.Code -ne 0 -and $run.Report.NotRunSuites -eq $harmonySuiteCount -and $frameworkRejections.Count -eq $harmonySuiteCount) 'Framework Harmony is rejected before any Harmony suite runs.'
        $run = Run-Case 'failures' 'failures' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.FailedSuites -eq 5 -and $run.Report.PassedSuites -eq ($suiteCount - 5)) 'Restore, build, test exit, missing summary and failed count all fail; later suites continue.'
        foreach ($mode in @('old-sdk', 'missing-runtime')) {
            $run = Run-Case $mode $mode $HarmonyAssemblyPath
            Assert ($run.Code -ne 0 -and $run.Report.NotRunSuites -eq $suiteCount) "$mode marks all suites NotRun."
        }
        $extra = Join-Path $fixture 'Tests/Unregistered.csproj'
        '<Project />' | Set-Content -LiteralPath $extra
        $run = Run-Case 'unregistered' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.NotRunSuites -eq $suiteCount) 'An unlisted project cannot silently escape validation.'
        Remove-Item -LiteralPath $extra
        $run = Run-Case 'blocked-package' 'pass' '' $true
        Assert ($run.Code -ne 0 -and -not (Test-Path -LiteralPath $run.OutputDir)) 'Incomplete validation stops packaging before any output directory is created.'
        $env:SSC_TEST_HARMONY_PATH = $HarmonyAssemblyPath
        $run = Run-Case 'package' 'pass' '' $true
        Assert ($run.Code -eq 0 -and $run.Report.PassedSuites -eq $suiteCount) 'Packaging uses the unified entry and the Harmony environment variable.'
        $zipPath = @(Get-ChildItem -LiteralPath $run.OutputDir -Filter '*.zip')[0].FullName
        $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $entry = @($zip.Entries | Where-Object FullName -like '*/RELEASE-MANIFEST.json')[0]
            $reader = [IO.StreamReader]::new($entry.Open())
            try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            Assert ($manifest.validation.suiteCount -eq $suiteCount -and $manifest.validation.passedCases -eq $suiteCount) 'Package manifest includes completed validation counts.'
            Assert ($manifest.validation.checks.Count -eq 2 -and $manifest.validation.checks[0].Name -eq 'DefMirrors' -and $manifest.validation.checks[0].Status -eq 'Passed' -and $manifest.validation.checks[0].CheckedFiles -eq 1) 'Package manifest records the successful Def mirror gate separately.'
            Assert ($manifest.validation.checks[1].Name -eq 'LanguagesMirrors' -and $manifest.validation.checks[1].Status -eq 'Passed' -and $manifest.validation.checks[1].CheckedFiles -eq $languageFileCount) 'Package manifest records the successful Languages mirror gate separately.'
            Assert ($manifest.layoutValidation.status -eq 'Passed' -and $manifest.layoutValidation.name -eq 'ModLayout' -and $manifest.layoutValidation.checkedFiles -gt 0) 'Package manifest records the real versioned layout and frozen baseline gate.'
            Assert ($manifest.gameVersions.'1.6'.baselineVersion -eq '2.4.1' -and $manifest.gameVersions.'1.6'.productVersion -eq '2.4.1' -and $manifest.gameVersions.'1.7'.productVersion -eq $fixtureVersion) 'Each game version records its own DLL metadata; 1.6 retains the 2.4.1 baseline.'
            Assert ($manifest.gameValidation.status -eq 'NotRun' -and $manifest.validation.gameVersion -eq '1.7' -and $manifest.validation.testRuntime -eq '.NET 9') 'Regression results explicitly identify 1.7 resources and .NET 9 without claiming game runtime verification.'
            Assert ($manifest.repositoryRef -eq 'main') 'Development packages default documentation links to main and record the selected ref.'
            Assert (@($zip.Entries | Where-Object { $_.FullName -like '*0Harmony.dll' -or $_.FullName -like '*/Tests/*' }).Count -eq 0) 'Test dependencies and test sources are not packaged.'
            $packageRoot = 'RJW-SexSlaveCraft-TieJin-Modify/'
            $dllEntries = @($zip.Entries | Where-Object FullName -like '*.dll' | ForEach-Object { $_.FullName.Substring($packageRoot.Length) })
            Assert ($dllEntries.Count -eq 2 -and @(Compare-Object @('1.6/Assemblies/Sexslavecraft.dll', '1.7/Assemblies/Sexslavecraft.dll') $dllEntries).Count -eq 0) 'The ZIP contains exactly one mod DLL for each game version.'
            Assert ($null -ne $zip.GetEntry($packageRoot + 'LoadFolders.xml') -and $null -ne $zip.GetEntry($packageRoot + '1.6/BASELINE.json') -and $null -ne $zip.GetEntry($packageRoot + '1.7/BUILD-INFO.json')) 'LoadFolders and both version provenance files are included explicitly.'
            $reader = [IO.StreamReader]::new($zip.GetEntry($packageRoot + '1.6/BASELINE.json').Open())
            try { $packagedBaseline = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            $allFrozenFilesMatch = $true
            foreach ($file in $packagedBaseline.files) {
                $entry = $zip.GetEntry($packageRoot + $file.path)
                if ($null -eq $entry -or $entry.Length -ne $file.bytes) { $allFrozenFilesMatch = $false; break }
                $stream = $entry.Open()
                try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
                finally { $stream.Dispose() }
                if ($hash -cne $file.sha256) { $allFrozenFilesMatch = $false; break }
            }
            Assert ($allFrozenFilesMatch -and $packagedBaseline.files.Count -gt 0) 'Every frozen 1.6 file and original shared texture is present in the ZIP with its baseline bytes and hash.'
            Assert (@($zip.Entries | Where-Object { $_.FullName -match '/(?:Defs|Languages|Textures|Resources|Assemblies)/' -and $_.FullName -notmatch '/(?:Common|1\.6|1\.7)/' }).Count -eq 0) 'Runtime files are stored only below Common or the selected version directories.'
            Assert (@($zip.Entries | Where-Object { $_.FullName -like '*/DeveloperNotes.md' -or $_.FullName -like '*/Tools/*' }).Count -eq 0) 'Version folders use runtime subdirectory whitelists and exclude development files.'
            $docs = @($zip.Entries | Where-Object FullName -like "$packageRoot`Docs/*" | ForEach-Object { $_.FullName.Substring($packageRoot.Length) })
            $expectedDocs = @('Docs/模组介绍.md', 'Docs/Players/README.md', "Docs/Releases/$fixtureVersion/${fixtureVersion}发布说明.md")
            Assert ($docs.Count -eq 3 -and @(Compare-Object $expectedDocs $docs).Count -eq 0) 'Only the three player documentation files enter Docs; development, backups and old verification are excluded.'
            $reader = [IO.StreamReader]::new($zip.GetEntry($packageRoot + 'README.md').Open())
            try { $packagedReadme = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $devUrl = 'https://github.com/RavenHu001/RJW-SexSlaveCraft/blob/main/Docs/Development/' + [Uri]::EscapeDataString('开发 记录.md') + '#核对'
            Assert ($packagedReadme.Contains("[Fixture development](<$devUrl> `"Dev title`")") -and $packagedReadme.Contains("[fixture-dev]: <$devUrl> `"Reference title`"")) 'Inline and reference links use the selected repository ref, preserving Chinese anchors, spaces and titles.'
            Assert ($packagedReadme.Contains('[Fixture directory](https://github.com/RavenHu001/RJW-SexSlaveCraft/tree/main/Docs/Development/)')) 'Excluded directory links use the selected repository ref and tree view.'
            Assert ($packagedReadme.Contains('[Fixture local guide](QUICK_START_EN.md#2-getting-started "Local title")') -and $packagedReadme.Contains('[Fixture external](https://example.com/keep?q=1#anchor)') -and $packagedReadme.Contains('[Fixture anchor](#fixture-anchor)')) 'Packaged guide links, external URLs and local anchors stay unchanged.'
            Assert ((Get-FileHash -LiteralPath (Join-Path $fixture 'README.md')).Hash -eq $readmeHash) 'Packaging never rewrites source documentation.'
        }
        finally { $zip.Dispose() }
    }
    finally { Pop-Location }
}
finally {
    $env:PATH = $savedPath
    $env:SSC_TEST_HARMONY_PATH = $savedHarmony
    $env:SSC_VALIDATION_FIXTURE_MODE = $savedMode
    $env:SSC_DOTNET_PATH = $savedNativeDotnet
    $env:SSC_GAME_ROOT = $savedNativeGameRoot
}
Write-Host "$checks orchestration checks passed. Fixture and logs: $fixture"
