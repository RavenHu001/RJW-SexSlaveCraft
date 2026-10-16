#requires -Version 7.0
<#
.SYNOPSIS
检查 Def／Languages 镜像并运行全部回归套件，生成日志及 JSON 汇总；任何失败或未执行均返回失败。
.DESCRIPTION
需要 .NET SDK 9（或更新 SDK）及 .NET 9 运行时。SharedBed、InteractionProtection、RitualSelection、CombatantSpecialization、PnaLoad 和 BondRelations 需要 net9.0 的 0Harmony.dll，
通过 -HarmonyAssemblyPath 或 SSC_TEST_HARMONY_PATH 提供，不自动下载、不使用游戏的 Harmony。
默认将报告保存在 .builds/validation/<本次运行目录>；不编译或安装游戏模组。
默认检查 1.7 运行资源并回归当前源码；-GameVersion 1.6 只核验冻结基线和 XML，不能证明旧版源码或游戏兼容性。
#>
[CmdletBinding()]
param(
    [string]$HarmonyAssemblyPath = $env:SSC_TEST_HARMONY_PATH,
    [string]$ReportDirectory,
    [ValidateSet('1.6', '1.7')][string]$GameVersion = '1.7',
    [string]$RuntimeRoot,
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Native exit codes are checked explicitly so one failed suite does not hide the remaining results.
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $RuntimeRoot) { $RuntimeRoot = Join-Path $repoRoot $GameVersion }
$runtimeRootPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RuntimeRoot)
$startedAt = [DateTime]::UtcNow
if (-not $ReportDirectory) {
    $ReportDirectory = Join-Path $repoRoot ('.builds/validation/' + $startedAt.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
}
$reportRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReportDirectory)
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$savedTestEnvironment = @{}
foreach ($name in @('SSC_TEST_REPOSITORY_ROOT', 'SSC_TEST_RUNTIME_ROOT', 'SSC_TEST_COMMON_ROOT')) {
    $savedTestEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
$env:SSC_TEST_REPOSITORY_ROOT = $repoRoot
$env:SSC_TEST_RUNTIME_ROOT = $runtimeRootPath
$env:SSC_TEST_COMMON_ROOT = Join-Path $repoRoot 'Common'

# The single suite list used by standalone checks and release packaging. Arguments are absolute.
$suites = @(
    @{ Name = 'RitualProgression'; Arguments = @((Join-Path $runtimeRootPath 'Defs/HediffDefs/HediffOfSexSlave.xml')) },
    @{ Name = 'RitualLifecycle'; Arguments = @() },
    @{ Name = 'ForcedSexProps'; Arguments = @() },
    @{ Name = 'TrainingExperience'; Arguments = @() },
    @{ Name = 'DesireInfestationFeedback'; Arguments = @($repoRoot) },
    @{ Name = 'RitualSelection'; Arguments = @($repoRoot) },
    @{ Name = 'PersonalityTraits'; Arguments = @() },
    @{ Name = 'PersonalityInsertion'; Arguments = @() },
    @{ Name = 'GenderChangeMemory'; Arguments = @($repoRoot) },
    @{ Name = 'RitualAnimationFallback'; Arguments = @() },
    @{ Name = 'PermanentLactation'; Arguments = @($repoRoot) },
    @{ Name = 'PnaLoad'; Arguments = @($repoRoot) },
    @{ Name = 'PersonalityMemories'; Arguments = @() },
    @{ Name = 'PersonalitySpecializations'; Arguments = @() },
    @{ Name = 'PetSpecialization'; Arguments = @() },
    @{ Name = 'PetAffectionFollowup'; Arguments = @($repoRoot) },
    @{ Name = 'PetCatAbility'; Arguments = @($repoRoot) },
    @{ Name = 'PetDogAbility'; Arguments = @($repoRoot) },
    @{ Name = 'CombatantSpecialization'; Arguments = @($repoRoot) },
    @{ Name = 'PersonalityCardLayout'; Arguments = @($repoRoot) },
    @{ Name = 'MasterPanelUI'; Arguments = @($repoRoot) },
    @{ Name = 'RaceInjection'; Arguments = @() },
    @{ Name = 'InteractionProtection'; Arguments = @() },
    @{ Name = 'BusTrade'; Arguments = @() },
    @{ Name = 'TrainerIdentity'; Arguments = @($repoRoot) },
    @{ Name = 'MasterQualificationItem'; Arguments = @($repoRoot) },
    @{ Name = 'MasterEntry'; Arguments = @($repoRoot) },
    @{ Name = 'TrainerIntegration'; Arguments = @() },
    @{ Name = 'TrainerTrainingMemories'; Arguments = @($repoRoot) },
    @{ Name = 'RestrictionCore'; Arguments = @($repoRoot) },
    @{ Name = 'SelfTrainingManual'; Arguments = @() },
    @{ Name = 'SharedBed'; Arguments = @($repoRoot) },
    @{ Name = 'BondRelations'; Arguments = @($repoRoot) }
)
if ($GameVersion -eq '1.6') { $suites = @() }

<#
.SYNOPSIS
只读检查 Harmony 程序集名称和目标框架；可用于 net9.0 测试时返回空值，否则返回失败原因。
.DESCRIPTION
仅解析 PE 元数据，不执行第三方程序集；无论检查成功或失败都会释放文件句柄。
#>
function Get-HarmonyProblem {
    param([string]$Path)
    if (-not $Path) { return 'Supply -HarmonyAssemblyPath or SSC_TEST_HARMONY_PATH with a net9.0 0Harmony.dll.' }
    $stream = $null
    $pe = $null
    try {
        # Read metadata only: no third-party code is loaded into the PowerShell process.
        $stream = [IO.File]::OpenRead($Path)
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $definition = $reader.GetAssemblyDefinition()
        if ($reader.GetString($definition.Name) -ne '0Harmony') { return 'The supplied assembly is not 0Harmony.' }
        foreach ($handle in $definition.GetCustomAttributes()) {
            $attribute = $reader.GetCustomAttribute($handle)
            if ($attribute.Constructor.Kind -ne [System.Reflection.Metadata.HandleKind]::MemberReference) { continue }
            $constructor = $reader.GetMemberReference([System.Reflection.Metadata.MemberReferenceHandle]$attribute.Constructor)
            if ($constructor.Parent.Kind -ne [System.Reflection.Metadata.HandleKind]::TypeReference) { continue }
            $type = $reader.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$constructor.Parent)
            if ($reader.GetString($type.Namespace) -ne 'System.Runtime.Versioning' -or
                $reader.GetString($type.Name) -ne 'TargetFrameworkAttribute') { continue }
            $blob = $reader.GetBlobReader($attribute.Value)
            if ($blob.ReadUInt16() -ne 1) { return 'Invalid Harmony target-framework metadata.' }
            $framework = $blob.ReadSerializedString()
            if ($framework -ne '.NETCoreApp,Version=v9.0') {
                return "Harmony targets $framework; supply the net9.0 build, not the game's .NET Framework build."
            }
            return $null
        }
        return 'Harmony has no supported target-framework metadata; supply the net9.0 build.'
    }
    catch { return "Cannot inspect Harmony: $($_.Exception.Message)" }
    finally {
        if ($null -ne $pe) { $pe.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
}

<#
.SYNOPSIS
执行单步 dotnet 命令，将阶段名及完整输出追加到指定日志，并返回输出文本。
.DESCRIPTION
非零退出码会抛出带日志路径的异常，由套件级调用方记录失败并继续其他套件。
#>
function Invoke-DotnetStep {
    param([string[]]$CommandArguments, [string]$Stage, [string]$LogPath)
    Add-Content -LiteralPath $LogPath -Value "`n[$Stage]" -Encoding utf8
    $lines = @(& $dotnetPath @CommandArguments 2>&1)
    $code = $LASTEXITCODE
    $output = ($lines | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    Add-Content -LiteralPath $LogPath -Value $output -Encoding utf8
    if ($code -ne 0) { throw "$Stage exited with code $code. See $LogPath" }
    return $output
}

$globalProblems = [Collections.Generic.List[string]]::new()
$layoutValidation = $null
try {
    $layoutValidation = & (Join-Path $PSScriptRoot 'Test-ModLayout.ps1') -RepositoryRoot $repoRoot -PassThru
    if ($null -eq $layoutValidation -or -not $layoutValidation.Success -or $layoutValidation.Name -ne 'ModLayout') {
        throw 'Mod layout and frozen baseline validation returned no complete successful result.'
    }
}
catch {
    $layoutValidation = [pscustomobject]@{ Name = 'ModLayout'; Success = $false; Reason = $_.Exception.Message }
    $globalProblems.Add("Mod layout / frozen baseline validation failed: $($_.Exception.Message)")
}
# 资源前置检查分别登记，机制套件和用例计数保持不变；失败时所有套件标为 NotRun。
# 单独入口与发布打包共用此处，保留逐检查日志和失败原因，缺少检查脚本也不能放行。
$checks = [Collections.Generic.List[object]]::new()
$resourceChecks = @(
    @{ Name = 'DefMirrors'; Script = 'Test-DefMirrors.ps1' },
    @{ Name = 'LanguagesMirrors'; Script = 'Sync-LanguagesMirror.ps1' }
)
foreach ($definition in $resourceChecks) {
    $check = [pscustomobject][ordered]@{
        Name = $definition.Name; Status = 'Failed'; CheckedFiles = $null
        Reason = ''; LogPath = (Join-Path $reportRoot ($definition.Name + '.log'))
    }
    Set-Content -LiteralPath $check.LogPath -Value ("Check: " + $check.Name) -Encoding utf8
    try {
        # 不传 -Sync；一个检查失败后仍执行其余检查，分别保留诊断信息。
        $checkResult = & (Join-Path $PSScriptRoot $definition.Script) -RepositoryRoot $repoRoot -GameVersion $GameVersion -RuntimeRoot $runtimeRootPath -PassThru 6>> $check.LogPath
        if ($null -eq $checkResult -or -not $checkResult.Success -or
            $checkResult.Name -ne $check.Name -or $checkResult.CheckedFiles -le 0) {
            throw "$($check.Name) returned no complete successful result."
        }
        $check.Status = 'Passed'
        $check.CheckedFiles = $checkResult.CheckedFiles
    }
    catch {
        $check.Reason = $_.Exception.Message
        Add-Content -LiteralPath $check.LogPath -Value $check.Reason -Encoding utf8
        $globalProblems.Add("$($check.Name) failed: $($check.Reason) See $($check.LogPath)")
    }
    $checks.Add($check)
}
$dotnetPath = $null
if ($GameVersion -eq '1.7') {
try {
    $dotnetPath = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $sdk = @(& $dotnetPath --version 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($sdk -join '') -notmatch '^(\d+)\.' -or [int]$Matches[1] -lt 9) {
        throw '.NET SDK 9 or newer must be selected by dotnet.'
    }
    $runtimes = @(& $dotnetPath --list-runtimes 2>&1)
    if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft\.NETCore\.App 9\.')) {
        throw 'The .NET 9 runtime is required by the test executables.'
    }
}
catch { $globalProblems.Add($_.Exception.Message) }

$configPath = Join-Path $repoRoot 'Tests/NuGet.Config'
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { $globalProblems.Add('Tests/NuGet.Config is missing.') }
# A newly added project must be deliberately registered, including its required arguments.
$expectedProjects = @($suites | ForEach-Object { "Tests/$($_.Name)/$($_.Name).csproj" })
$unlisted = @(if (Test-Path -LiteralPath (Join-Path $repoRoot 'Tests') -PathType Container) {
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Tests') -Filter '*.csproj' -Recurse | ForEach-Object {
    [IO.Path]::GetRelativePath($repoRoot, $_.FullName).Replace('\', '/')
    } | Where-Object { $_ -notin $expectedProjects }
})
if ($unlisted.Count -gt 0) { $globalProblems.Add('Unregistered test projects: ' + ($unlisted -join ', ')) }

$harmonyProblem = $null
try {
    if ($HarmonyAssemblyPath) { $HarmonyAssemblyPath = (Resolve-Path -LiteralPath $HarmonyAssemblyPath -ErrorAction Stop).Path }
    $harmonyProblem = Get-HarmonyProblem $HarmonyAssemblyPath
}
catch { $harmonyProblem = "Cannot resolve Harmony path: $($_.Exception.Message)" }
}

$results = [Collections.Generic.List[object]]::new()
Write-Host "Validation reports: $reportRoot"
foreach ($suite in $suites) {
    $name = $suite.Name
    $project = Join-Path $repoRoot "Tests/$name/$name.csproj"
    $logPath = Join-Path $reportRoot "$name.log"
    $result = [pscustomobject][ordered]@{
        Name = $name; Status = 'NotRun'; PassedCases = $null; TotalCases = $null
        Reason = ''; LogPath = $logPath
    }
    $problems = @($globalProblems.ToArray())
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { $problems += "Project missing: $project" }
    if ($name -in @('SharedBed', 'InteractionProtection', 'RitualSelection', 'CombatantSpecialization', 'PnaLoad', 'BondRelations') -and $harmonyProblem) { $problems += $harmonyProblem }
    if ($problems.Count -gt 0) {
        $result.Reason = $problems -join ' '
        Set-Content -LiteralPath $logPath -Value $result.Reason -Encoding utf8
    }
    else {
        Write-Host "Running $name ..."
        Set-Content -LiteralPath $logPath -Value "Suite: $name" -Encoding utf8
        try {
            $properties = @()
            if ($name -in @('SharedBed', 'InteractionProtection', 'RitualSelection', 'CombatantSpecialization', 'PnaLoad', 'BondRelations')) { $properties += "-p:HarmonyAssemblyPath=$HarmonyAssemblyPath" }
            $null = Invoke-DotnetStep -CommandArguments (@('restore', $project, '--configfile', $configPath, '--verbosity', 'quiet') + $properties) -Stage 'restore' -LogPath $logPath
            # Rebuild prevents a changed Harmony reference from reusing a previous test binary.
            $null = Invoke-DotnetStep -CommandArguments (@('build', $project, '--configuration', 'Release', '--no-restore', '--target:Rebuild', '--verbosity', 'quiet') + $properties) -Stage 'build' -LogPath $logPath
            $testDll = Join-Path $repoRoot "Tests/$name/bin/Release/net9.0/$name.dll"
            $output = Invoke-DotnetStep -CommandArguments (@($testDll) + $suite.Arguments) -Stage 'tests' -LogPath $logPath
            # Counts come from the suite, not a hard-coded historical total of 390.
            $summary = [regex]::Matches($output, '(?m)^(?:结果：|RESULT:\s*)?(\d+)/(\d+) (?:项通过。|(?:cases |lifecycle tests )?passed\b[^\r\n]*)\s*$')
            if ($summary.Count -ne 1) { throw 'Missing or ambiguous case summary; cannot confirm complete execution.' }
            $result.PassedCases = [int]$summary[0].Groups[1].Value
            $result.TotalCases = [int]$summary[0].Groups[2].Value
            if ($result.TotalCases -le 0 -or $result.PassedCases -ne $result.TotalCases) { throw 'The case summary is not fully passing.' }
            $result.Status = 'Passed'
        }
        catch {
            $result.Status = 'Failed'
            $result.Reason = $_.Exception.Message
            Add-Content -LiteralPath $logPath -Value "`nFAILED: $($result.Reason)" -Encoding utf8
        }
    }
    $results.Add($result)
    Write-Host ("{0}: {1} {2}" -f $name, $result.Status, $result.Reason)
}

$passed = @($results | Where-Object Status -eq 'Passed').Count
$failed = @($results | Where-Object Status -eq 'Failed').Count
$notRun = @($results | Where-Object Status -eq 'NotRun').Count
$passedChecks = @($checks | Where-Object Status -eq 'Passed').Count
$failedChecks = @($checks | Where-Object Status -ne 'Passed').Count
$report = [pscustomobject][ordered]@{
    StartedAtUtc = $startedAt.ToString('o'); FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
    Success = ($passed -eq $suites.Count -and $failedChecks -eq 0 -and $globalProblems.Count -eq 0); SuiteCount = $suites.Count
    GameVersion = $GameVersion; RepositoryRoot = $repoRoot; RuntimeRoot = $runtimeRootPath
    SourceValidationScope = $(if ($GameVersion -eq '1.7') { 'Current development source with selected 1.7 runtime resources; model regression, not in-game compatibility.' } else { 'Frozen 2.4.1 artifacts and 1.6 XML only; no 1.6 source rebuild or source regression.' })
    LayoutValidation = $layoutValidation; GlobalProblems = $globalProblems.ToArray()
    PassedSuites = $passed; FailedSuites = $failed; NotRunSuites = $notRun
    PassedCases = $(if ($results.Count) { [int](($results | Measure-Object PassedCases -Sum).Sum) } else { 0 })
    TotalCases = $(if ($results.Count) { [int](($results | Measure-Object TotalCases -Sum).Sum) } else { 0 })
    CheckCount = $checks.Count; PassedChecks = $passedChecks; FailedChecks = $failedChecks
    Checks = $checks.ToArray(); HarmonyAssemblyPath = $HarmonyAssemblyPath; Results = $results.ToArray()
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reportRoot 'summary.json') -Encoding utf8
$results | Select-Object Name, Status, PassedCases, TotalCases, Reason | Format-Table -AutoSize -Wrap | Out-Host
Write-Host "Suites: $passed passed, $failed failed, $notRun not run. Reported cases: $($report.PassedCases)/$($report.TotalCases)."
Write-Host "Resource checks: $passedChecks passed, $failedChecks failed."
Write-Host "Summary: $(Join-Path $reportRoot 'summary.json')"
if (-not $report.Success) { throw 'Validation failed or incomplete. Release packaging must stop; see summary.json and suite logs.' }
# Only the structured successful result goes to the success stream for New-Release.ps1.
if ($PassThru) { return $report }
}
finally {
    foreach ($name in $savedTestEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedTestEnvironment[$name], 'Process')
    }
}
