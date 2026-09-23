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
$checks = 0
$suiteCount = @(Get-ChildItem -LiteralPath (Join-Path $repo 'Tests') -Recurse -Filter '*.csproj').Count

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
    $report = Get-Content -LiteralPath (Join-Path $reportDir 'summary.json') -Raw | ConvertFrom-Json
    return [pscustomobject]@{ Code=$code; Report=$report; OutputDir=$outputDir }
}

try {
    foreach ($dir in @('Scripts', 'Tests', 'shim', 'About', 'Assemblies', 'Sexslavecraft/Properties', 'Defs', 'Languages', 'Textures', 'Resources', 'Docs')) {
        [IO.Directory]::CreateDirectory((Join-Path $fixture $dir)) | Out-Null
    }
    foreach ($file in @('Scripts/Test-All.ps1', 'Scripts/New-Release.ps1', 'Tests/NuGet.Config', 'VERSION', 'CHANGELOG.md',
        'About/About.xml', 'Assemblies/Sexslavecraft.dll', 'Sexslavecraft/Properties/AssemblyInfo.cs', 'README.md',
        'QUICK_START_EN.md', '读我，玩法介绍.md', '机制详解.md', 'PLAYER_GUIDE_EN.md')) {
        Copy-Item -LiteralPath (Join-Path $repo $file) -Destination (Join-Path $fixture $file)
    }
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
    $env:PATH = $shim + [IO.Path]::PathSeparator + $savedPath
    $env:SSC_TEST_HARMONY_PATH = $null
    # Run outside both repositories to check independence from the caller's working directory.
    Push-Location $shim
    try {
        $run = Run-Case 'success' 'pass' $HarmonyAssemblyPath
        Assert ($run.Code -eq 0 -and $run.Report.PassedSuites -eq $suiteCount -and $run.Report.TotalCases -eq $suiteCount) 'All suites execute; counts come from actual summaries (not hard-coded 390).'
        $run = Run-Case 'missing-harmony' 'pass' ''
        Assert ($run.Code -ne 0 -and $run.Report.PassedSuites -eq ($suiteCount - 3) -and $run.Report.NotRunSuites -eq 3) 'Missing Harmony is NotRun and fails the overall check.'
        $run = Run-Case 'wrong-harmony' 'pass' $FrameworkHarmonyAssemblyPath
        Assert ($run.Code -ne 0 -and $run.Report.NotRunSuites -eq 3 -and $run.Report.Results[-1].Reason -match 'net9.0') 'Framework Harmony is rejected before any Harmony suite runs.'
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
            Assert (@($zip.Entries | Where-Object { $_.FullName -like '*0Harmony.dll' -or $_.FullName -like '*/Tests/*' }).Count -eq 0) 'Test dependencies and test sources are not packaged.'
        }
        finally { $zip.Dispose() }
    }
    finally { Pop-Location }
}
finally {
    $env:PATH = $savedPath
    $env:SSC_TEST_HARMONY_PATH = $savedHarmony
    $env:SSC_VALIDATION_FIXTURE_MODE = $savedMode
}
Write-Host "$checks orchestration checks passed. Fixture and logs: $fixture"
